# AppRazor explanations

`AppRazor` is the ASP.NET Core **Razor Pages** front-end of the GoodMusic solution (.NET 10). Each page is a `.cshtml` template plus a PageModel class with handlers (`OnGetAsync`, `OnPostAsync`, ...), and pages call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`12-edit-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `11-del-list-item`. The same feature exists in [AppMvc](appmvc-explanations.md); differences are pointed out.

---

## 1. This branch: Editing and creating a music group with the InputModel pattern (compared with `11-del-list-item`)

### Purpose
Until now the list pages could show and delete groups. This branch adds **one page that both edits an existing group and creates a new one** – `Members/EditGroup`. The user can change the group's name, genre and established year, and add, edit and delete the group's **artists** and **albums** – and nothing is written to the database until the user presses **Save**. The page is built on the **InputModel pattern**: the whole editable state lives in the form and is posted back and forth, and the server only touches the database at the very end.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Pages/Members/EditGroup.cshtml.cs](AppRazor/Pages/Members/EditGroup.cshtml.cs) | – | **new** PageModel `EditGroupModel`: handlers `OnGetAsync(Guid? id)`, `OnPostAddArtist`, `OnPostEditArtist`, `OnPostDeleteArtist`, `OnPostAddAlbum`, `OnPostEditAlbum`, `OnPostDeleteAlbum`, `OnPostUndoAsync`, `OnPostSaveAsync`; private `SaveAlbums` / `SaveArtists`; nested **InputModel** classes `MusicGroupIM`, `ArtistIM`, `AlbumIM` and the `StatusIM` enum |
| [Pages/Members/EditGroup.cshtml](AppRazor/Pages/Members/EditGroup.cshtml) | – | **new** edit/create form (route `/Members/EditGroup`) |
| [Pages/ListOfGroups.cshtml](AppRazor/Pages/ListOfGroups.cshtml) | *Del* button per row | + **New** button in the header and **Edit** button per row (links to `/Members/EditGroup`) |
| [Pages/ViewGroup.cshtml](AppRazor/Pages/ViewGroup.cshtml) | read-only details | + an **edit icon** next to the heading (link to `/Members/EditGroup?id=<id>`) |

The service and repository layers are unchanged – create, update and delete methods for music groups, albums and artists already existed. `SeidoHelpers.IsValidPartially(...)` and `PopulateSelectList<TEnum>()` (in `SeidoHelpers/`) are used here for the first time, and `select-list-items.js` and Bootstrap's collapse component do the client-side work. The page lives in the **`Members`** folder – the place intended for pages that will later require a signed-in user.

### The idea: the InputModel pattern

A page that edits a *graph* of objects (a group with several artists and albums) has a problem: each user action – add an artist, rename an album, delete a row – should not hit the database, because the user may still press *Undo* or leave. The pattern used here solves this **without server-side session state**:

1. **InputModel classes** (`MusicGroupIM`, `ArtistIM`, `AlbumIM`) are small classes that hold *only* the editable properties of the real model, plus validation attributes and a `StatusIM` flag. They are what the form binds to.
2. **Every input of the InputModel is in the form.** Values the user cannot edit directly (ids, status, current name/year of each artist and album) are written as **hidden fields** (`<input type="hidden" …>`). The comment in the code says it: *"EVERY property must be bound to an `<input>` tag in the `<form>`"*.
3. **Every button posts the whole form.** Razor Pages rebuilds the entire InputModel from the posted fields (`[BindProperty]`), the handler changes it **in memory** (adds a row, marks a row deleted, …) and renders the same page again with the changed model. The browser always holds the complete current state.
4. **`Save`** is the only handler that reads the flags and writes to the database; **`Undo`** discards the in-memory changes by re-reading the group from the database.

#### The `StatusIM` flag – what will happen to an item at Save
```csharp
public enum StatusIM { Unknown, Unchanged, Inserted, Modified, Deleted }
```
| Status | Set by | At *Save* |
|---|---|---|
| `Unchanged` | the constructor that copies a database object into the InputModel | nothing |
| `Inserted` | `OnPostAddArtist` / `OnPostAddAlbum` (and `OnGetAsync` without id for the group itself) | created in the database |
| `Modified` | `OnPostEditArtist` / `OnPostEditAlbum` (only if the item was not `Inserted`) | updated in the database |
| `Deleted` | `OnPostDeleteArtist` / `OnPostDeleteAlbum` | deleted from the database |

Deleted rows are **kept in the list** and merely hidden in the page (`d-none`). That is deliberate: the posted lists are bound by index (`Artists[0]`, `Artists[1]` …), and model binding of lists needs the indexes to stay contiguous, so removing an element in the middle would shift or lose items.

### The PageModel – `EditGroupModel`
```csharp
public class EditGroupModel : PageModel
{
    [BindProperty] public MusicGroupIM MusicGroupInput { get; set; }
    [BindProperty] public string PageHeader { get; set; }                  // kept between posts in a hidden field
    public List<SelectListItem> GenreItems { get; set; } = new List<SelectListItem>().PopulateSelectList<MusicGenre>();
    public ModelValidationResult ValidationResult { get; set; } = new ModelValidationResult(false, null, null);

    // handlers …
    // nested types: StatusIM, ArtistIM, AlbumIM, MusicGroupIM
}
```
- **`[BindProperty]`** on `MusicGroupInput` and `PageHeader` is what makes Razor Pages rebuild the InputModel from the posted form **before** the handler runs; the handlers then simply work on `MusicGroupInput`. (In AppMvc the same object arrives as an action parameter.) `GenreItems` and `ValidationResult` are not posted.
- **The InputModel classes are nested** in the PageModel: `MusicGroupIM`, `ArtistIM`, `AlbumIM` and `StatusIM` – the PageModel is handler, view model and home of the InputModel at once. AppMvc has them in `ViewModels/EditGroup.cs`.
- **`MusicGroupIM`** – `StatusIM`, `MusicGroupId`, `Name` (`[Required]`), `EstablishedYear` (`[YearUpToNow(1900)]` – 1900 up to the current year), `Genre` (`MusicGenre?`, `[Required]`), the lists `Albums` and `Artists`, and the two *"new item" input holders* `NewAlbum` / `NewArtist` (bound to the inline *New* forms). `Genre` is nullable + required on purpose: on a new group no genre is pre-selected and the user must make an active choice.
- **`ArtistIM` / `AlbumIM`** – `StatusIM`, the id, the current values (`FirstName`, `LastName` / `AlbumName`, `ReleaseYear`) **and a second set of "edit" values** (`editFirstName`, `editLastName` / `editAlbumName`, `editReleaseYear`). The edit values are what the inline edit boxes are bound to; they only become the real values when the user confirms with the row's *Ok* button. This lets each row be edited and confirmed independently.
- **Three constructors per class** express the data flows: a copy constructor (used when a *New* item is copied into the list), a constructor from the model interface (`IArtist` / `IAlbum` / `IMusicGroup`: database → InputModel, status `Unchanged`, edit values = values) and the parameterless one (for the model binder).
- **`UpdateModel(model)` / `CreateCUdto()`** – the way back: `UpdateModel` copies InputModel values onto a model read from the database (for update), `CreateCUdto()` makes a Create/Update DTO with `Id = null` (for insert).
- **`GenreItems`** – the `<select>` options, generated from the `MusicGenre` enum by `PopulateSelectList<MusicGenre>()`. They are **not posted**, so the list is rebuilt in the property initializer every time the PageModel is instantiated (before every GET and POST).

### Request flows

```
(1) Opening the page
GET /Members/EditGroup?id=<guid>      or      /Members/EditGroup   (no id = create)
   └─ OnGetAsync(Guid? id)
        ├─ id given:   ReadMusicGroupAsync(id, flat:false) → MusicGroupInput = new MusicGroupIM(mg.Item)    PageHeader = "Edit details of a music group"
        └─ no id:      MusicGroupInput = new MusicGroupIM { StatusIM = Inserted, Genre = null }              PageHeader = "Create a new a music group"
      → Page()   (no database writes)

(2) Any button except Save / Undo      POST /Members/EditGroup?handler=<AddArtist|EditArtist|DeleteArtist|AddAlbum|EditAlbum|DeleteAlbum>
   whole form ─► [BindProperty] rebuilds MusicGroupInput ─► handler changes it in memory ─► Page()   (no database access)

(3) Undo                               POST /Members/EditGroup?handler=Undo
   ReadMusicGroupAsync(MusicGroupInput.MusicGroupId) → MusicGroupInput = new MusicGroupIM(mg.Item); ModelState.Clear(); Page()

(4) Save                               POST /Members/EditGroup?handler=Save
   validate group fields → (create group if Inserted) → SaveAlbums → SaveArtists → update the group → 302 redirect
```
- **Handler selection:** each button has `asp-page-handler="<Name>"`, which renders `formaction="/Members/EditGroup?handler=<Name>"`; Razor Pages runs `OnPost<Name>[Async]`. `AddArtist` ↔ `OnPostAddArtist`, `Save` ↔ `OnPostSaveAsync` – handlers that do no I/O are synchronous (`IActionResult`, no suffix), those that call the service are `async` with the `Async` suffix.
- **Antiforgery:** Razor Pages validates the token on every POST automatically (the form tag helper adds it); a POST without it is rejected with `400` (verified for every edit handler).

### How the client side picks the row – `select-list-items.js`
Each row's **Edit** and **Del** buttons carry `data-seido-selected-item-id="<artistId or albumId>"`. The page contains two placeholders, `<input type="hidden" name="artistId" data-seido-selected-item-id-target />` and `<input type="hidden" name="albumId" … />`. When a button is clicked, `select-list-items.js` copies its id into **all** target inputs; the form is then submitted by the button (*Del*) and the handler receives the id as a parameter (`OnPostDeleteArtist(Guid artistId)`) – the same mechanism as the delete dialog in the list, here without a modal. For *Edit* the button has `type="button"`: it only toggles a Bootstrap **collapse** panel (`data-bs-toggle="collapse" data-bs-target="#collapseEditArtist_<id>"`) that contains the edit boxes and the row's *Ok* button; the id is copied at the same time, so the *Ok* (`EditArtist`) knows which row to confirm. The *New* buttons just toggle the collapse panels `#collapseAddArtist` / `#collapseAddAlbum`.

### The handlers in detail

**`OnGetAsync(Guid? id)`** – shown in flow (1). `Guid?` makes the id optional (bound from the query string), which is what lets one page serve both *edit* and *create*: *New* in the list links to `/Members/EditGroup` (no id), *Edit* to `/Members/EditGroup?id=<guid>`.

**`OnPostAddArtist` / `OnPostAddAlbum`**
```csharp
string[] keys = { "MusicGroupInput.NewArtist.FirstName", "MusicGroupInput.NewArtist.LastName" };
if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
{ ValidationResult = validationResult; return Page(); }

MusicGroupInput.NewArtist.StatusIM = StatusIM.Inserted;
MusicGroupInput.NewArtist.ArtistId = Guid.NewGuid();                    // temporary id so the row can be edited/deleted in the form
MusicGroupInput.Artists.Add(new ArtistIM(MusicGroupInput.NewArtist));
MusicGroupInput.NewArtist = new ArtistIM();                             // clear the input boxes
return Page();
```
The new item gets a **temporary Guid**; the real id is created by the database layer at Save. The `NewArtist` holder is then cleared so the next artist can be typed.

**`OnPostEditArtist(Guid artistId)` / `OnPostEditAlbum(Guid albumId)`** – finds the row by id, validates only that row's edit fields (`Artists[idx].editFirstName`, …), marks it `Modified` (unless it is `Inserted` – a new item stays `Inserted`) and copies the `edit…` values into the real values.

**`OnPostDeleteArtist` / `OnPostDeleteAlbum`** – sets `StatusIM = Deleted`; the page renders the row with `d-none`.

**`OnPostUndoAsync`** – re-reads the group from the database and rebuilds the InputModel. **`ModelState.Clear()`** is essential here: the tag helpers show the *posted* values (from ModelState) in preference to the model's values, so without clearing the page would still show the discarded edits. For the same reason the page writes explicit `value="@Model.…"` attributes on the hidden and edit inputs – an explicit attribute wins over ModelState, which is how the *changed* in-memory values (new status, copied name) reach the browser after a handler has modified the model.

#### Partial validation – `IsValidPartially`
The data annotations (`[Required]`, `[Range]`) are checked by the model binder for **the whole posted model, on every post**. On this page that means ModelState holds errors that are irrelevant to the button just pressed – for instance the empty *New artist* boxes are "invalid" while the user presses *Delete* or *Save*. `ModelState.IsValid` would therefore almost always be false. `IsValidPartially(out result, keys)` (in `SeidoHelpers/SeidoHelpers.cs`) filters the ModelState errors to the **keys named by the handler** – the fields that belong to that button – and returns their messages in `ModelValidationResult`. Each handler names its own keys: `OnPostAddArtist` → the new artist's fields, `OnPostEditAlbum` → `Albums[idx].editAlbumName` / `editReleaseYear`, `OnPostSaveAsync` → the group's name, year and genre. The page lists the messages in red alert boxes (server-side fallback) and each input has an `asp-validation-for` span for client-side jQuery validation (`_ValidationScriptsPartial` in the `script` section).

Verified messages: *"You must provide a first name"* / *"…a last name"* (add or edit an artist), *"You must enter an album name"*, *"You must provide a year between 1900 and 2026"* (the upper limit is the current year), *"You must provide a group name"*, *"You must select a music genre"*.

#### `OnPostSaveAsync`
```csharp
if (!ModelState.IsValidPartially(out validationResult, { Name, EstablishedYear, Genre })) { … return Page(); }

if (MusicGroupInput.StatusIM == StatusIM.Inserted)                          // create mode
{
    var newMg = await _mg_service.CreateMusicGroupAsync(MusicGroupInput.CreateCUdto());
    MusicGroupInput.MusicGroupId = newMg.Item.MusicGroupId;                 // the id is created by the database layer
}
await SaveAlbums();                         // deletes → creates → updates
var mg = await SaveArtists();               // deletes → creates → updates; returns the freshly read group
mg = MusicGroupInput.UpdateModel(mg);       // the group's own fields
await _mg_service.UpdateMusicGroupAsync(new MusicGroupCUdto(mg));

return Inserted ? RedirectToPage("/ListOfGroups") : RedirectToPage("/ViewGroup", new { id = MusicGroupInput.MusicGroupId });
```
- Order matters: **deleted** items are removed first, then **new** items are created (albums get `MusicGroupId`; artists get `MusicGroupsId = [group id]` – artists and groups are many-to-many), then **modified** items are updated from a **fresh read** of the group, and finally the group itself is updated.
- A successful Save ends with a redirect (Post/Redirect/Get): after *creating*, to the list; after *editing*, to the group's detail page. `RedirectToPage` builds the URLs, including the `id` query value.
- The database calls are separate service calls, each committed on its own – there is **no single transaction** around Save, so a failure half-way would leave a partly saved group.

### Verified behaviour (against the running application)
| Test | Result |
|---|---|
| Open edit for an existing group | 54 form fields, all items `Unchanged`, genre list *Blues/Jazz/Metal* + selected *Rock* + *Pick one* |
| *Add artist* with empty names | 200, two validation messages; nothing added |
| *Add artist* "Zed Testson" | new row with status `Inserted`, the *new* boxes cleared |
| *Del* on that new row | status `Deleted`, row hidden |
| *Edit artist* (first name → "Changed") | value changed, status `Modified`; an empty name gives *"You must provide a first name"* |
| *Add album* with year 1899 or 2027 | both rejected: *"You must provide a year between 1900 and 2026"*; 1900 and 2026 (the current year) are accepted |
| *Save* with name removed | *"You must provide a group name"*, nothing saved |
| *Undo* | the group is reloaded from the database (2 artists again) |
| Real edit: rename group, add an album, rename an artist → *Save* | `302` to `/ViewGroup?id=…`; the changes are visible; then reverted the same way – the group was back to its original state |
| Any edit POST without the antiforgery token | `400` |

(The same flows were run against AppMvc with identical results, plus a *create new group* run there.)

### Known limitations (found while testing)
- **A new item that is added and then deleted before Save causes an error:** `OnPostDeleteArtist` / `OnPostDeleteAlbum` set `Deleted` even for an `Inserted` item, and `Save` then tries to delete the *temporary* Guid from the database → `ArgumentException` → HTTP **500** (verified; the request fails before any change is written; outside Development the developer exception page is shown). The fix is to *remove* such an item from the list instead of marking it `Deleted`.
- **Year validation (minor caveat):** valid years run from 1900 up to the current year, implemented by the attribute `[YearUpToNow(1900)]` (in `SeidoHelpers/SeidoHelpers.cs`) on the group's `EstablishedYear` and on both album year fields. Attribute arguments must be compile-time constants, so the attribute derives from `RangeAttribute` and passes `DateTime.Now.Year` to the base constructor; the error text is the template *"You must provide a year between {1} and {2}"*. Because it is still a `RangeAttribute`, the jQuery client-side validation works unchanged (the inputs get `data-val-range-min="1900"` and `data-val-range-max="2026"`). The upper limit is fixed when the attribute is first used, i.e. once per application start (a long-running site would pick up a new year at its next restart).
- **Created groups do not show up in the list:** a new group is saved as *unseeded* (`Seeded = false`), while the list only shows seeded groups (`UseSeeds = true`); it can be found by unchecking *Use seeded data* and searching, and is counted as "unseeded" in the data overview.
- **Unknown ids:** `ReadMusicGroupAsync` throws for an id that does not exist (HTTP 500), and the `.First(a => a.ArtistId == artistId)` calls in the handlers throw if a posted id is not in the posted list.
- **No concurrency check and no authorization:** two users editing the same group overwrite each other (last Save wins), and anyone who can reach the page can edit – the `Members` folder is where authorization will be applied.
- The edit page has no *Cancel* link; leaving the page simply discards the in-memory changes. The file has an unused `using Newtonsoft.Json`.

### Trying it
1. Open **Famous groups** and click **Edit** on a row (or the pencil icon on a group's detail page).
2. Change the name, press **New** under *Artists*, enter a first and last name and press **Ok** – the artist appears in the list; **Edit** and **Del** work on each row. Do the same for albums.
3. Press the **save** icon in the heading: you are returned to the group's detail page with the changes. Press the **undo** icon instead to discard the unsaved changes.
4. Press **New** in the list header to create a group: fill name, genre and year, add artists/albums and press **save**.

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
│   ├── ListOfGroups.cshtml (+ .cs)    Search, paged list, delete with modal, New/Edit buttons -> /ListOfGroups?pagenr=N&search=text
│   ├── ViewGroup.cshtml (+ .cs)       Group details    -> /ViewGroup?id=<guid>
│   ├── Members/
│   │   ├── Seed.cshtml (+ .cs)        Seed form        -> /Members/Seed
│   │   └── EditGroup.cshtml (+ .cs)   Edit / create a group (InputModels) -> /Members/EditGroup[?id=<guid>]
│   └── Shared/                   _Layout.cshtml, _LoginPartial.cshtml (empty placeholder), _ValidationScriptsPartial.cshtml
├── SeidoHelpers/SeidoHelpers.cs  IsValidPartially(...), PopulateSelectList<TEnum>()
└── wwwroot/                      index.html, css/ (incl. grid.css), js/seido/ (modals.js, select-list-items.js), favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

### [Program.cs](AppRazor/Program.cs)
- **Services:** `AddRazorPages()`; `AddSecrets(env)` (user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`); `AddEncryptions`; `AddDatabaseConnections`; `AddUserBasedDbContext`; `AddVersionInfo` and `AddEnvironmentInfo`; a named `HttpClient` "MusicWebApi"; scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).
- **Pipeline:** `UseHsts` and `UseDeveloperExceptionPage` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`; aliases `/version` (JSON with version info), `/static` (redirect to `/index.html`), `/rock` and `/queen` (redirects to filtered lists).

### [appsettings.json](AppRazor/appsettings.json) and launch settings
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are kept in secrets. In `launchSettings.json` the `https` profile runs with `ASPNETCORE_ENVIRONMENT=Production` (and an extra `MyOwn` variable); `http`, `IIS Express` and `AppMusicRazor` run as Development.

### Layout, imports and footer
- `_ViewImports.cshtml` enables tag helpers (`asp-page`, `asp-page-handler`, `asp-route-*`, `asp-append-version`, the form tag helper with antiforgery token) in all pages and layouts, and lets pages refer to models without full namespaces.
- `_Layout.cshtml` is the common page frame (title *AppMusicRazor*): favicons, Bootstrap and extra CSS, an SVG icon sprite (including the *edit*, *undo* and *save* icons used on the edit page), header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), footer and the scripts (Bootstrap, jQuery, `modals.js`, `select-list-items.js`). The *Static* link is a plain `href="~/index.html"` since it points to a static file.
- The footer shows **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user), read from `IOptions<EnvironmentOptions>`. No credentials are displayed.

### List, search, delete and detail pages
- The list is read one page (10 groups) at a time (`pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too). Page numbers are zero-based in the URL and 1-based on screen. The search form posts `SearchFilter` and `UseSeeds` to `OnPostSearchAsync` (`[BindProperty]` on both properties); page links are GET links that carry `pagenr` and `search`.
- *Del* opens a confirmation modal and posts `OnPostDeleteGroupAsync`, which deletes and then redirects back to the same page of the (filtered) list.
- Each group name links to `ViewGroupModel.OnGetAsync(Guid id)` (`/ViewGroup?id=<guid>`), which shows the group with its artists and albums read-only.

### Data overview and Seed pages
- `DataSourceInfoModel.OnGetAsync()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `Pages/Members/Seed.cshtml` + `.cs` (`SeedModel`): `OnGetAsync()` loads `NrOfGroups`; `OnPostAsync()` validates the form, optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`) and redirects to the data overview.

---

## 3. Comparison with AppMvc and next steps

| | AppMvc | AppRazor |
|---|---|---|
| Edit page | `GroupController.EditGroup` + 8 POST actions, `ViewModels/EditGroup.cs`, `Views/Group/EditGroup.cshtml` | `Members/EditGroup.cshtml` + `EditGroupModel` with 9 handlers |
| InputModel classes | in `ViewModels/EditGroup.cs` | nested in the PageModel |
| How the form reaches the code | the view model is an **action parameter** (`Save(EditGroup vm)`) | `[BindProperty]` on `MusicGroupInput` and `PageHeader` |
| Button target | `asp-controller="Group" asp-action="Save"` → `/Group/Save` | `asp-page-handler="Save"` → `/Members/EditGroup?handler=Save` |
| Antiforgery | explicit `[ValidateAntiForgeryToken]` on every POST action | automatic for Razor Pages POST |
| Redirect after Save | `RedirectToAction("ListOfGroups")` / `("ViewGroup", new { id })` | `RedirectToPage("/ListOfGroups")` / `("/ViewGroup", new { id })` |
| Optional id | `EditGroup(Guid? id)` – route segment or query | `OnGetAsync(Guid? id)` – query string |
| URL | `/Group/EditGroup[/<guid>]` | `/Members/EditGroup[?id=<guid>]` |

Next: authentication and authorization with the prepared pipeline – creating, editing and deleting should be restricted to authorised users (the `Members` folder is the natural unit) – and hardening the edit page (the limitations above).
