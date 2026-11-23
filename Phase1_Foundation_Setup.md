# Microsoft Identity Integration

## Purpose
Adding security to AppRazor and AppMvc by integrating ASP.NET Core Identity and adding the Identity tables to the application database. This phase only lays the **foundation**: the Identity framework is wired into the software stack and the database model. There are no login or registration pages and no access restrictions yet – those are the subject of the following phases.

## Overview
This guide covers the initial setup of Microsoft Identity in both Razor Pages (`AppRazor`) and MVC (`AppMvc`). The same stack is used by both applications:

```
AppRazor / AppMvc   (Program.cs: AddDefaultIdentity<User>, UseAuthentication, UseAuthorization)
        │
Services ── ILoginService / LoginServiceDb (JWT for later phases)
        │
DbRepos
        │
DbContext ── MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
        │
Models ── Models.Authorization.User : IdentityUser<Guid>
```

### What changes in this phase

| Area | Before | After |
|---|---|---|
| User class | `Models/User.cs` (`class User : IUser`, own `UserId`, `UserName`, `PasswordHash`, `UserRole`) and `DbModels/UserDbM.cs` (table `dbo.Users`) | `Models/Authorization/User.cs`: `class User : IdentityUser<Guid>` |
| User interface | `IUser` in `Models/Interfaces.cs` | removed |
| `MainDbContext` | `: Microsoft.EntityFrameworkCore.DbContext`, `DbSet<UserDbM> Users` | `: IdentityDbContext<User, IdentityRole<Guid>, Guid>`; `Users` now comes from Identity |
| Login against the database | stored procedure `spLogin` (SQL Server, MySQL, PostgreSQL) comparing an encrypted password in `dbo.Users`; scripts `verify-login.sql` | removed – credentials are verified by Identity (`UserManager`/`SignInManager`) |
| Seeding users | `AdminDbRepos.SeedUsersAsync` inserted `UserDbM` rows | method kept in the interface but throws `NotImplementedException` ("handled by Identity") |
| Web applications | `UseAuthentication()` / `UseAuthorization()` in the pipeline, but no authentication scheme registered | `AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()` registers Identity, its cookie authentication scheme and the user/sign-in managers |
| Services | – | `ILoginService` / `LoginServiceDb` added (creates a JWT for a logged-in user; used in a later phase) |

## Steps

### 1. Update User Model
In the `Models` project, create the folder `Authorization` and the class `User` that inherits from `IdentityUser<Guid>`. The primary key (`Id`) becomes a `Guid`, like the keys of the other tables (`MusicGroupId`, `AlbumId`, …). The class body is empty – all properties (`Id`, `UserName`, `Email`, `PasswordHash`, `SecurityStamp`, …) come from `IdentityUser<Guid>`; later phases add own properties here (e.g. first and last name).

`Models/Authorization/User.cs`
```csharp
using Microsoft.AspNetCore.Identity;

namespace Models.Authorization;

public class User : IdentityUser<Guid>
{
}
```

Remove the old user classes that Identity now replaces:
- `Models/User.cs` and the `IUser` interface in `Models/Interfaces.cs`
- `DbModels/UserDbM.cs`

(The `Models` project needs no extra package reference: `IdentityUser<TKey>` is part of the ASP.NET Core shared framework that the project already uses through `Microsoft.AspNetCore.Mvc.NewtonsoftJson`.)

### 2. Update DbContext - Add Namespaces
In `DbContext/MainDbContext.cs` add the required namespaces:

```csharp
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Models.Authorization;
```

The `DbContext` project references the package `Microsoft.AspNetCore.Identity.EntityFrameworkCore` (version `10.0.13`, the same as the other `Microsoft.*` packages in the solution).

### 3. Update DbContext - Inherit from IdentityDbContext
Have `MainDbContext` inherit from `IdentityDbContext` instead of `DbContext`, using the `User` class, the standard role class with a `Guid` key, and `Guid` as key type:

```csharp
public class MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>
```

Comment out (or delete) the old `DbSet<UserDbM> Users` – the `Users` property is now inherited:

```csharp
//User for login
//now created by Identity
//public DbSet<UserDbM> Users { get; set; }
```

The database-specific contexts (`SqlServerDbContext`, `MySqlDbContext`, `PostgresDbContext`) are nested classes that inherit from `MainDbContext`, so they get the Identity model automatically. When the migration is created (step 7) EF Core adds the standard Identity tables: `AspNetUsers`, `AspNetRoles`, `AspNetUserRoles`, `AspNetUserClaims`, `AspNetRoleClaims`, `AspNetUserLogins` and `AspNetUserTokens`, next to the music tables.

### 3b. Remove the custom login plumbing (replaced by Identity)
- **SQL scripts** (`DbContext/SqlScripts/sqlserver`, `mysql`, `postgres`): the stored procedure/function `spLogin` is removed from `initDatabase.sql` and from `clearDatabase.sql`, and the three `verify-login.sql` scripts are deleted. The grants and roles for the music data (`gstusr`, `usr`, `supusr`, `dbo`) are unchanged.
- **`AdminDbRepos.SeedUsersAsync`** (and `IAdminService.SeedUsersAsync`): the implementation that inserted `UserDbM` rows is replaced by `throw new NotImplementedException("User seeding is now handled by Identity and not by AdminDbRepos anymore.")`.
- **`ILoginService` / `LoginServiceDb`** are added to `Services` (`Services/Interfaces.cs`, `Services/LoginServiceDb.cs`). `LoginServiceDb` takes a user session from `LoginDbRepos` and creates a **JWT** with the claims `UserId`, `UserRole` and `UserName` (`JwtEncryptions.CreateToken`). It is the preparation for the dual authentication described in `Security_Overview.md` (phase 7) and is **not registered in the DI container and not used by the applications yet**. Note that `LoginDbRepos` still calls the stored procedure `spLogin`, which no longer exists in the database, so it must be reworked before it is used.

## AppRazor Configuration

### 4. Configure Authentication and Authorization
In `Program.cs`, ensure the app uses Authentication and Authorization, in this order and between `UseRouting()` and the endpoint mapping:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapRazorPages();
```

`UseAuthentication()` reads the Identity cookie and sets `HttpContext.User`; `UseAuthorization()` then evaluates `[Authorize]`/folder rules against that user. (At this stage no rule exists, so every page is still reachable by everyone.)

### 5. Add Package Reference
In `AppRazor.csproj`, the following reference is needed (it is already present, together with the project references to `Configuration`, `Encryptions`, `Models` and `Services`):

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.13" />
```

The package contains the default Identity user interface (Razor class library with the pages `Account/Login`, `Register`, `Manage/…` under the *Identity* area) and brings in the Identity services.

### 6. Configure Identity Services
In `Program.cs`, add the usings and configure the Identity services **after** `AddUserBasedDbContext()` (the Identity stores use `MainDbContext`):

```csharp
using Microsoft.AspNetCore.Authorization;
using Models.Authorization;
// …

// Add IdentityServices to DbContext.MainDbContext
builder.Services.AddDefaultIdentity<User>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<DbContext.MainDbContext>();
```

What `AddDefaultIdentity<User>` registers:
- the **cookie authentication scheme** (`Identity.Application`, default sign-in/authenticate scheme) and the external/two-factor helper cookies,
- `UserManager<User>`, `SignInManager<User>`, the password hasher and validators, and the security-stamp validation,
- the default Identity UI pages (from the `Identity.UI` package) and a *no-op* `IEmailSender`.

`AddEntityFrameworkStores<MainDbContext>()` tells Identity to store users in the application database. The options relax the password rules (no digit, upper/lower case or special character required) and **do not require a confirmed e-mail** to sign in – convenient for development, but both should be tightened for a production system.

### 6b. Note on the default Identity UI in AppRazor
`MapRazorPages()` also exposes the default Identity pages (`/Identity/Account/Login`, `/Register`, …). They are **not usable in this foundation**: the default Identity layout requires a partial view `_LoginPartial` (in `/Pages/Shared/` or `/Views/Shared/`), and the empty `Pages/Shared/_LoginPartial.cshtml` placeholder was removed together with this phase. Opening `/Identity/Account/Login` therefore ends in an `InvalidOperationException` ("The default Identity UI layout requires a partial view '_LoginPartial'…", HTTP 500). Phases 2–3 replace the default pages with the application's own registration and login pages (and a login partial in the layout).

### 7. Database Migration and Update
With Terminal in folder `_scripts`, run:

**Linux/macOS:**
```bash
./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppRazor
```

**Windows:**
```powershell
./database-rebuild-all.ps1 sql-music sqlserver docker dbo ../AppRazor
```

The arguments are *database name*, *database type* (`sqlserver`, `mysql`, `postgresql`), *server* (`docker`, `azure`), *default data user* and the *application folder* whose `appsettings.json` is used. The script
1. writes `UseDataSetWithTag` (`<name>.<type>.<server>`) and `DefaultDataUser` into that `appsettings.json`,
2. **drops the existing database** (docker only),
3. removes the old migration folder `DbContext/Migrations/<context>`,
4. creates a **new migration** `miInitial` for the chosen context (it now contains the Identity tables) and
5. updates the database from it.

**Important:** 
- The script is **destructive** – existing data in the database is lost. Seed again afterwards.
- Ensure no errors from build, migration or database update
- Execute the `initDatabase.sql` script (`DbContext/SqlScripts/<db_type>/initDatabase.sql`, for Azure Data Studio `…/azure/initDatabase.sql`) in a database access tool. It creates the guest/user/super-user schemas, roles and logins, the view `gstusr.vwInfoDb` and the procedure `supusr.spDeleteAll`; it no longer creates a login procedure.

## AppMvc Configuration

### 4. Configure Authentication and Authorization
In `Program.cs`, ensure the app uses Authentication and Authorization:

```csharp
app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute();
```

### 5. Add Package Reference
In `AppMvc.csproj`, the following reference is needed (it is already present, together with the project references to `Configuration`, `Encryptions`, `Models` and `Services`):

```xml
<PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.13" />
```

### 6. Configure Identity Services
In `AppMvc.Program.cs`, add the usings and configure the Identity services (same code as in AppRazor, again after `AddUserBasedDbContext()`):

```csharp
using Microsoft.AspNetCore.Authorization;
using Models.Authorization;
// …

// Add IdentityServices to DbContext.MainDbContext
builder.Services.AddDefaultIdentity<User>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<DbContext.MainDbContext>();
```

The registered services are the same as for AppRazor. **AppMvc does not map the default Identity pages**: it calls `AddControllersWithViews()` and `MapDefaultControllerRoute()` but neither `AddRazorPages()` nor `MapRazorPages()`, so `/Identity/Account/Login` returns `404`. Registration and login will be implemented as MVC controllers and views in the following phases.

### 7. Database Migration and Update
With Terminal in folder `_scripts`, run:

**Linux/macOS:**
```bash
./database-rebuild-all.sh sql-music sqlserver docker dbo ../AppMvc
```

**Windows:**
```powershell
./database-rebuild-all.ps1 sql-music sqlserver docker dbo ../AppMvc
```

**Important:** 
- Ensure no errors from build, migration or database update
- Execute the `initDatabase.sql` script in a database access tool
- AppRazor and AppMvc share the same database; the rebuild only has to be done once (for either application folder), and the same applies to the generated migration.

## Preparations for the next phases (already in the code, not yet active)
- **`@using Microsoft.AspNetCore.Authorization/Identity` and `@using Models.Authorization`** were added to the layouts and to the list/detail views of both applications, and `using Microsoft.AspNetCore.Authorization;` to `GroupController` and `SeedController`. They are not used yet; they make `IAuthorizationService`, `UserManager<User>` and the `User` class available to the views.
- **AppRazor `ListOfGroupsModel`** now takes an `IAuthorizationService` in its constructor (stored in `_autService`, not used yet), and `OnPostDeleteGroupAsync` reads the group before deleting it (`var mg = (await _service.ReadMusicGroupAsync(groupId, true)).Item;`, comment *"Model level CRUD authorization"*). This is where phase 5 will check whether the signed-in user may delete the group. Today the extra read is unused.
- **AppMvc `ViewModels/ConfirmEmail.cs`** (a view model with a `ValidationResult`) is added for the e-mail confirmation page of phase 2.
- **`MainDbContext` registration (`AddUserBasedDbContext`)** already contains the logic to pick the database connection (the *database user*: `gstusr`, `usr`, `supusr` or `dbo`) from the `UserRole` claim of a JWT stored in the authentication properties of the signed-in user (`HttpContext.GetTokenAsync("access_token")`). That logic is only active when `JwtEncryptions` is registered in the container; the applications do not register it yet, so the configured `DatabaseConnections:DefaultDataUser` is always used.

## Result and verification
After this phase (and after the database rebuild):
- `dotnet build GoodMusic.slnx` succeeds without warnings or errors.
- Both applications start and behave as before: lists, search, view, edit, delete, seed and the data overview are unchanged and still **open to everybody** – `[Authorize]` / `AuthorizeFolder` are introduced in phase 4 (verified: `/Members/Seed` still answers `200` for an anonymous request).
- AppRazor: `/Identity/Account/Manage` redirects (`302`) an anonymous visitor to `/Identity/Account/Login`, and `/Identity/Account/Login`/`Register` fail with the `_LoginPartial` error described above. AppMvc: the same URLs return `404`.
- The database contains the Identity tables (`AspNetUsers`, …). No user exists yet; users are created by the registration of phase 2.

## Things to clean up
- `AppRazor/TempMail/*.eml` is a saved e-mail message (with a confirmation link for a test address) left over from a test of the e-mail workflow in phase 2. It is a runtime artefact and should be deleted and ignored by git; it is not needed in this phase.
- `ILoginService`/`LoginServiceDb`/`LoginDbRepos` are not wired into the applications (see 3b).
- The Identity password and sign-in options above are development settings.
