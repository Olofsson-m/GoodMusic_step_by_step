# Phase 3: Authentication (Login and Logout)

## Purpose
Let a registered user log in and out of AppRazor and AppMvc with ASP.NET Core Identity. This phase identifies the user with a cookie and adapts the menu. It does not yet restrict access to any page.

## Prerequisite
Phase 2 (`15-sec-registration`) is in place: users can register, and the Identity tables hold them with `FirstName` and `LastName`. The user name of a registered user is the e-mail address.

## What this phase delivers
- Login and logout in both apps.
- A tuned authentication cookie (`GoodMusicAuth`).
- A layout menu that depends on the login state and shows the user's first name.

Not part of this phase: `[Authorize]`, roles, returning to the requested page, lockout, two-factor.

---

## Common: cookie configuration (both apps)

In `Program.cs`, directly after `AddDefaultIdentity<User>(...).AddEntityFrameworkStores<...>()`:

```csharp
builder.Services.ConfigureApplicationCookie(options =>
{
    options.LoginPath = "/Account/Login";
    options.AccessDeniedPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.ExpireTimeSpan = TimeSpan.FromMinutes(60);
    options.SlidingExpiration = true;
    options.Cookie.HttpOnly = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.SameAsRequest; // Allow both HTTP and HTTPS in development
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.Name = "GoodMusicAuth";
});
```

| Setting | Meaning |
|---|---|
| `LoginPath`, `AccessDeniedPath` | Where Identity redirects an unauthenticated or forbidden request. Both go to the app's own login page. They take effect when `[Authorize]` is introduced |
| `LogoutPath` | `/Account/Logout` |
| `ExpireTimeSpan` + `SlidingExpiration` | The cookie lasts 60 minutes, restarting while the user is active |
| `HttpOnly` | JavaScript cannot read the cookie |
| `SecurePolicy = SameAsRequest` | Works on http in development. Use `Always` in production |
| `SameSite = Lax` | The cookie is not sent on cross-site POSTs |

---

## AppRazor

### 1. Pages
In `Pages/Account/` add:

| Page | Purpose |
|---|---|
| `Login` | Form + `LoginModel`. `[BindProperty] LoginIM LoginCreds` has `Email`, `Password` and `RememberMe` |
| `Logout` | `OnPostAsync` signs out and redirects to `/`. `OnGet` only redirects, so a link cannot log the user out. `Logout.cshtml` is an empty page that is never shown |

`LoginModel.OnPostAsync`:

```csharp
if (!ModelState.IsValidPartially(out var validationResult)) { ValidationResult = validationResult; return Page(); }

var result = await _signInManager.PasswordSignInAsync(
    LoginCreds.Email, LoginCreds.Password, LoginCreds.RememberMe, lockoutOnFailure: false);

if (result.Succeeded)       return LocalRedirect("/");
if (result.RequiresTwoFactor) return RedirectToPage("./LoginWith2fa", ...);   // page does not exist
if (result.IsLockedOut)     return RedirectToPage("./Lockout");               // page does not exist

ValidationResult = new ModelValidationResult(true, new List<string> { "Invalid login attempt." }, null);
return Page();
```

`LogoutModel`:

```csharp
//A GET (typing the URL or following a link) must not sign the user out
public IActionResult OnGet() => LocalRedirect(Url.Content("~/"));

public async Task<IActionResult> OnPostAsync()
{
    await _signInManager.SignOutAsync();
    return LocalRedirect(Url.Content("~/"));
}
```

Razor Pages validate the antiforgery token on POST by default. Do not add `[IgnoreAntiforgeryToken]` to the Login or Logout pages.

### 2. `_LoginPartial`
Add an (empty) `Pages/Shared/_LoginPartial.cshtml`. The default Identity UI that `AddDefaultIdentity` maps renders a partial with this name, and without the file those pages return a 500.

### 3. Layout
In `Pages/Shared/_Layout.cshtml`:

```razor
@using Microsoft.AspNetCore.Identity;
@using Models.Authorization;
@inject UserManager<User> userManager;
```

`User` lives in `Models.Authorization`. Without these `using` lines the project does not compile.

Menu:

```razor
@if (User.Identity.IsAuthenticated)
{
    <li class="nav-item"><a asp-area="" asp-page="/Members/Seed" class="nav-link">Seed</a></li>
}

@if (!User.Identity.IsAuthenticated)
{
    <li class="nav-item"><a asp-area="" asp-page="/Account/Login" class="nav-link">Login</a></li>
    <li class="nav-item"><a asp-area="" asp-page="/Account/Register" class="nav-link">Register</a></li>
}
else
{
    var usr = await userManager.GetUserAsync(User);
    <li class="nav-item">
        <form asp-area="" asp-page="/Account/Logout" method="post" class="d-inline">
            <button type="submit" class="nav-link">@usr.FirstName Logout</button>
        </form>
    </li>
}
```

Logout is a form with a button styled as `nav-link`, so it is sent as a POST. The form tag helper adds the antiforgery token.

---

## AppMvc

### 1. Controller actions
In `AccountController` add:

| Action | Verb | Purpose |
|---|---|---|
| `Login(Login vm)` | GET | Shows the empty form |
| `LoginOk(Login vm)` | POST, `[ValidateAntiForgeryToken]` | Validates, calls `PasswordSignInAsync`, redirects to `/` or shows "Invalid login attempt." |
| `Logout()` | POST, `[ValidateAntiForgeryToken]` | `SignOutAsync`, redirect to `/` |
| `LogoutGet()` with `[ActionName("Logout")]` | GET | Only redirects to `/`, so a link cannot log the user out |

The `PasswordSignInAsync` handling is the same as in AppRazor. The two `RedirectToPage(...)` branches have no target in an MVC-only app.

### 2. View model and view
- `ViewModels/Login.cs`: the `Login` view model with the nested InputModel `LoginIM` (`Email`, `Password`, `RememberMe`) and a `ValidationResult`.
- `Views/Account/Login.cshtml`: the form. The submit button has `asp-controller="Account" asp-action="LoginOk"`.

### 3. Layout
In `Views/Shared/_Layout.cshtml`, add the same three lines as AppRazor:

```razor
@using Microsoft.AspNetCore.Identity
@using Models.Authorization
@inject UserManager<User> userManager;
```

Menu, with `asp-controller` / `asp-action` links:

```razor
@if (User.Identity.IsAuthenticated) { /* Seed */ }
@if (!User.Identity.IsAuthenticated) { /* Login, Register */ }
else
{
    var usr = await userManager.GetUserAsync(User);
    <form asp-controller="Account" asp-action="Logout" method="post" class="d-inline">
        <button type="submit" class="nav-link">@usr.FirstName Logout</button>
    </form>
}
```

AppMvc has no `_LoginPartial` and no default Identity UI (`/Identity/...` is 404).

---

## Testing (both apps)

1. Register a user, or use an existing one. Click **Logout**. The menu shows *Login* and *Register*.
2. Click **Login** and submit empty fields → "The Email field is required." / "The Password field is required."
3. A malformed e-mail → "The Email field is not a valid e-mail address."
4. A wrong password, or an unknown e-mail → "Invalid login attempt." (the same message in both cases).
5. Correct credentials → redirect to `/`. The menu shows *Seed* and `<FirstName> Logout`.
6. In the browser developer tools, check that `GoodMusicAuth` is `HttpOnly` and `SameSite=Lax`.
7. Click **Logout** (a button in the menu) → the menu returns to *Login* and *Register*. Typing `/Account/Logout` in the address bar does not log you out.
8. A POST to the login or logout endpoint without an antiforgery token returns 400.

Verified for both apps against the SQL Server container. In AppRazor the default Identity pages at `/Identity/Account/Login` also render (200) now that `_LoginPartial` exists. In AppMvc they are 404.

---

## Known limitations of this phase

- **No access control yet.** *Seed* is only hidden from the menu. `/Seed/Seed` (AppMvc) and `/Members/Seed` (AppRazor) still work for anonymous users.
- **`returnUrl` is ignored.** Login always redirects to `/`.
- **Lockout is off** (`lockoutOnFailure: false`).
- **Stale cookie.** If the cookie is valid but the user no longer exists, `usr` is `null` and the layout throws (read from the code, not triggered).
- **AppRazor exposes a second, default login/registration UI** under `/Identity/Account/...`.
- The layouts needed `using` lines for `UserManager<User>` and `User` to compile.
