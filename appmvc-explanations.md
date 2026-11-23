# AppMvc explanations

> **Branch `3-seido-apps-foundation`.**
> In branch `2-ms-apps-template`, `AppMvc` was the unmodified Microsoft ASP.NET Core MVC template (`dotnet new mvc`, .NET 10).
> This branch turns it into the **foundation** of the GoodMusic MVC application: it is wired to the rest of the software stack
> (`Configuration`, `Encryptions`, `DbContext`, `DbRepos`, `Services`, `Models`) and prepared for the feature work in the coming branches.
> **No music features exist yet** – the app still only shows a welcome page. This document explains what changed compared with the template and why.

## Summary of the changes

| Area | Microsoft template | `3-seido-apps-foundation` |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppMvc/Program.cs)) | `AddControllersWithViews` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` (pipeline ready for the security branches) |
| Routing | `MapControllerRoute(...)` | `MapDefaultControllerRoute()` + two route aliases (`/version`, `/static`) |
| Controller | `Index`, `Privacy`, `Error` | `Index`, `Error`; constructor-injects logger and `IAdminService` |
| Error model | `Models/ErrorViewModel` | `ViewModels/Error` (MVVM naming) |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout: SVG icons, modals, footer, own CSS and JS |
| Configuration | logging only | secret storage, database connections, data service (Database vs WebApi) |
| Helpers | none | `SeidoHelpers/SeidoHelpers.cs` |
| Removed | – | `Privacy` page, `appsettings.Development.json`, `site.css`, `site.js`, `favicon.ico`, `Models/ErrorViewModel.cs` |

## Project structure

```
AppMvc/
├── AppMvc.csproj                 Project references to Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline (see below)
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Seed data (a list of quotes) – identical copy exists in AppRazor
├── Properties/launchSettings.json  Profiles http :5204, https :7029, IIS Express
├── Controllers/
│   └── HomeController.cs         Index + Error actions, injects IAdminService
├── ViewModels/
│   └── Error.cs                  View model for the error view (RequestId)
├── SeidoHelpers/
│   └── SeidoHelpers.cs           Extension methods for model validation and enum select lists
├── Views/
│   ├── _ViewImports.cshtml       @using AppMvc / AppMvc.ViewModels + tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Home/Index.cshtml         Welcome page ("Welcome to AppMusic example")
│   └── Shared/
│       ├── _Layout.cshtml        Master page (header, menu, modals, footer, scripts)
│       ├── _Layout.cshtml.css    Scoped CSS (only trivial changes from the template)
│       ├── Error.cshtml          Error view typed to ViewModels.Error
│       └── _ValidationScriptsPartial.cshtml  jQuery validation, configured for Bootstrap classes
└── wwwroot/
    ├── index.html                Plain static page outside MVC routing (reachable via /static)
    ├── css/                      features.css, grid.css, modals.css, navbars-offcanvas.css, sign-in.css (Bootstrap example styles)
    ├── js/seido/                 modals.js, select-list-items.js (Seido script helpers)
    ├── favicon_io/               Favicon set + web manifest (replaces favicon.ico)
    └── lib/                      Bootstrap, jQuery, jquery-validation(-unobtrusive) – trimmed to the non-minified files that are used
```

## What changed and why

### [AppMvc.csproj](AppMvc/AppMvc.csproj)
- Adds project references to `Configuration`, `Encryptions`, `Models` and `Services`. `Services` in turn brings in `DbRepos`, `DbContext` and `DbModels`, so the whole stack is available to the web app.
- Adds `Microsoft.AspNetCore.Identity.UI` for the coming authentication/authorization branches.
- Sets `<Nullable>disable</Nullable>` to be consistent with the other projects (hence `string RequestId` instead of `string?`).
- Empty `RunConfiguration` property groups are Visual Studio launch-profile artefacts.

### [Program.cs](AppMvc/Program.cs)
Service registration (in order):
1. `AddControllersWithViews()` – MVC, unchanged.
2. `builder.Configuration.AddSecrets(env)` – adds a secrets source, user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`.
3. `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext` – encryption, the configured database connections and a DbContext chosen by database user.
4. `AddVersionInfo()` / `AddEnvironmentInfo()` – expose version and environment options (`VersionOptions`, `EnvironmentOptions`).
5. `AddHttpClient("MusicWebApi", ...)` – named HttpClient for a remote music Web API (`DataService:WebApiBaseUri`, `Accept: application/json`). This lets the services read data from the Web API instead of the database.
6. `AddScoped` for the DbRepos (`AdminDbRepos`, `MusicGroupsDbRepos`, `AlbumsDbRepos`, `ArtistsDbRepos`) and for the services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`). Controllers can therefore simply inject the service interfaces.

Pipeline changes:
- Outside Development: `UseHsts()` then `UseExceptionHandler("/Home/Error")`.
- `UseStaticFiles()` replaces `MapStaticAssets()`/`WithStaticAssets()` (no build-time fingerprinting; files are served as-is from `wwwroot`).
- `UseAuthentication()` added before `UseAuthorization()`. No authentication scheme is configured yet; this is groundwork.
- `MapDefaultControllerRoute()` is the shorthand for the template's `{controller=Home}/{action=Index}/{id?}` route.
- Route aliases: `GET /version` returns the `VersionOptions` as JSON; `GET /static` redirects to `/index.html`.

### [appsettings.json](AppMvc/appsettings.json)
- `ApplicationSecrets:SecretStorage` – `UserSecrets` or `AzureKeyVault`.
- `DatabaseConnections` – `DefaultDataUser` (default `dbo`), `MigrationUser` (`root`) and `UseDataSetWithTag` (default `sql-music.sqlserver.docker`; MySQL/PostgreSQL alternatives are listed in the comment). The actual connection strings and credentials are kept in secrets, not in this file.
- `DataService` – `DataSource` (`Database` or `WebApi`) and `WebApiBaseUri` (default `https://music.api.public.seido.se/api/`).
- `appsettings.Development.json` was removed.

### [HomeController.cs](AppMvc/Controllers/HomeController.cs)
- Constructor injection of `ILogger<HomeController>` and `IAdminService` – the first place where the controller is connected to the service layer. The service is stored but not yet used.
- `Index()` is now `async Task<IActionResult>` (prepared for awaiting service calls; it currently awaits nothing).
- `Privacy()` and its view are removed.
- `Error()` returns the new `ViewModels.Error` instead of `ErrorViewModel`.

### [ViewModels/Error.cs](AppMvc/ViewModels/Error.cs)
`ErrorViewModel` moved from `Models/` to `ViewModels/` and was renamed `Error`. The folder separates web-app view models (MVVM) from the domain `Models` project. Future view models belong here.

### [SeidoHelpers/SeidoHelpers.cs](AppMvc/SeidoHelpers/SeidoHelpers.cs)
Reusable extension methods in namespace `AppMvc.SeidoHelpers`:
- `ModelStateDictionary.IsValidPartially(out ModelValidationResult, string[] validateOnlyKeys)` – validates only selected model properties and returns the error messages and invalid keys (useful for forms where only part of a model is posted).
- `List<SelectListItem>.PopulateSelectList<TEnum>()` – fills a `<select>` list from the values of an enum.

### Views and layout
- [_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml) is completely replaced by a Seido layout based on Bootstrap's example pages: favicons, Bootstrap (non-minified) plus the extra CSS files, an SVG icon sprite (home, edit, undo, save, boombox, album, artist), a header with *Home* and *Static* links, a footer, and three reusable Bootstrap modals (`softModal`, `hardModal`, `dangerModal`). It injects `IConfiguration` and `IOptions<EnvironmentOptions>` for later use, and offers an optional `script` section at the end of the page.
- [Index.cshtml](AppMvc/Views/Home/Index.cshtml) is a placeholder welcome page. The comments state that individual pages will later become separate pages.
- [Error.cshtml](AppMvc/Views/Shared/Error.cshtml) uses `@model Error`.
- [_ValidationScriptsPartial.cshtml](AppMvc/Views/Shared/_ValidationScriptsPartial.cshtml) loads the non-minified jQuery validation scripts and sets Bootstrap's `is-valid` / `is-invalid` classes as the validator defaults.
- [_ViewImports.cshtml](AppMvc/Views/_ViewImports.cshtml) imports `AppMvc.ViewModels` instead of `AppMvc.Models`.

### wwwroot
- `js/seido/modals.js` – populates a Bootstrap modal from `data-seido-modal-*` attributes on the triggering button (title, body, button texts) and can post JSON to `data-seido-modal-post-url` before closing. Will be used for e.g. delete confirmations.
- `js/seido/select-list-items.js` – copies `data-seido-selected-item-id` from a clicked element into inputs marked `data-seido-selected-item-id-target`.
- `index.html` – simple static page with Bootstrap, deliberately outside MVC routing (`/static` → `/index.html`).
- `css/*.css` – small style sheets taken from Bootstrap's examples; `site.css`/`site.js`/`favicon.ico` from the template are removed.

## Request flow (example)

`GET /` → `UseStaticFiles` (no match) → routing → default route → `HomeController.Index()` (constructed with `ILogger` and `IAdminService` from DI) → `Views/Home/Index.cshtml` rendered in `_Layout.cshtml` → HTML.

## Context in the solution

- `AppWebApi` has been removed from the solution file and from `.vscode/launch.json` in this branch; the apps read remote data through the `MusicWebApi` HttpClient if `DataService:DataSource` is `WebApi`.
- `AppMvc` and [AppRazor](apprazor-explanations.md) now share the same configuration, startup registrations, helpers, CSS/JS and look; they differ in the page model (controllers/views vs. Razor Pages).

## What comes next

The coming branches will build on this foundation: pages that read and list music groups, albums and artists through the injected services, CRUD with the modals and validation helpers, and authentication/authorization using the already prepared pipeline.
