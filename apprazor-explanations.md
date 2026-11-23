# AppRazor explanations

> **Branch `4-apps-info`.**
> `AppRazor` started as the unmodified Microsoft ASP.NET Core Razor Pages template (`dotnet new razor`, .NET 10, branch `2-ms-apps-template`).
> Branch `3-seido-apps-foundation` turned it into the foundation of the GoodMusic Razor Pages app by wiring it to the rest of the software stack.
> **This branch (`4-apps-info`)** takes the first step in *using* that stack: the page footer now shows basic information about the application environment,
> secrets and database that is read through the `Configuration` project. There are still no music features.
> The changes mirror those in [AppMvc](appmvc-explanations.md); differences between the two apps are pointed out below.

## What is new in `4-apps-info`

Only one file changed in AppRazor: [Pages/Shared/_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml). Three new columns were added to the footer (identical to AppMvc):

| Footer column | Shown values | Source property (`EnvironmentOptions`) |
|---|---|---|
| **Environment** | `Development` / `Production` ... | `AppEnvironment` |
| **Secret** | `UserSecrets` or `AzureKeyVault`, and the secret id (user secrets id, or the Key Vault secret name) | `SecretSource`, `SecretId` |
| **Data** | the text *Database*, the active data set tag (e.g. `sql-music.sqlserver.docker`) and the default data user (e.g. `dbo`) | `DatabaseInfo.DataConnectionTag`, `DatabaseInfo.DefaultDataUser` |

How it works:
1. In `Program.cs` (added in branch 3) `AddEnvironmentInfo()` registers `EnvironmentOptions` with the options pattern. Its values are read in [EnvironmentOptions.ReadEnvironment](Configuration/Options/EnvironmentOptions.cs) from the `ASPNETCORE_ENVIRONMENT` variable, `ApplicationSecrets:SecretStorage`, the user secrets id of the `Configuration` assembly, and the active data set in `DatabaseConnections`.
2. Unlike AppMvc, the Razor layout did not yet inject the options, so this branch adds to the top of the layout `@using Models;`, `@using Configuration;`, `@inject IConfiguration configuration;` and `@inject IOptions<EnvironmentOptions> envOptions;` (the `IConfiguration` injection and the two extra `@using` lines are not used yet).
3. The footer renders `@envOptions.Value.<Property>` inside `<span>` elements. As the footer is part of `_Layout.cshtml`, the information appears on **every page**.

Purpose: quickly verify that the app is running in the expected environment, with the expected secret source and against the expected database/user. No credentials or connection strings are shown. This is a useful sanity check as later branches add database access, seeding and authentication.

Verified by running the app in Development: the footer shows `Development`, `UserSecrets` + the user secrets id, and `Database`, `sql-music.sqlserver.docker`, `dbo`.

Related (from branch 3, not changed here): `GET /version` returns `VersionOptions` (assembly/file version, git commit hash, build info in Debug builds, company, product, ...) as JSON.

## Summary of changes compared with the Microsoft template

| Area | Microsoft template | Now |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppRazor/Program.cs)) | `AddRazorPages` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` |
| Routing | `MapRazorPages().WithStaticAssets()` | `MapRazorPages()` + two route aliases (`/version`, `/static`) |
| Pages | Index, Privacy, Error | **Index only**; `Privacy` and `Error` removed |
| `IndexModel` | empty `OnGet()` | constructor-injects `ILogger<IndexModel>` |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout: SVG icons, favicons, own CSS and JS; **footer with environment/secret/data info (branch 4)** |
| Configuration | logging only | secret storage, database connections, data service (Database vs WebApi) |
| Helpers | none | `SeidoHelpers/SeidoHelpers.cs` |
| Removed | – | `Privacy`, `Error`, `_Layout.cshtml.css`, `appsettings.Development.json`, `site.css`, `site.js`, `favicon.ico` |

## Project structure

```
AppRazor/
├── AppRazor.csproj               Project references to Configuration, Encryptions, Models, Services; Nullable disabled
├── Program.cs                    DI registrations and HTTP pipeline (see below)
├── appsettings.json              Secrets, DatabaseConnections and DataService settings
├── app-seeds.json                Seed data (a list of quotes) – identical copy exists in AppMvc
├── Properties/launchSettings.json  Profiles http :5183, https :5001/:5000, IIS Express, AppMusicRazor
├── Pages/
│   ├── _ViewImports.cshtml       @using AppRazor, @namespace AppRazor.Pages, tag helpers
│   ├── _ViewStart.cshtml         Sets _Layout
│   ├── Index.cshtml (+ .cs)      Welcome page          -> /
│   └── Shared/
│       ├── _Layout.cshtml        Master page (header, menu, footer with environment info, scripts)
│       ├── _LoginPartial.cshtml  Empty placeholder for the future login UI
│       └── _ValidationScriptsPartial.cshtml  jQuery validation, configured for Bootstrap classes
├── SeidoHelpers/
│   └── SeidoHelpers.cs           Extension methods for model validation and enum select lists
└── wwwroot/
    ├── index.html                Plain static page outside Razor routing (reachable via /static)
    ├── css/                      features.css, grid.css, modals.css, navbars-offcanvas.css, sign-in.css (Bootstrap example styles)
    ├── js/seido/                 modals.js, select-list-items.js (Seido script helpers)
    ├── favicon_io/               Favicon set + web manifest (replaces favicon.ico)
    └── lib/                      Bootstrap, jQuery, jquery-validation(-unobtrusive) – trimmed to the non-minified files that are used
```

## Details from the foundation (branch 3)

### [AppRazor.csproj](AppRazor/AppRazor.csproj)
- Project references to `Configuration`, `Encryptions`, `Models` and `Services`. `Services` in turn brings in `DbRepos`, `DbContext` and `DbModels`.
- `Microsoft.AspNetCore.Identity.UI` for the coming authentication/authorization branches.
- `<Nullable>disable</Nullable>` for consistency with the other projects.

### [Program.cs](AppRazor/Program.cs)
Service registration (in order):
1. `AddRazorPages()` – unchanged.
2. `builder.Configuration.AddSecrets(env)` – user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`.
3. `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext`.
4. `AddVersionInfo()` / `AddEnvironmentInfo()` – expose `VersionOptions` and `EnvironmentOptions` (the latter is used by the footer in this branch).
5. `AddHttpClient("MusicWebApi", ...)` – named HttpClient for a remote music Web API (`DataService:WebApiBaseUri`).
6. `AddScoped` for the DbRepos and the services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`).

Pipeline: outside Development `UseHsts()` and `UseDeveloperExceptionPage()` (the template's `UseExceptionHandler("/Error")` is gone because the `Error` page was removed – this shows detailed errors also in production, something to revisit before deployment), `UseHttpsRedirection`, `UseStaticFiles`, `UseRouting`, `UseAuthentication`, `UseAuthorization`, `MapRazorPages()`, and the route aliases `/version` (JSON) and `/static` (redirect to `/index.html`).

### [appsettings.json](AppRazor/appsettings.json)
Same as AppMvc: `ApplicationSecrets:SecretStorage`, `DatabaseConnections` (`DefaultDataUser`, `MigrationUser`, `UseDataSetWithTag`) and `DataService` (`DataSource`, `WebApiBaseUri`). Connection strings and credentials are in secrets.

### [launchSettings.json](AppRazor/Properties/launchSettings.json)
Visual Studio style profiles: `http` (:5183), `https` (:5001/:5000, with `ASPNETCORE_ENVIRONMENT` set to `Production` and an extra `MyOwn` variable), `IIS Express` and `AppMusicRazor` (Development). Because the footer shows the environment, running the `https` profile will display `Production`.

### Pages
- [IndexModel](AppRazor/Pages/Index.cshtml.cs) takes an `ILogger<IndexModel>` through its constructor – the pattern for injecting services into PageModels later.
- [Index.cshtml](AppRazor/Pages/Index.cshtml) is a placeholder welcome page, using the short `@model IndexModel` (possible thanks to `@namespace` in `_ViewImports.cshtml`).
- `Privacy` and `Error` pages are removed.
- [_ViewImports.cshtml](AppRazor/Pages/_ViewImports.cshtml) is kept from the template: `@using AppRazor`, `@namespace AppRazor.Pages` and `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`. It enables tag helpers (`asp-page`, `asp-append-version`, ...) in all pages and layouts and lets pages refer to their models without full namespaces.
- [_LoginPartial.cshtml](AppRazor/Pages/Shared/_LoginPartial.cshtml) is an empty placeholder for the login UI; it is not referenced yet.
- [_ValidationScriptsPartial.cshtml](AppRazor/Pages/Shared/_ValidationScriptsPartial.cshtml) sets Bootstrap's `is-valid` / `is-invalid` classes as jQuery validation defaults.

### [_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml)
The same Seido layout as in AppMvc (favicons, Bootstrap and extra CSS, SVG icon sprite, header with *Home* and *Static* links, footer, `modals.js` and `select-list-items.js`, optional `script` section). Differences from AppMvc's layout: title *AppMusicRazor*, links use `asp-page="/Index"` instead of `asp-controller`/`asp-action`, and the Bootstrap modals (`softModal`, `hardModal`, `dangerModal`) are not in the layout.

### [SeidoHelpers/SeidoHelpers.cs](AppRazor/SeidoHelpers/SeidoHelpers.cs)
Same helpers as in AppMvc (namespace `AppRazor.SeidoHelpers`): `IsValidPartially(...)` and `PopulateSelectList<TEnum>()`.

### wwwroot
Same additions as in AppMvc: `js/seido/modals.js`, `js/seido/select-list-items.js`, `index.html`, the Bootstrap example CSS files and the `favicon_io` set.

## Request flow (example)

`GET /` → `UseStaticFiles` (no match) → Razor Pages routing → `Pages/Index.cshtml` / `IndexModel` (constructed with `ILogger` from DI) → rendered inside `_Layout.cshtml` (whose footer reads `EnvironmentOptions` from DI) → HTML.

## Context in the solution

- `AppWebApi` has been removed from the solution; the apps read remote data through the `MusicWebApi` HttpClient if `DataService:DataSource` is `WebApi`.
- `AppRazor` and AppMvc share the same configuration, startup registrations, helpers, CSS/JS, look and footer information; they differ in the page model (Razor Pages vs. controllers/views).

## What comes next

The coming branches will build on this: seeding and database overview pages, listing/reading/editing music groups, albums and artists through injected services, CRUD with modals and validation helpers, and authentication/authorization using the already prepared pipeline.
