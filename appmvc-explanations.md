# AppMvc explanations

> **Branch `4-apps-info`.**
> `AppMvc` started as the unmodified Microsoft ASP.NET Core MVC template (`dotnet new mvc`, .NET 10, branch `2-ms-apps-template`).
> Branch `3-seido-apps-foundation` turned it into the foundation of the GoodMusic MVC app by wiring it to the rest of the software stack.
> **This branch (`4-apps-info`)** takes the first step in *using* that stack: the page footer now shows basic information about the application environment,
> secrets and database that is read through the `Configuration` project. There are still no music features.
> This document explains the code structure and purpose, what changed from the template, and what is new in this branch.

## What is new in `4-apps-info`

Only one file changed in AppMvc: [Views/Shared/_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml). Three new columns were added to the footer:

| Footer column | Shown values | Source property (`EnvironmentOptions`) |
|---|---|---|
| **Environment** | `Development` / `Production` ... | `AppEnvironment` |
| **Secret** | `UserSecrets` or `AzureKeyVault`, and the secret id (user secrets id, or the Key Vault secret name) | `SecretSource`, `SecretId` |
| **Data** | the text *Database*, the active data set tag (e.g. `sql-music.sqlserver.docker`) and the default data user (e.g. `dbo`) | `DatabaseInfo.DataConnectionTag`, `DatabaseInfo.DefaultDataUser` |

How it works:
1. In `Program.cs` (added in branch 3) `AddEnvironmentInfo()` registers `EnvironmentOptions` with the options pattern. Its values are read in [EnvironmentOptions.ReadEnvironment](Configuration/Options/EnvironmentOptions.cs) from the `ASPNETCORE_ENVIRONMENT` variable, `ApplicationSecrets:SecretStorage`, the user secrets id of the `Configuration` assembly, and the active data set in `DatabaseConnections`.
2. The layout already had `@inject IOptions<EnvironmentOptions> envOptions` (from branch 3). Branch 4 starts using it: `@envOptions.Value.<Property>` is rendered inside `<span>` elements.
3. As the footer is part of `_Layout.cshtml`, the information appears on **every page** of the application.

Purpose: quickly verify that the app is running in the expected environment, with the expected secret source and against the expected database/user. No credentials or connection strings are shown. This is a useful sanity check as later branches add database access, seeding and authentication.

Related (from branch 3, not changed here): `GET /version` returns `VersionOptions` (assembly/file version, git commit hash, build time/machine/user in Debug builds, company, product, ...) as JSON.

## Summary of changes compared with the Microsoft template

| Area | Microsoft template | Now |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppMvc/Program.cs)) | `AddControllersWithViews` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` (pipeline ready for the security branches) |
| Routing | `MapControllerRoute(...)` | `MapDefaultControllerRoute()` + two route aliases (`/version`, `/static`) |
| Controller | `Index`, `Privacy`, `Error` | `Index`, `Error`; constructor-injects logger and `IAdminService` |
| Error model | `Models/ErrorViewModel` | `ViewModels/Error` (MVVM naming) |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout: SVG icons, modals, own CSS and JS; **footer with environment/secret/data info (branch 4)** |
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
│       ├── _Layout.cshtml        Master page (header, menu, modals, footer with environment info, scripts)
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

## Details from the foundation (branch 3)

### [AppMvc.csproj](AppMvc/AppMvc.csproj)
- Project references to `Configuration`, `Encryptions`, `Models` and `Services`. `Services` in turn brings in `DbRepos`, `DbContext` and `DbModels`, so the whole stack is available to the web app.
- `Microsoft.AspNetCore.Identity.UI` for the coming authentication/authorization branches.
- `<Nullable>disable</Nullable>` for consistency with the other projects.

### [Program.cs](AppMvc/Program.cs)
Service registration (in order):
1. `AddControllersWithViews()` – MVC, unchanged.
2. `builder.Configuration.AddSecrets(env)` – user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`.
3. `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext` – encryption, the configured database connections and a DbContext chosen by database user.
4. `AddVersionInfo()` / `AddEnvironmentInfo()` – expose `VersionOptions` and `EnvironmentOptions` (the latter is used by the footer in this branch).
5. `AddHttpClient("MusicWebApi", ...)` – named HttpClient for a remote music Web API (`DataService:WebApiBaseUri`).
6. `AddScoped` for the DbRepos and the services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).

Pipeline: `UseHsts` + `UseExceptionHandler("/Home/Error")` outside Development, `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapDefaultControllerRoute()`, and the route aliases `/version` (JSON) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppMvc/appsettings.json)
- `ApplicationSecrets:SecretStorage` – `UserSecrets` or `AzureKeyVault`.
- `DatabaseConnections` – `DefaultDataUser` (`dbo`), `MigrationUser` (`root`), `UseDataSetWithTag` (`sql-music.sqlserver.docker`; MySQL/PostgreSQL alternatives in the comment). Connection strings and credentials are in secrets, not in this file.
- `DataService` – `DataSource` (`Database` or `WebApi`) and `WebApiBaseUri`.

### [HomeController.cs](AppMvc/Controllers/HomeController.cs)
Injects `ILogger<HomeController>` and `IAdminService` (stored, not yet used). `Index()` is `async Task<IActionResult>`, `Error()` returns `ViewModels.Error`. `Privacy()` is removed.

### [ViewModels/Error.cs](AppMvc/ViewModels/Error.cs)
`ErrorViewModel` moved from `Models/` and renamed `Error`; the `ViewModels` folder separates web-app view models (MVVM) from the domain `Models` project.

### [SeidoHelpers/SeidoHelpers.cs](AppMvc/SeidoHelpers/SeidoHelpers.cs)
- `ModelStateDictionary.IsValidPartially(...)` – validates only selected model properties.
- `List<SelectListItem>.PopulateSelectList<TEnum>()` – fills a `<select>` from an enum.

### Views and layout
- [_Layout.cshtml](AppMvc/Views/Shared/_Layout.cshtml) is a Seido layout based on Bootstrap's examples: favicons, Bootstrap plus extra CSS, an SVG icon sprite, a header with *Home* and *Static* links, three reusable modals (`softModal`, `hardModal`, `dangerModal`), the footer, and an optional `script` section.
- [Index.cshtml](AppMvc/Views/Home/Index.cshtml) is a placeholder welcome page.
- [Error.cshtml](AppMvc/Views/Shared/Error.cshtml) uses `@model Error`.
- [_ValidationScriptsPartial.cshtml](AppMvc/Views/Shared/_ValidationScriptsPartial.cshtml) sets Bootstrap's `is-valid` / `is-invalid` classes as jQuery validation defaults.

### wwwroot
- `js/seido/modals.js` – populates a Bootstrap modal from `data-seido-modal-*` attributes and can post JSON before closing.
- `js/seido/select-list-items.js` – copies `data-seido-selected-item-id` from a clicked element into inputs marked `data-seido-selected-item-id-target`.
- `index.html` – simple static page outside MVC routing.

## Request flow (example)

`GET /` → `UseStaticFiles` (no match) → routing → default route → `HomeController.Index()` (constructed with `ILogger` and `IAdminService` from DI) → `Views/Home/Index.cshtml` rendered in `_Layout.cshtml` (whose footer reads `EnvironmentOptions` from DI) → HTML.

## Context in the solution

- `AppWebApi` has been removed from the solution; the apps read remote data through the `MusicWebApi` HttpClient if `DataService:DataSource` is `WebApi`.
- `AppMvc` and [AppRazor](apprazor-explanations.md) share the same configuration, startup registrations, helpers, CSS/JS, look and footer information; they differ in the page model (controllers/views vs. Razor Pages).

## What comes next

The coming branches will build on this: seeding and database overview pages, listing/reading/editing music groups, albums and artists through the injected services, CRUD with the modals and validation helpers, and authentication/authorization using the already prepared pipeline.
