# AppRazor explanations

> **Branch `5-data-seeding`.**
> `AppRazor` started as the unmodified Microsoft ASP.NET Core Razor Pages template (`dotnet new razor`, .NET 10, branch `2-ms-apps-template`).
> Branch `3-seido-apps-foundation` wired it to the rest of the software stack, and branch `4-apps-info` added environment information to the footer.
> **This branch (`5-data-seeding`)** adds the first real feature: a *Seed* page that uses the `SeedGenerator` (through the service and repo layers) to generate test data and write it to the database.
> The changes mirror those in [AppMvc](appmvc-explanations.md); differences between the two apps are pointed out below.

## What is new in `5-data-seeding`

Files added or changed in AppRazor:

| File | Change | Purpose |
|---|---|---|
| [Pages/Members/Seed.cshtml](AppRazor/Pages/Members/Seed.cshtml) | new | The Seed form (route `/Members/Seed`) |
| [Pages/Members/Seed.cshtml.cs](AppRazor/Pages/Members/Seed.cshtml.cs) | new | `SeedModel` PageModel with `OnGet` / `OnPost` |
| [Pages/Shared/_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml) | changed | New *Seed* link in the header menu |

The page lives in a `Members` folder. In Razor Pages the folder becomes part of the URL, and a folder is the natural unit for later applying authorization to a group of pages.

### How seeding works end-to-end

```
Browser ──GET /Members/Seed──► SeedModel.OnGetAsync()
                                 └─ LoadNrOfGroupsAsync(): IAdminService.GuestInfoAsync() ─► AdminDbRepos.InfoAsync() ─► InfoDbView
                                 └─ Page()

Browser ──POST /Members/Seed─► (antiforgery token validated automatically) SeedModel.OnPostAsync()
                                 ├─ (if RemoveSeeds) IAdminService.RemoveSeedAsync(true) and RemoveSeedAsync(false)
                                 ├─ IAdminService.SeedAsync(NrOfItemsToSeed) ─► AdminDbRepos.SeedAsync(n)
                                 └─ RedirectToPage("/Index")
```

### [Seed.cshtml.cs](AppRazor/Pages/Members/Seed.cshtml.cs) – `SeedModel`
- Constructor injection of `IAdminService` and `ILogger<SeedModel>` ("just like in WebApi").
- `NrOfGroups` – plain property with the current number of music groups (seeded + unseeded). It is loaded by the private async helper `LoadNrOfGroupsAsync()` (based on `GuestInfoAsync()`), the same approach as AppMvc's controller; there is no blocking `.Result` call any more.
- `[BindProperty] NrOfItemsToSeed` – default `100`, `[Required]` with a custom message.
- `[BindProperty] RemoveSeeds` – default `true`. (`[BindProperty]` is correct and needed here: in a PageModel it is what makes a property bind from the posted form. `NrOfGroups` is deliberately *not* bound, since it is display-only.)
- `OnGetAsync()` – loads `NrOfGroups` and returns the page.
- `OnPostAsync()` – if the model state is valid: when `RemoveSeeds` is set, calls `RemoveSeedAsync(true)` and `RemoveSeedAsync(false)` (this removes seeded **and** unseeded data, i.e. everything in the music tables); then calls `SeedAsync(NrOfItemsToSeed)` and redirects with `RedirectToPage("/Index")`. If invalid, `NrOfGroups` is loaded again and the page is shown with the validation messages.
- The class is in namespace `AppRazor.Pages` even though the file is in `Members/`; `_ViewImports.cshtml` sets `@namespace AppRazor.Pages`, so the page can use `@model SeedModel`.

Unlike AppMvc, there is no separate view model class: the PageModel itself *is* the view model (bound properties + handlers).

### [Seed.cshtml](AppRazor/Pages/Members/Seed.cshtml)
- `@page` + `@model SeedModel`; shows "the database contains N music groups".
- A POST form with a text input for the number of items (`asp-for`, `asp-validation-for`) and a *Clear before seeding* checkbox.
- Tag helpers come from the app-wide `_ViewImports.cshtml`.
- Antiforgery protection: Razor Pages validates the antiforgery token on POST automatically, and the form tag helper (enabled by `_ViewImports.cshtml`) adds the hidden `__RequestVerificationToken` field. A post without the token is answered with `400`. (`[IgnoreAntiforgeryToken]` was removed.)
- The `script` section includes `_ValidationScriptsPartial` for client-side jQuery validation.

### [_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml)
A *Seed* link (`asp-page="/Members/Seed"`) was added to the header menu. All internal links in the layout (*Home*, *Seed* and the footer link) use `asp-page` tag helpers; the *Static* link stays a plain `href="~/index.html"` since it points to a static file.

### What the seeding code in the lower layers does
(Existing code from earlier branches – nothing changed there in this branch.)
- `AdminDbRepos.SeedAsync(n)` uses `SeedGenerator` (reads `./app-seeds.json`, the copy in the app's content root) to create `n` `MusicGroupDbM` objects, gives each a few albums (2–4) and picks 2–4 artists for each from a pool of 100 generated artists, adds them with `MusicGroups.AddRange` and calls `SaveChangesAsync`. Albums and artists are stored through EF navigation properties. The entities implement `ISeed<T>`, so they know how to fill themselves with random data, and are flagged `Seeded`.
- `AdminDbRepos.RemoveSeedAsync(seeded)` calls the database stored procedure `supusr.spDeleteAll` (SQL Server, MySQL and PostgreSQL variants) which deletes seeded or unseeded rows.
- `AdminDbRepos.InfoAsync()` reads the `InfoDbView` database view with the counts of seeded/unseeded music groups, albums and artists.
- The operations run as the database user configured in `DatabaseConnections:DefaultDataUser` (shown in the footer, default `dbo`), so that user needs the required rights.

## From branch 4 (`4-apps-info`)
The footer in `_Layout.cshtml` shows three columns read from `IOptions<EnvironmentOptions>` (registered with `AddEnvironmentInfo()`): **Environment** (`ASPNETCORE_ENVIRONMENT`), **Secret** (secret source and id) and **Data** (active data set tag and default data user). To make this work, the Razor layout got `@inject IOptions<EnvironmentOptions> envOptions` (plus an unused `@inject IConfiguration` and two unused `@using` lines). No credentials are shown. It is a sanity check of which environment/database the app talks to – particularly useful now that the app can write to the database. Note that the `https` launch profile runs as `Production`, which the footer will show.

## Summary of changes compared with the Microsoft template

| Area | Microsoft template | Now |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppRazor/Program.cs)) | `AddRazorPages` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` |
| Routing | `MapRazorPages().WithStaticAssets()` | `MapRazorPages()` + route aliases `/version`, `/static` |
| Pages | Index, Privacy, Error | `Index`; **`Members/Seed` (branch 5)**; `Privacy` and `Error` removed |
| `IndexModel` | empty `OnGet()` | constructor-injects `ILogger<IndexModel>` |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout; footer with environment info (branch 4); **Seed menu link (branch 5)** |
| Configuration | logging only | secret storage, database connections, data service (Database vs WebApi) |
| Helpers | none | `SeidoHelpers/SeidoHelpers.cs` |
| Removed | – | `Privacy`, `Error`, `_Layout.cshtml.css`, `appsettings.Development.json`, `site.css`, `site.js`, `favicon.ico` |

## Project structure

```
AppRazor/
├── AppRazor.csproj               Project references to Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Source data for SeedGenerator (quotes, names, music, ...)
├── Properties/launchSettings.json  Profiles http :5183, https :5001/:5000, IIS Express, AppMusicRazor
├── Pages/
│   ├── _ViewImports.cshtml       @using AppRazor, @namespace AppRazor.Pages, tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Index.cshtml (+ .cs)      Welcome page          -> /
│   ├── Members/
│   │   ├── Seed.cshtml           Seed form             -> /Members/Seed          <- branch 5
│   │   └── Seed.cshtml.cs        SeedModel                                       <- branch 5
│   └── Shared/
│       ├── _Layout.cshtml        Master page (header/menu, footer with environment info, scripts)
│       ├── _LoginPartial.cshtml  Empty placeholder for the future login UI
│       └── _ValidationScriptsPartial.cshtml  jQuery validation, configured for Bootstrap classes
├── SeidoHelpers/SeidoHelpers.cs  Extension methods for model validation and enum select lists
└── wwwroot/                      index.html, css/, js/seido/, favicon_io/, lib/ (Bootstrap, jQuery, jquery-validation)
```

## Details from the foundation (branch 3)

### [AppRazor.csproj](AppRazor/AppRazor.csproj)
Project references to `Configuration`, `Encryptions`, `Models` and `Services` (which brings in `DbRepos`, `DbContext` and `DbModels`), the `Microsoft.AspNetCore.Identity.UI` package for later security branches, and `<Nullable>disable</Nullable>`.

### [Program.cs](AppRazor/Program.cs)
Service registration: `AddRazorPages()`, `AddSecrets(env)` (user secrets or Azure Key Vault via `ApplicationSecrets:SecretStorage`), `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext`, `AddVersionInfo`, `AddEnvironmentInfo`, a named `HttpClient` "MusicWebApi", and scoped DbRepos and services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`). Because `IAdminService` is registered here, `SeedModel` can simply inject it.

Pipeline: outside Development `UseHsts()` and `UseDeveloperExceptionPage()` (the template's `UseExceptionHandler("/Error")` is gone because the `Error` page was removed – this shows detailed errors also in production, to revisit before deployment), `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()` (which also maps `/Members/Seed`), and aliases `/version` and `/static`.

### [appsettings.json](AppRazor/appsettings.json)
Same as AppMvc: `ApplicationSecrets:SecretStorage`, `DatabaseConnections` and `DataService`. Connection strings and credentials are in secrets.

### Pages and layout
- [IndexModel](AppRazor/Pages/Index.cshtml.cs) injects `ILogger<IndexModel>`; [Index.cshtml](AppRazor/Pages/Index.cshtml) is a placeholder welcome page.
- [_ViewImports.cshtml](AppRazor/Pages/_ViewImports.cshtml) is kept from the template: `@using AppRazor`, `@namespace AppRazor.Pages` and `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`. It enables tag helpers (`asp-page`, `asp-append-version`, form tag helper with antiforgery token, ...) in all pages and layouts and lets pages refer to their models without full namespaces (`@model IndexModel`, `@model SeedModel`).
- [_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml) – Seido layout (title *AppMusicRazor*): favicons, Bootstrap, SVG icons, header with *Home*, *Static* and *Seed* links, footer, `modals.js` and `select-list-items.js`. The Bootstrap modals that AppMvc has in its layout are not included here.
- `_LoginPartial.cshtml` is an empty placeholder; `_ValidationScriptsPartial.cshtml` sets Bootstrap's `is-valid` / `is-invalid` classes as jQuery validation defaults.

### [launchSettings.json](AppRazor/Properties/launchSettings.json)
Visual Studio style profiles: `http` (:5183), `https` (:5001/:5000, `ASPNETCORE_ENVIRONMENT=Production` and an extra `MyOwn` variable), `IIS Express` and `AppMusicRazor` (Development).

## Request flow (example)

`POST /Members/Seed` (form with antiforgery token) → Razor Pages routing → antiforgery validation → `SeedModel.OnPostAsync()` (model bound from the form, `IAdminService` from DI) → `AdminServiceDb` → `AdminDbRepos` (`SeedGenerator` + `MainDbContext`) → database → redirect to `/Index`.

## Context in the solution

- `AppWebApi` has been removed from the solution; the same service layer previously used by the Web API controllers is now called directly from the PageModel.
- `AppRazor` and AppMvc share the same configuration, startup registrations, helpers, look, footer and Seed feature; they differ in the page model (Razor Pages vs. controllers/views).

## What comes next

The coming branches will build on this: database overview and listing pages for music groups, albums and artists (with paging, search, view, edit and delete), CRUD with modals and validation helpers, and authentication/authorization using the already prepared pipeline (the `Members` folder is a natural place for protected pages).
