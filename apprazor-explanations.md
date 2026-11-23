# AppRazor explanations

> **Branch `3-seido-apps-foundation`.**
> In branch `2-ms-apps-template`, `AppRazor` was the unmodified Microsoft ASP.NET Core Razor Pages template (`dotnet new razor`, .NET 10).
> This branch turns it into the **foundation** of the GoodMusic Razor Pages application: it is wired to the rest of the software stack
> (`Configuration`, `Encryptions`, `DbContext`, `DbRepos`, `Services`, `Models`) and prepared for the feature work in the coming branches.
> **No music features exist yet** – the app still only shows a welcome page. This document explains what changed compared with the template and why.
> The changes mirror those in [AppMvc](appmvc-explanations.md); the differences between the two apps are pointed out below.

## Summary of the changes

| Area | Microsoft template | `3-seido-apps-foundation` |
|---|---|---|
| Project references | none | `Configuration`, `Encryptions`, `Models`, `Services` (+ `Microsoft.AspNetCore.Identity.UI` package) |
| Nullable reference types | enabled | **disabled** (matches the rest of the solution) |
| Startup ([Program.cs](AppRazor/Program.cs)) | `AddRazorPages` only | Secrets, encryption, database connections, DbContext, version/environment info, HttpClient, DbRepos and Services registered in DI |
| Static files | `MapStaticAssets()` | classic `UseStaticFiles()` |
| Authentication | `UseAuthorization()` only | `UseAuthentication()` + `UseAuthorization()` |
| Routing | `MapRazorPages().WithStaticAssets()` | `MapRazorPages()` + two route aliases (`/version`, `/static`) |
| Pages | Index, Privacy, Error | **Index only**; `Privacy` and `Error` removed |
| `IndexModel` | empty `OnGet()` | constructor-injects `ILogger<IndexModel>` |
| Layout / UI | Bootstrap navbar + footer | Custom Seido layout: SVG icons, favicons, own CSS and JS |
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
│       ├── _Layout.cshtml        Master page (header, menu, footer, scripts)
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

## What changed and why

### [AppRazor.csproj](AppRazor/AppRazor.csproj)
- Adds project references to `Configuration`, `Encryptions`, `Models` and `Services`. `Services` in turn brings in `DbRepos`, `DbContext` and `DbModels`, so the whole stack is available to the web app.
- Adds `Microsoft.AspNetCore.Identity.UI` for the coming authentication/authorization branches.
- Sets `<Nullable>disable</Nullable>` to be consistent with the other projects.

### [Program.cs](AppRazor/Program.cs)
Service registration (in order):
1. `AddRazorPages()` – unchanged.
2. `builder.Configuration.AddSecrets(env)` – adds a secrets source, user secrets or Azure Key Vault, chosen by `ApplicationSecrets:SecretStorage`.
3. `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext` – encryption, the configured database connections and a DbContext chosen by database user.
4. `AddVersionInfo()` / `AddEnvironmentInfo()` – expose version and environment options.
5. `AddHttpClient("MusicWebApi", ...)` – named HttpClient for a remote music Web API (`DataService:WebApiBaseUri`, `Accept: application/json`).
6. `AddScoped` for the DbRepos (`AdminDbRepos`, `MusicGroupsDbRepos`, `AlbumsDbRepos`, `ArtistsDbRepos`) and the services (`IAdminService`→`AdminServiceDb`, `IMusicGroupsService`→`MusicGroupsServiceDb`, `IAlbumsService`→`AlbumsServiceDb`, `IArtistsService`→`ArtistsServiceDb`). PageModels can therefore simply inject the service interfaces.

Pipeline changes:
- Outside Development: `UseHsts()` and `UseDeveloperExceptionPage()`. The template's `UseExceptionHandler("/Error")` is gone because the `Error` page was removed. Note that this shows detailed errors also in production – something to revisit before deployment.
- `UseStaticFiles()` replaces `MapStaticAssets()`/`WithStaticAssets()` (no build-time fingerprinting).
- `UseAuthentication()` added before `UseAuthorization()`. No authentication scheme is configured yet; this is groundwork.
- `MapRazorPages()` maps everything under `Pages/`.
- Route aliases: `GET /version` returns the `VersionOptions` as JSON; `GET /static` redirects to `/index.html`.

### [appsettings.json](AppRazor/appsettings.json)
Same as in AppMvc:
- `ApplicationSecrets:SecretStorage` – `UserSecrets` or `AzureKeyVault`.
- `DatabaseConnections` – `DefaultDataUser`, `MigrationUser` and `UseDataSetWithTag` (default `sql-music.sqlserver.docker`). Connection strings and credentials are kept in secrets.
- `DataService` – `DataSource` (`Database` or `WebApi`) and `WebApiBaseUri`.
- `appsettings.Development.json` was removed.

### [launchSettings.json](AppRazor/Properties/launchSettings.json)
Replaced with Visual Studio style profiles: `http` (:5183), `https` (:5001/:5000, note: `ASPNETCORE_ENVIRONMENT` is `Production` here and an extra `MyOwn` variable is set), `IIS Express` and `AppMusicRazor` (Development).

### Pages
- [IndexModel](AppRazor/Pages/Index.cshtml.cs) takes an `ILogger<IndexModel>` through its constructor; it is the pattern for injecting services into PageModels later.
- [Index.cshtml](AppRazor/Pages/Index.cshtml) is a placeholder welcome page, using the short `@model IndexModel` (possible thanks to `@namespace` in `_ViewImports.cshtml`).
- `Privacy` and `Error` pages are removed.
- [_ViewImports.cshtml](AppRazor/Pages/_ViewImports.cshtml) is kept from the template: `@using AppRazor`, `@namespace AppRazor.Pages` and `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`. It enables tag helpers (`asp-page`, `asp-append-version`, ...) in all pages and layouts and lets pages refer to their models without full namespaces.
- [_ViewStart.cshtml](AppRazor/Pages/_ViewStart.cshtml) sets `_Layout` (with a course comment).
- [_LoginPartial.cshtml](AppRazor/Pages/Shared/_LoginPartial.cshtml) is an empty placeholder for the login UI; it is not referenced yet.
- [_ValidationScriptsPartial.cshtml](AppRazor/Pages/Shared/_ValidationScriptsPartial.cshtml) loads the non-minified jQuery validation scripts and sets Bootstrap's `is-valid` / `is-invalid` classes as validator defaults.

### [_Layout.cshtml](AppRazor/Pages/Shared/_Layout.cshtml)
Replaced by the same Seido layout as in AppMvc (favicons, Bootstrap and extra CSS, SVG icon sprite, header with *Home* and *Static* links, footer, scripts for `modals.js` and `select-list-items.js`, optional `script` section). Differences from AppMvc's layout:
- Title is *AppMusicRazor*.
- Links use `asp-page="/Index"` instead of `asp-controller`/`asp-action`.
- The Bootstrap modals (`softModal`, `hardModal`, `dangerModal`) are **not** in the layout.
- `_Layout.cshtml.css` (scoped CSS) was removed.

### [SeidoHelpers/SeidoHelpers.cs](AppRazor/SeidoHelpers/SeidoHelpers.cs)
Same helpers as in AppMvc (namespace `AppRazor.SeidoHelpers`): `ModelStateDictionary.IsValidPartially(...)` for validating only selected model properties, and `List<SelectListItem>.PopulateSelectList<TEnum>()` for filling `<select>` lists from an enum.

### wwwroot
Same additions as in AppMvc: `js/seido/modals.js`, `js/seido/select-list-items.js`, `index.html` (static page outside Razor routing), the Bootstrap example CSS files and the `favicon_io` set. Template files `site.css`, `site.js` and `favicon.ico` are removed.

## Request flow (example)

`GET /` → `UseStaticFiles` (no match) → Razor Pages routing → `Pages/Index.cshtml` / `IndexModel` (constructed with `ILogger` from DI) → rendered inside `_Layout.cshtml` → HTML.

## Context in the solution

- `AppWebApi` has been removed from the solution file and from `.vscode/launch.json` in this branch; the apps read remote data through the `MusicWebApi` HttpClient if `DataService:DataSource` is `WebApi`.
- `AppRazor` and AppMvc now share the same configuration, startup registrations, helpers, CSS/JS and look; they differ in the page model (Razor Pages vs. controllers/views).

## What comes next

The coming branches will build on this foundation: pages that read and list music groups, albums and artists through injected services, CRUD with modals and validation helpers, and authentication/authorization using the already prepared pipeline.
