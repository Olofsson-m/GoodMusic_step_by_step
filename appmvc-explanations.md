# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`8-list-pagination`**, with the emphasis on what this branch adds compared with the previous branch, `7-data-list`.

---

## 1. This branch: Pagination of the group list (compared with `7-data-list`)

### Purpose
The previous branch listed *all* music groups on one page by reading the whole table. This branch changes the list so that only **one page of 10 groups** is read from the database and shown at a time, with **page links** (« 1 2 3 … » ) to move between pages. Paging keeps the page small and the database work bounded however many groups exist.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Controllers/GroupController.cs](AppMvc/Controllers/GroupController.cs) | `ListOfGroups()` – two service calls, reads all rows | `ListOfGroups(int pagenr)` – **one** service call, reads one page, calls `vm.UpdatePagination(...)` |
| [ViewModels/ListOfGroups.cs](AppMvc/ViewModels/ListOfGroups.cs) | `UseSeeds`, `MusicGroups`, `NrOfGroups` | + **pagination state** (`PageSize`, `ThisPageNr`, `PrevPageNr`, `NextPageNr`, `NrOfPages`, `NrVisiblePages`) and `UpdatePagination(...)` |
| [Views/Group/ListOfGroups.cshtml](AppMvc/Views/Group/ListOfGroups.cshtml) | loop over the groups | loop over the groups **+ a Bootstrap pagination bar** |

`Program.cs`, the layout and the service layer are unchanged: the service call already had `pageNumber` and `pageSize` parameters, which the previous branch only used to fetch "everything". The URL is now `/Group/ListOfGroups?pagenr=N` (or `/Group/ListOfGroups/…` via the default route; the page number is a query-string value).

### Request flow

```
Browser ──GET /Group/ListOfGroups?pagenr=3──► GroupController.ListOfGroups(int pagenr)
   │                                            │ vm = new ListOfGroups { ThisPageNr = 3 }
   │                                            │ ReadMusicGroupsAsync(seeded: true, flat: false, filter: null, pageNumber: 3, pageSize: 10)
   │                                            ▼
   │                                         MusicGroupsDbRepos: count query + page query
   │                                            │   SELECT COUNT(*) …                      → DbItemsCount (all matching rows)
   │                                            │   … ORDER … OFFSET 30 ROWS FETCH 10 …    → PageItems (rows 31–40)
   │                                            ▼
   │                                         vm.MusicGroups = PageItems; vm.NrOfGroups = DbItemsCount
   │                                         vm.UpdatePagination(DbItemsCount)   → NrOfPages, PrevPageNr, NextPageNr, NrVisiblePages
   ◄── HTML: 10 names + pagination bar ───── View(vm)
```

### The controller action
```csharp
public async Task<IActionResult> ListOfGroups(int pagenr)
{
    var vm = new ListOfGroups() { ThisPageNr = pagenr };

    var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, null, vm.ThisPageNr, vm.PageSize);
    vm.MusicGroups = _resp.PageItems;
    vm.NrOfGroups = _resp.DbItemsCount;

    vm.UpdatePagination(_resp.DbItemsCount);
    return View(vm);
}
```
- **Parameter binding:** `int pagenr` is bound by MVC from the query string (`?pagenr=3`). If the parameter is missing, the default `0` is used, so `/Group/ListOfGroups` shows the first page. (The page number is **zero-based** in URLs and in the service; the view displays it as `pagenr + 1`.)
- **One request, one page:** the previous version needed two service calls (one to learn the count, one to read everything). The repo already returns both in the same response: `PageItems` (the requested page) and `DbItemsCount` (the total number of matching rows). Now a single call is enough, and only `PageSize` (10) groups – with their albums and artists – are loaded.
- **Where paging happens:** in the database. The repo applies `Skip(pageNumber * pageSize).Take(pageSize)` to the EF Core query, so only the 10 rows are transferred; a separate `CountAsync()` gives the total used for the page links.
- **Separation of concerns:** the controller only passes the numbers; the arithmetic for the page links lives in the view model.

### The view model – pagination logic
```csharp
public int NrOfPages { get; set; }
public int PageSize { get; } = 10;
public int ThisPageNr { get; set; } = 0;
public int PrevPageNr { get; set; } = 0;
public int NextPageNr { get; set; } = 0;
public int NrVisiblePages { get; set; } = 0;

public void UpdatePagination(int nrOfItems)
{
    NrOfPages      = (int)Math.Ceiling((double)nrOfItems / PageSize);
    PrevPageNr     = Math.Max(0, ThisPageNr - 1);
    NextPageNr     = Math.Min(NrOfPages - 1, ThisPageNr + 1);
    NrVisiblePages = Math.Min(10, NrOfPages);
}
```
| Property | Meaning | Example with 100 groups, on page index 3 |
|---|---|---|
| `PageSize` | rows per page (read-only, 10) | 10 |
| `ThisPageNr` | zero-based index of the page being shown (set by the controller from `pagenr`) | 3 |
| `NrOfPages` | `ceil(total / PageSize)` | 10 |
| `PrevPageNr` | previous page, never below 0 | 2 |
| `NextPageNr` | next page, never above the last page (`NrOfPages - 1`) | 4 |
| `NrVisiblePages` | how many numbered links to render, at most 10 | 10 |

The `Math.Max`/`Math.Min` clamping means « on the first page and » on the last page link to the same page instead of to a non-existing one. Keeping this logic in the view model (instead of in the view) makes it testable and keeps the Razor view free of arithmetic.

### The view – the pagination bar
Inside the `@if (Model.MusicGroups is not null)` block, after the list of names:
```cshtml
<nav aria-label="Standard pagination example">
  <ul class="pagination">
    <li class="page-item"><a class="page-link" asp-controller="Group" asp-action="ListOfGroups" asp-route-pagenr="@Model.PrevPageNr" aria-label="Previous"><span aria-hidden="true">&laquo;</span></a></li>
    @for (int i = 0; i < Model.NrVisiblePages; i++)
    {
      <li class="page-item"><a class="page-link" asp-controller="Group" asp-action="ListOfGroups" asp-route-pagenr="@i">@(i + 1)</a></li>
    }
    <li class="page-item"><a class="page-link" asp-controller="Group" asp-action="ListOfGroups" asp-route-pagenr="@Model.NextPageNr" aria-label="Next"><span aria-hidden="true">&raquo;</span></a></li>
  </ul>
</nav>
```
- It uses Bootstrap's *pagination* component (`nav` > `ul.pagination` > `li.page-item` > `a.page-link`); the `aria-label`s make the previous/next arrows understandable to screen readers.
- **Links are generated, not hand-written:** `asp-controller`, `asp-action` and `asp-route-pagenr` make the anchor tag helper build `/Group/ListOfGroups?pagenr=N` through the routing system. Because `pagenr` is not part of the route pattern, it is appended as a query-string value.
- The numbered links are produced by a `for` loop from `0` to `NrVisiblePages - 1`, labelled `i + 1` (users see 1-based numbers, the URL is zero-based).
- Each page request is a normal **GET** link – no form post, no antiforgery token, and the page can be bookmarked, shared and navigated with the browser's back button.

### Behaviour and limitations to be aware of
- **No "current page" highlight:** the markup has no `active` class on the current page's `page-item`, and the « / » items are never `disabled`. Adding `class="page-item @(i == Model.ThisPageNr ? "active" : "")"` would show the current page.
- **Only the first 10 pages are numbered:** `NrVisiblePages = min(10, NrOfPages)`; with more than 100 groups, pages beyond the 10th can only be reached with ». A sliding window around `ThisPageNr` would be the next refinement.
- **Out-of-range page numbers:** `?pagenr=10` or `99` with 100 groups gives an empty list (still HTTP 200, with the pagination bar). A **negative** value (`?pagenr=-1`) is not validated and makes the database query fail (HTTP 500, because `Skip` gets a negative number). Clamping `pagenr` in the controller (`Math.Max(0, pagenr)`) would harden this.
- **Empty database:** `NrOfPages` is 0, so the « and » links get `pagenr=-1`; the same validation would cover this case.
- `NrOfGroups` is stored in the view model (total count) but not displayed yet.

### Trying it
1. Make sure the database holds more than 10 seeded groups (use **Seed**, default 100).
2. Open **Famous groups** – 10 names and the bar `« 1 2 … 10 »` are shown. Click a number or the arrows to move between pages; watch the URL change (`?pagenr=…`).

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
│   ├── SeedController.cs         Seed (GET/POST)
│   └── GroupController.cs        ListOfGroups(pagenr)
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs, ListOfGroups.cs (with pagination)
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/ListOfGroups.cshtml Paged list of music groups
│   ├── Seed/Seed.cshtml          Seed form
│   └── Shared/                   _Layout.cshtml (+ .css), Error.cshtml, _ValidationScriptsPartial.cshtml
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppMvc/Program.cs)
- **Services:** `AddControllersWithViews()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`); scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()`; aliases `/version` (JSON with version info) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppMvc/appsettings.json)
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets.

### Layout and footer
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), three reusable Bootstrap modals, and a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user) from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### Data overview and Seed pages
- `HomeController.DataSourceInfo()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `SeedController` + `Views/Seed/Seed.cshtml`: `GET /Seed/Seed` shows the current number of groups; the POST (protected by `[ValidateAntiForgeryToken]`) optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for the modals and for selecting list items.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: searching in the list, viewing/editing/deleting a group (using the album and artist services already injected in `GroupController`), and authentication/authorization with the prepared pipeline.
