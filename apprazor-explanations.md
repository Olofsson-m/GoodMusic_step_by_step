# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`14-sec-foundation`**, with the emphasis on what this branch adds compared with the previous branch, `13-apps-public`. The same change exists in [AppMvc](appmvc-explanations.md); differences are pointed out. The step-by-step guide for the change is [Phase1_Foundation_Setup.md](Phase1_Foundation_Setup.md); the overall security plan is in [Security_Overview.md](Security_Overview.md).

---

## 1. This branch: The security foundation – ASP.NET Core Identity (compared with `13-apps-public`)

### Purpose
The previous branch was a complete **public** application: every page was open to everybody and the project had its own, simple user table. This branch replaces that with **Microsoft ASP.NET Core Identity** and wires it into the whole software stack, so that the following branches can build real security on top of it: registration, login, folder-based authorization of `Members`, resource authorization and secure Web API access. **Nothing is restricted yet and there is no working login or registration page** – this is groundwork. The visible behaviour of the application's own pages is unchanged; what changes is what is registered underneath.

### Differences at a glance

| Where | Previous branch | This branch |
|---|---|---|
| **AppRazor** [Program.cs](AppRazor/Program.cs) | no authentication scheme (the pipeline already called `UseAuthentication`/`UseAuthorization`) | `AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()` – Identity, its cookie scheme, the user/sign-in managers **and the default Identity UI** |
| **Models** | `Models/User.cs` (own class, own `UserId`/`PasswordHash`/`UserRole`) + `IUser` | `Models/Authorization/User.cs`: `class User : IdentityUser<Guid>` |
| **DbModels** | `UserDbM` (table `dbo.Users`) | removed |
| **DbContext** | `MainDbContext : Microsoft.EntityFrameworkCore.DbContext` with `DbSet<UserDbM> Users` | `MainDbContext : IdentityDbContext<User, IdentityRole<Guid>, Guid>` – the Identity tables are part of the model |
| **SQL scripts** | stored procedure `spLogin` (3 database types) + `verify-login.sql` | removed |
| **DbRepos** | `AdminDbRepos.SeedUsersAsync` seeded `UserDbM` rows | throws `NotImplementedException` (users are now created through Identity) |
| **Services** | – | `ILoginService` / `LoginServiceDb` (JWT creation; not yet registered or used) |
| **AppRazor pages** | `Pages/Shared/_LoginPartial.cshtml` (empty placeholder) | **deleted** |

No page or folder has an authorization rule yet, and no page shows anything that depends on who is signed in. The pages themselves contain no security-related code.

### The pieces and how they fit together

```
 Browser ──cookie──► UseAuthentication ──► HttpContext.User
                         ▲  (Identity.Application cookie scheme, registered by AddDefaultIdentity)
                         │
 UserManager<User> / SignInManager<User>   (registered; used by the default Identity UI pages)
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
- **Cookie authentication:** registers the Identity cookie scheme (`Identity.Application`) as the default authenticate/sign-in scheme. From now on `UseAuthentication()` – which was already in the pipeline – actually has a scheme to run: it reads the cookie and sets `HttpContext.User`. Before this branch the call did nothing useful because no scheme existed. When authorization rules are added, an unauthenticated visitor will be redirected to the cookie scheme's login path, `/Identity/Account/Login`; this is already visible: `/Identity/Account/Manage` (a page of the default UI that requires a signed-in user) answers an anonymous request with `302` to `/Identity/Account/Login?ReturnUrl=…`.
- **Managers:** `UserManager<User>` (create users, set passwords, find users, confirm e-mail) and `SignInManager<User>` (password sign-in, sign-out, issue the cookie), plus the password hasher and validators and the security-stamp validation. No page of the application uses them yet; the default Identity UI does.
- **Options:** `Password.Require…` = `false` accepts any password (the default demands digit, upper- and lower-case letters and a symbol – the policy is relaxed so that test users with simple passwords can be created); `SignIn.RequireConfirmedAccount = false` allows sign-in without a confirmed e-mail address. Both are **development settings** that should be tightened before the application is deployed.
- **`AddEntityFrameworkStores<MainDbContext>()`** connects the managers to the database through `MainDbContext`.
- **Default UI:** `AddDefaultIdentity` also registers the default Identity user interface from the `Microsoft.AspNetCore.Identity.UI` package (a Razor class library with the pages `Account/Login`, `Register`, `Manage/…` in the *Identity* area) and a no-op `IEmailSender`.
- The pipeline order stays: `UseRouting` → `UseAuthentication` → `UseAuthorization` → `MapRazorPages()` – authentication must come before authorization because authorization evaluates the user that authentication has established. (The pipeline code was also tidied: `UseHttpsRedirection`/`UseStaticFiles` are adjacent and some comments were shortened; behaviour is the same.)

#### 4. The default Identity UI is mapped but does not work (verified)
Because AppRazor calls `MapRazorPages()`, the default Identity pages are reachable under `/Identity/…`. But the default Identity layout (`Areas/Identity/Pages/_Layout.cshtml`) renders a partial view named **`_LoginPartial`** and throws if it cannot find one in `/Pages/Shared/` or `/Views/Shared/`. This branch **deletes** the empty `Pages/Shared/_LoginPartial.cshtml` placeholder (the application's own layout never rendered it), so:

| Request | Result |
|---|---|
| `/Identity/Account/Login`, `/Register`, `/Logout`, `/AccessDenied` | **500** – `InvalidOperationException: The default Identity UI layout requires a partial view '_LoginPartial' usually located at '/Pages/_LoginPartial' or at '/Views/Shared/_LoginPartial' to work.` |
| `/Identity/Account/Manage` (anonymous) | `302` to `/Identity/Account/Login?ReturnUrl=%2FIdentity%2FAccount%2FManage` (which then fails as above) |

This is consistent with the plan: the next branches replace the default pages with the application's own registration and login pages in `Pages/` and add a login partial to the layout (which would also let the default pages render). Until then the Identity UI should be considered "registered but unusable".

#### 5. Removal of the old login machinery
- The SQL stored procedure/function `spLogin` (and `verify-login.sql` for SQL Server, MySQL and PostgreSQL) compared an encrypted password in `dbo.Users` and returned the user's database role. It is removed from `initDatabase.sql` and `clearDatabase.sql`; credentials are now checked by `SignInManager` against `AspNetUsers`.
- `AdminDbRepos.SeedUsersAsync` (still declared in `IAdminService`) now throws `NotImplementedException("User seeding is now handled by Identity …")`; nothing in AppRazor calls it.
- **`ILoginService` / `LoginServiceDb`** (new, in `Services`): given credentials it asks `LoginDbRepos` for a user session and builds a **JWT** with the claims `UserId`, `UserRole` and `UserName` via `JwtEncryptions.CreateToken` (in Debug builds it decodes the token again and compares the id). It prepares the later phase in which the web applications also authenticate against a secured Web API with a bearer token. It is **not registered in the DI container and not used** yet, and `LoginDbRepos` still calls the removed `spLogin`, so it must be reworked when it is taken into use.

#### 6. What is deliberately *not* in the pages yet
AppRazor's pages and page models do not reference Identity in this branch: no `AuthorizeFolder`, no `IAuthorizationService`, no `UserManager<User>`/`SignInManager<User>`, no login or logout element in the layout, and no unused placeholder code. The next branches add exactly these, where they are needed (`AuthorizeFolder("/Members")`, the group check before a delete in `OnPostDeleteGroupAsync`, hiding the *Edit*/*Del* buttons for anonymous visitors, the user's name and a *Login/Logout* link in the header).

### What the database interplay looks like
`AddUserBasedDbContext()` already contains logic that chooses the **database user** (`gstusr`, `usr`, `supusr`, `dbo`) for a request from the `UserRole` claim of a JWT stored in the signed-in user's authentication properties (`HttpContext.GetTokenAsync("access_token")`). That logic only runs when `JwtEncryptions` is registered, which the applications do not do yet, so every request still uses `DatabaseConnections:DefaultDataUser` (default `dbo`). This is the hook that the later "secure Web API / role based database access" phase will use.

### Verified behaviour
| Check | Result |
|---|---|
| `dotnet build GoodMusic.slnx` | 0 warnings, 0 errors |
| AppRazor starts; `/`, `/ListOfGroups`, `/Members/Seed` as an anonymous visitor | `200` – existing pages work unchanged and are still open (no `AuthorizeFolder("/Members")` yet) |
| `/Identity/Account/Login`, `Register`, `Logout`, `AccessDenied` | `500` (missing `_LoginPartial`, see above) |
| `/Identity/Account/Manage` anonymous | `302` to the login path |
| Cookies | no Identity (authentication) cookie is set for an anonymous visitor |

---

## 2. Application structure (current)

```
AppRazor/
├── AppRazor.csproj               References Configuration, Encryptions, Models, Services + package Microsoft.AspNetCore.Identity.UI 10.0.13; Nullable disabled
├── Program.cs                    DI registrations (incl. Identity), HTTP pipeline, aliases /version /static /rock /queen
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5183, https :5001/:5000, IIS Express, AppMusicRazor
├── Pages/
│   ├── _ViewImports.cshtml       @using AppRazor, @namespace AppRazor.Pages, tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Index.cshtml (+ .cs)      Welcome page          -> /
│   ├── DataSourceInfo.cshtml (+ .cs)  Data overview    -> /DataSourceInfo
│   ├── ListOfGroups.cshtml (+ .cs)    Search, paged list, delete with modal, New/Edit buttons -> /ListOfGroups?pagenr=N&search=text
│   ├── ViewGroup.cshtml (+ .cs)       Group details    -> /ViewGroup?id=<guid>
│   ├── Members/
│   │   ├── Seed.cshtml (+ .cs)        Seed form        -> /Members/Seed
│   │   └── EditGroup.cshtml (+ .cs)   Edit / create a group (InputModels) -> /Members/EditGroup[?id=<guid>]
│   └── Shared/                   _Layout.cshtml, _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  IsValidPartially(...), PopulateSelectList<TEnum>(), YearUpToNow attribute
└── wwwroot/                      index.html, css/, js/seido/ (modals.js, select-list-items.js), favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services (in order):** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; **`AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()`**; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, **`UseAuthentication`**, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info), `/static`, `/rock` and `/queen` (redirects).

### The features (unchanged by this branch)
- **List / search / paging:** `ListOfGroupsModel.OnGetAsync(int pagenr, string search)` reads 10 groups per page (database `Skip/Take`, total count in the same response); the search form posts to `OnPostSearchAsync`; page links are GET links carrying `pagenr` and `search`.
- **Delete:** the *Del* button opens a Bootstrap confirmation modal; *Ok* posts `OnPostDeleteGroupAsync`, which deletes and redirects back to the same page of the (filtered) list.
- **Detail, edit and create:** `ViewGroupModel.OnGetAsync(Guid id)` shows a group with its artists and albums; `Members/EditGroup` edits or creates a group with the InputModel pattern (nested `MusicGroupIM`/`ArtistIM`/`AlbumIM` with a `StatusIM` flag per item; *Save* writes to the database, *Undo* reloads). Years must be between 1900 and the current year (`[YearUpToNow(1900)]`).
- **Seed and data overview:** `Members/Seed` seeds test data with `SeedGenerator`; `DataSourceInfoModel` shows six counters from the database view `gstusr.vwInfoDb`.
- Razor Pages validates the antiforgery token on every POST automatically.

### Layout and footer
`_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), and the scripts. The header has no login/logout element yet.

---

## 3. Comparison with AppMvc and next steps

| | AppMvc | AppRazor |
|---|---|---|
| Identity registration | same `AddDefaultIdentity<User>(…).AddEntityFrameworkStores<MainDbContext>()` | same |
| Default Identity UI | **not mapped** (no `AddRazorPages`/`MapRazorPages`) → `404` | mapped by `MapRazorPages()`, but fails with `500` because `_LoginPartial` is missing |
| Where authorization will be applied | controllers/actions (`[Authorize]`, `[AllowAnonymous]`) | the `Members` folder (`AuthorizeFolder("/Members")`) plus page-model checks |

Next (see [Security_Overview.md](Security_Overview.md)): user registration with e-mail confirmation, login/logout with cookie settings and a login partial in the layout, `AuthorizeFolder("/Members")`, resource-based authorization for create/update/delete, UI that hides actions the user may not perform, and JWT-based access to a secured Web API.
