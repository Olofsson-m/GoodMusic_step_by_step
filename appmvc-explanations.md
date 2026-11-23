# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`10-list-search`**, with the emphasis on what this branch adds compared with the previous branch, `9-view-list-item`.

---

## 1. This branch: Searching the group list (compared with `9-view-list-item`)

### Purpose
The paged list could only show the groups in database order. This branch adds a **search box** above the list: the user types a word, and only the groups that match are listed (still paged, 10 per page). The search is carried through the page links, so the filtered result can be paged through, bookmarked and shared. Two short URLs (`/rock`, `/queen`) are added as ready-made searches.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Controllers/GroupController.cs](AppMvc/Controllers/GroupController.cs) | `ListOfGroups(int pagenr)` – filter always `null` | `ListOfGroups(int pagenr, string search)` passes the filter on; + **`SearchGroup(ListOfGroups vm)`** (POST) |
| [ViewModels/ListOfGroups.cs](AppMvc/ViewModels/ListOfGroups.cs) | paging state | + **`SearchFilter`** (the text typed by the user) |
| [Views/Group/ListOfGroups.cshtml](AppMvc/Views/Group/ListOfGroups.cshtml) | list + pagination bar | + a **`<form method="post">`** with *Use seeded data* checkbox, search box and *Search* button; pagination links also carry `asp-route-search`; the count is shown |
| [Program.cs](AppMvc/Program.cs) | aliases `/version`, `/static` | + aliases **`/rock`** and **`/queen`** |

The service and repository layers are unchanged: `ReadMusicGroupsAsync(seeded, flat, filter, pageNumber, pageSize)` already had a `filter` parameter, which the previous branches passed as `null`.

### Two ways to search – GET and POST

```
(A) Search from the form (POST)
Browser ──POST /Group/SearchGroup (SearchFilter, UseSeeds, antiforgery token)──► GroupController.SearchGroup(ListOfGroups vm)
   │                                         │ ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, vm.ThisPageNr (=0), vm.PageSize)
   │                                         │ vm.UpdatePagination(...)
   ◄── HTML (View("ListOfGroups", vm)) ─────┘   first page of the result

(B) Page links / bookmarks / aliases (GET)
Browser ──GET /Group/ListOfGroups?pagenr=2&search=Rock──► GroupController.ListOfGroups(int pagenr, string search)
   │                                         │ vm = new ListOfGroups { ThisPageNr = 2, SearchFilter = "Rock" }
   │                                         │ ReadMusicGroupsAsync(true, false, "Rock", 2, 10) → UpdatePagination
   ◄── HTML ───────────────────────────────┘

/rock ─302─► /Group/ListOfGroups?search=Rock        /queen ─302─► /Group/ListOfGroups?search=Queen
```
The search *form* posts (A); everything else – paging, bookmarks and the aliases – uses GET (B) with the filter in the query string. Both end in the same view and use the same view model.

### The form in the view
```cshtml
<form method="post" class="needs-validation" novalidate>
    <input asp-for="UseSeeds" class="form-check-input" type="checkbox" id="useSeeds"> Use seeded data
    <input asp-for="SearchFilter" class="form-control" type="search" placeholder="Search" aria-label="Search">
    <button asp-controller="Group" asp-action="SearchGroup" class="btn btn-outline-success" type="submit">Search</button>
    <input type="hidden" name="groupId" data-seido-selected-item-id-target />
    ...list and pagination bar...
</form>
```
- **`asp-for`** binds each input to a view-model property: it generates `name`/`id`/`value` (and for the bool, a hidden companion field so an *unchecked* box posts `false`). After a POST the text box shows the same text again, because the view model is rendered back.
- **`asp-controller` / `asp-action` on the button** make the button submit this form to `/Group/SearchGroup` instead of the form's own URL (rendered as a `formaction` attribute). The form itself has no action. This pattern lets one form contain several buttons with different targets – used in later pages.
- **Antiforgery:** the form tag helper automatically adds a hidden `__RequestVerificationToken` field, and the action has `[ValidateAntiForgeryToken]`; a POST without the token is answered with `400`.
- The hidden `groupId` input with `data-seido-selected-item-id-target` is a placeholder that `wwwroot/js/seido/select-list-items.js` fills when an item is selected; it is not used by this branch's functionality.
- A `<p>` above the form now shows `NrOfGroups`: *"The database contains N music groups the search criteria"* – the count of matching groups (the word *with* is missing in the text).

### The controller
```csharp
public async Task<IActionResult> ListOfGroups(int pagenr, string search)
{
    var vm = new ListOfGroups() { ThisPageNr = pagenr, SearchFilter = search };
    var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, vm.ThisPageNr, vm.PageSize);
    vm.MusicGroups = _resp.PageItems;  vm.NrOfGroups = _resp.DbItemsCount;
    vm.UpdatePagination(_resp.DbItemsCount);
    return View(vm);
}

[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> SearchGroup(ListOfGroups vm)
{
    var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, vm.ThisPageNr, vm.PageSize);
    vm.MusicGroups = _resp.PageItems;  vm.NrOfGroups = _resp.DbItemsCount;
    vm.UpdatePagination(_resp.DbItemsCount);
    return View("ListOfGroups", vm);
}
```
- **Binding:** in the GET action, `pagenr` and `search` are bound from the query string; in the POST action, MVC binds the **whole view model** from the posted form fields (`SearchFilter`, `UseSeeds`; `ThisPageNr` is not in the form, so it stays 0 and a new search always starts at the first page). No `[BindProperty]` is needed on a model that is an action parameter.
- **Returning another view:** `View("ListOfGroups", vm)` names the view explicitly; `View(vm)` would look for a view called `SearchGroup`.
- Both actions repeat the same read-and-paginate steps – a candidate for a small private helper.
- `SearchGroup` returns the page directly from the POST (no redirect). That is simple, but the browser shows `/Group/SearchGroup`, and *refreshing* asks the user to re-submit the form. The GET links (below) avoid this.

### The view model
```csharp
public bool UseSeeds { get; set; } = true;
public string SearchFilter { get; set; } = null;   // "ModelBinding for the form"
```
`SearchFilter` is the new property: it is posted from the text box, passed in from the query string by the GET action, handed to the service and rendered back into the text box and the page links.

### How the filter is applied (repository)
```csharp
filter ??= "";
.Where(i => i.Seeded == seeded &&
            (i.Name.ToLower().Contains(filter) ||
             i.strGenre.ToLower().Contains(filter) ||
             i.EstablishedYear.ToString().Contains(filter)))
```
- A group matches if the text occurs in its **name, genre or established year** (substring match). `null` is treated as `""`, which matches everything.
- The same `Where` is used for both the `CountAsync()` (total hits, used for paging) and the page query (`Skip/Take`), so `NrOfGroups` and the page links always reflect the *filtered* result.
- The filter is translated to SQL `LIKE '%…%'` and runs in the database; special characters are parameterised, so it is safe against SQL injection.
- **Case:** the C# side lowercases the *column*, not the filter text; case-insensitive matching therefore relies on the database collation (SQL Server's default is case-insensitive: `Rock` and `rock` both give 30 hits). On a case-sensitive collation, uppercase search words would not match.

Examples against the seeded sample data (100 groups): `Rock` → 30 groups (genre or name), `Jazz` → 22, `Floyd` → 17, `1986` → 1 (established year), `zzzz` → 0, empty → 100.

### Pagination keeps the search
Every page link now carries the filter:
```cshtml
<a class="page-link" asp-controller="Group" asp-action="ListOfGroups" asp-route-pagenr="@i" asp-route-search="@Model.SearchFilter">@(i + 1)</a>
```
`asp-route-search` is added to the query string (`?pagenr=1&search=Rock`); when `SearchFilter` is `null` the parameter is simply omitted. This is why the paged result is a GET link: it is bookmarkable and works with the browser's back button, which the POST result is not.

### The aliases
```csharp
app.MapGet("/rock",  () => Results.Redirect("/Group/ListOfGroups?search=Rock"));
app.MapGet("/queen", () => Results.Redirect("/Group/ListOfGroups?search=Queen"));
```
Minimal-API endpoints in `Program.cs` that answer with an HTTP 302 redirect to the search URL – handy short links (`/rock`) to a pre-filtered list, and a demonstration that the search can be driven entirely through the query string.

### Behaviour and limitations to be aware of
- **"Use seeded data" is not carried by the page links:** it is posted by the form, but the pagination links only carry `pagenr` and `search`. After unchecking the box, a search shows the unseeded rows (none in a freshly seeded database → 0 hits), but clicking a page number goes back to seeded data.
- **No results:** with 0 hits `NrOfPages` is 0 and the « / » links point to `pagenr=-1`; following one gives HTTP 500, because a negative page number is not validated (see the pagination notes: clamp `pagenr` with `Math.Max(0, pagenr)`).
- **No live feedback:** the search runs when the button is pressed (Enter inside the text box also submits, using the form's first submit button).
- The *Use seeded data* label is empty and the text *"Use seeded data"* is written after the checkbox – cosmetic; the label `for` does not wrap the text.

### Trying it
1. Open **Famous groups**, type `Rock` and press **Search**: the count line shows 30 and the first 10 matches are listed.
2. Click page *2*: the URL is `/Group/ListOfGroups?pagenr=1&search=Rock` and the filter is preserved.
3. Open `/queen` – you are redirected to the list filtered on `Queen`.

---

## 2. Application structure (current)

```
AppMvc/
├── AppMvc.csproj                 References Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations, HTTP pipeline, aliases /version /static /rock /queen
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5204, https :7029, IIS Express
├── Controllers/
│   ├── HomeController.cs         Index, DataSourceInfo, Error
│   ├── SeedController.cs         Seed (GET/POST)
│   └── GroupController.cs        ListOfGroups(pagenr, search), SearchGroup (POST), ViewGroup(id)
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs, ListOfGroups.cs (paging + search), ViewGroup.cs
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/                    ListOfGroups.cshtml (search form, paged list), ViewGroup.cshtml (details)
│   ├── Seed/Seed.cshtml          Seed form
│   └── Shared/                   _Layout.cshtml (+ .css), Error.cshtml, _ValidationScriptsPartial.cshtml
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppMvc/Program.cs)
- **Services:** `AddControllersWithViews()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`); scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()`; aliases `/version` (JSON with version info), `/static` (redirect to `/index.html`), `/rock` and `/queen` (redirects to filtered lists).

### [appsettings.json](AppMvc/appsettings.json)
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets.

### Layout and footer
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), three reusable Bootstrap modals, and a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user) from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### Paged list and detail page
- The list is read one page (10 groups) at a time: `pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too. `ViewModels/ListOfGroups.UpdatePagination(...)` computes previous/next page numbers (clamped) and the number of numbered links (max 10). Page numbers are zero-based in the URL and 1-based on screen.
- Each group name links to `GroupController.ViewGroup(Guid id)` (`/Group/ViewGroup/<guid>`), which reads the group with its artists and albums and shows them in a read-only form-style view.

### Data overview and Seed pages
- `HomeController.DataSourceInfo()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `SeedController` + `Views/Seed/Seed.cshtml`: `GET /Seed/Seed` shows the current number of groups; the POST (protected by `[ValidateAntiForgeryToken]`) optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for the modals and for selecting list items.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: deleting and editing groups from the list (the form around the list and the hidden `groupId` field are prepared for selecting an item), and authentication/authorization with the prepared pipeline.
