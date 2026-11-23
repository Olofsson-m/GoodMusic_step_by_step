# AppRazor – Branch `15-sec-registration`

This branch adds **user registration with e-mail confirmation** to AppRazor. A visitor can open a registration page, create an account in the ASP.NET Core Identity tables, and receive a confirmation e-mail. The e-mail is not sent over SMTP. It is written as an `.eml` file to a `TempMail` folder, so the flow can be tried without a mail server.

Everything below is compared with the previous branch (`14-sec-foundation`), where Identity was configured but no user could be created through the application.

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| `User` (shared `Models` layer) | Only the `IdentityUser<Guid>` columns | Adds `FirstName` and `LastName` |
| Database schema | Identity tables without name columns | `AspNetUsers` has `FirstName` and `LastName` (migration `miInitial` regenerated) |
| E-mail | None | `Services/EmailService` implements `IEmailSender`, writes `.eml` files to `<AppRazor>/TempMail/` |
| `Program.cs` | Identity only | Also registers `IEmailSender` → `EmailService` (transient) |
| Pages | `Index`, `ListOfGroups`, `ViewGroup`, `Members/…` | New folder `Pages/Account` with `Register`, `RegisterConfirmation`, `ConfirmEmail` |
| Layout | – | New **Register** menu item (`asp-page="/Account/Register"`) |

Not changed: login, logout, `[Authorize]`. All existing pages are still open to anonymous visitors.

---

## 2. The registration flow

```
GET  /Account/Register                         → Register page (empty UserIM)
POST /Account/Register                         → validate → CreateAsync → send e-mail → …
GET  /Account/ConfirmEmail?userId=…&code=…     → ConfirmEmailAsync → ConfirmEmail page
GET  /Account/RegisterConfirmation             → "check your e-mail" page (only used when confirmation is required)
```

### 2.1 `Register` page model

The page model has a `[BindProperty] UserIM RegUser` and a `ModelValidationResult ValidationResult`. `UserIM` is a nested InputModel class, which is the same pattern as `EditGroup`. It contains only the posted properties:

| Property | Validation |
|---|---|
| `FirstName`, `LastName` | `[Required]` |
| `Email` | `[Required]`, `[EmailAddress]` |
| `Password` | `[Required]`, `[StringLength(100, MinimumLength = 6)]`, `[DataType(Password)]` |
| `ConfirmPassword` | `[Required]`, `[Compare("Password")]` |

`UserIM.UpdateModel(User)` copies first name, last name and e-mail to a `User`. The password is deliberately not copied. It is passed separately to Identity, which hashes it.

The constructor receives `UserManager<User>`, `IUserStore<User>`, `SignInManager<User>`, `IEmailSender` and a logger. It fails fast if the user store does not support e-mail.

**`OnPostAsync`:**

1. `ModelState.IsValidPartially(...)` checks the `UserIM` rules. On errors the page is re-rendered with the messages in `ValidationResult`.
2. `RegUser.UpdateModel(new User())` builds the entity. The user name and the e-mail are set through the user and e-mail stores (user name = e-mail address).
3. `UserManager.CreateAsync(user, password)` creates the user. Identity validates the password rules and that the user name is unique. Its errors (for example *Username '…' is already taken*) are shown through the same `ValidationResult` mechanism.
4. A confirmation link is built and sent with `IEmailSender`.
5. What happens next depends on `Identity:SignIn:RequireConfirmedAccount`:
   - `true` → `RedirectToPage("RegisterConfirmation")`.
   - `false` (the current development setting) → `SignInManager.SignInAsync(user)` and redirect to `/`. The user is signed in immediately.

### 2.2 The confirmation link

This is the standard Identity format:

```
token = UserManager.GenerateEmailConfirmationTokenAsync(user)
code  = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token))
link  = Url.Page("/Account/ConfirmEmail", values: new { userId, code, returnUrl }, protocol: Request.Scheme)
```

The user id is a plain query parameter. The e-mail body is HTML with `<a href='{callbackUrl}'>clicking here</a>`.

### 2.3 `ConfirmEmail` page model

`OnGet(Guid userId, string code, Uri returnUrl)` finds the user, decodes the Base64Url `code` and calls `UserManager.ConfirmEmailAsync(user, token)`.
- Success → the page shows "Your email is now confirmed".
- Failure → the Identity errors are put in `ValidationResult`, another confirmation e-mail is sent, and the same page is shown ("Invalid token. We had a problem with you email confirmation. Another confirmation email has been sent.").

### 2.4 `RegisterConfirmation`

A static page with a thank-you message and an empty page model. It is the target when `RequireConfirmedAccount` is `true`.

### 2.5 `EmailService`

```csharp
public class EmailService : IEmailSender
{
    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    // builds a MimeMessage (From test@test.com, HTML body)
    // saves it as <ContentRoot>/TempMail/<guid>.eml
}
```

It lives in the shared `Services` project (MailKit/MimeKit 4.18.1), so AppRazor and AppMvc use the same implementation. The class also contains a real SMTP method, `SendEmail`, with placeholder server and credentials. It is not called. Switching to real mail later means calling it (with real settings) instead of saving to the pickup folder.

---

## 3. Files in AppRazor touched by this branch

```
AppRazor/
├─ Pages/Account/Register.cshtml(.cs)              NEW  form + RegisterModel + UserIM
├─ Pages/Account/RegisterConfirmation.cshtml(.cs)  NEW
├─ Pages/Account/ConfirmEmail.cshtml(.cs)          NEW
├─ Pages/Shared/_Layout.cshtml                     Register menu item
├─ Program.cs                                      IEmailSender registration
└─ TempMail/                                       created at run-time, holds the .eml files
```

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
| Link with a tampered `code` | 200, "Invalid token…", and a new e-mail is written. **The new e-mail contains the same tampered `code`** (see limitation 2) |
| `/Account/ConfirmEmail` without `code` | **500** (`ArgumentNullException` in `Base64UrlDecode`) |
| Valid POST **without** an antiforgery token | **400, no user is created** (Razor Pages validate the token by default, the form tag helper emits it) |

---

## 5. Limitations to be aware of

1. **The "send another e-mail" branch of `ConfirmEmail` re-sends the old code.** It generates `codeNew` but builds the link from `code`, so the new e-mail has the same invalid link. Using `codeNew` in the link fixes it.
2. **Missing parameters give an unhandled 500.** `ConfirmEmail` does not guard against a missing `code` or an unknown `userId`, where `FindByIdAsync` returns `null`.
3. **`RegisterConfirmation` is not reached today.** It is only used when `RequireConfirmedAccount` is `true`. The current setting is `false`, so users are signed in right after registering and the page was not exercised.
4. **Small leftovers in the code:**
   - The unused `GetEmailStore()` method and a duplicate of its check in the constructor.
   - The unused `returnUrl` parameter of `OnGetAsync`.
   - Unused `using` lines.
   - `ConfirmEmail` is in the namespace `AppRazor.Pages.Account` and `Register` in `AppRazor.Pages`.
5. **Development-only Identity settings**: relaxed password rules and no required confirmation (set in `Program.cs` on the previous branch).
6. There is no login, logout or authorization yet. Registering signs the user in with a cookie, but nothing in the UI reflects it and no page checks it.

---

## 6. Trying it

1. Make sure the database has been rebuilt with the new migration (`_scripts/database-rebuild-all.sh sql-music sqlserver docker dbo ../AppRazor`, then run `initDatabase.sql`).
2. `dotnet run` in `AppRazor`, click **Register**, fill in the form.
3. Open `AppRazor/TempMail/*.eml`, and open the link in it.
4. Check `AspNetUsers`: `EmailConfirmed` goes from `0` to `1` after the link is clicked.

---

## 7. Next steps

Login and logout (`SignInManager.PasswordSignInAsync`), then `[Authorize]` and roles on the pages that change data.
