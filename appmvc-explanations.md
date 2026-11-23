# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`6-data-overview`**, with the emphasis on what this branch adds compared with the previous branch, `5-data-seeding`.

---

## 1. This branch: Data overview (compared with `5-data-seeding`)

### Purpose
The previous branch could *write* test data to the database (the Seed page). This branch adds the first page that **reads information from the database and presents it**: a *Data overview* page showing how many **seeded** and **unseeded** music groups, albums and artists the database contains. After seeding, the user is now taken straight to this page, so the effect of seeding is visible immediately.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Controllers/HomeController.cs](AppMvc/Controllers/HomeController.cs) | `Index`, `Error` | + **`DataSourceInfo()`** action |
| [ViewModels/DataSourceInfo.cs](AppMvc/ViewModels/DataSourceInfo.cs) | – | **new** view model |
| [Views/Home/DataSourceInfo.cshtml](AppMvc/Views/Home/DataSourceInfo.cshtml) | – | **new** view |
| [Views/Shared/_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml) | menu: Home, Static, Seed | menu: Home, Static, **Data overview**, Seed |
| [Controllers/SeedController.cs](AppMvc/Controllers/SeedController.cs) | after seeding → `Home/Index` | after seeding → **`Home/DataSourceInfo`** |

No changes were needed in `Program.cs`, the services or the database layers – everything the new page needs (`IAdminService`, `AdminDbRepos`, `MainDbContext`, the database view) already existed and is reused.

### Request flow

```
Browser ──GET /Home/DataSourceInfo──► HomeController.DataSourceInfo()
   │                                     │ await _service.GuestInfoAsync()           (IAdminService, injected)
   │                                     ▼
   │                                  AdminServiceDb.GuestInfoAsync()  ── 1:1 ──►  AdminDbRepos.InfoAsync()
   │                                                                                 │ _dbContext.InfoDbView.FirstAsync()
   │                                                                                 ▼
   │                                                                      database view gstusr.vwInfoDb  (one row, six counters)
   │                                     ▲
   │                                     │ ResponseItemDto<GstUsrInfoAllDto>.Item
   │                                  new DataSourceInfo { Info = item }  → View(vm)
   ◄── HTML (Views/Home/DataSourceInfo.cshtml inside _Layout.cshtml) ──┘
```

### The controller action
```csharp
public async Task<IActionResult> DataSourceInfo()
{
    var vm = new DataSourceInfo(){Info = (await _service.GuestInfoAsync()).Item};
    return View(vm);
}
```
- Follows the MVC conventions: the action name `DataSourceInfo` selects `Views/Home/DataSourceInfo.cshtml`, and the default route `{controller=Home}/{action=Index}/{id?}` makes it available as `/Home/DataSourceInfo` without any extra route configuration.
- It is `async` because the whole call chain is asynchronous (EF Core `FirstAsync`), so the request thread is not blocked while the database is queried.
- `IAdminService` was already injected in `HomeController`'s constructor; the controller only depends on the interface, not on the database implementation (`AdminServiceDb`), which is what later allows another implementation (e.g. one that uses the Web API) to be swapped in through DI.
- `GuestInfoAsync()` returns a `ResponseItemDto<GstUsrInfoAllDto>`; only its `Item` is used. (In Debug builds the response also carries the connection string without password – it is not shown on the page.)
- The action has no error handling of its own: if the database is unreachable, the exception propagates and – outside Development – is handled by `UseExceptionHandler("/Home/Error")`.

### The view model
```csharp
public class DataSourceInfo
{
    public GstUsrInfoAllDto Info { get; set; }
}
```
A deliberately thin class in the `ViewModels` folder (MVVM): the view depends on this type only, and the controller decides how it is filled. It wraps the DTO from the `Models` project instead of exposing database entities to the view. The file has some unused `using` directives.

### The view
[Views/Home/DataSourceInfo.cshtml](AppMvc/Views/Home/DataSourceInfo.cshtml) is typed with `@model AppMvc.ViewModels.DataSourceInfo` and sets `ViewData["Title"] = "Overview"`, which the layout uses in the `<title>` and header text.

Structure of the markup:
- `div.container` → `div.row.row-cols-1.row-cols-sm-2.row-cols-md-3.row-cols-lg-5` – a responsive Bootstrap grid (1 column on phones, up to 5 on large screens).
- Three `div.col.d-flex` cards, one each for **Music groups**, **Albums** and **Artists**. Each has
  - an icon: `<svg …><use xlink:href="#boombox"/></svg>` (`#album`, `#artist`) – these reference `<symbol>` definitions in the inline SVG sprite at the top of `_Layout.cshtml`, so no extra image requests are needed;
  - a heading and two lines with Razor expressions, e.g. `@(Model.Info.Db.NrSeededMusicGroups) seeded music groups` and `@(Model.Info.Db.NrUnseededMusicGroups) unseeded music groups`. The parentheses make Razor treat the whole property path as one expression.
- Razor HTML-encodes output by default; the values are integers, so there is no injection risk.

### Layout and navigation
In `_Layout.cshtml` a new menu item was added:
```html
<a asp-area="" asp-controller="Home" asp-action="DataSourceInfo" class="nav-link" aria-current="overview">Data overview</a>
```
The anchor tag helper generates the URL from controller/action through the routing system, so the link keeps working if the route pattern changes. (`aria-current="overview"` is not a valid ARIA value – valid values are `page`, `step`, `location`, `date`, `time`, `true` – and could be changed to `page`.)

### The seed redirect
`SeedController.Seed(Seed vm)` now ends a successful POST with `RedirectToAction("DataSourceInfo", "Home")`. The POST–redirect–GET pattern prevents the seed operation from being repeated if the user refreshes the page, and the overview shows the new counters.

### Where the numbers come from (existing layers that the page reuses)

| Layer | Code | Role |
|---|---|---|
| Service | `AdminServiceDb.GuestInfoAsync()` | one-line delegate to the repo ("simple 1:1 calls … as Services expands, this will no longer need to be the case") |
| Repo | `AdminDbRepos.InfoAsync()` → `DbInfo()` | `info.Db = await _dbContext.InfoDbView.FirstAsync();` wrapped in a `ResponseItemDto` |
| DbContext | `DbSet<GstUsrInfoDbDto> InfoDbView` | `modelBuilder.Entity<GstUsrInfoDbDto>().ToView("vwInfoDb", "gstusr").HasNoKey();` – a **keyless** entity mapped to a database *view*, so EF Core only reads it and never tries to insert/update it |
| Database | view `gstusr.vwInfoDb` (SQL scripts in `DbContext/SqlScripts`) | counts rows per table where `Seeded = 1` / `Seeded = 0` |
| DTOs | `GstUsrInfoAllDto { GstUsrInfoDbDto Db }` and `GstUsrInfoDbDto` | six `int` counters: `NrSeededMusicGroups`, `NrUnseededMusicGroups`, `NrSeededAlbums`, `NrUnseededAlbums`, `NrSeededArtists`, `NrUnseededArtists` |

The SQL Server view:
```sql
CREATE OR ALTER VIEW gstusr.vwInfoDb AS
    SELECT (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 1) as nrSeededMusicGroups,
           (SELECT COUNT(*) FROM supusr.MusicGroups WHERE Seeded = 0) as nrUnseededMusicGroups,
           ... same for Albums and Artists ...
```
Design points:
- The view lives in the **`gstusr`** (guest user) schema, while the data tables are in `supusr`. This follows the solution's security model: the guest role may *read this summary* without having access to the tables themselves.
- "**Seeded**" rows are those generated by `SeedGenerator` (flag `Seeded = 1`); "**unseeded**" rows are created by users. The distinction lets the seeding feature remove only generated data, and lets the overview show both.
- The view always returns exactly one row, so `FirstAsync()` never throws "no elements" (counts are 0 on an empty database).
- Because the query is a single aggregated row, the cost of the page does not grow with the number of rows returned – the database does the counting.
- The same call is reused by the Seed page (`NrOfGroups` = seeded + unseeded music groups), and by `RemoveSeedAsync`, which returns the same info after deleting.

### Trying it
1. Start the database and run AppMvc (profile `http` → `http://localhost:5204`).
2. Open **Data overview** in the menu – the counters for the current database are shown.
3. Open **Seed**, choose a number of items, submit – you are redirected to *Data overview* with updated numbers.

---

## 2. Application structure (current)

```
AppMvc/
├── AppMvc.csproj                 References Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5204, https :7029, IIS Express
├── Controllers/
│   ├── HomeController.cs         Index, DataSourceInfo, Error
│   └── SeedController.cs         Seed (GET/POST)
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Seed/Seed.cshtml          Seed form
│   └── Shared/                   _Layout.cshtml (+ .css), Error.cshtml, _ValidationScriptsPartial.cshtml
└── wwwroot/                      index.html, css/, js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppMvc/Program.cs)
- **Services:** `AddControllersWithViews()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`); scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()`; aliases `/version` (JSON with version info) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppMvc/appsettings.json)
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets, not in this file.

### Layout and footer
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu, three reusable Bootstrap modals (`softModal`, `hardModal`, `dangerModal`), and a footer. The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>` – a quick check of which environment and database the app is talking to. No credentials are displayed.

### Seed page
`SeedController` + `ViewModels/Seed.cs` + `Views/Seed/Seed.cshtml`:
- `GET /Seed/Seed` shows the current number of music groups; `POST` validates the form, optionally removes all existing data (`RemoveSeedAsync(true)` and `(false)` – seeded **and** unseeded), calls `SeedAsync(n)` and redirects to the overview.
- The POST action has `[ValidateAntiForgeryToken]`; the form tag helper adds the matching hidden `__RequestVerificationToken` field.
- `SeedAsync(n)` uses `SeedGenerator` with `app-seeds.json` to create `n` music groups, each with 2–4 albums and 2–4 artists picked from 100 generated artists. Operations run as `DatabaseConnections:DefaultDataUser`.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for the modals and for selecting list items.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: list pages for music groups, albums and artists (paging, search, view, edit, delete) through the injected services, CRUD with the modals and validation helpers, and authentication/authorization using the already prepared pipeline.
