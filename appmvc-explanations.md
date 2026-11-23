# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`14-sec-foundation`**, with the emphasis on what this branch adds compared with the previous branch, `13-apps-public`. The step-by-step guide for the same change is [Phase1_Foundation_Setup.md](Phase1_Foundation_Setup.md); the overall security plan is in [Security_Overview.md](Security_Overview.md).

---

## 1. This branch: The security foundation – ASP.NET Core Identity (compared with `13-apps-public`)

### Purpose
The previous branch was a complete **public** application: every page was open to everybody and the project had its own, simple user table. This branch replaces that with **Microsoft ASP.NET Core Identity** and wires it into the whole software stack, so that the following branches can build real security on top of it: registration, login, page/controller authorization, resource authorization and secure Web API access. **Nothing is restricted yet and there is no login or registration page** – this is groundwork. The visible behaviour of AppMvc is unchanged; what changes is what is registered underneath.

### Differences at a glance

| Where | Previous branch | This branch |
|---|---|---|
| **AppMvc** [Program.cs](AppMvc/Program.cs) | no authentication scheme (the pipeline already called `UseAuthentication`/`UseAuthorization`) | `AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()` – Identity, its cookie scheme and the user/sign-in managers |
| **Models** | `Models/User.cs` (own class, own `UserId`/`PasswordHash`/`UserRole`) + `IUser` | `Models/Authorization/User.cs`: `class User : IdentityUser<Guid>` |
| **DbModels** | `UserDbM` (table `dbo.Users`) | removed |
| **DbContext** | `MainDbContext : Microsoft.EntityFrameworkCore.DbContext` with `DbSet<UserDbM> Users` | `MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` – the Identity tables are part of the model |
| **SQL scripts** | stored procedure `spLogin` (3 database types) + `verify-login.sql` | removed |
| **DbRepos** | `AdminDbRepos.SeedUsersAsync` seeded `UserDbM` rows | throws `NotImplementedException` (users are now created through Identity) |
| **Services** | – | `ILoginService` / `LoginServiceDb` (JWT creation; not yet registered or used) |

No controller action or view has an authorization attribute or an authentication-dependent element yet, and the views and controllers contain no security-related code at all.

### The pieces and how they fit together

```
 Browser ──cookie──► UseAuthentication ──► HttpContext.User
                         ▲  (Identity.Application cookie scheme, registered by AddDefaultIdentity)
                         │
 UserManager<User> / SignInManager<User>   (registered, not yet called by any controller)
                         │  uses the user store
                         ▼
 IdentityDbContext<User, IdentityRole<Guid>, Guid>   =  MainDbContext   (the application's one DbContext)
                         │
                         ▼
 database: AspNetUsers, AspNetRoles, AspNetUserRoles, AspNetUserClaims, AspNetRoleClaims, AspNetUserLogins, AspNetUserTokens  +  music tables
```

#### 1. The user class – `Models/Authorization/User.cs`
```csharp
namespace Models.Authorization;
public class User : IdentityUser<Guid> { }
```
- `IdentityUser<TKey>` supplies `Id`, `UserName`, `NormalizedUserName`, `Email`, `EmailConfirmed`, `PasswordHash`, `SecurityStamp`, `PhoneNumber`, lockout and two-factor fields. The type argument `Guid` makes the key the same type as the keys of the music tables.
- The class body is **empty on purpose**: it is the extension point. Later branches add the user's own properties (first and last name) here, and the database follows through a migration.
- It lives in the `Models` project (folder `Authorization`), which every layer references, so the web applications, `Services` and `DbContext` all use the same `User`. (`IdentityUser` is available there because the project's `Microsoft.AspNetCore.Mvc.NewtonsoftJson` package brings in the ASP.NET Core shared framework.)
- The old custom user (`UserId`, `UserName`, `Email`, `PasswordHash`, `UserRole` with the roles `usr`/`supusr`/`dbo`) is gone. In its place Identity keeps the **password hash** (salted, iterated hashing – never the clear or reversibly encrypted password) and handles roles with its own role tables.

#### 2. The database context – `MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>`
- `IdentityDbContext<TUser, TRole, TKey>` is an ordinary EF Core `DbContext` that also declares the Identity entities. Because `MainDbContext` inherits it, the database-specific nested contexts (`SqlServerDbContext`, `MySqlDbContext`, `PostgresDbContext`) used for migrations inherit the Identity model automatically, and **one migration creates the music tables and the Identity tables together**.
- `IdentityRole<Guid>` is the standard role class with a `Guid` key. The role tables exist, but the applications do not use roles yet (see "things to know" below).
- The old `public DbSet<UserDbM> Users` is commented out: `Users` is now inherited from `IdentityDbContext`.
- Everything the other pages use (`MusicGroups`, `Albums`, `Artists`, the `InfoDbView`) is unchanged, which is why all existing pages keep working.

#### 3. Registration in `Program.cs`
```csharp
builder.Services.AddDefaultIdentity<User>(options => {
    options.Password.RequireDigit = false;
    options.Password.RequireLowercase = false;
    options.Password.RequireNonAlphanumeric = false;
    options.Password.RequireUppercase = false;
    options.SignIn.RequireConfirmedAccount = false;
}).AddEntityFrameworkStores<DbContext.MainDbContext>();
```
It is placed after `AddUserBasedDbContext()` because the user store needs `MainDbContext` to be registered. What it does:
- **Cookie authentication:** registers the Identity cookie scheme (`Identity.Application`) as the default authenticate/sign-in scheme. From now on `UseAuthentication()` – which was already in the pipeline – actually has a scheme to run: it reads the cookie and sets `HttpContext.User`. Before this branch the call did nothing useful because no scheme existed.
- **Managers:** `UserManager<User>` (create users, set passwords, find users, confirm e-mail) and `SignInManager<User>` (password sign-in, sign-out, issue the cookie), plus the password hasher and validators and the security-stamp validation. They are available for injection, but no controller uses them yet.
- **Options:** `Password.Require…` = `false` accepts any password (the default demands digit, upper- and lower-case letters and a symbol – the policy is relaxed so that test users with simple passwords can be created); `SignIn.RequireConfirmedAccount = false` allows sign-in without a confirmed e-mail address. Both are **development settings** that should be tightened before the application is deployed.
- **`AddEntityFrameworkStores<MainDbContext>()`** connects the managers to the database through `MainDbContext`.
- **Default UI:** `AddDefaultIdentity` also registers the default Identity user interface from the `Microsoft.AspNetCore.Identity.UI` package. In AppMvc this UI is **not exposed** (see below).
- The pipeline order stays: `UseRouting` → `UseAuthentication` → `UseAuthorization` → `MapDefaultControllerRoute()` – authentication must come before authorization because authorization evaluates the user that authentication has established.

#### 4. What is *not* mapped in AppMvc
The default Identity pages are Razor Pages. AppMvc calls `AddControllersWithViews()` and `MapDefaultControllerRoute()` but **not** `AddRazorPages()`/`MapRazorPages()`, so `/Identity/Account/Login` and `/Identity/Account/Register` answer **404** (verified). That fits the plan: AppMvc will get its own registration and login controllers and views in the next branches, in the MVC style, rather than the default pages.

#### 5. Removal of the old login machinery
- The SQL stored procedure/function `spLogin` (and `verify-login.sql` for SQL Server, MySQL and PostgreSQL) compared an encrypted password in `dbo.Users` and returned the user's database role. It is removed from `initDatabase.sql` and `clearDatabase.sql`; credentials are now checked by `SignInManager` against `AspNetUsers`.
- `AdminDbRepos.SeedUsersAsync` (still declared in `IAdminService`) now throws `NotImplementedException("User seeding is now handled by Identity …")`; nothing in AppMvc calls it.
- **`ILoginService` / `LoginServiceDb`** (new, in `Services`): given credentials it asks `LoginDbRepos` for a user session and builds a **JWT** with the claims `UserId`, `UserRole` and `UserName` via `JwtEncryptions.CreateToken` (in Debug builds it decodes the token again and compares the id). It prepares the later phase in which the web applications also authenticate against a secured Web API with a bearer token. It is **not registered in the DI container and not used** by AppMvc yet, and `LoginDbRepos` still calls the removed `spLogin`, so it must be reworked when it is taken into use.

#### 6. What is deliberately *not* in the views and controllers yet
AppMvc's controllers and views do not reference Identity at all in this branch: no `[Authorize]`, no `IAuthorizationService`, no `UserManager<User>`/`SignInManager<User>`, no login or logout element in the layout, and no unused placeholder code. The next branches add exactly these, where they are needed (`[Authorize]` on controllers and actions, hiding the *Edit*/*Del* buttons for anonymous visitors, the user's name and a *Login/Logout* link in the header, an e-mail confirmation page).

### What the database interplay looks like
`AddUserBasedDbContext()` already contains logic that chooses the **database user** (`gstusr`, `usr`, `supusr`, `dbo`) for a request from the `UserRole` claim of a JWT stored in the signed-in user's authentication properties (`HttpContext.GetTokenAsync("access_token")`). That logic only runs when `JwtEncryptions` is registered, which the applications do not do yet, so every request still uses `DatabaseConnections:DefaultDataUser` (default `dbo`). This is the hook that the later "secure Web API / role based database access" phase will use.

### Verified behaviour
| Check | Result |
|---|---|
| `dotnet build GoodMusic.slnx` | 0 warnings, 0 errors |
| AppMvc starts; `/` and `/Group/ListOfGroups` | `200` – existing pages work unchanged |
| `/Seed/Seed`, `/Group/EditGroup` as an anonymous visitor | `200` – still reachable without login (no `[Authorize]` yet) |
| `/Identity/Account/Login`, `/Identity/Account/Register` | `404` – the default Identity UI is not mapped in AppMvc |
| Cookies | no Identity (authentication) cookie is set; form pages only set the antiforgery cookie |

### Things to know / clean up
- **No roles yet:** `AddDefaultIdentity` does not register `RoleManager`; roles need `.AddRoles<IdentityRole<Guid>>()` in a later branch even though the role tables exist.
- **The database must be rebuilt once** to get the Identity tables: `_scripts/database-rebuild-all.sh sql-music sqlserver docker dbo ../AppMvc` (drops and recreates the database, makes a new `miInitial` migration, then run `initDatabase.sql`) – see the phase guide. The same database serves AppRazor.
- **Relaxed password and sign-in options** are for development only.
- `LoginServiceDb`/`LoginDbRepos` (and the `LoginCredentialsDto`/`LoginUserSessionDto` DTOs they use, and the throwing `SeedUsersAsync`) are dead code until reworked.

---

## 2. Application structure (current)

```
AppMvc/
├── AppMvc.csproj                 References Configuration, Encryptions, Models, Services + package Microsoft.AspNetCore.Identity.UI 10.0.13; Nullable disabled
├── Program.cs                    DI registrations (incl. Identity), HTTP pipeline, aliases /version /static /rock /queen
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5204, https :7029, IIS Express
├── Controllers/
│   ├── HomeController.cs         Index, DataSourceInfo, Error
│   ├── SeedController.cs         Seed (GET/POST)
│   └── GroupController.cs        ListOfGroups, SearchGroup, DeleteGroup, ViewGroup, EditGroup + 8 edit POST actions
├── ViewModels/                   Error, Seed, DataSourceInfo, ListOfGroups, ViewGroup, EditGroup (InputModels)
├── SeidoHelpers/SeidoHelpers.cs  IsValidPartially(...), PopulateSelectList<TEnum>(), YearUpToNow attribute
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/                    ListOfGroups.cshtml, ViewGroup.cshtml, EditGroup.cshtml
│   ├── Seed/Seed.cshtml          Seed form
│   └── Shared/                   _Layout.cshtml (+ .css), Error.cshtml, _ValidationScriptsPartial.cshtml
└── wwwroot/                      index.html, css/, js/seido/ (modals.js, select-list-items.js), favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppMvc/Program.cs)
- **Services (in order):** `AddControllersWithViews()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; **`AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()`**; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, **`UseAuthentication`**, `UseAuthorization`, `MapDefaultControllerRoute()`; aliases `/version` (JSON with version info), `/static`, `/rock` and `/queen` (redirects).

### The features (unchanged by this branch)
- **List / search / paging:** `GroupController.ListOfGroups(int pagenr, string search)` reads 10 groups per page (database `Skip/Take`, total count in the same response); the search form posts to `SearchGroup`; page links are GET links carrying `pagenr` and `search`.
- **Delete:** the *Del* button opens a Bootstrap confirmation modal; *Ok* posts `DeleteGroup`, which deletes and redirects back to the same page of the (filtered) list.
- **Detail, edit and create:** `ViewGroup(Guid id)` shows a group with its artists and albums; `EditGroup(Guid? id)` edits or creates a group with the InputModel pattern (`ViewModels/EditGroup.cs`; in-memory add/edit/delete of artists and albums with a `StatusIM` flag per item; *Save* writes to the database, *Undo* reloads). Years must be between 1900 and the current year (`[YearUpToNow(1900)]`).
- **Seed and data overview:** `SeedController` seeds test data with `SeedGenerator`; `HomeController.DataSourceInfo()` shows six counters from the database view `gstusr.vwInfoDb`.
- All POST actions are protected with `[ValidateAntiForgeryToken]`; the form tag helper adds the token.

### Layout and footer
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), three reusable Bootstrap modals, a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), and the scripts. The header has no login/logout element yet.

---

## 3. Context and next steps

- [AppRazor](apprazor-explanations.md) received the same Identity registration; the two applications differ in how the default Identity UI is exposed (mapped but not working in AppRazor, not mapped in AppMvc).
- Next (see [Security_Overview.md](Security_Overview.md)): user registration with e-mail confirmation, login/logout with cookie settings and a login partial in the layout, `[Authorize]`/`[AllowAnonymous]` on controllers and actions, resource-based authorization for create/update/delete, UI that hides actions the user may not perform, and JWT-based access to a secured Web API.
