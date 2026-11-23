# Security Overview – GoodMusic Apps

## Purpose
This document describes the security that is **implemented in the code** of the two web applications, `AppMvc` (MVC) and `AppRazor` (Razor Pages), as of branch `19-apps-secure`. It is built up in phases. Each phase has its own guide, which is written on the branch that implements it.

Both applications share the security building blocks in the common projects (`Models`, `DbContext`, `Services`) and differ only in how they attach them to pages or controllers.

## Security layers

```
┌───────────────────────────────────────────────────────────┐
│ 6  UI authorization        buttons and icons follow rule  │
├───────────────────────────────────────────────────────────┤
│ 5  Resource authorization  "may this user Delete THIS     │
│                             group?" (handler)             │
├───────────────────────────────────────────────────────────┤
│ 4  Page / controller       [Authorize], AuthorizeFolder   │
│    authorization                                          │
├───────────────────────────────────────────────────────────┤
│ 3  Authentication          login, logout, auth cookie     │
├───────────────────────────────────────────────────────────┤
│ 2  Registration            register + e-mail confirmation │
├───────────────────────────────────────────────────────────┤
│ 1  Identity foundation     User, MainDbContext, Identity  │
└───────────────────────────────────────────────────────────┘
        Cross-cutting: antiforgery tokens on every POST
```

Layers 4 and 5 are enforced **on the server**. Layer 6 only changes what is *shown*. It is a convenience, not protection.

---

## Phases

| Phase | Guide | What it adds |
|---|---|---|
| 1 | `Phase1_Foundation_Setup.md` | ASP.NET Core Identity, `User`, `MainDbContext`, Identity options |
| 2 | `Phase2_User_Registration.md` | `FirstName`/`LastName`, `EmailService`, register and confirm-e-mail flows |
| 3 | `Phase3_Authentication.md` | Login and logout, cookie settings, state-dependent menu |
| 4 | `Phase4_Page_Controller_Authorization.md` | Folder rule (Razor), `[Authorize]`/`[AllowAnonymous]` (MVC) |
| 5 | `Phase5_Resource_Authorization.md` | `CrudOperations`, `MusicGroupAuthorizationHandler`, check on delete |
| 6 | [Phase6_Dynamic_UI_Security.md](Phase6_Dynamic_UI_Security.md) | Buttons and icons shown by permission |

The guides for phases 1–5 are in their own branches (`14-sec-foundation` … `18-sec-resource-authorization`). Only the guide for the current branch is in the working tree.

---

## 1. Identity foundation (shared by both apps)

| Piece | Where | Role |
|---|---|---|
| `User : IdentityUser<Guid>` | `Models/Authorization/User.cs` | The user, with `FirstName` and `LastName` added |
| `MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` | `DbContext/MainDbContext.cs` | Holds the Identity tables (`AspNetUsers`, …) next to the music data |
| `AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()` | both `Program.cs` | Registers `UserManager`, `SignInManager`, password hashing and the cookie scheme |
| `app.UseAuthentication(); app.UseAuthorization();` | both `Program.cs` | The pipeline stages that read the cookie and enforce the rules |

Identity options in use (development settings):

| Option | Value |
|---|---|
| Password: digit, lower case, upper case, non-alphanumeric | **not required** (`StringLength` 6–100 is checked by the form) |
| `SignIn.RequireConfirmedAccount` | `false` |

Passwords are stored hashed by Identity. User name and e-mail are the same value.

Identity's **role tables exist, but roles are not used** by any rule yet.

## 2. Registration with e-mail confirmation

| | AppMvc | AppRazor |
|---|---|---|
| Entry | `AccountController`: `Register` (GET), `RegisterSave` (POST) | `Pages/Account/Register` |
| Confirmation | `ConfirmEmail` (GET), `RegisterConfirmation` (GET) | `Pages/Account/ConfirmEmail`, `RegisterConfirmation` |
| Input model | `ViewModels/Register.cs` → `UserIM` | nested `UserIM` in `RegisterModel` |
| Confirmation code | `userId` + Identity token, **AES-encrypted** (`Encryptions`) into one `code` | plain `userId` + Base64Url-encoded Identity token |

Common to both:
- `UserIM` validates first name, last name, e-mail, password (6–100 characters) and a matching confirmation.
- `UserManager.CreateAsync(user, password)` applies Identity's own rules (for example a duplicate user name) and the messages are shown on the form.
- **E-mail is not sent.** `EmailService : IEmailSender` (in `Services`, MailKit/MimeKit) writes an `.eml` file to `<app>/TempMail/` (ignored by git). A real SMTP method exists in the class but is not called.
- Because `RequireConfirmedAccount` is `false`, a newly registered user is **signed in immediately**. `RegisterConfirmation` is therefore never reached today.

## 3. Authentication

Identity's application cookie is tuned in both `Program.cs`:

| Setting | Value |
|---|---|
| Cookie name | `GoodMusicAuth` |
| `HttpOnly` | `true` |
| `SameSite` | `Lax` |
| `SecurePolicy` | `SameAsRequest` (works on http in development; use `Always` in production) |
| Lifetime | 60 minutes, `SlidingExpiration = true` |
| `LoginPath` / `AccessDeniedPath` | `/Account/Login` |
| `LogoutPath` | `/Account/Logout` |

| | AppMvc | AppRazor |
|---|---|---|
| Login | `Login` (GET) + `LoginOk` (POST), `Login`/`LoginIM` view model | `Pages/Account/Login`, nested `LoginIM` |
| Logout | `Logout` (**POST**, token validated). GET (`LogoutGet`) only redirects | `OnPostAsync` signs out. `OnGet` only redirects |
| Menu | `_Layout.cshtml` | `_Layout.cshtml` |

- `SignInManager.PasswordSignInAsync(email, password, rememberMe, lockoutOnFailure: false)`. A wrong password and an unknown e-mail both give **"Invalid login attempt."**, so the form does not reveal which e-mails exist.
- Logout is a POST so that a link or image cannot log a user out. The menu shows it as a button inside a form.
- The layout injects `UserManager<User>` and shows *Login* and *Register* to anonymous visitors, and *Seed* plus `"<FirstName> Logout"` to a logged-in user.

## 4. Page and controller authorization

A refused anonymous request is **challenged**: a 302 to `/Account/Login?ReturnUrl=…`.

### AppMvc: attributes
| Controller | Rule |
|---|---|
| `GroupController` | `[Authorize]` on the class. **Public** (`[AllowAnonymous]`): `ListOfGroups`, `SearchGroup`, `ViewGroup`. Everything else (`EditGroup`, `DeleteGroup`, `Save`, `Undo`, `AddArtist`, …) needs a login |
| `SeedController` | `[Authorize]` on the class |
| `AccountController`, `HomeController` | Open. `[Authorize]` on `AccountController` would make the login page redirect to itself |

New actions in `GroupController` are protected by default.

### AppRazor: folder convention
```csharp
builder.Services.AddRazorPages(option => option.Conventions.AuthorizeFolder("/Members"));
```
| Page | Rule |
|---|---|
| `/Members/EditGroup` (all handlers), `/Members/Seed` | Login required |
| `/ListOfGroups`, `/ViewGroup`, `/DataSourceInfo`, `/Index`, `/Account/*` | Public |

The folder is the unit of protection. A page placed outside `/Members` is public. Page-level rules cannot separate two handlers on the same page, and that is why the delete handler on the public list page needs layer 5.

## 5. Resource authorization

Shared code in `Models/Authorization/MusicGroupAuth.cs`:

- `CrudOperations`: named operations `Create`, `Read`, `Edit`, `Delete` (`OperationAuthorizationRequirement`).
- `MusicGroupAuthorizationHandler : AuthorizationHandler<OperationAuthorizationRequirement, IMusicGroup>`.
- Registered in both apps as `AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>()`.

The rule:

| User | Create | Read | Edit | Delete |
|---|---|---|---|---|
| Anonymous | no | yes | no | no |
| Logged in | yes | yes | yes | yes |

The handler only grants. If nothing calls `Succeed`, the check fails.

Where it is **called on the server**:

| App | Place | Check |
|---|---|---|
| AppMvc | `GroupController.DeleteGroup` | `AuthorizeAsync(User, group, CrudOperations.Delete)`, `Forbid()` if refused, **then** delete |
| AppRazor | `ListOfGroupsModel.OnPostDeleteGroupAsync` | The same. This closes the hole that folder authorization leaves, because the handler lives on a public page |

The group is read before the check, since a resource-based check needs the resource.

## 6. UI authorization

`ListOfGroups` and `ViewGroup` (both apps) inject `IAuthorizationService` and ask once at the top:

| Question | Resource | Controls |
|---|---|---|
| `Delete` | first group in the list (list) / the shown group (view) | *Edit* and *Del* buttons, name-column width, edit icon |
| `Create` | a new empty `MusicGroup` | *New* button and header-column width. Independent of the rows, so it works for an empty list |

The UI uses the same handler as the server, so a changed rule is reflected in both. Hiding does not protect: typing `/Group/EditGroup`, `/Members/EditGroup` or posting a delete is refused by layers 4 and 5.

## Cross-cutting: antiforgery

| | AppMvc | AppRazor |
|---|---|---|
| Mechanism | `[ValidateAntiForgeryToken]` on every POST action (`GroupController`, `SeedController`, `RegisterSave`, `LoginOk`, `Logout`) | Validated automatically on every POST handler |
| Token | Rendered by form tag helpers | Rendered by form tag helpers |
| No `IgnoreAntiforgeryToken` anywhere | ✔ | ✔ |

A POST without a token returns 400 (tested for register, login and logout).

---

## Who can do what

| Action | Anonymous | Logged in |
|---|---|---|
| Browse, search and view groups | ✔ | ✔ |
| Register, log in | ✔ | ✔ |
| Open edit or *New* pages | ✘ → login | ✔ |
| Seed the database | ✘ → login | ✔ |
| Delete a group | ✘ → login | ✔ |
| Log out | – | ✔ (POST) |

Both apps give the same result. The difference is how it is expressed: attributes per action in MVC, a folder rule plus a handler check in Razor Pages.

## MVC and Razor Pages compared

| | Razor Pages | MVC |
|---|---|---|
| Coarse rule | `AuthorizeFolder("/Members")` in `Program.cs` | `[Authorize]` on the controller |
| Public exceptions | Page outside the folder | `[AllowAnonymous]` on the action |
| One page with several operations | Cannot be separated by the page rule | Each operation is its own action |
| Resource check | In the page handler (needed for delete) | In the action |
| Default Identity UI | Mapped, reachable under `/Identity/Account/…` | Not present (404) |
| Confirmation link | Plain user id + encoded token | AES-encrypted combined code |

---
