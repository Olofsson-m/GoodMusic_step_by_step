# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`9-view-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `8-list-pagination`.

---

## 1. This branch: Detail view of a music group (compared with `8-list-pagination`)

### Purpose
The paged list showed only the *names* of the groups. This branch adds a **detail page for one group** – name, genre, established year, its **artists** and its **albums** – and turns each name in the list into a **link** to that page. It is the first page that works on a single item identified by its id.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Controllers/GroupController.cs](AppMvc/Controllers/GroupController.cs) | `ListOfGroups(int pagenr)` | + **`ViewGroup(Guid id)`** action |
| [ViewModels/ViewGroup.cs](AppMvc/ViewModels/ViewGroup.cs) | – | **new** view model (one property: `IMusicGroup MusicGroup`) |
| [Views/Group/ViewGroup.cshtml](AppMvc/Views/Group/ViewGroup.cshtml) | – | **new** read-only detail view |
| [Views/Group/ListOfGroups.cshtml](AppMvc/Views/Group/ListOfGroups.cshtml) | group names as plain text | each name is a **link** `asp-controller="Group" asp-action="ViewGroup" asp-route-id="@item.MusicGroupId"` |

`Program.cs`, the layout and the service layer are unchanged. The detail page is not in the menu; it is reached only by clicking a group in the list.

### Request flow

```
Browser ──click "The Floyd …" in the list──► GET /Group/ViewGroup/9ad6a0fb-…   (also works as /Group/ViewGroup?id=9ad6a0fb-…)
   │                                           ▼
   │                                      GroupController.ViewGroup(Guid id)
   │                                           │ _mg_service.ReadMusicGroupAsync(id, flat: false)
   │                                           ▼
   │                                      MusicGroupsServiceDb ── 1:1 ──► MusicGroupsDbRepos.ReadMusicGroupAsync(id, false)
   │                                           │ _dbContext.MusicGroups.AsNoTracking()
   │                                           │     .Include(ArtistsDbM).Include(AlbumsDbM).Where(MusicGroupId == id).FirstOrDefaultAsync()
   │                                           ▼
   │                                      ResponseItemDto<IMusicGroup>.Item  (group + artists + albums)
   │                                      vm = new ViewGroup { MusicGroup = Item }  → View(vm)
   ◄── HTML (Views/Group/ViewGroup.cshtml inside _Layout.cshtml) ──┘
```

### The link in the list
```cshtml
<a asp-controller="Group" asp-action="ViewGroup" asp-route-id="@item.MusicGroupId">@item.Name</a>
```
The anchor tag helper builds the URL from the controller/action names and route values. Because the default route is `{controller=Home}/{action=Index}/{id?}`, the **`id` route value becomes a URL segment**: `/Group/ViewGroup/9ad6a0fb-27b9-4062-9e6e-00ad03ec77a0`. This is a plain GET link – bookmarkable and shareable.

### The controller action
```csharp
public async Task<IActionResult> ViewGroup(Guid id)
{
    Guid _groupId = id;
    var mg = await _mg_service.ReadMusicGroupAsync(_groupId, false);
    var vm = new ViewGroup() { MusicGroup = mg.Item };
    return View(vm);
}
```
- **Model binding of the id:** the action parameter `Guid id` is bound from the route segment (`/Group/ViewGroup/<guid>`) or from the query string (`?id=<guid>`) – both work. The `{id?}` part of the default route is what the segment form relies on. The commented-out line in the code shows the manual alternative, `Guid.Parse(Request.Query["id"])`.
- **Full object graph:** `flat: false` makes the repo load the group **together with its artists and albums** (`.Include(...)`), which is exactly what the page needs. With `flat: true` only the group's own columns would be read and the artist and album lists would be empty.
- **Read-only query:** the repo uses `AsNoTracking()`, since nothing is going to be modified; this is cheaper and avoids circular-reference problems when entities reference each other (group ↔ artists).
- **Interface-typed result:** the service returns `IMusicGroup` (from the `Models` project) with `Name`, `Genre`, `EstablishedYear`, `Albums` and `Artists`, so the web app never touches the EF entity classes.
- **Naming:** the action is called `ViewGroup`, as is the view model class; inside the controller `ViewGroup` as a *type* (in `new ViewGroup()`) and as a *method name* coexist without conflict.
- The injected `IAlbumsService` and `IArtistsService` are not needed here – the albums and artists come with the group.

### The view model
```csharp
public class ViewGroup { public IMusicGroup MusicGroup { get; set; } }
```
A thin wrapper in `ViewModels/`, following the same MVVM pattern as the other pages: the view depends only on this type. Here it simply exposes the whole `IMusicGroup`, because the page is a faithful read-only presentation of that object.

### The view
[Views/Group/ViewGroup.cshtml](AppMvc/Views/Group/ViewGroup.cshtml) is typed with `@model ViewGroup`, title *View group*. It has three sections, all inside a `<form class="needs-validation" novalidate>`:
1. **Group details** – three `<input type="text" class="form-control" … readonly>` fields with the group's **name**, **genre** and **established year** (`value="@Model.MusicGroup.Name"` etc.). `readonly` shows the values in form-control styling but prevents editing; this layout is reused as the basis for an edit form.
2. **Artists** – a heading and a grid with a header cell *Name*, then a `foreach` over `Model.MusicGroup.Artists` that prints `@item.FirstName @item.LastName` in a `themed-grid-col` cell.
3. **Albums** – a grid with header cells *Name* and *Release Year*, then a `foreach` over `Model.MusicGroup.Albums` printing `@item.Name` (`col-md-10`) and `@item.ReleaseYear` (`col-md-2`).

Notes:
- Bootstrap grid classes (`row`, `col-sm-6`, `col-md-10`, `col-md-2`, `g-3`) lay the page out responsively; `themed-grid-head-col` / `themed-grid-col` come from `wwwroot/css/grid.css`.
- Razor HTML-encodes all values (including inside the `value="…"` attributes), so data from the database cannot inject markup.
- The `<form>` has no action, method or inputs that post anything; it is only a container for Bootstrap's form styling. There is nothing to submit and therefore no antiforgery handling is needed on this page.
- `<label for="…">` is connected to each input via `id`, which is good for accessibility.

### Error behaviour (verified)
| Request | Result |
|---|---|
| `/Group/ViewGroup/<existing guid>` or `?id=<existing guid>` | 200 – details shown |
| `/Group/ViewGroup/<well-formed guid that does not exist>` | **500** – the repo throws `ArgumentException("Item … is not existing")` |
| `/Group/ViewGroup` (no id) or a malformed id (`not-a-guid`) | **500** – `id` becomes `Guid.Empty`, which does not exist |

Outside Development, such exceptions are routed to `/Home/Error` (`UseExceptionHandler`), but a friendlier result would be `NotFound()` when the item does not exist (e.g. by catching the exception or checking for it in the action).

### Trying it
1. Open **Famous groups** and click a group name.
2. The detail page shows name, genre and year plus the group's artists and albums; use the browser's back button to return to the same list page.

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
│   └── GroupController.cs        ListOfGroups(pagenr), ViewGroup(id)
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs, ListOfGroups.cs (with pagination), ViewGroup.cs
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/                    ListOfGroups.cshtml (paged list with links), ViewGroup.cshtml (details)
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

### Paged list
`GroupController.ListOfGroups(int pagenr)` reads one page of 10 groups with one service call (`pageNumber = pagenr`, `pageSize = 10`; the database does `Skip/Take` and returns the total count too). `ViewModels/ListOfGroups.UpdatePagination(...)` computes the previous/next page numbers (clamped) and the number of numbered links (max 10); the view renders a Bootstrap pagination bar with `asp-route-pagenr` links. Page numbers are zero-based in the URL and 1-based on screen.

### Data overview and Seed pages
- `HomeController.DataSourceInfo()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `SeedController` + `Views/Seed/Seed.cshtml`: `GET /Seed/Seed` shows the current number of groups; the POST (protected by `[ValidateAntiForgeryToken]`) optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`: `n` groups with 2–4 albums and 2–4 artists each) and redirects to the data overview.

### Other
- `SeidoHelpers.cs`: `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()`.
- `wwwroot/js/seido/modals.js` and `select-list-items.js`: data-attribute driven helpers for the modals and for selecting list items.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: searching in the list, editing and deleting a group (the read-only fields of the detail view are a natural starting point), and authentication/authorization with the prepared pipeline.
