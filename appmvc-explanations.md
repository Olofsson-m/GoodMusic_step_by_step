# AppMvc – Branch `16-sec-authentication`

This branch lets a registered user **log in and log out** with ASP.NET Core Identity. After a successful login the browser holds an authentication cookie, and the menu changes to show who is logged in. Nothing is protected by it yet: it identifies the user, but no page requires a login.

Everything below is compared with the previous branch (`15-sec-registration`), where a user could register but there was no login or logout.

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| Login | None (a new user was signed in only by registering) | `AccountController.Login` (GET) and `LoginOk` (POST) |
| Logout | None | `AccountController.Logout` (POST with antiforgery token; a GET just redirects) |
| View model | `Register`, `ConfirmEmail` | New `ViewModels/Login.cs` with the InputModel `LoginIM` |
| Views | `Register`, `RegisterConfirmation`, `ConfirmEmail` | New `Views/Account/Login.cshtml` |
| `Program.cs` | Identity only | `ConfigureApplicationCookie(...)` sets login/logout paths, lifetime and cookie flags |
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

Without this, Identity would redirect to its default `/Identity/Account/Login`, which does not exist in AppMvc (404). The paths point at the new `AccountController` actions instead. They only matter once something uses `[Authorize]`, which comes in a later branch.

---

## 3. The login flow

```
GET  /Account/Login   → Login.cshtml with an empty LoginIM
POST /Account/LoginOk → validate → PasswordSignInAsync → redirect "/" or re-render with an error
POST /Account/Logout  → SignOutAsync → redirect "/"   (GET /Account/Logout only redirects to "/")
```

### 3.1 `Login` view model (InputModel pattern)

```csharp
public class Login
{
    [BindProperty] public LoginIM LoginCreds { get; set; }
    public ModelValidationResult ValidationResult { get; set; }

    public class LoginIM
    {
        [Required, EmailAddress]           public string Email { get; set; }
        [Required, DataType(Password)]     public string Password { get; set; }
        public bool RememberMe { get; set; }
    }
}
```

`LoginIM` holds exactly what the form posts. The `ValidationResult` carries both data-annotation errors and the login error to the view.

### 3.2 `LoginOk` (POST)

1. `ModelState.IsValidPartially(...)` checks `LoginIM`. On errors, `Login.cshtml` is shown again with the messages.
2. `SignInManager.PasswordSignInAsync(email, password, RememberMe, lockoutOnFailure: false)` looks the user up (the user name is the e-mail, set at registration), verifies the password hash and, if it matches, issues the cookie.
3. The result is handled in this order:
   - `Succeeded` → `LocalRedirect("/")`.
   - `RequiresTwoFactor` → redirect to a `LoginWith2fa` page (does not exist; 2FA is not enabled).
   - `IsLockedOut` → redirect to a `Lockout` page (does not exist; `lockoutOnFailure` is `false`, so lockout never happens).
   - otherwise → the view is shown again with the single message **"Invalid login attempt."**

An unknown e-mail and a wrong password give the same message, so the page does not reveal which e-mails are registered.

`RememberMe` makes the cookie persistent, so it survives closing the browser. Without it, it is a session cookie.

### 3.3 `Logout` (POST)

`[HttpPost] [ValidateAntiForgeryToken]`. `SignInManager.SignOutAsync()` removes the cookie, then the user is redirected to `/`. A GET to `/Account/Logout` (a typed URL, a link, an image) does **not** sign out. It only redirects to `/`. This is a second action method `LogoutGet` with `[ActionName("Logout")]`.

---

## 4. The layout

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
    <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
        <button type="submit" class="nav-link">@usr.FirstName Logout</button>
    </form>
}
```

The logout is a small form with a button styled as `nav-link`, so it can send a POST. The form tag helper adds the antiforgery token.

`GetUserAsync` reads the user from the database on every page view, to get `FirstName`. This is also why `FirstName` was added in the previous branch.

**Build fix applied while analysing this branch:** the layout used `UserManager<User>` and `User` without importing their namespaces, so the project did not compile. `@using Microsoft.AspNetCore.Identity` and `@using Models.Authorization` were added to [_Layout.cshtml](/Users/Martin/Development/projects/WebDevelopment_Dec_2026/GoodMusic_step_by_step/AppMvc/Views/Shared/_Layout.cshtml).

---

## 5. Files in AppMvc touched by this branch

```
AppMvc/
├─ Controllers/AccountController.cs   + Login, LoginOk, Logout (POST) and LogoutGet
├─ ViewModels/Login.cs                NEW  Login + LoginIM
├─ Views/Account/Login.cshtml         NEW
├─ Views/Shared/_Layout.cshtml        injects UserManager, state-dependent menu (+ using lines)
└─ Program.cs                         ConfigureApplicationCookie(...)
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
| `/Identity/Account/Login` | 404 (no default Identity UI in AppMvc) |
| `/Seed/Seed` and `/Group/ListOfGroups` anonymously | **200**, same as when logged in |
| POST to `LoginOk` **without** an antiforgery token | **400, no login** |
| POST to `LoginOk` with the token | 302 to `/` |
| `GET /Account/Login` | 200, one form, no "TEMPLATE" text |

---

## 7. Limitations to be aware of

1. **Nothing is protected.** There is no `[Authorize]`. The *Seed* link is hidden from anonymous users, but `/Seed/Seed` still works for them when typed in.
2. **`returnUrl` is ignored.** Both success paths redirect to `/`, so a user sent to the login page from a protected page is not returned to it.
3. **Dead branches in `LoginOk`.** `RedirectToPage("./LoginWith2fa")` and `RedirectToPage("./Lockout")` are Razor Pages calls copied from the Identity template. They have no target in an MVC-only app, and the pages do not exist.
4. **Lockout is off** (`lockoutOnFailure: false`), so passwords can be guessed without limit.
5. **A stale cookie breaks the menu.** If the cookie is valid but the user no longer exists (for example after a database rebuild), `GetUserAsync` returns `null` and `usr.FirstName` throws. This follows from the code, and was not triggered in the test.
6. The login page is still reachable when already logged in.

---

## 8. Trying it

1. `dotnet run` in `AppMvc` and open **Register**. Create a user (or use one from the previous branch).
2. Click *Logout*, then *Login*, and sign in. The menu shows your first name and *Seed*.
3. Open the browser developer tools: the cookie `GoodMusicAuth` is `HttpOnly` and `SameSite=Lax`.
4. Try a wrong password and an unknown e-mail. Both show "Invalid login attempt."

---

## 9. Next steps

Protect pages and actions with `[Authorize]` (the login and access-denied paths above will then take effect), honour `returnUrl`, and add roles.
