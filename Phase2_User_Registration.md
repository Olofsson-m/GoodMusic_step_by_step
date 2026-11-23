# Phase 2: User Registration with E-mail Confirmation

## Purpose
Add user registration to AppRazor and AppMvc using ASP.NET Core Identity, with an e-mail confirmation step. E-mails are not sent over SMTP. They are saved as `.eml` files in a `TempMail` folder, so the flow can be tested without a mail server.

## Prerequisite
Phase 1 (`14-sec-foundation`) is in place: `User : IdentityUser<Guid>`, `MainDbContext : IdentityDbContext<…>` and `AddDefaultIdentity<User>(…)` in both `Program.cs` files.

## What this phase delivers
- `User` gets `FirstName` and `LastName`, and the database is rebuilt with these columns.
- A reusable `EmailService : IEmailSender` in the `Services` project.
- A Register page, a "check your e-mail" page and a ConfirmEmail page in each app.
- A **Register** menu item in each layout.

Not part of this phase: login, logout, `[Authorize]` and roles (later phases).

---

## Common steps

### 1. Extend the User model
`Models/Authorization/User.cs`:

```csharp
public class User : IdentityUser<Guid>
{
    public string FirstName { get; set; }
    public string LastName { get; set; }
}
```

### 2. Add the e-mail package
`Services/Services.csproj`:

```xml
<PackageReference Include="MailKit" Version="4.18.1" />
```

MailKit brings in MimeKit, which is used to build the message.

### 3. Create the e-mail service
`Services/EmailService.cs`. It implements `Microsoft.AspNetCore.Identity.UI.Services.IEmailSender` and writes each message as `<ContentRoot>/TempMail/<guid>.eml`:

```csharp
public class EmailService : IEmailSender
{
    private readonly IHostEnvironment _environment;
    public EmailService(IHostEnvironment environment) => _environment = environment;

    public async Task SendEmailAsync(string email, string subject, string htmlMessage)
    {
        var pickupDirectory = Path.Combine(_environment.ContentRootPath, "TempMail");
        var message = new MimeMessage();
        message.From.Add(MailboxAddress.Parse("test@test.com"));
        message.To.Add(MailboxAddress.Parse(email));
        message.Subject = subject;
        message.Body = new TextPart("html") { Text = htmlMessage };

        await SaveToPickupDirectory(message, pickupDirectory);   // creates the folder if needed
    }
}
```

The file also contains a real SMTP method (`SendEmail`) with placeholder server and credentials. It is not called. It is the starting point if real mail is needed later.

### 4. Register the service (both apps)
In `Program.cs`, after the Identity registration:

```csharp
builder.Services.AddTransient<Microsoft.AspNetCore.Identity.UI.Services.IEmailSender, EmailService>();
```

### 5. Rebuild the database
`User` changed, so the migration and the database must be recreated. Run this from the `_scripts` folder. The script drops and recreates the database, creates the migration in the `DbContext` project and updates the app's `appsettings.json`. Run it for the app you are working on (`../AppRazor` or `../AppMvc`):

```bash
# Linux/macOS
./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppRazor
# Windows
./database-rebuild-all.ps1 sql-music sqlserver docker dbo ../AppRazor
```

Then:
- Make sure the build, migration and database update report no errors.
- Run `initDatabase.sql` in a database tool. The script is destructive, so only use it on a development database.
- Check that `AspNetUsers` now has `FirstName` and `LastName`.

### 6. The `TempMail` folder
It is created by `EmailService` on the first e-mail, inside each app's folder. No manual step is needed. `TempMail/` is listed in `.gitignore`, so the generated `.eml` files are not committed.

---

## AppRazor

### 7. Pages
Add the folder `Pages/Account` with three Razor Pages. Each has a `.cshtml` and a `.cshtml.cs`:

| Page | Purpose |
|---|---|
| `Register` | Form + `RegisterModel`. `[BindProperty] UserIM RegUser` is the InputModel (`FirstName`, `LastName`, `Email`, `Password`, `ConfirmPassword`) |
| `RegisterConfirmation` | "Thank you for registering. An email has been sent…" (used when `RequireConfirmedAccount` is `true`) |
| `ConfirmEmail` | `OnGet(Guid userId, string code, Uri returnUrl)` confirms the e-mail and shows the result |

`Register.OnPostAsync` does the following:

```csharp
if (!ModelState.IsValidPartially(out var validationResult)) { ValidationResult = validationResult; return Page(); }

var user = RegUser.UpdateModel(new User());
await _userStore.SetUserNameAsync(user, user.Email, CancellationToken.None);
await _emailStore.SetEmailAsync(user, user.Email, CancellationToken.None);
var result = await _userManager.CreateAsync(user, RegUser.Password);

if (result.Succeeded)
{
    var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
    var code  = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(token));
    var link  = Url.Page("/Account/ConfirmEmail", null, new { userId, code, returnUrl }, Request.Scheme);
    await _emailSender.SendEmailAsync(user.Email, "Confirm your email", $"… <a href='{link}'>clicking here</a>.");

    if (_userManager.Options.SignIn.RequireConfirmedAccount)
        return RedirectToPage("RegisterConfirmation", new { email = user.Email, returnUrl });

    await _signInManager.SignInAsync(user, isPersistent: false);
    return LocalRedirect(returnUrl);
}
// Identity errors (duplicate user, password rules) are shown through ValidationResult
```

### 8. Menu item
`Pages/Shared/_Layout.cshtml`:

```html
<li class="nav-item"><a asp-area="" asp-page="/Account/Register" class="nav-link">Register</a></li>
```

---

## AppMvc

### 7. Controller, view model and views

`Controllers/AccountController.cs` has these actions:

| Action | Verb | Purpose |
|---|---|---|
| `Register` | GET | Shows the empty form |
| `RegisterSave` | POST | Validates, creates the user, sends the e-mail |
| `ConfirmEmail(ConfirmEmail vm, string code)` | GET | Confirms the e-mail |
| `RegisterConfirmation` | GET | "Check your e-mail" page |
| `Error` | GET | Error view |

`ViewModels/Register.cs` holds the `Register` view model, with the nested InputModel `UserIM` (same validation as in AppRazor). `ViewModels/ConfirmEmail.cs` carries a `ModelValidationResult` to the view.

`Views/Account/`: `Register.cshtml`, `RegisterConfirmation.cshtml` and `ConfirmEmail.cshtml`.

**The confirmation code differs from AppRazor.** The user id and the Identity token are bundled in one AES-encrypted, Base64 string:

```csharp
private record ConfirmationToken(string userId, string token);

var token = await _userManager.GenerateEmailConfirmationTokenAsync(user);
var code  = _encryptions.AesEncryptToBase64(new ConfirmationToken(userId, token));
var link  = Url.Action("ConfirmEmail", "Account", new { code }, Request.Scheme);
```

`ConfirmEmail` reverses it: `AesDecryptFromBase64<ConfirmationToken>(code)` → `FindByIdAsync` → `ConfirmEmailAsync`.

### 8. Menu item
`Views/Shared/_Layout.cshtml`:

```html
<li class="nav-item"><a asp-area="" asp-controller="Account" asp-action="Register" class="nav-link">Register</a></li>
```

---

## Testing (both apps)

1. Start the app and click **Register**.
2. Submit empty or invalid values → field messages appear. A password under 6 characters is refused.
3. Register a valid user → you are redirected to `/`. The user is in `AspNetUsers` with `EmailConfirmed = 0`.
4. Open `TempMail/<guid>.eml` and open the link in it → "Your email is now confirmed". `EmailConfirmed` is now `1`.
5. Register the same e-mail again → "Username '…' is already taken."

Verified for both apps against the SQL Server container: validation messages, creating a user, one `.eml` per registration, duplicate detection and confirmation.

## Known limitations of this phase

- Antiforgery is enforced: the Register form carries the token (the form tag helper emits it), and in AppMvc `RegisterSave` has `[ValidateAntiForgeryToken]`. A POST without a token returns 400 and creates no user. Do not add `[IgnoreAntiforgeryToken]` to these pages.
- With `RequireConfirmedAccount = false` (development), the user is signed in right after registering. `RegisterConfirmation` is only used if it is set to `true`.
- A confirmation link can be used more than once. Identity does not invalidate the token after use.
- `ConfirmEmail` returns a 500 if `code` is missing or invalid (AppMvc), or missing (AppRazor), and does not check for an unknown user.
- AppRazor's "send another e-mail" re-sends the old `code` instead of the new one. AppMvc's equivalent uses `Url.Page(...)`, which has no target in an MVC-only app and should be `Url.Action(...)`.
- Login, logout and authorization come in the next phases.
