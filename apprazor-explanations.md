# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`8-list-pagination`**, with the emphasis on what this branch adds compared with the previous branch, `7-data-list`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Pagination of the group list (compared with `7-data-list`)

### Purpose
The previous branch listed *all* music groups on one page by reading the whole table. This branch changes the list so that only **one page of 10 groups** is read from the database and shown at a time, with **page links** (« 1 2 3 … ») to move between pages. Paging keeps the page small and the database work bounded however many groups exist.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/ListOfGroups.cshtml.cs](AppRazor/Pages/ListOfGroups.cshtml.cs) | `OnGetAsync()` – two service calls, reads all rows | `OnGetAsync(int pagenr)` – **one** service call, reads one page, calls `UpdatePagination(...)` |
| `ListOfGroupsModel` properties | `UseSeeds`, `MusicGroups`, `NrOfGroups` | + **pagination state** (`PageSize`, `ThisPageNr`, `PrevPageNr`, `NextPageNr`, `NrOfPages`, `NrVisiblePages`) |
| [Pages/ListOfGroups.cshtml](AppRazor/Pages/ListOfGroups.cshtml) | loop over the groups | loop over the groups **+ a Bootstrap pagination bar** |

`Program.cs`, the layout and the service layer are unchanged: the service call already had `pageNumber` and `pageSize` parameters, which the previous branch only used to fetch "everything". The URL is now `/ListOfGroups?pagenr=N`.

### Request flow

```
Browser ──GET /ListOfGroups?pagenr=3──► ListOfGroupsModel.OnGetAsync(int pagenr)
   │                                      │ ThisPageNr = 3
   │                                      │ ReadMusicGroupsAsync(seeded: true, flat: false, filter: null, pageNumber: 3, pageSize: 10)
   │                                      ▼
   │                                   MusicGroupsDbRepos: count query + page query
   │                                      │   SELECT COUNT(*) …                      → DbItemsCount (all matching rows)
   │                                      │   … ORDER … OFFSET 30 ROWS FETCH 10 …    → PageItems (rows 31–40)
   │                                      ▼
   │                                   MusicGroups = PageItems; NrOfGroups = DbItemsCount
   │                                   UpdatePagination(DbItemsCount)  → NrOfPages, PrevPageNr, NextPageNr, NrVisiblePages
   ◄── HTML: 10 names + pagination bar ─ Page()
```

### The PageModel
```csharp
public async Task<IActionResult> OnGetAsync(int pagenr)
{
    ThisPageNr = pagenr;

    var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, null, ThisPageNr, PageSize);
    MusicGroups = resp.PageItems;
    NrOfGroups = resp.DbItemsCount;

    UpdatePagination(resp.DbItemsCount);
    return Page();
}

private void UpdatePagination(int nrOfItems)
{
    NrOfPages      = (int)Math.Ceiling((double)nrOfItems / PageSize);
    PrevPageNr     = Math.Max(0, ThisPageNr - 1);
    NextPageNr     = Math.Min(NrOfPages - 1, ThisPageNr + 1);
    NrVisiblePages = Math.Min(10, NrOfPages);
}
```
- **Handler parameter binding:** `int pagenr` is bound by Razor Pages from the query string (`?pagenr=3`). If it is missing, `0` is used, so `/ListOfGroups` shows the first page. The page number is **zero-based** in URLs and in the service; the page displays it as `pagenr + 1`.
- **One request, one page:** the previous version needed two service calls (one to learn the count, one to read everything). The repo already returns both in the same response: `PageItems` (the requested page) and `DbItemsCount` (total number of matching rows). A single call is enough now, and only `PageSize` (10) groups – with their albums and artists – are loaded.
- **Where paging happens:** in the database. The repo applies `Skip(pageNumber * pageSize).Take(pageSize)` to the EF Core query, so only the 10 rows are transferred; a separate `CountAsync()` gives the total used for the page links.
- **PageModel = view model:** the pagination values are public properties of the PageModel and are read by the page as `Model.*`; the pagination arithmetic is a private method of the same class. (AppMvc has the same logic in a separate `ViewModels/ListOfGroups` class with a public `UpdatePagination`.) None of the properties is posted back, so there are no `[BindProperty]` attributes.

| Property | Meaning | Example with 100 groups, on page index 3 |
|---|---|---|
| `PageSize` | rows per page (read-only, 10) | 10 |
| `ThisPageNr` | zero-based index of the page being shown (set from `pagenr`) | 3 |
| `NrOfPages` | `ceil(total / PageSize)` | 10 |
| `PrevPageNr` | previous page, never below 0 | 2 |
| `NextPageNr` | next page, never above the last page (`NrOfPages - 1`) | 4 |
| `NrVisiblePages` | how many numbered links to render, at most 10 | 10 |

The `Math.Max`/`Math.Min` clamping means « on the first page and » on the last page link to the same page instead of to a non-existing one.

### The page – the pagination bar
Inside the `@if (Model.MusicGroups is not null)` block, after the list of names:
```cshtml
<nav aria-label="Standard pagination example">
  <ul class="pagination">
    <li class="page-item"><a class="page-link" asp-page="/ListOfGroups" asp-route-pagenr="@Model.PrevPageNr" aria-label="Previous"><span aria-hidden="true">&laquo;</span></a></li>
    @for (int i = 0; i < Model.NrVisiblePages; i++)
    {
      <li class="page-item"><a class="page-link" asp-page="/ListOfGroups" asp-route-pagenr="@i">@(i + 1)</a></li>
    }
    <li class="page-item"><a class="page-link" asp-page="/ListOfGroups" asp-route-pagenr="@Model.NextPageNr" aria-label="Next"><span aria-hidden="true">&raquo;</span></a></li>
  </ul>
</nav>
```
- It uses Bootstrap's *pagination* component (`nav` > `ul.pagination` > `li.page-item` > `a.page-link`); the `aria-label`s make the previous/next arrows understandable to screen readers.
- **Links are generated, not hand-written:** `asp-page="/ListOfGroups"` and `asp-route-pagenr` make the anchor tag helper (enabled by `_ViewImports.cshtml`) build `/ListOfGroups?pagenr=N`. Because `pagenr` is not part of the page's route template, it is appended as a query-string value; the handler parameter `int pagenr` receives it.
- The numbered links come from a `for` loop from `0` to `NrVisiblePages - 1`, labelled `i + 1` (users see 1-based numbers, the URL is zero-based).
- Each page request is a normal **GET** link – no form post, no antiforgery token, and the page can be bookmarked, shared and navigated with the browser's back button.

### Behaviour and limitations to be aware of
- **No "current page" highlight:** the markup has no `active` class on the current page's `page-item`, and the « / » items are never `disabled`. Adding `class="page-item @(i == Model.ThisPageNr ? "active" : "")"` would show the current page.
- **Only the first 10 pages are numbered:** `NrVisiblePages = min(10, NrOfPages)`; with more than 100 groups, pages beyond the 10th can only be reached with ». A sliding window around `ThisPageNr` would be the next refinement.
- **Out-of-range page numbers:** a page index past the last page gives an empty list (still HTTP 200, with the pagination bar). A **negative** value is not validated and makes the database query fail (`Skip` receives a negative number). Clamping `pagenr` in the handler (`Math.Max(0, pagenr)`) would harden this. (This was verified against the equivalent AppMvc page: `?pagenr=10` with 100 groups → empty list, `?pagenr=-1` → HTTP 500; the Razor handler runs the same logic.)
- **Empty database:** `NrOfPages` is 0, so the « and » links get `pagenr=-1`; the same validation would cover this case.
- `NrOfGroups` is stored (total count) but not displayed yet.
- Outside Development this app uses `UseDeveloperExceptionPage()`, so such an error would show a detailed error page – another reason to validate input.

### Trying it
1. Make sure the database holds more than 10 seeded groups (use **Seed**, default 100).
2. Open **Famous groups** – 10 names and the bar `« 1 2 … 10 »` are shown. Click a number or the arrows to move between pages; watch the URL change (`?pagenr=…`).

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
| Paged list | `GroupController.ListOfGroups(int pagenr)` + `ViewModels/ListOfGroups.UpdatePagination` + `Views/Group/ListOfGroups.cshtml` | `ListOfGroupsModel.OnGetAsync(int pagenr)` + private `UpdatePagination` + `Pages/ListOfGroups.cshtml` |
| Pagination state | public properties on the view model | public properties on the PageModel |
| Page links | `asp-controller` + `asp-action` + `asp-route-pagenr` | `asp-page` + `asp-route-pagenr` |
| URL | `/Group/ListOfGroups?pagenr=N` | `/ListOfGroups?pagenr=N` |

Next: searching in the list, viewing/editing/deleting a group, and authentication/authorization with the prepared pipeline.
