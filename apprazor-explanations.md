# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`11-del-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `10-list-search`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Deleting a group from the list with modal confirmation (compared with `10-list-search`)

### Purpose
The list could be searched and paged but was read-only. This branch adds a **Del** button on every row. Clicking it opens a **confirmation dialog** ("*&lt;name&gt; is about to be deleted.*" – *Cancel* / *Ok*); only after the user confirms with *Ok* is the group deleted in the database, and the list is shown again on the **same page** with the search filter kept. It is the first page that **changes** data from the list.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/ListOfGroups.cshtml](AppRazor/Pages/ListOfGroups.cshtml) | name column `col-md-12` | name column `col-md-10` + a `col-md-2` column with a **Del button per row**; a **Bootstrap modal (`#delModal`)** placed inside the form; a hidden **`pagenr`** field with the current page |
| [Pages/ListOfGroups.cshtml.cs](AppRazor/Pages/ListOfGroups.cshtml.cs) | handlers `OnGetAsync`, `OnPostSearchAsync` | + **`OnPostDeleteGroupAsync(Guid groupId, int pagenr)`** (ends with a redirect back to the list) |

No new JavaScript was needed: the two small helpers that already exist in `wwwroot/js/seido/` (`modals.js` and `select-list-items.js`, loaded by the layout) do the client-side work; this branch is the first to use them. Service and repository code are unchanged – `IMusicGroupsService.DeleteMusicGroupAsync(Guid)` already existed.

### How it works – step by step

```
 1. User clicks [Del] on a row                         (button type="button" – does NOT submit)
      ├─ Bootstrap opens #delModal                     (data-bs-toggle="modal" data-bs-target="#delModal")
      ├─ select-list-items.js  copies the row's id into the hidden input   <input type="hidden" name="groupId" data-seido-selected-item-id-target>
      └─ modals.js (on "show.bs.modal") sets the modal text from the button's data-seido-modal-body
 2. Modal shows "<name> is about to be deleted."   [Cancel] [Ok]
      ├─ Cancel / × / Esc  → modal closes, nothing is sent
      └─ Ok  (button type="submit" formaction="/ListOfGroups?handler=DeleteGroup")  → the surrounding <form> is POSTed
 3. POST /ListOfGroups?handler=DeleteGroup   fields: groupId, pagenr, UseSeeds, SearchFilter, __RequestVerificationToken
      ▼
    ListOfGroupsModel.OnPostDeleteGroupAsync(Guid groupId, int pagenr)       (token validated automatically)
      │ [BindProperty] has filled UseSeeds and SearchFilter from the form
      │ await _service.DeleteMusicGroupAsync(groupId)
      │ count what is left → clamp pagenr to the last existing page
      ▼
    302 redirect → GET /ListOfGroups?pagenr=<same page>&search=<same filter>   (Post/Redirect/Get)
      ▼
    OnGetAsync(pagenr, search)       the list, on the same page, without the deleted group
```

### The row button
```cshtml
<button type="button" class="btn btn-danger btn-sm m-1" data-bs-toggle="modal" data-bs-target="#delModal"
        data-seido-modal-body="@item.Name is about to be deleted."
        data-seido-selected-item-id="@item.MusicGroupId">
    Del
</button>
```
- **`type="button"`** – the button never submits the form; its only job is to open the dialog. All rows share **one** modal (`#delModal`); what differs per row is carried in `data-*` attributes.
- **`data-bs-toggle` / `data-bs-target`** – Bootstrap's declarative API: a click opens the element with id `delModal`.
- **`data-seido-modal-body`** – the text to put in the dialog for *this* row. Razor HTML-encodes `@item.Name` inside the attribute, and `modals.js` writes it with `textContent` (not `innerHTML`), so a group name can not inject markup.
- **`data-seido-selected-item-id`** – the id of the row's group (`MusicGroupId`), picked up by `select-list-items.js`.

### The two JavaScript helpers (existing files, now in use)
- **`select-list-items.js`** – at load it adds a `click` listener to every element that has `data-seido-selected-item-id`. When clicked, it copies that value into **every** `input[data-seido-selected-item-id-target]`, i.e. the hidden field `<input type="hidden" name="groupId" data-seido-selected-item-id-target />` that sits in the form. That is how the server later learns *which* group was chosen. (The Del buttons are rendered by the server, so they exist when the script runs.)
- **`modals.js`** – at load it adds a `show.bs.modal` listener to every `.modal.fade`. When a modal is about to show, it reads `event.relatedTarget` (the button that triggered it) and sets the modal's title, body, *Cancel* text and *Ok* text from `data-seido-modal-title/-body/-cancel/-ok` – only those that are present are changed, the others keep the modal's own text. It finds the buttons by class: `.btn-secondary` = *Cancel*, `.btn-primary` = *Ok* (the Ok button has both `btn-primary` and `btn-danger`). It also supports an AJAX variant (`data-seido-modal-post-url`), which this page does not use.

Because the order of the events is *click → select-list-items copies the id → Bootstrap shows the modal → modals.js sets the text*, the hidden field already holds the right id when the user presses *Ok*.

### The modal markup – inside the form
```cshtml
<div class="modal fade" id="delModal" tabindex="-1" aria-labelledby="softModalLabel" aria-hidden="true">
  <div class="modal-dialog"><div class="modal-content">
    <div class="modal-header"><h5 class="modal-title text-danger" id="softModalLabel">Confirm deletion</h5>
         <button type="button" class="btn-close" data-bs-dismiss="modal" aria-label="Close"></button></div>
    <div class="modal-body">Hello from modal</div>        <!-- replaced by data-seido-modal-body -->
    <div class="modal-footer">
      <button type="button" class="btn btn-secondary" data-bs-dismiss="modal">Cancel</button>
      <button type="submit" asp-page-handler="DeleteGroup" class="btn btn-primary btn-danger">Ok</button>
    </div>
  </div></div>
</div>
```
- The comment in the page says it: *"Modal in the form tag"*. Since the modal is written **inside** the `<form>`, its *Ok* submit button submits that form. That is why `groupId`, `UseSeeds`, `SearchFilter` and the antiforgery token (all fields of the form) are posted without any extra code.
- **`asp-page-handler="DeleteGroup"` on the Ok button** produces `formaction="/ListOfGroups?handler=DeleteGroup"`; Razor Pages then runs the handler `OnPostDeleteGroup[Async]`. The *Search* button of the same form uses `asp-page-handler="Search"`. One form, several handlers.
- *Cancel*, the × button and the Esc key close the dialog (`data-bs-dismiss="modal"`); nothing is submitted.
- Unlike AppMvc, the layout of this application contains no other modals; `#delModal` is the only `.modal.fade` on the page.

### The handler
```csharp
public async Task<IActionResult> OnPostDeleteGroupAsync(Guid groupId, int pagenr)
{
    await _service.DeleteMusicGroupAsync(groupId);

    //Count what is left, so we never redirect to a page that no longer exists
    var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, 0, 1);
    int lastPageNr = Math.Max(0, (int)Math.Ceiling((double)resp.DbItemsCount / PageSize) - 1);
    pagenr = Math.Clamp(pagenr, 0, lastPageNr);

    //Post/Redirect/Get
    return RedirectToPage("/ListOfGroups", new { pagenr, search = SearchFilter });
}
```
- **Two sources of input:** `Guid groupId` and `int pagenr` are *handler parameters* bound from the form fields `groupId` (the hidden input filled by `select-list-items.js`) and `pagenr` (a second hidden field holding **the page the user is on**: `<input type="hidden" name="pagenr" value="@Model.ThisPageNr" />`, added next to the `groupId` field); `UseSeeds` and `SearchFilter` are `[BindProperty]` properties of the PageModel, filled from the other form fields before the handler runs. The page number travels in a field named `pagenr` – the same name as in the list's query string – so the search handler is unaffected.
- **Handler naming:** `OnPostDeleteGroupAsync` ↔ `asp-page-handler="DeleteGroup"` (the `On<Verb>` prefix and `Async` suffix are not part of the handler name).
- **POST only:** `OnPost…` handlers answer POST requests; a delete is never triggered by a link. A plain GET to `/ListOfGroups?handler=DeleteGroup` does not delete anything – it simply runs `OnGetAsync` and shows the list.
- **Antiforgery:** Razor Pages validates the token on every POST automatically; the form tag helper adds the token field. A POST without a valid token is rejected with `400`, which blocks cross-site request forgery. (AppMvc needs an explicit `[ValidateAntiForgeryToken]` for the same protection.)
- **Post/Redirect/Get (PRG):** instead of rendering the list directly from the POST, the handler answers with an **HTTP 302 redirect** to the ordinary GET list URL (`RedirectToPage` builds `/ListOfGroups?pagenr=N&search=text`). The browser then does a normal GET, so the address bar shows the list URL, and **a refresh repeats only that harmless GET – the delete is not submitted again** (previously a refresh re-posted the delete of an already deleted id and ended in a `500`). The list itself is produced by the existing `OnGetAsync(int pagenr, string search)`, so the count, rows and page links come from the single code path used by every other visit to the list.
- **Stays on the same page:** `pagenr` and the search filter are passed on in the redirect, so the user returns to the page they were on, with the filter still in the search box.
- **Clamping to the last page:** if the deleted item was the only one on the last page, that page no longer exists. The handler therefore counts the remaining groups (a one-row read of page 0 – the repo returns the total `DbItemsCount` with every read), computes `lastPageNr = ceil(count / PageSize) - 1` (at least 0) and uses `Math.Clamp(pagenr, 0, lastPageNr)`. Example (verified): 101 groups, a delete posted with `pagenr=10` → the redirect goes to `pagenr=9`.

### What the repository does
`MusicGroupsDbRepos.DeleteMusicGroupAsync(Guid id)`:
1. Finds the group (`Where(MusicGroupId == id)` – a tracked entity).
2. **Throws `ArgumentException("Item … is not existing")` if there is none.**
3. `_dbContext.MusicGroups.Remove(item)` and `SaveChangesAsync()` – one SQL `DELETE` in a unit of work.
4. Returns the removed item in a `ResponseItemDto`.

How the group's albums and artist links are treated is decided by the relationship rules of the database model (outside this branch). The database user in `DatabaseConnections:DefaultDataUser` must be allowed to delete: with the default `dbo` it is; the lower-privileged `gstusr` and `usr` roles have no `DELETE` right on the data schema.

### Trying it
1. Open **Famous groups**, optionally search for a group (e.g. `Floyd`).
2. Click **Del** on a row: the dialog says "*&lt;name&gt; is about to be deleted.*". Press **Cancel** – nothing happens.
3. Go to page *2* of the list, click **Del** again and press **Ok** – the row disappears, you are still on page *2* (the URL is the normal list URL, e.g. `/ListOfGroups?pagenr=1`), the count drops by one and the search filter is still in the search box. Pressing F5 now just reloads the list.
4. Check **Data overview**: the number of seeded music groups is one lower.

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
│   ├── ListOfGroups.cshtml (+ .cs)    Search, paged list, delete with modal -> /ListOfGroups?pagenr=N&search=text
│   ├── ViewGroup.cshtml (+ .cs)       Group details    -> /ViewGroup?id=<guid>
│   ├── Members/Seed.cshtml (+ .cs)    Seed form        -> /Members/Seed
│   └── Shared/                   _Layout.cshtml, _LoginPartial.cshtml (empty placeholder), _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/ (modals.js, select-list-items.js), favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services:** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info), `/static` (redirect to `/index.html`), `/rock` and `/queen` (redirects to filtered lists).

### [appsettings.json](AppRazor/appsettings.json) and launch settings
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets. In `launchSettings.json` the `https` profile runs with `ASPNETCORE_ENVIRONMENT=Production` (and an extra `MyOwn` variable); `http`, `IIS Express` and `AppMusicRazor` run as Development.

### Layout, imports and footer
- `_ViewImports.cshtml` enables tag helpers (`asp-page`, `asp-page-handler`, `asp-route-*`, `asp-append-version`, the form tag helper with antiforgery token) in all pages and layouts, and lets pages refer to models without full namespaces.
- `_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), footer and the scripts (Bootstrap, jQuery, `modals.js`, `select-list-items.js`). The *Static* link is a plain `href="~/index.html"` since it points to a static file.
- The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### List, search and detail pages
- The list is read one page (10 groups) at a time (`pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too); the private `UpdatePagination(...)` computes the page links. Page numbers are zero-based in the URL and 1-based on screen.
- The search form posts `SearchFilter` and `UseSeeds` to `OnPostSearchAsync` (`[BindProperty]` on both properties); page links are GET links that carry `pagenr` and `search`. The filter matches name, genre or established year.
- Each group name links to `ViewGroupModel.OnGetAsync(Guid id)` (`/ViewGroup?id=<guid>`), which shows the group with its artists and albums read-only.

### Data overview and Seed pages
- `DataSourceInfoModel.OnGetAsync()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `Pages/Members/Seed.cshtml` + `.cs` (`SeedModel`): `OnGetAsync()` loads `NrOfGroups`; `OnPostAsync()` validates the form, optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.

---

## 3. Comparison with AppMvc and next steps

| | AppMvc | AppRazor |
|---|---|---|
| Delete action | `[HttpPost] [ValidateAntiForgeryToken] DeleteGroup(Guid groupId, int pagenr, ListOfGroups vm)` → `RedirectToAction` | `OnPostDeleteGroupAsync(Guid groupId, int pagenr)` → `RedirectToPage` |
| Ok button target | `asp-controller` + `asp-action` → `formaction="/Group/DeleteGroup"` | `asp-page-handler="DeleteGroup"` → `formaction="/ListOfGroups?handler=DeleteGroup"` |
| Binding of the filter | whole view model bound from the form | `[BindProperty]` on `UseSeeds` and `SearchFilter` |
| Antiforgery | explicit attribute on the action | automatic for Razor Pages POST |
| GET on the delete URL | `405` | `200` (list shown, nothing deleted) |
| Modals on the page | `#delModal` + the three modals from the layout | only `#delModal` |

Next: creating and editing groups (with their albums and artists), and authentication/authorization with the prepared pipeline – deletion in particular should later be restricted to authorised users.
