# AppMvc explanations

> **Branch `5-data-seeding`.**
> `AppMvc` started as the unmodified Microsoft ASP.NET Core MVC template (`dotnet new mvc`, .NET 10, branch `2-ms-apps-template`).
> Branch `3-seido-apps-foundation` wired it to the rest of the software stack, and branch `4-apps-info` added environment information to the footer.
> **This branch (`5-data-seeding`)** adds the first real feature: a *Seed* page that uses the `SeedGenerator` (through the service and repo layers) to generate test data and write it to the database.
> This document explains the code structure and purpose, what changed from the template, and what is new in this branch.

## What is new in `5-data-seeding`

Files added or changed in AppMvc:

| File | Change | Purpose |
|---|---|---|
| [Controllers/SeedController.cs](AppMvc/Controllers/SeedController.cs) | new | Handles `GET`/`POST /Seed/Seed` |
| [ViewModels/Seed.cs](AppMvc/ViewModels/Seed.cs) | new | View model for the Seed page |
| [Views/Seed/Seed.cshtml](AppMvc/Views/Seed/Seed.cshtml) | new | The Seed form |
| [Views/Shared/_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml) | changed | New *Seed* link in the header menu |

### How seeding works end-to-end

```
Browser ──GET /Seed/Seed──► SeedController.Seed()
                              └─ NrOfGroupsAsync(): IAdminService.GuestInfoAsync() ─► AdminDbRepos.InfoAsync() ─► InfoDbView (counts)
                              └─ new Seed { NrOfGroups = seeded + unseeded music groups }  ─► Views/Seed/Seed.cshtml

Browser ──POST /Seed/Seed──► SeedController.Seed(Seed vm)   [ValidateAntiForgeryToken]
                              ├─ (if vm.RemoveSeeds) IAdminService.RemoveSeedAsync(true) and RemoveSeedAsync(false)
                              ├─ IAdminService.SeedAsync(vm.NrOfItemsToSeed)  ─► AdminDbRepos.SeedAsync(n)
                              └─ RedirectToAction("Index", "Home")
```

All data access is done by the layers that were wired in branch 3; the controller only calls the service interface, exactly like the Web API controllers did.

### [SeedController.cs](AppMvc/Controllers/SeedController.cs)
- Constructor injection of `IAdminService` and `ILogger<SeedController>`.
- `[HttpGet] Seed()` – reads the current number of music groups (helper `NrOfGroupsAsync()`, based on `GuestInfoAsync()`: seeded + unseeded), puts it in a `Seed` view model and renders the view.
- `[HttpPost] [ValidateAntiForgeryToken] Seed(Seed vm)` – if the model state is valid:
  1. when `RemoveSeeds` is checked, calls `RemoveSeedAsync(true)` and `RemoveSeedAsync(false)` – this removes seeded **and** unseeded data, i.e. everything in the music tables;
  2. calls `SeedAsync(vm.NrOfItemsToSeed)` to generate and store new data;
  3. redirects to `Home/Index` with `RedirectToAction`.
  If the model is invalid, `NrOfGroups` is loaded again (it is display-only and not posted by the form) and the view is re-rendered with the validation messages.
- `Error()` is a copy of the action in `HomeController` returning the `Error` view model.

### [ViewModels/Seed.cs](AppMvc/ViewModels/Seed.cs)
| Property | Default | Meaning |
|---|---|---|
| `NrOfGroups` | – | Current number of music groups (display only, set by the controller) |
| `NrOfItemsToSeed` | `100` | Number of music groups to generate; `[Required]` with a custom message |
| `RemoveSeeds` | `true` | Clear existing data before seeding |

A plain view model without `[BindProperty]`: that attribute only has an effect on controller/PageModel properties, and an action parameter (`Seed vm`) is bound from the form anyway.

### [Views/Seed/Seed.cshtml](AppMvc/Views/Seed/Seed.cshtml)
- `@model Seed`, shows "the database contains N music groups".
- A POST form with a text input for the number of items (`asp-for`, with `asp-validation-for` feedback) and a *Clear before seeding* checkbox; the submit button uses `asp-controller="Seed" asp-action="Seed"`.
- Antiforgery protection: the form tag helper automatically adds a hidden `__RequestVerificationToken` field, and the POST action validates it with `[ValidateAntiForgeryToken]` (a post without the token is answered with `400`). A global `AutoValidateAntiforgeryTokenAttribute` filter would be an alternative once there are several POST actions.
- The `script` section includes `_ValidationScriptsPartial` so client-side jQuery validation works.

### [_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml)
A *Seed* link (`asp-controller="Seed" asp-action="Seed"`) was added to the header menu next to *Home* and *Static*.

### What the seeding code in the lower layers does
(Existing code from the earlier branches – nothing changed there in this branch.)
- `AdminDbRepos.SeedAsync(n)` uses `SeedGenerator` (reads `./app-seeds.json`, the copy in the app's content root) to create `n` `MusicGroupDbM` objects, gives each of them a few albums (2–4) and picks 2–4 artists for each from a pool of 100 generated artists, adds them with `MusicGroups.AddRange` and calls `SaveChangesAsync`. Albums and artists are stored through EF navigation properties. The entities implement `ISeed<T>`, so they know how to fill themselves with random data, and are flagged `Seeded`.
- `AdminDbRepos.RemoveSeedAsync(seeded)` calls the database stored procedure `supusr.spDeleteAll` (SQL Server, MySQL and PostgreSQL variants) which deletes seeded or unseeded rows.
- `AdminDbRepos.InfoAsync()` reads the `InfoDbView` database view with the counts of seeded/unseeded music groups, albums and artists.
- The operations run as the database user configured in `DatabaseConnections:DefaultDataUser` (shown in the footer, default `dbo`), so that user needs the required rights.

## From branch 4 (`4-apps-info`)
The footer in `_Layout.cshtml` shows three columns read from `IOptions<EnvironmentOptions>` (registered with `AddEnvironmentInfo()`): **Environment** (`ASPNETCORE_ENVIRONMENT`), **Secret** (secret source and id) and **Data** (active data set tag and default data user). No credentials are shown. It is a sanity check of which environment/database the app talks to – particularly useful now that the app can write to the database.

## Summary of changes compared with the Microsoft template

| Area | Microsoft template | Now |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppMvc/Program.cs)) | `AddControllersWithViews` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` (ready for the security branches) |
| Routing | `MapControllerRoute(...)` | `MapDefaultControllerRoute()` + route aliases `/version`, `/static` |
| Controllers | `HomeController`: `Index`, `Privacy`, `Error` | `HomeController`: `Index`, `Error`; **`SeedController` (branch 5)** |
| View models | `Models/ErrorViewModel` | `ViewModels/Error`, **`ViewModels/Seed` (branch 5)** |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout: SVG icons, modals, own CSS/JS; footer with environment info (branch 4); **Seed menu link (branch 5)** |
| Configuration | logging only | secret storage, database connections, data service (Database vs WebApi) |
| Helpers | none | `SeidoHelpers/SeidoHelpers.cs` |
| Removed | – | `Privacy` page, `appsettings.Development.json`, `site.css`, `site.js`, `favicon.ico`, `Models/ErrorViewModel.cs` |

## Project structure

```
AppMvc/
├── AppMvc.csproj                 Project references to Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator (quotes, names, music, ...)
├── Properties/launchSettings.json  Profiles http :5204, https :7029, IIS Express
├── Controllers/
│   ├── HomeController.cs         Index + Error
│   └── SeedController.cs         Seed (GET/POST)                                  <- branch 5
├── ViewModels/
│   ├── Error.cs                  View model for the error view (RequestId)
│   └── Seed.cs                   View model for the Seed page                      <- branch 5
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/Index.cshtml         Welcome page
│   ├── Seed/Seed.cshtml          Seed form                                         <- branch 5
│   └── Shared/
│       ├── _Layout.cshtml        Master page (header/menu, modals, footer with environment info, scripts)
│       ├── _Layout.cshtml.css    Scoped CSS
│       ├── Error.cshtml          Error view typed to ViewModels.Error
│       └── _ValidationScriptsPartial.cshtml  jQuery validation, configured for Bootstrap classes
└── wwwroot/
    ├── index.html                Plain static page outside MVC routing (reachable via /static)
    ├── css/                      features.css, grid.css, modals.css, navbars-offcanvas.css, sign-in.css
    ├── js/seido/                 modals.js, select-list-items.js
    ├── favicon_io/               Favicon set + web manifest
    └── lib/                      Bootstrap, jQuery, jquery-validation(-unobtrusive)
```

## Details from the foundation (branch 3)

### [AppMvc.csproj](AppMvc/AppMvc.csproj)
Project references to `Configuration`, `Encryptions`, `Models` and `Services` (which brings in `DbRepos`, `DbContext` and `DbModels`), the `Microsoft.AspNetCore.Identity.UI` package for later security branches, and `<Nullable>disable</Nullable>`.

### [Program.cs](AppMvc/Program.cs)
Service registration: `AddControllersWithViews()`, `AddSecrets(env)` (user secrets or Azure Key Vault via `ApplicationSecrets:SecretStorage`), `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext`, `AddVersionInfo`, `AddEnvironmentInfo`, a named `HttpClient` "MusicWebApi" (`DataService:WebApiBaseUri`), and scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`). Because `IAdminService` is registered here, `SeedController` can simply inject it.

Pipeline: `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()` (`{controller=Home}/{action=Index}/{id?}`, which also routes `/Seed/Seed`), and aliases `/version` (JSON `VersionOptions`) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppMvc/appsettings.json)
`ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser` `dbo`, `MigrationUser` `root`, `UseDataSetWithTag` `sql-music.sqlserver.docker`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are in secrets.

### Other files
- [HomeController.cs](AppMvc/Controllers/HomeController.cs) injects `ILogger` and `IAdminService`; `Index()` and `Error()`.
- [ViewModels/Error.cs](AppMvc/ViewModels/Error.cs) – moved from `Models/ErrorViewModel`; `ViewModels` separates web-app view models (MVVM) from the domain `Models` project.
- [SeidoHelpers.cs](AppMvc/SeidoHelpers/SeidoHelpers.cs) – `ModelStateDictionary.IsValidPartially(...)` and `List<SelectListItem>.PopulateSelectList<TEnum>()` (not used yet).
- `_Layout.cshtml` – Seido layout with favicons, Bootstrap, SVG icons, three reusable modals (`softModal`, `hardModal`, `dangerModal`), footer and an optional `script` section.
- `wwwroot/js/seido/modals.js` and `select-list-items.js` – data-attribute driven helpers for modals and selected items (not used yet).

## Request flow (example)

`POST /Seed/Seed` (form with antiforgery token) → routing → antiforgery validation → `SeedController.Seed(Seed vm)` (model bound from the form, `IAdminService` from DI) → `AdminServiceDb` → `AdminDbRepos` (`SeedGenerator` + `MainDbContext`) → database → redirect to `/Home/Index`.

## Context in the solution

- `AppWebApi` has been removed from the solution; the same service layer previously used by the Web API controllers is now called directly from the MVC controller.
- `AppMvc` and [AppRazor](apprazor-explanations.md) share the same configuration, startup registrations, helpers, look, footer and Seed feature; they differ in the page model (controllers/views vs. Razor Pages).

## What comes next

The coming branches will build on this: database overview and listing pages for music groups, albums and artists (with paging, search, view, edit and delete), CRUD with the modals and validation helpers, and authentication/authorization using the already prepared pipeline.
