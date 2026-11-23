# AppMvc – Branch `15-sec-registration`

This branch adds **user registration with e-mail confirmation** to AppMvc. A visitor can open a registration form, create an account in the ASP.NET Core Identity tables, and receive a confirmation e-mail. The e-mail is not sent over SMTP. It is written as an `.eml` file to a `TempMail` folder, so the flow can be tried without a mail server.

Everything below is compared with the previous branch (`14-sec-foundation`), where Identity was configured but no user could be created through the application.

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| `User` (shared `Models` layer) | Only the `IdentityUser<Guid>` columns | Adds `FirstName` and `LastName` |
| Database schema | Identity tables without name columns | `AspNetUsers` has `FirstName` and `LastName` (migration `miInitial` regenerated) |
| E-mail | None | `Services/EmailService` implements `IEmailSender`, writes `.eml` files to `<AppMvc>/TempMail/` |
| `Program.cs` | Identity only | Also registers `IEmailSender` → `EmailService` (transient) |
| Controllers | `Home`, `Group`, `Seed` | New `AccountController` |
| View models | `ListOfGroups`, `EditGroup`, … | New `Register` (the InputModel `UserIM`) and `ConfirmEmail` |
| Views | – | New `Views/Account/Register`, `RegisterConfirmation`, `ConfirmEmail` |
| Layout | – | New **Register** menu item |

Not changed: login, logout, `[Authorize]`. All existing pages are still open to anonymous visitors.

---

## 2. The registration flow

```
GET  /Account/Register             → Register.cshtml (empty UserIM)
POST /Account/RegisterSave         → validate → CreateAsync → send e-mail → …
GET  /Account/ConfirmEmail?code=…  → decrypt code → ConfirmEmailAsync → ConfirmEmail.cshtml
GET  /Account/RegisterConfirmation → "check your e-mail" page (only used when confirmation is required)
```

### 2.1 `Register` (GET)

Creates a fresh `Register.UserIM` and renders the form. The Register view model follows the same InputModel pattern as `EditGroup`. Only the properties that are posted live in `UserIM`:

| Property | Validation |
|---|---|
| `FirstName`, `LastName` | `[Required]` |
| `Email` | `[Required]`, `[EmailAddress]` |
| `Password` | `[Required]`, `[StringLength(100, MinimumLength = 6)]`, `[DataType(Password)]` |
| `ConfirmPassword` | `[Required]`, `[Compare("Password")]` |

`UserIM.UpdateModel(User)` copies first name, last name and e-mail to a `User`. The password is deliberately not copied. It is passed separately to Identity, which hashes it.

### 2.2 `RegisterSave` (POST)

1. `ModelState.IsValidPartially(...)` checks the `UserIM` rules. On errors the form is re-rendered with the messages in `vm.ValidationResult`.
2. `UserIM.UpdateModel(new User())` builds the entity.
3. The user name is set to the e-mail address through the `IUserStore`, and the e-mail through the e-mail store.
4. `UserManager.CreateAsync(user, password)` creates the user. Identity validates the password rules and that the user name is unique. Its errors (for example *Username '…' is already taken*) are shown through the same `ValidationResult` mechanism as the data-annotation errors.
5. On success, a confirmation link is built and handed to `IEmailSender`.
6. What happens next depends on `Identity:SignIn:RequireConfirmedAccount`:
   - `true` → `RegisterConfirmation` ("an e-mail has been sent").
   - `false` (the current development setting) → `SignInManager.SignInAsync(user)` and redirect to `/`. The user is signed in immediately.

### 2.3 The confirmation code

The e-mail link looks like `/Account/ConfirmEmail?code=<AES-encrypted Base64>`. The `code` is built from a private record `ConfirmationToken(userId, token)`:

```
Identity token  = UserManager.GenerateEmailConfirmationTokenAsync(user)
code            = Encryptions.AesEncryptToBase64( new ConfirmationToken(userId, token) )
link            = Url.Action("ConfirmEmail", "Account", new { code }, Request.Scheme)
```

So the user id and the Identity token travel together in one opaque, encrypted value. The default Identity UI instead puts a plain `userId` and a Base64Url-encoded token in the query string.

### 2.4 `ConfirmEmail` (GET)

Decrypts `code`, finds the user, and calls `UserManager.ConfirmEmailAsync(user, token)`.
- Success → `ConfirmEmail.cshtml` shows "Your email is now confirmed".
- Failure → the Identity errors are put in `ValidationResult`, a new confirmation e-mail is generated, and the same view is shown.

### 2.5 `EmailService`

```csharp
public class EmailService : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    // builds a MimeMessage (From test@test.com, HTML body)
    // saves it as <ContentRoot>/TempMail/<guid>.eml
}
```

It lives in the shared `Services` project (MailKit/MimeKit 4.18.1), so AppMvc and AppRazor use the same implementation. The class also contains a real SMTP method, `SendEmail`, with placeholder server and credentials. It is not called. Switching to real mail later means calling it (with real settings) instead of saving to the pickup folder.

---

## 3. Files in AppMvc touched by this branch

```
AppMvc/
├─ Controllers/AccountController.cs        NEW  Register, RegisterSave, ConfirmEmail, RegisterConfirmation, Error
├─ ViewModels/Register.cs                  NEW  Register + UserIM (InputModel)
├─ ViewModels/ConfirmEmail.cs              NEW  carries ValidationResult to the view
├─ Views/Account/Register.cshtml           NEW
├─ Views/Account/RegisterConfirmation.cshtml NEW
├─ Views/Account/ConfirmEmail.cshtml       NEW
├─ Views/Shared/_Layout.cshtml             Register menu item
├─ Program.cs                              IEmailSender registration
└─ TempMail/                               created at run-time, holds the .eml files
```

`AccountController` receives `UserManager<User>`, `IUserStore<User>`, `SignInManager<User>`, `IEmailSender` and `Encryptions` through constructor injection.

---

## 4. Verified behaviour

The solution builds with 0 errors. Both apps were run against the SQL Server container, and the flow was exercised over HTTP:

| Test | Result |
|---|---|
| `GET /Account/Register` | 200, form with an antiforgery token |
| POST with empty or invalid fields | 200, field messages ("The FirstName field is required", "…not a valid e-mail address", …) |
| POST with a 3-character password | 200, "The Password must be at least 6 and at max 100 characters long." |
| Valid POST | 302 → `/`; a user is inserted into `AspNetUsers`; one `.eml` file appears in `TempMail` |
| Same e-mail again | 200, "Username '…' is already taken." |
| Click the e-mail link | 200, "Thank you. Your email is now confirmed" |
| Click the same link a second time | Still 200 "confirmed". Identity's confirmation token is not consumed by a successful confirmation |
| `/Account/ConfirmEmail` without `code`, or with a tampered `code` | **500** (exception page) |
| Valid POST **without** an antiforgery token | **400, no user is created** (`RegisterSave` has `[ValidateAntiForgeryToken]`; the form tag helper emits the token) |

---

## 5. Limitations to be aware of

1. **Bad or missing `code` gives an unhandled 500.** `ConfirmEmail` does not guard against a null code, a failed decrypt or an unknown user id.
2. **The "send another e-mail" branch of `ConfirmEmail` uses `Url.Page(...)`.** AppMvc has no Razor Pages, so this very likely returns `null` and the resent e-mail would have an empty link. It should use `Url.Action("ConfirmEmail", "Account", new { code = codeNew }, Request.Scheme)` as `RegisterSave` does. This path was read in the code, but not triggered, because a valid-looking encrypted code with a bad Identity token cannot be produced without the key.
3. **`RegisterConfirmation` is not reached today.** It is only used when `RequireConfirmedAccount` is `true`. The current setting is `false`, so users are signed in right after registering and the page was not exercised. The controller also passes an anonymous object as model to a view that has no `@model`.
5. **Development-only Identity settings**: relaxed password rules and no required confirmation (set in `Program.cs` on the previous branch).
6. There is no login, logout or authorization yet. Registering signs the user in with a cookie, but nothing in the UI reflects it and no page checks it.

---

## 6. Trying it

1. Make sure the database has been rebuilt with the new migration (`_scripts/database-rebuild-all.sh sql-music sqlserver docker dbo ../AppMvc`, then run `initDatabase.sql`).
2. `dotnet run` in `AppMvc`, click **Register**, fill in the form.
3. Open `AppMvc/TempMail/*.eml`, and open the link in it.
4. Check `AspNetUsers`: `EmailConfirmed` goes from `0` to `1` after the link is clicked.

---

## 7. Next steps

Login and logout (`SignInManager.PasswordSignInAsync`), then `[Authorize]` and roles on the pages that change data.
