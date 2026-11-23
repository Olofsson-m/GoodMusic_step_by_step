# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`9-view-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `8-list-pagination`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Detail view of a music group (compared with `8-list-pagination`)

### Purpose
The paged list showed only the *names* of the groups. This branch adds a **detail page for one group** – name, genre, established year, its **artists** and its **albums** – and turns each name in the list into a **link** to that page. It is the first page that works on a single item identified by its id.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/ViewGroup.cshtml](AppRazor/Pages/ViewGroup.cshtml) | – | **new** read-only detail page (route `/ViewGroup`) |
| [Pages/ViewGroup.cshtml.cs](AppRazor/Pages/ViewGroup.cshtml.cs) | – | **new** `ViewGroupModel` PageModel with `OnGetAsync(Guid id)` |
| [Pages/ListOfGroups.cshtml](AppRazor/Pages/ListOfGroups.cshtml) | group names as plain text | each name is a **link** `asp-page="/ViewGroup" asp-route-id="@item.MusicGroupId"` |

`Program.cs`, the layout and the service layer are unchanged; `MapRazorPages()` picks up the new page automatically because the route comes from the file location. The detail page is not in the menu; it is reached only by clicking a group in the list.

### Request flow

```
Browser ──click "The Floyd …" in the list──► GET /ViewGroup?id=9ad6a0fb-…
   │                                           ▼
   │                                      ViewGroupModel.OnGetAsync(Guid id)
   │                                           │ _service.ReadMusicGroupAsync(id, flat: false)
   │                                           ▼
   │                                      MusicGroupsServiceDb ── 1:1 ──► MusicGroupsDbRepos.ReadMusicGroupAsync(id, false)
   │                                           │ _dbContext.MusicGroups.AsNoTracking()
   │                                           │     .Include(ArtistsDbM).Include(AlbumsDbM).Where(MusicGroupId == id).FirstOrDefaultAsync()
   │                                           ▼
   │                                      ResponseItemDto<IMusicGroup>.Item  (group + artists + albums)
   │                                      MusicGroup = Item  → Page()
   ◄── HTML (Pages/ViewGroup.cshtml inside _Layout.cshtml) ──┘
```

### The link in the list
```cshtml
<a asp-page="/ViewGroup" asp-route-id="@item.MusicGroupId">@item.Name</a>
```
The anchor tag helper (enabled by `_ViewImports.cshtml`) builds the URL from the page path and route values. As `ViewGroup.cshtml` has a plain `@page` directive without a route template, `id` is appended as a **query-string value**: `/ViewGroup?id=9ad6a0fb-27b9-4062-9e6e-00ad03ec77a0`. (A route template such as `@page "{id:guid}"` would produce `/ViewGroup/<guid>` instead.) It is a plain GET link – bookmarkable and shareable.

### The PageModel
```csharp
public class ViewGroupModel : PageModel
{
    readonly IMusicGroupsService _service;  readonly ILogger<ViewGroupModel> _logger;
    public IMusicGroup MusicGroup { get; set; }

    public async Task<IActionResult> OnGetAsync(Guid id)
    {
        MusicGroup = (await _service.ReadMusicGroupAsync(id, false)).Item;
        return Page();
    }

    public ViewGroupModel(IMusicGroupsService service, ILogger<ViewGroupModel> logger) { ... }
}
```
- **Handler parameter binding:** `Guid id` is bound by Razor Pages from the query string (`?id=<guid>`) – the same mechanism as `int pagenr` in the list page. No manual parsing (`Guid.Parse(Request.Query["id"])`) is needed.
- **Full object graph:** `flat: false` makes the repo load the group **together with its artists and albums** (`.Include(...)`), which is exactly what the page needs. With `flat: true` only the group's own columns would be read and the artist and album lists would be empty.
- **Read-only query:** the repo uses `AsNoTracking()`, since nothing is going to be modified; this is cheaper and avoids circular-reference problems when entities reference each other (group ↔ artists).
- **Interface-typed result:** the service returns `IMusicGroup` (from the `Models` project) with `Name`, `Genre`, `EstablishedYear`, `Albums` and `Artists`, so the web app never touches the EF entity classes.
- **PageModel = view model:** the single public property `MusicGroup` is read by the page as `Model.MusicGroup`. (AppMvc needs a separate `ViewModels/ViewGroup` class for this.) It has no `[BindProperty]` since nothing is posted.

### The page
[Pages/ViewGroup.cshtml](AppRazor/Pages/ViewGroup.cshtml) starts with `@page` and `@model ViewGroupModel` (short name thanks to `@namespace` in `_ViewImports.cshtml`), title *View group*. It has three sections, all inside a `<form class="needs-validation" novalidate>`:
1. **Group details** – three `<input type="text" class="form-control" … readonly>` fields with the group's **name**, **genre** and **established year** (`value="@Model.MusicGroup.Name"` etc.). `readonly` shows the values in form-control styling but prevents editing; this layout is reused as the basis for an edit form.
2. **Artists** – a heading and a grid with a header cell *Name*, then a `foreach` over `Model.MusicGroup.Artists` that prints `@item.FirstName @item.LastName` in a `themed-grid-col` cell.
3. **Albums** – a grid with header cells *Name* and *Release Year*, then a `foreach` over `Model.MusicGroup.Albums` printing `@item.Name` (`col-md-10`) and `@item.ReleaseYear` (`col-md-2`).

Notes:
- Bootstrap grid classes (`row`, `col-sm-6`, `col-md-10`, `col-md-2`, `g-3`) lay the page out responsively; `themed-grid-head-col` / `themed-grid-col` come from `wwwroot/css/grid.css`.
- Razor HTML-encodes all values (including inside the `value="…"` attributes), so data from the database cannot inject markup.
- The `<form>` has no method or inputs that post anything; it is only a container for Bootstrap's form styling. There is nothing to submit, so no antiforgery handling is needed on this page.
- `<label for="…">` is connected to each input via `id`, which is good for accessibility.

### Error behaviour (verified)
| Request | Result |
|---|---|
| `/ViewGroup?id=<existing guid>` | 200 – details shown |
| `/ViewGroup?id=<well-formed guid that does not exist>` | **500** – the repo throws `ArgumentException("Item … is not existing")` |
| `/ViewGroup` (no id) | **500** – `id` becomes `Guid.Empty`, which does not exist |

Outside Development this app uses `UseDeveloperExceptionPage()`, so such an error would show a detailed error page. A friendlier result would be `NotFound()` when the item does not exist.

### Trying it
1. Open **Famous groups** and click a group name.
2. The detail page shows name, genre and year plus the group's artists and albums; use the browser's back button to return to the same list page.

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
│   ├── ListOfGroups.cshtml (+ .cs)    Paged list       -> /ListOfGroups?pagenr=N
│   ├── ViewGroup.cshtml (+ .cs)       Group details    -> /ViewGroup?id=<guid>
│   ├── Members/Seed.cshtml (+ .cs)    Seed form        -> /Members/Seed
│   └── Shared/                   _Layout.cshtml, _LoginPartial.cshtml (empty placeholder), _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services:** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppRazor/appsettings.json) and launch settings
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets. In `launchSettings.json` the `https` profile runs with `ASPNETCORE_ENVIRONMENT=Production` (and an extra `MyOwn` variable); `http`, `IIS Express` and `AppMusicRazor` run as Development.

### Layout, imports and footer
- `_ViewImports.cshtml` enables tag helpers (`asp-page`, `asp-route-*`, `asp-append-version`, the form tag helper with antiforgery token) in all pages and layouts, and lets pages refer to models without full namespaces.
- `_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), footer and the Seido scripts. The *Static* link is a plain `href="~/index.html"` since it points to a static file. Unlike AppMvc, this layout does not contain the Bootstrap modals.
- The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### Paged list
`ListOfGroupsModel.OnGetAsync(int pagenr)` reads one page of 10 groups with one service call (`pageNumber = pagenr`, `pageSize = 10`; the database does `Skip/Take` and returns the total count too). The private `UpdatePagination(...)` computes the previous/next page numbers (clamped) and the number of numbered links (max 10); the page renders a Bootstrap pagination bar with `asp-route-pagenr` links. Page numbers are zero-based in the URL and 1-based on screen.

### Data overview and Seed pages
- `DataSourceInfoModel.OnGetAsync()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `Pages/Members/Seed.cshtml` + `.cs` (`SeedModel`): `OnGetAsync()` loads `NrOfGroups`; `OnPostAsync()` validates the form, optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview. `[BindProperty]` is used on `NrOfItemsToSeed` and `RemoveSeeds`; Razor Pages validates the antiforgery token on POST automatically.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for modals and selecting list items.

---

## 3. Comparison with AppMvc and next steps

| | AppMvc | AppRazor |
|---|---|---|
| Detail page | `GroupController.ViewGroup(Guid id)` + `ViewModels/ViewGroup` + `Views/Group/ViewGroup.cshtml` | `ViewGroupModel.OnGetAsync(Guid id)` + `Pages/ViewGroup.cshtml` |
| Id binding | route segment `/Group/ViewGroup/<guid>` or `?id=` (default route `{id?}`) | query string `/ViewGroup?id=<guid>` (plain `@page`) |
| View model | separate class in `ViewModels/` | the PageModel itself |
| Link in the list | `asp-controller` + `asp-action` + `asp-route-id` | `asp-page` + `asp-route-id` |

Next: searching in the list, editing and deleting a group (the read-only fields of the detail page are a natural starting point), and authentication/authorization with the prepared pipeline.
