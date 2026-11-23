# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`11-del-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `10-list-search`.

---

## 1. This branch: Deleting a group from the list with modal confirmation (compared with `10-list-search`)

### Purpose
The list could be searched and paged but was read-only. This branch adds a **Del** button on every row. Clicking it opens a **confirmation dialog** ("*&lt;name&gt; is about to be deleted.*" – *Cancel* / *Ok*); only after the user confirms with *Ok* is the group deleted in the database, and the list is shown again on the **same page** with the search filter kept. It is the first page that **changes** data from the list.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Views/Group/ListOfGroups.cshtml](AppMvc/Views/Group/ListOfGroups.cshtml) | name column `col-md-12` | name column `col-md-10` + a `col-md-2` column with a **Del button per row**; a **Bootstrap modal (`#delModal`)** placed inside the form; a hidden **`pagenr`** field with the current page |
| [Controllers/GroupController.cs](AppMvc/Controllers/GroupController.cs) | `ListOfGroups`, `SearchGroup` | + **`DeleteGroup(Guid groupId, int pagenr, ListOfGroups vm)`** (POST, ends with a redirect back to the list) |

No new JavaScript was needed: the two small helpers that already exist in `wwwroot/js/seido/` (`modals.js` and `select-list-items.js`, loaded by the layout) do the client-side work; this branch is the first to use them. The view model, service and repository code are unchanged – `IMusicGroupsService.DeleteMusicGroupAsync(Guid)` already existed.

### How it works – step by step

```
 1. User clicks [Del] on a row                         (button type="button" – does NOT submit)
      ├─ Bootstrap opens #delModal                     (data-bs-toggle="modal" data-bs-target="#delModal")
      ├─ select-list-items.js  copies the row's id into the hidden input   <input type="hidden" name="groupId" data-seido-selected-item-id-target>
      └─ modals.js (on "show.bs.modal") sets the modal text from the button's data-seido-modal-body
 2. Modal shows "<name> is about to be deleted."   [Cancel] [Ok]
      ├─ Cancel / × / Esc  → modal closes, nothing is sent
      └─ Ok  (button type="submit" formaction="/Group/DeleteGroup")  → the surrounding <form> is POSTed
 3. POST /Group/DeleteGroup   fields: groupId, pagenr, UseSeeds, SearchFilter, __RequestVerificationToken
      ▼
    GroupController.DeleteGroup(Guid groupId, int pagenr, ListOfGroups vm)   [HttpPost] [ValidateAntiForgeryToken]
      │ await _mg_service.DeleteMusicGroupAsync(groupId)
      │ count what is left → clamp pagenr to the last existing page
      ▼
    302 redirect → GET /Group/ListOfGroups?pagenr=<same page>&search=<same filter>   (Post/Redirect/Get)
      ▼
    ListOfGroups(pagenr, search)       the list, on the same page, without the deleted group
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
      <button type="submit" asp-controller="Group" asp-action="DeleteGroup" class="btn btn-primary btn-danger">Ok</button>
    </div>
  </div></div>
</div>
```
- The comment in the view says it: *"Modal in the form tag"*. Since the modal is written **inside** the `<form>`, its *Ok* submit button submits that form. That is why `groupId`, `UseSeeds`, `SearchFilter` and the antiforgery token (all fields of the form) are posted without any extra code.
- **`asp-controller` / `asp-action` on the Ok button** produce `formaction="/Group/DeleteGroup"`, so this button posts to the delete action while the *Search* button of the same form posts to `SearchGroup`. One form, several actions.
- *Cancel*, the × button and the Esc key close the dialog (`data-bs-dismiss="modal"`); nothing is submitted.
- The layout contains three more Bootstrap modals (`softModal`, `hardModal`, `dangerModal`). `modals.js` attaches to all `.modal.fade` elements, so it also handles them; `#delModal` is a page-specific copy in the form.

### The controller action
```csharp
[HttpPost]
[ValidateAntiForgeryToken]
public async Task<IActionResult> DeleteGroup(Guid groupId, int pagenr, ListOfGroups vm)
{
    await _mg_service.DeleteMusicGroupAsync(groupId);

    //Count what is left, so we never redirect to a page that no longer exists
    var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, 0, 1);
    int lastPageNr = Math.Max(0, (int)Math.Ceiling((double)_resp.DbItemsCount / vm.PageSize) - 1);
    pagenr = Math.Clamp(pagenr, 0, lastPageNr);

    //Post/Redirect/Get
    return RedirectToAction(nameof(ListOfGroups), new { pagenr, search = vm.SearchFilter });
}
```
- **Bound input:** `Guid groupId` comes from the hidden form field `groupId`; `int pagenr` from a second hidden field that holds **the page the user is on** (`<input type="hidden" name="pagenr" value="@Model.ThisPageNr" />`, added next to the `groupId` field); `ListOfGroups vm` is bound from the remaining form fields (`UseSeeds`, `SearchFilter`). The page number deliberately travels in a field named `pagenr` – the same name as in the list's query string – instead of binding `vm.ThisPageNr`, so that the search action, which binds the whole view model, is unaffected.
- **`[HttpPost]`** – a delete must never be triggered by a GET (links, prefetching, crawlers). A GET to `/Group/DeleteGroup` is answered with `405 Method Not Allowed`.
- **`[ValidateAntiForgeryToken]`** – the form tag helper adds the token field; a POST without a valid token is rejected with `400`, which blocks cross-site request forgery (another website making the user's browser post a delete).
- **Post/Redirect/Get (PRG):** instead of rendering the list directly from the POST, the action answers with an **HTTP 302 redirect** to the ordinary GET list URL (`RedirectToAction` builds `/Group/ListOfGroups?pagenr=N&search=text`). The browser then does a normal GET, so the address bar shows the list URL, and **a refresh repeats only that harmless GET – the delete is not submitted again** (previously a refresh re-posted the delete of an already deleted id and ended in a `500`). The list is read by the existing `ListOfGroups` action, so the count, rows and page links come from the single code path used by every other visit to the list.
- **Stays on the same page:** `pagenr` and the search filter are passed on in the redirect, so the user returns to the page they were on, with the filter still in the search box.
- **Clamping to the last page:** if the deleted item was the only one on the last page, that page no longer exists. The action therefore counts the remaining groups (a one-row read of page 0 – the repo returns the total `DbItemsCount` with every read), computes `lastPageNr = ceil(count / PageSize) - 1` (at least 0) and uses `Math.Clamp(pagenr, 0, lastPageNr)`. Example: 101 groups, the user deletes the single group on page index 10 → the redirect goes to page index 9.

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
3. Go to page *2* of the list, click **Del** again and press **Ok** – the row disappears, you are still on page *2* (the URL is the normal list URL, e.g. `/Group/ListOfGroups?pagenr=1`), the count drops by one and the search filter is still in the search box. Pressing F5 now just reloads the list.
4. Check **Data overview**: the number of seeded music groups is one lower.

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
│   └── GroupController.cs        ListOfGroups(pagenr, search), DeleteGroup (POST), SearchGroup (POST), ViewGroup(id)
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs, ListOfGroups.cs (paging + search), ViewGroup.cs
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/                    ListOfGroups.cshtml (search form, list with Del buttons, delete modal), ViewGroup.cshtml (details)
│   ├── Seed/Seed.cshtml          Seed form
│   └── Shared/                   _Layout.cshtml (+ .css), Error.cshtml, _ValidationScriptsPartial.cshtml
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/ (modals.js, select-list-items.js), favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppMvc/Program.cs)
- **Services:** `AddControllersWithViews()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`); scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()`; aliases `/version` (JSON with version info), `/static` (redirect to `/index.html`), `/rock` and `/queen` (redirects to filtered lists).

### [appsettings.json](AppMvc/appsettings.json)
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets.

### Layout and footer
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite, header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), three reusable Bootstrap modals, a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user) from `IOptions<EnvironmentOptions>`, and the scripts (Bootstrap, jQuery, `modals.js`, `select-list-items.js`). No credentials are displayed.

### List, search and detail pages
- The list is read one page (10 groups) at a time (`pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too); `ViewModels/ListOfGroups.UpdatePagination(...)` computes the page links. Page numbers are zero-based in the URL and 1-based on screen.
- The search form posts `SearchFilter` and `UseSeeds` to `SearchGroup`; page links are GET links that carry `pagenr` and `search`. The filter matches name, genre or established year.
- Each group name links to `ViewGroup(Guid id)` (`/Group/ViewGroup/<guid>`), which shows the group with its artists and albums read-only.

### Data overview and Seed pages
- `HomeController.DataSourceInfo()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `SeedController` + `Views/Seed/Seed.cshtml`: `GET /Seed/Seed` shows the current number of groups; the POST (protected by `[ValidateAntiForgeryToken]`) optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: creating and editing groups (with their albums and artists), and authentication/authorization with the prepared pipeline – deletion in particular should later be restricted to authorised users.
