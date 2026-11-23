# AppRazor – Branch `16-sec-authentication`

This branch lets a registered user **log in and log out** with ASP.NET Core Identity. After a successful login the browser holds an authentication cookie, and the menu changes to show who is logged in. Nothing is protected by it yet: it identifies the user, but no page requires a login.

Everything below is compared with the previous branch (`15-sec-registration`), where a user could register but there was no login or logout.

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| Login | None (a new user was signed in only by registering) | New page `Pages/Account/Login` (`LoginModel`) |
| Logout | None | New page `Pages/Account/Logout` (`LogoutModel`) |
| `Program.cs` | Identity only | `ConfigureApplicationCookie(...)` sets login/logout paths, lifetime and cookie flags |
| `Pages/Shared/_LoginPartial.cshtml` | Missing | Empty file added |
| Layout | Static *Seed* and *Register* links | Menu depends on `User.Identity.IsAuthenticated`, shows the user's first name |

---

## 2. The authentication cookie

`AddDefaultIdentity<User>(...)` (unchanged) already registers cookie authentication. This branch tunes the cookie in `Program.cs`:

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";          // where an [Authorize] redirect would go
    options.AccessDeniedPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;              // the 60 minutes restart while the user is active
    options.Cookie.HttpOnly = true;                // not readable from JavaScript
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest;  // works on http in development
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "GoodMusicAuth";
});
```

Without this, Identity would redirect to its default `/Identity/Account/Login`. The paths now point at the app's own pages. They only matter once something uses `[Authorize]`, which comes in a later branch.

---

## 3. The login flow

```
GET  /Account/Login   → Login page with an empty LoginIM
POST /Account/Login   → validate → PasswordSignInAsync → redirect "/" or re-render with an error
POST /Account/Logout → SignOutAsync → redirect "/"   (GET /Account/Logout only redirects to "/")
```

### 3.1 `LoginModel`

The page model has a `[BindProperty] LoginIM LoginCreds` and a `ModelValidationResult ValidationResult`. `LoginIM` is a nested InputModel class that holds exactly what the form posts:

| Property | Validation |
|---|---|
| `Email` | `[Required]`, `[EmailAddress]` |
| `Password` | `[Required]`, `[DataType(Password)]` |
| `RememberMe` | – |

It receives `SignInManager<User>` and a logger.

### 3.2 `OnPostAsync`

1. `ModelState.IsValidPartially(...)` checks `LoginIM`. On errors, the page is shown again with the messages.
2. `SignInManager.PasswordSignInAsync(email, password, RememberMe, lockoutOnFailure: false)` looks the user up (the user name is the e-mail, set at registration), verifies the password hash and, if it matches, issues the cookie.
3. The result is handled in this order:
   - `Succeeded` → `LocalRedirect("/")`.
   - `RequiresTwoFactor` → redirect to a `LoginWith2fa` page (does not exist; 2FA is not enabled).
   - `IsLockedOut` → redirect to a `Lockout` page (does not exist; `lockoutOnFailure` is `false`, so lockout never happens).
   - otherwise → the page is shown again with the single message **"Invalid login attempt."**

An unknown e-mail and a wrong password give the same message, so the page does not reveal which e-mails are registered.

`RememberMe` makes the cookie persistent, so it survives closing the browser. Without it, it is a session cookie.

### 3.3 `LogoutModel`

`OnPostAsync` calls `SignInManager.SignOutAsync()` and redirects to `/`. Razor Pages validate the antiforgery token on POST by default. `OnGet` does **not** sign out. It only redirects to `/`, so a typed URL, a link or an image cannot log the user out. `Logout.cshtml` is an empty page that is never shown, because both handlers redirect.

---

## 4. The layout and `_LoginPartial`

`_Layout.cshtml` injects `UserManager<User>` and builds the menu from the login state:

| State | Menu |
|---|---|
| Anonymous | *Login*, *Register* |
| Logged in | *Seed*, and a *"<FirstName> Logout"* button |

```csharp
@if (User.Identity.IsAuthenticated)  { /* Seed */ }
@if (!User.Identity.IsAuthenticated) { /* Login, Register */ }
else
{
    var usr = await userManager.GetUserAsync(User);
    <form asp-page="/Account/Logout" method="post" class="d-inline">
        <button type="submit" class="nav-link">@usr.FirstName Logout</button>
    </form>
}
```

The logout is a small form with a button styled as `nav-link`, so it can send a POST. The form tag helper adds the antiforgery token.

`GetUserAsync` reads the user from the database on every page view, to get `FirstName`.

`_LoginPartial.cshtml` is empty. The default Identity UI pages (mapped by `AddDefaultIdentity`) render a partial with this name, so without the file they failed with a 500. The app's own menu does not use it.

**Build fix applied while analysing this branch:** the layout used `UserManager<User>` and `User` without importing the `User` namespace, so the project did not compile. `@using Models.Authorization` was added to [_Layout.cshtml](/Users/Martin/Development/projects/WebDevelopment_Dec_2026/GoodMusic_step_by_step/AppRazor/Pages/Shared/_Layout.cshtml).

---

## 5. Files in AppRazor touched by this branch

```
AppRazor/
├─ Pages/Account/Login.cshtml(.cs)      NEW  form + LoginModel + LoginIM
├─ Pages/Account/Logout.cshtml(.cs)     NEW  signs out and redirects
├─ Pages/Shared/_LoginPartial.cshtml    NEW  empty
├─ Pages/Shared/_Layout.cshtml          injects UserManager, state-dependent menu (+ using line)
└─ Program.cs                           ConfigureApplicationCookie(...)
```

---

## 6. Verified behaviour

The solution builds with 0 errors (after the `using` fix). The app was run against the SQL Server container and tested over HTTP:

| Test | Result |
|---|---|
| Register a new user | 302 to `/`, cookie `GoodMusicAuth` set, menu shows *Lena Logout* |
| `GET /Account/Logout` | 302 to `/`, user **stays logged in** |
| POST to `/Account/Logout` without a token | 400, user stays logged in |
| POST to `/Account/Logout` with the token (menu button) | 302 to `/`, menu shows *Login* and *Register* again |
| Login with empty fields | 200, "The Email field is required." and "The Password field is required." |
| Login with a malformed e-mail | 200, "The Email field is not a valid e-mail address." |
| Wrong password / unknown user | 200, "Invalid login attempt." |
| Correct credentials | 302 to `/`, menu shows *Seed* and *Lena Logout* |
| Cookie flags | `path=/; samesite=lax; httponly` |
| `/Identity/Account/Login` and `/Identity/Account/Register` | **200**, the default Identity UI now renders |
| `/Members/Seed` and `/ListOfGroups` anonymously | **200**, same as when logged in |
| POST to `/Account/Login` **without** an antiforgery token | **400, no login** |
| POST to `/Account/Login` with the token | 302 to `/` |
| `GET /Account/Login` | 200, one form, no "TEMPLATE" text |

---

## 7. Limitations to be aware of

1. **Nothing is protected.** There is no `[Authorize]`. The *Seed* link is hidden from anonymous users, but `/Members/Seed` still works for them when typed in.
2. **`returnUrl` is ignored.** Both success paths redirect to `/`, so a user sent to the login page from a protected page is not returned to it.
3. **A second, unused login exists.** The default Identity UI is reachable at `/Identity/Account/Login` and `/Identity/Account/Register`. It works, but it bypasses the app's own forms, validation and e-mail service flow.
4. **Lockout is off** (`lockoutOnFailure: false`), so passwords can be guessed without limit.
5. **A stale cookie breaks the menu.** If the cookie is valid but the user no longer exists (for example after a database rebuild), `GetUserAsync` returns `null` and `usr.FirstName` throws. This follows from the code, and was not triggered in the test.
6. **Leftovers in the code:**
   - Many unused `using` lines in `LoginModel` and `LogoutModel`, and the unused `ReturnUrl` comment.
   - The empty `Logout.cshtml` and `_LoginPartial.cshtml`.
7. The login page is still reachable when already logged in.

---

## 8. Trying it

1. `dotnet run` in `AppRazor` and open **Register**. Create a user (or use one from the previous branch).
2. Click *Logout*, then *Login*, and sign in. The menu shows your first name and *Seed*.
3. Open the browser developer tools: the cookie `GoodMusicAuth` is `HttpOnly` and `SameSite=Lax`.
4. Try a wrong password and an unknown e-mail. Both show "Invalid login attempt."

---

## 9. Next steps

Protect pages with `[Authorize]` (the login and access-denied paths above will then take effect), honour `returnUrl`, and add roles.
