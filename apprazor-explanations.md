# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`6-data-overview`**, with the emphasis on what this branch adds compared with the previous branch, `5-data-seeding`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Data overview (compared with `5-data-seeding`)

### Purpose
The previous branch could *write* test data to the database (the Seed page). This branch adds the first page that **reads information from the database and presents it**: a *Data overview* page showing how many **seeded** and **unseeded** music groups, albums and artists the database contains. After seeding, the user is now taken straight to this page, so the effect of seeding is visible immediately.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/DataSourceInfo.cshtml](AppRazor/Pages/DataSourceInfo.cshtml) | – | **new** page (route `/DataSourceInfo`) |
| [Pages/DataSourceInfo.cshtml.cs](AppRazor/Pages/DataSourceInfo.cshtml.cs) | – | **new** `DataSourceInfoModel` PageModel |
| [Pages/Shared/_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml) | menu: Home, Static, Seed | menu: Home, Static, **Data overview**, Seed |
| [Pages/Members/Seed.cshtml.cs](AppRazor/Pages/Members/Seed.cshtml.cs) | after seeding → `Index` | after seeding → **`DataSourceInfo`** |

No changes were needed in `Program.cs`, the services or the database layers – everything the new page needs (`IAdminService`, `AdminDbRepos`, `MainDbContext`, the database view) already existed and is reused. `MapRazorPages()` picks the new page up automatically, because the route is derived from the file location.

### Request flow

```
Browser ──GET /DataSourceInfo──► DataSourceInfoModel.OnGetAsync()
   │                                │ await _service.GuestInfoAsync()           (IAdminService, injected)
   │                                ▼
   │                             AdminServiceDb.GuestInfoAsync()  ── 1:1 ──►  AdminDbRepos.InfoAsync()
   │                                                                            │ _dbContext.InfoDbView.FirstAsync()
   │                                                                            ▼
   │                                                                 database view gstusr.vwInfoDb  (one row, six counters)
   │                                ▲
   │                                │ ResponseItemDto<GstUsrInfoAllDto>.Item
   │                             Info = item  → Page()
   ◄── HTML (Pages/DataSourceInfo.cshtml inside _Layout.cshtml) ──┘
```

### The PageModel
```csharp
public class DataSourceInfoModel : PageModel
{
    public Models.DTO.GstUsrInfoAllDto Info { get; set; }

    readonly ILogger<DataSourceInfoModel> _logger;
    readonly IAdminService _service = null;

    public DataSourceInfoModel(ILogger<DataSourceInfoModel> logger, IAdminService service) { ... }

    public async Task<IActionResult> OnGetAsync()
    {
        Info = (await _service.GuestInfoAsync()).Item;
        return Page();
    }
}
```
- **Constructor injection:** ASP.NET Core creates the PageModel per request and supplies `ILogger<T>` and `IAdminService` from the DI container (registered in `Program.cs`). The page depends only on the interface, not on `AdminServiceDb`, so another implementation could be swapped in through DI.
- **Handler:** `OnGetAsync` runs for `GET /DataSourceInfo`. It is `async` because the whole call chain is asynchronous (EF Core `FirstAsync`), so the request thread is not blocked during the query. The `Async` suffix is the Razor Pages naming convention for async handlers; `Task<IActionResult>` with `Page()` renders the page.
- **View model role:** the PageModel itself is the view model – the public `Info` property is what the page reads through `Model.Info`. (AppMvc needs a separate `ViewModels/DataSourceInfo` class for this; in Razor Pages the PageModel serves both as handler and model.)
- `GuestInfoAsync()` returns a `ResponseItemDto<GstUsrInfoAllDto>`; only its `Item` is used. (In Debug builds the response also carries the connection string without password – it is not shown on the page.)
- There is no error handling in the handler: if the database is unreachable, the exception propagates. Note that AppRazor currently uses `UseDeveloperExceptionPage()` outside Development as well, so detailed errors would be shown – to be revisited before deployment.

### The page
[Pages/DataSourceInfo.cshtml](AppRazor/Pages/DataSourceInfo.cshtml) starts with `@page` (this directive makes it a routable Razor Page) and `@model DataSourceInfoModel` (the short name works because `_ViewImports.cshtml` sets `@namespace AppRazor.Pages`). It sets `ViewData["Title"] = "Data overview"`, used by the layout for the `<title>` and header text.

Structure of the markup:
- `div.container` → `div.row.row-cols-1.row-cols-sm-2.row-cols-md-3.row-cols-lg-5` – a responsive Bootstrap grid (1 column on phones, up to 5 on large screens).
- Three `div.col.d-flex` cards, one each for **Music groups**, **Albums** and **Artists**. Each has
  - an icon: `<svg …><use xlink:href="#boombox"/></svg>` (`#album`, `#artist`) – these reference `<symbol>` definitions in the inline SVG sprite at the top of `_Layout.cshtml`, so no extra image requests are needed;
  - a heading and two lines with Razor expressions, e.g. `@(Model.Info.Db.NrSeededMusicGroups) seeded music groups` and `@(Model.Info.Db.NrUnseededMusicGroups) unseeded music groups`. The parentheses make Razor treat the whole property path as one expression.
- Razor HTML-encodes output by default; the values are integers, so there is no injection risk.

### Layout and navigation
In `_Layout.cshtml` a new menu item was added:
```html
<a asp-area="" asp-page="/DataSourceInfo" class="nav-link">Data overview</a>
```
The `asp-page` tag helper (enabled by `_ViewImports.cshtml`) generates the URL from the page path, so the link keeps working if the page is moved or the routing is customised.

### The seed redirect
`SeedModel.OnPostAsync()` now ends a successful POST with `RedirectToPage("/DataSourceInfo")`. The POST–redirect–GET pattern prevents the seed operation from being repeated if the user refreshes the page, and the overview shows the new counters.

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
- Because the query is a single aggregated row, the cost of the page does not grow with the amount of data – the database does the counting.
- The same call is reused by the Seed page (`NrOfGroups` = seeded + unseeded music groups), and `RemoveSeedAsync` returns the same info after deleting.

### Trying it
1. Start the database and run AppRazor (profile `http` → `http://localhost:5183`).
2. Open **Data overview** in the menu – the counters for the current database are shown.
3. Open **Seed**, choose a number of items, submit – you are redirected to *Data overview* with updated numbers.

---

## 2. Application structure (current)

```
AppRazor/
├── AppRazor.csproj               References Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5183, https :5001/:5000, IIS Express, AppMusicRazor
├── Pages/
│   ├── _ViewImports.cshtml       @using AppRazor, @namespace AppRazor.Pages, tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Index.cshtml (+ .cs)      Welcome page          -> /
│   ├── DataSourceInfo.cshtml (+ .cs)  Data overview    -> /DataSourceInfo
│   ├── Members/Seed.cshtml (+ .cs)    Seed form        -> /Members/Seed
│   └── Shared/                   _Layout.cshtml, _LoginPartial.cshtml (empty placeholder), _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
└── wwwroot/                      index.html, css/, js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services:** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`); scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppRazor/appsettings.json) and launch settings
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets. In `launchSettings.json` the `https` profile runs with `ASPNETCORE_ENVIRONMENT=Production` (and an extra `MyOwn` variable); `http`, `IIS Express` and `AppMusicRazor` run as Development.

### Layout, imports and footer
- `_ViewImports.cshtml` enables tag helpers (`asp-page`, `asp-append-version`, the form tag helper with antiforgery token) in all pages and layouts, and lets pages refer to models without full namespaces.
- `_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Seed*), footer and the Seido scripts. The *Static* link is a plain `href="~/index.html"` since it points to a static file. Unlike AppMvc, this layout does not contain the Bootstrap modals.
- The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>` (injected in the layout) – a quick check of which environment and database the app is talking to. No credentials are displayed.

### Seed page
`Pages/Members/Seed.cshtml` + `Seed.cshtml.cs` (`SeedModel`); the `Members` folder is a natural place for protected pages later.
- `OnGetAsync()` loads `NrOfGroups` (seeded + unseeded music groups, from `GuestInfoAsync()`) and shows the page; `OnPostAsync()` validates the form, optionally removes all existing data (`RemoveSeedAsync(true)` and `(false)` – seeded **and** unseeded), calls `SeedAsync(n)` and redirects to the overview. An invalid POST reloads `NrOfGroups` before re-rendering.
- `[BindProperty]` on `NrOfItemsToSeed` (default 100, `[Required]`) and `RemoveSeeds` (default `true`) makes them bind from the posted form; `NrOfGroups` is display-only and not bound.
- Razor Pages validates the antiforgery token on POST automatically; the form tag helper adds the hidden `__RequestVerificationToken` field.
- `SeedAsync(n)` uses `SeedGenerator` with `app-seeds.json` to create `n` music groups, each with 2–4 albums and 2–4 artists picked from 100 generated artists. Operations run as `DatabaseConnections:DefaultDataUser`.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for modals and for selecting list items.

---

## 3. Comparison with AppMvc and next steps

| | AppMvc | AppRazor |
|---|---|---|
| Overview page | `HomeController.DataSourceInfo()` + `ViewModels/DataSourceInfo` + `Views/Home/DataSourceInfo.cshtml` | `Pages/DataSourceInfo.cshtml` + `DataSourceInfoModel` |
| Entry point | action method, found via `{controller}/{action}` route | `OnGetAsync` handler, route from the file path |
| View model | separate class in `ViewModels/` | the PageModel itself |
| Menu link | `asp-controller` + `asp-action` | `asp-page` |
| URL | `/Home/DataSourceInfo` | `/DataSourceInfo` |

Next: list pages for music groups, albums and artists (paging, search, view, edit, delete) through the injected services, CRUD with modals and validation helpers, and authentication/authorization using the already prepared pipeline.
