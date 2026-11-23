# AppMvc explanations

`AppMvc` is the ASP.NET Core **MVC** front-end of the GoodMusic solution (.NET 10). Pages are rendered on the server with Razor views; controllers handle requests and call the shared service layer (`Services` → `DbRepos` → `DbContext` → database).
This document describes the code as it is in branch **`12-edit-list-item`**, with the emphasis on what this branch adds compared with the previous branch, `11-del-list-item`.

---

## 1. This branch: Editing and creating a music group with the InputModel pattern (compared with `11-del-list-item`)

### Purpose
Until now the list pages could show and delete groups. This branch adds **one page that both edits an existing group and creates a new one** – `EditGroup`. The user can change the group's name, genre and established year, and add, edit and delete the group's **artists** and **albums** – and nothing is written to the database until the user presses **Save**. The page is built on the **InputModel pattern**: the whole editable state lives in the form and is posted back and forth, and the server only touches the database at the very end.

### Differences at a glance

| File | Previous branch | This branch |
|---|---|---|
| [Controllers/GroupController.cs](AppMvc/Controllers/GroupController.cs) | `ListOfGroups`, `DeleteGroup`, `SearchGroup`, `ViewGroup` | + **9 actions** for the edit page: `EditGroup(Guid? id)` (GET) and the POST actions `AddArtist`, `EditArtist`, `DeleteArtist`, `AddAlbum`, `EditAlbum`, `DeleteAlbum`, `Undo`, `Save`; + private helpers `SaveAlbums` / `SaveArtists` |
| [ViewModels/EditGroup.cs](AppMvc/ViewModels/EditGroup.cs) | – | **new** view model with the nested **InputModel** classes `MusicGroupIM`, `ArtistIM`, `AlbumIM` and the `StatusIM` enum |
| [Views/Group/EditGroup.cshtml](AppMvc/Views/Group/EditGroup.cshtml) | – | **new** edit/create form |
| [Views/Group/ListOfGroups.cshtml](AppMvc/Views/Group/ListOfGroups.cshtml) | *Del* button per row | + **New** button in the header and **Edit** button per row (links to `EditGroup`) |
| [Views/Group/ViewGroup.cshtml](AppMvc/Views/Group/ViewGroup.cshtml) | read-only details | + an **edit icon** next to the heading (link to `EditGroup/<id>`) |

The service and repository layers are unchanged – create, update and delete methods for music groups, albums and artists already existed. `SeidoHelpers.IsValidPartially(...)` and `PopulateSelectList<TEnum>()` (in `SeidoHelpers/`) are used here for the first time, and `select-list-items.js` and Bootstrap's collapse component do the client-side work.

### The idea: the InputModel pattern

A page that edits a *graph* of objects (a group with several artists and albums) has a problem: each user action – add an artist, rename an album, delete a row – should not hit the database, because the user may still press *Undo* or leave. The pattern used here solves this **without server-side session state**:

1. **InputModel classes** (`MusicGroupIM`, `ArtistIM`, `AlbumIM`) are small classes that hold *only* the editable properties of the real model, plus validation attributes and a `StatusIM` flag. They are what the form binds to.
2. **Every input of the InputModel is in the form.** Values the user cannot edit directly (ids, status, current name/year of each artist and album) are written as **hidden fields** (`<input type="hidden" …>`). The comment in the code says it: *"EVERY property must be bound to an `<input>` tag in the `<form>`"*.
3. **Every button posts the whole form.** The model binder rebuilds the entire InputModel from the posted fields, the action changes it **in memory** (adds a row, marks a row deleted, …) and renders the same view again with the changed model. The browser always holds the complete current state.
4. **`Save`** is the only action that reads the flags and writes to the database; **`Undo`** discards the in-memory changes by re-reading the group from the database.

#### The `StatusIM` flag – what will happen to an item at Save
```csharp
public enum StatusIM { Unknown, Unchanged, Inserted, Modified, Deleted }
```
| Status | Set by | At *Save* |
|---|---|---|
| `Unchanged` | the constructor that copies a database object into the InputModel | nothing |
| `Inserted` | `AddArtist` / `AddAlbum` (and `EditGroup` without id for the group itself) | created in the database |
| `Modified` | `EditArtist` / `EditAlbum` (only if the item was not `Inserted`) | updated in the database |
| `Deleted` | `DeleteArtist` / `DeleteAlbum` | deleted from the database |

Deleted rows are **kept in the list** and merely hidden in the page (`d-none`). That is deliberate: the posted lists are bound by index (`Artists[0]`, `Artists[1]` …), and model binding of lists needs the indexes to stay contiguous, so removing an element in the middle would shift or lose items.

### The view model – `ViewModels/EditGroup.cs`
```csharp
public class EditGroup
{
    public MusicGroupIM MusicGroupInput { get; set; }
    public string PageHeader { get; set; }                       // kept between posts in a hidden field
    public List<SelectListItem> GenreItems { get; set; } = new List<SelectListItem>().PopulateSelectList<MusicGenre>();
    public ModelValidationResult ValidationResult { get; set; } = new ModelValidationResult(false, null, null);

    public enum StatusIM { … }
    public class ArtistIM { … }   public class AlbumIM { … }   public class MusicGroupIM { … }
}
```
- **`MusicGroupIM`** – `StatusIM`, `MusicGroupId`, `Name` (`[Required]`), `EstablishedYear` (`[YearUpToNow(1900)]` – 1900 up to the current year), `Genre` (`MusicGenre?`, `[Required]`), the lists `Albums` and `Artists`, and the two *"new item" input holders* `NewAlbum` / `NewArtist` (bound to the inline *New* forms). `Genre` is nullable + required on purpose: on a new group no genre is pre-selected and the user must make an active choice.
- **`ArtistIM` / `AlbumIM`** – `StatusIM`, the id, the current values (`FirstName`, `LastName` / `AlbumName`, `ReleaseYear`) **and a second set of "edit" values** (`editFirstName`, `editLastName` / `editAlbumName`, `editReleaseYear`). The edit values are what the inline edit boxes are bound to; they only become the real values when the user confirms with the row's *Ok* button. This lets each row be edited and confirmed independently.
- **Three constructors per class** express the data flows: a copy constructor `ArtistIM(ArtistIM)` (used when a *New* item is copied into the list), `ArtistIM(IArtist model)` (database → InputModel: status `Unchanged`, edit values = values) and the parameterless one (for the model binder).
- **`UpdateModel(model)` / `CreateCUdto()`** – the way back: `UpdateModel` copies InputModel values onto a model read from the database (for update), `CreateCUdto()` makes a Create/Update DTO with `Id = null` (for insert).
- **`GenreItems`** – the `<select>` options, generated from the `MusicGenre` enum by the extension method `PopulateSelectList<MusicGenre>()`. They are **not posted**, so the list is rebuilt in the property initializer every time the view model is instantiated (before every GET and POST).
- **`ValidationResult`** – the list of error messages that the action fills when a *partial* validation fails (see below).
- The view model is a plain class: it is an action parameter, so MVC binds its public properties from the form without `[BindProperty]`.

### Request flows

```
(1) Opening the page
GET /Group/EditGroup/<guid>   or   /Group/EditGroup  (no id = create)
   └─ EditGroup(Guid? id)
        ├─ id given:   ReadMusicGroupAsync(id, flat:false) → new MusicGroupIM(mg.Item)   PageHeader = "Edit details of a music group"
        └─ no id:      new MusicGroupIM { StatusIM = Inserted, Genre = null }            PageHeader = "Create a new a music group"
      → View(vm)   (no database writes)

(2) Any button except Save / Undo      POST /Group/<AddArtist|EditArtist|DeleteArtist|AddAlbum|EditAlbum|DeleteAlbum>
   whole form ─► model binder rebuilds EditGroup vm ─► action changes vm.MusicGroupInput in memory ─► View("EditGroup", vm)   (no database access)

(3) Undo                               POST /Group/Undo
   ReadMusicGroupAsync(vm.MusicGroupInput.MusicGroupId) → vm.MusicGroupInput = new MusicGroupIM(mg.Item); ModelState.Clear(); View

(4) Save                               POST /Group/Save
   validate group fields → (create group if Inserted) → SaveAlbums → SaveArtists → update the group → 302 redirect
```
All POST actions carry `[HttpPost] [ValidateAntiForgeryToken]`; the form tag helper adds the token, and a POST without it is rejected with `400` (verified for every edit action).

### How the client side picks the row – `select-list-items.js`
Each row's **Edit** and **Del** buttons carry `data-seido-selected-item-id="<artistId or albumId>"`. The page contains two placeholders, `<input type="hidden" name="artistId" data-seido-selected-item-id-target />` and `<input type="hidden" name="albumId" … />`. When a button is clicked, `select-list-items.js` copies its id into **all** target inputs; the form is then submitted by the button (*Del*) and the action receives the id as a parameter (`DeleteArtist(Guid artistId, EditGroup vm)`) – the same mechanism as the delete dialog in the list, here without a modal. For *Edit* the button has `type="button"`: it only toggles a Bootstrap **collapse** panel (`data-bs-toggle="collapse" data-bs-target="#collapseEditArtist_<id>"`) that contains the edit boxes and the row's *Ok* button; the id is copied at the same time, so the *Ok* (`EditArtist`) knows which row to confirm. The *New* buttons just toggle the collapse panels `#collapseAddArtist` / `#collapseAddAlbum`.

### The actions in detail

**`EditGroup(Guid? id)` (GET)** – shown in flow (1). `Guid?` makes the id optional, which is what lets one action serve both *edit* and *create*: *New* in the list links to `/Group/EditGroup` (no id), *Edit* to `/Group/EditGroup/<guid>`.

**`AddArtist` / `AddAlbum`**
```csharp
string[] keys = { "MusicGroupInput.NewArtist.FirstName", "MusicGroupInput.NewArtist.LastName" };
if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
{ vm.ValidationResult = validationResult; return View("EditGroup", vm); }

vm.MusicGroupInput.NewArtist.StatusIM = StatusIM.Inserted;
vm.MusicGroupInput.NewArtist.ArtistId = Guid.NewGuid();                 // temporary id so the row can be edited/deleted in the form
vm.MusicGroupInput.Artists.Add(new ArtistIM(vm.MusicGroupInput.NewArtist));
vm.MusicGroupInput.NewArtist = new ArtistIM();                          // clear the input boxes
return View("EditGroup", vm);
```
The new item gets a **temporary Guid**; the real id is created by the database layer at Save. The `NewArtist` holder is then cleared so the next artist can be typed.

**`EditArtist` / `EditAlbum`** – finds the row by id, validates only that row's edit fields (`Artists[idx].editFirstName`, …), marks it `Modified` (unless it is `Inserted` – a new item stays `Inserted`) and copies the `edit…` values into the real values.

**`DeleteArtist` / `DeleteAlbum`** – sets `StatusIM = Deleted`; the view renders the row with `d-none`.

**`Undo`** – re-reads the group from the database and rebuilds the InputModel. **`ModelState.Clear()`** is essential here: MVC's tag helpers show the *posted* values (from ModelState) in preference to the model's values, so without clearing the page would still show the discarded edits.

#### Partial validation – `IsValidPartially`
The data annotations (`[Required]`, `[Range]`) are checked by the model binder for **the whole posted model, on every post**. On this page that means ModelState holds errors that are irrelevant to the button just pressed – for instance the empty *New artist* boxes are "invalid" while the user presses *Delete* or *Save*. `ModelState.IsValid` would therefore almost always be false. `IsValidPartially(out result, keys)` (in `SeidoHelpers/SeidoHelpers.cs`) filters the ModelState errors to the **keys named by the action** – the fields that belong to that button – and returns their messages in `ModelValidationResult`. Each action names its own keys: `AddArtist` → the new artist's fields, `EditAlbum` → `Albums[idx].editAlbumName` / `editReleaseYear`, `Save` → the group's name, year and genre. The view lists the messages in red alert boxes (server-side fallback) and each input has an `asp-validation-for` span for client-side jQuery validation (`_ValidationScriptsPartial` in the `script` section).

Verified messages: *"You must provide a first name"* / *"…a last name"* (add or edit an artist), *"You must enter an album name"*, *"You must provide a year between 1900 and 2026"* (the upper limit is the current year), *"You must provide a group name"*, *"You must select a music genre"* (a new group saved without filling the form gives the name, genre and year messages).

#### `Save`
```csharp
if (!ModelState.IsValidPartially(out validationResult, { Name, EstablishedYear, Genre })) { … return View(…); }

if (vm.MusicGroupInput.StatusIM == StatusIM.Inserted)                       // create mode
{
    var newMg = await _mg_service.CreateMusicGroupAsync(vm.MusicGroupInput.CreateCUdto());
    vm.MusicGroupInput.MusicGroupId = newMg.Item.MusicGroupId;              // the id is created by the database layer
}
await SaveAlbums(vm);                       // deletes → creates → updates
var mg = await SaveArtists(vm);             // deletes → creates → updates; returns the freshly read group
mg = vm.MusicGroupInput.UpdateModel(mg);    // the group's own fields
await _mg_service.UpdateMusicGroupAsync(new MusicGroupCUdto(mg));

return Inserted ? RedirectToAction("ListOfGroups") : RedirectToAction("ViewGroup", new { id = vm.MusicGroupInput.MusicGroupId });
```
- Order matters: **deleted** items are removed first, then **new** items are created (albums get `MusicGroupId`; artists get `MusicGroupsId = [group id]` – artists and groups are many-to-many), then **modified** items are updated from a **fresh read** of the group, and finally the group itself is updated.
- A successful Save ends with a redirect (Post/Redirect/Get): after *creating*, to the list; after *editing*, to the group's detail page.
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
| Real edit: rename group, rename an artist, add an album → *Save* (run on the equivalent AppRazor page, same logic) | `302` to the detail page; all three changes visible; then reverted the same way – the group was back to its original state |
| New group: open `/Group/EditGroup` → fill → *Save* | `302` to the list; database now has 1 unseeded group (removed again afterwards) |
| Any edit POST without the antiforgery token | `400` |

### Known limitations (found while testing)
- **A new item that is added and then deleted before Save causes an error:** `DeleteArtist` / `DeleteAlbum` set `Deleted` even for an `Inserted` item, and `Save` then tries to delete the *temporary* Guid from the database → `ArgumentException` → HTTP **500** (verified; the request fails before any change is written). The fix is to *remove* such an item from the list instead of marking it `Deleted` (or to skip `Deleted` items whose original status was `Inserted`).
- **Year validation (minor caveat):** valid years run from 1900 up to the current year, implemented by the attribute `[YearUpToNow(1900)]` (in `SeidoHelpers/SeidoHelpers.cs`) on the group's `EstablishedYear` and on both album year fields. Attribute arguments must be compile-time constants, so the attribute derives from `RangeAttribute` and passes `DateTime.Now.Year` to the base constructor; the error text is the template *"You must provide a year between {1} and {2}"*. Because it is still a `RangeAttribute`, the jQuery client-side validation works unchanged (the inputs get `data-val-range-min="1900"` and `data-val-range-max="2026"`). The upper limit is fixed when the attribute is first used, i.e. once per application start (a long-running site would pick up a new year at its next restart).
- **Created groups do not show up in the list:** a new group is saved as *unseeded* (`Seeded = false`), while the list only shows seeded groups (`UseSeeds = true`); it can be found by unchecking *Use seeded data* and searching, and is counted as "unseeded" in the data overview.
- **Unknown ids:** `ReadMusicGroupAsync` throws for an id that does not exist (HTTP 500), and the `.First(a => a.ArtistId == artistId)` calls in the actions throw if a posted id is not in the posted list.
- **No concurrency check and no authorization:** two users editing the same group overwrite each other (last Save wins), and anyone who can reach the page can edit.
- The edit page has no *Cancel* link; leaving the page simply discards the in-memory changes.

### Trying it
1. Open **Famous groups** and click **Edit** on a row (or the pencil icon on a group's detail page).
2. Change the name, press **New** under *Artists*, enter a first and last name and press **Ok** – the artist appears in the list; **Edit** and **Del** work on each row. Do the same for albums.
3. Press the **save** icon in the heading: you are returned to the group's detail page with the changes. Press the **undo** icon instead to discard the unsaved changes.
4. Press **New** in the list header to create a group: fill name, genre and year, add artists/albums and press **save**.

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
│   └── GroupController.cs        ListOfGroups, SearchGroup, DeleteGroup, ViewGroup, EditGroup + 8 edit POST actions
├── ViewModels/                   Error.cs, Seed.cs, DataSourceInfo.cs, ListOfGroups.cs, ViewGroup.cs, EditGroup.cs (InputModels)
├── SeidoHelpers/SeidoHelpers.cs  IsValidPartially(...), PopulateSelectList<TEnum>()
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/                     Index.cshtml, DataSourceInfo.cshtml
│   ├── Group/                    ListOfGroups.cshtml, ViewGroup.cshtml, EditGroup.cshtml
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
`_Layout.cshtml` is the common page frame: favicons, Bootstrap and extra CSS, an SVG icon sprite (including the *edit*, *undo* and *save* icons used on the edit page), header menu (*Home*, *Static*, *Data overview*, *Famous groups*, *Seed*), three reusable Bootstrap modals, a footer showing **Environment**, **Secret** (source and id) and **Data** (active data set tag and default data user) from `IOptions<EnvironmentOptions>`, and the scripts (Bootstrap, jQuery, `modals.js`, `select-list-items.js`). No credentials are displayed.

### List, search, delete and detail pages
- The list is read one page (10 groups) at a time (`pageNumber = pagenr`, `pageSize = 10`; the database applies `Skip/Take` and returns the total count too). Page numbers are zero-based in the URL and 1-based on screen. The search form posts `SearchFilter` and `UseSeeds` to `SearchGroup`; page links are GET links that carry `pagenr` and `search`.
- *Del* opens a confirmation modal and posts `DeleteGroup`, which deletes and then redirects back to the same page of the (filtered) list.
- Each group name links to `ViewGroup(Guid id)` (`/Group/ViewGroup/<guid>`), which shows the group with its artists and albums read-only.

### Data overview and Seed pages
- `HomeController.DataSourceInfo()` calls `IAdminService.GuestInfoAsync()` and shows six counters (seeded/unseeded music groups, albums, artists) from the database view `gstusr.vwInfoDb`.
- `SeedController` + `Views/Seed/Seed.cshtml`: `GET /Seed/Seed` shows the current number of groups; the POST (protected by `[ValidateAntiForgeryToken]`) optionally removes all data, calls `SeedAsync(n)` (`SeedGenerator` + `app-seeds.json`) and redirects to the data overview.

---

## 3. Context and next steps

- `AppMvc` and [AppRazor](apprazor-explanations.md) offer the same features on top of the same service layer; they differ in the page model (controllers/views here, Razor Pages there).
- Next: authentication and authorization with the prepared pipeline – creating, editing and deleting should be restricted to authorised users – and hardening the edit page (the limitations above).
