# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`10-list-search`**, with the emphasis on what this branch adds compared with the previous branch, `9-view-list-item`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Searching the group list (compared with `9-view-list-item`)

### Purpose
The paged list could only show the groups in database order. This branch adds a **search box** above the list: the user types a word, and only the groups that match are listed (still paged, 10 per page). The search is carried through the page links, so the filtered result can be paged through, bookmarked and shared. Two short URLs (`/rock`, `/queen`) are added as ready-made searches.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/ListOfGroups.cshtml.cs](AppRazor/Pages/ListOfGroups.cshtml.cs) | `OnGetAsync(int pagenr)` – filter always `null` | `OnGetAsync(int pagenr, string search)` passes the filter on; + **`OnPostSearchAsync()`** handler; **`[BindProperty]`** on `UseSeeds` and new `SearchFilter` |
| [Pages/ListOfGroups.cshtml](AppRazor/Pages/ListOfGroups.cshtml) | list + pagination bar | + a **`<form method="post">`** with *Use seeded data* checkbox, search box and *Search* button; pagination links also carry `asp-route-search`; the count is shown |
| [Program.cs](AppRazor/Program.cs) | aliases `/version`, `/static` | + aliases **`/rock`** and **`/queen`** |

The service and repository layers are unchanged: `ReadMusicGroupsAsync(seeded, flat, filter, pageNumber, pageSize)` already had a `filter` parameter, which the previous branches passed as `null`.

### Two ways to search – GET and POST

```
(A) Search from the form (POST, handler "Search")
Browser ──POST /ListOfGroups?handler=Search (SearchFilter, UseSeeds, antiforgery token)──► ListOfGroupsModel.OnPostSearchAsync()
   │                                         │ [BindProperty] fills SearchFilter and UseSeeds from the form
   │                                         │ ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, ThisPageNr (=0), PageSize)
   │                                         │ UpdatePagination(...)
   ◄── HTML (Page()) ───────────────────────┘   first page of the result

(B) Page links / bookmarks / aliases (GET)
Browser ──GET /ListOfGroups?pagenr=2&search=Rock──► ListOfGroupsModel.OnGetAsync(int pagenr, string search)
   │                                         │ ThisPageNr = 2; SearchFilter = "Rock"
   │                                         │ ReadMusicGroupsAsync(true, false, "Rock", 2, 10) → UpdatePagination
   ◄── HTML ───────────────────────────────┘

/rock ─302─► /ListOfGroups?search=Rock        /queen ─302─► /ListOfGroups?search=Queen
```
The search *form* posts (A); everything else – paging, bookmarks and the aliases – uses GET (B) with the filter in the query string. Both end in the same page and use the same PageModel.

### The form in the page
```cshtml
<form method="post" class="needs-validation" novalidate>
    <input asp-for="UseSeeds" class="form-check-input" type="checkbox" id="useSeeds">
    <input asp-for="SearchFilter" class="form-control" type="search" placeholder="Search" aria-label="Search">
    <button asp-page-handler="Search" class="btn btn-outline-success" type="submit">Search</button>
    <input type="hidden" name="groupId" data-seido-selected-item-id-target />
    ...list and pagination bar...
</form>
```
- **`asp-for`** binds each input to a PageModel property: it generates `name`/`id`/`value` (and for the bool, a hidden companion field so an *unchecked* box posts `false`). After a POST the text box shows the same text again, because the PageModel is rendered back.
- **`asp-page-handler="Search"`** on the button makes it submit to `/ListOfGroups?handler=Search` (rendered as a `formaction`); Razor Pages then calls the handler named `OnPostSearch[Async]`. The form itself has no action, so one form can hold several buttons with different handlers – used by later pages.
- **Antiforgery:** Razor Pages validates the token on every POST automatically; the form tag helper (enabled by `_ViewImports.cshtml`) adds the hidden `__RequestVerificationToken` field. A POST without the token is answered with `400`.
- The hidden `groupId` input with `data-seido-selected-item-id-target` is a placeholder that `wwwroot/js/seido/select-list-items.js` fills when an item is selected; it is not used by this branch's functionality.
- A `<p>` above the form now shows `NrOfGroups`: *"The database contains N music groups with the search criteria"* – the count of matching groups.

### The PageModel
```csharp
[BindProperty] public bool UseSeeds { get; set; } = true;
[BindProperty] public string SearchFilter { get; set; } = null;     // "ModelBinding for the form"

public async Task<IActionResult> OnGetAsync(int pagenr, string search)
{
    ThisPageNr = pagenr;
    SearchFilter = search;
    var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, ThisPageNr, PageSize);
    MusicGroups = resp.PageItems;  NrOfGroups = resp.DbItemsCount;
    UpdatePagination(resp.DbItemsCount);
    return Page();
}

public async Task<IActionResult> OnPostSearchAsync()
{
    var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, ThisPageNr, PageSize);
    MusicGroups = resp.PageItems;  NrOfGroups = resp.DbItemsCount;
    UpdatePagination(resp.DbItemsCount);
    return Page();     // "Page is rendered as the postback is part of the form tag"
}
```
- **`[BindProperty]`** is what makes Razor Pages copy the posted form fields into `UseSeeds` and `SearchFilter` *before* the handler runs. It applies to POST only (not GET) by default – that is why the GET handler receives the filter as a **handler parameter** (`string search`) instead and assigns it to the property itself.
- **Handler naming:** `OnPostSearchAsync` ↔ `asp-page-handler="Search"` (the `On<Verb>` prefix and the `Async` suffix are not part of the handler name).
- **New search = first page:** `ThisPageNr` is not part of the form, so it stays `0` after a POST.
- **PageModel = view model:** the PageModel holds the filter, the paging state and the result; the page reads them as `Model.*`.
- Both handlers repeat the same read-and-paginate steps – a candidate for a small private method.
- `OnPostSearchAsync` returns `Page()` directly (no redirect). That is simple, but the browser shows `/ListOfGroups?handler=Search`, and *refreshing* asks the user to re-submit the form. The GET links (below) avoid this.

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
<a class="page-link" asp-page="/ListOfGroups" asp-route-pagenr="@i" asp-route-search="@Model.SearchFilter">@(i + 1)</a>
```
`asp-route-search` is added to the query string (`?pagenr=1&search=Rock`) and received by `OnGetAsync(int pagenr, string search)`; when `SearchFilter` is `null` the parameter is simply omitted. This is why the paged result is a GET link: it is bookmarkable and works with the browser's back button, which the POST result is not.

### The aliases
```csharp
app.MapGet("/rock",  () => Results.Redirect("/ListOfGroups?search=Rock"));
app.MapGet("/queen", () => Results.Redirect("/ListOfGroups?search=Queen"));
```
Minimal-API endpoints in `Program.cs` that answer with an HTTP 302 redirect to the search URL – handy short links (`/rock`) to a pre-filtered list, running side by side with Razor Pages routing.

### Behaviour and limitations to be aware of
- **"Use seeded data" is not carried by the page links:** it is posted by the form, but the pagination links only carry `pagenr` and `search`. After unchecking the box, a search shows the unseeded rows (none in a freshly seeded database → 0 hits), but clicking a page number goes back to seeded data.
- **No results:** with 0 hits `NrOfPages` is 0 and the « / » links point to `pagenr=-1`; following one fails (a negative page number is not validated – clamp `pagenr` with `Math.Max(0, pagenr)`). Outside Development this app shows the detailed developer error page.
- **No live feedback:** the search runs when the button is pressed (Enter inside the text box also submits, using the form's first submit button).
- Verified results were observed in both applications (same counts for the same search words).

### Trying it
1. Open **Famous groups**, type `Rock` and press **Search**: the count line shows 30 and the first 10 matches are listed.
2. Click page *2*: the URL is `/ListOfGroups?pagenr=1&search=Rock` and the filter is preserved.
3. Open `/queen` – you are redirected to the list filtered on `Queen`.

---

## 2. Application structure (current)

```
AppRazor/
├── AppRazor.csproj               References Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations, HTTP pipeline, aliases /version /static /rock /queen
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator
├── Properties/launchSettings.json  Profiles http :5183, https :5001/:5000, IIS Express, AppMusicRazor
├── Pages/
│   ├── _ViewImports.cshtml       @using AppRazor, @namespace AppRazor.Pages, tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Index.cshtml (+ .cs)      Welcome page          -> /
│   ├── DataSourceInfo.cshtml (+ .cs)  Data overview    -> /DataSourceInfo
│   ├── ListOfGroups.cshtml (+ .cs)    Search + paged list -> /ListOfGroups?pagenr=N&search=text
│   ├── ViewGroup.cshtml (+ .cs)       Group details    -> /ViewGroup?id=<guid>
│   ├── Members/Seed.cshtml (+ .cs)    Seed form        -> /Members/Seed
│   └── Shared/                   _Layout.cshtml, _LoginPartial.cshtml (empty placeholder), _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services:** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info), `/static` (redirect to `/index.html`), `/rock` and `/queen` (redirects to filtered lists).

### [appsettings.json](AppRazor/appsettings.json) and launch settings
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets. In `launchSettings.json` the `https` profile runs with `ASPNETCORE_ENVIRONMENT=Production` (and an extra `MyOwn` variable); `http`, `IIS Express` and `AppMusicRazor` run as Development.

### Layout, imports and footer
- `_ViewImports.cshtml` enables tag helpers (`asp-page`, `asp-page-handler`, `asp-route-*`, `asp-append-version`, the form tag helper with antiforgery token) in all pages and layouts, and lets pages refer to models without full namespaces.
- `_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), footer and the Seido scripts. The *Static* link is a plain `href="~/index.html"` since it points to a static file. Unlike AppMvc, this layout does not contain the Bootstrap modals.
- The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### Paged list and detail page
- The list is read one page (10 groups) at a time: `pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too. The private `UpdatePagination(...)` computes previous/next page numbers (clamped) and the number of numbered links (max 10). Page numbers are zero-based in the URL and 1-based on screen.
- Each group name links to `ViewGroupModel.OnGetAsync(Guid id)` (`/ViewGroup?id=<guid>`), which reads the group with its artists and albums and shows them in a read-only form-style page.

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
| Search from the form | `[HttpPost] SearchGroup(ListOfGroups vm)`, button `asp-controller` + `asp-action` | `OnPostSearchAsync()`, button `asp-page-handler="Search"` |
| Binding of form fields | the action parameter (whole view model) is bound from the form | `[BindProperty]` on `UseSeeds` and `SearchFilter` |
| Antiforgery | explicit `[ValidateAntiForgeryToken]` on the action | automatic for Razor Pages POST |
| GET with filter | `ListOfGroups(int pagenr, string search)` | `OnGetAsync(int pagenr, string search)` |
| Search URL | `/Group/ListOfGroups?pagenr=N&search=text` | `/ListOfGroups?pagenr=N&search=text` |
| Aliases | `/rock`, `/queen` → `/Group/ListOfGroups?search=…` | `/rock`, `/queen` → `/ListOfGroups?search=…` |

Next: deleting and editing groups from the list (the form around the list and the hidden `groupId` field are prepared for selecting an item), and authentication/authorization with the prepared pipeline.
