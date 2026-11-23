# AppMvc explanations

> **Important:** `AppMvc` is the unmodified **Microsoft ASP.NET Core MVC project template** (`dotnet new mvc`, targeting .NET 10).
> It is used **only as a starting point**. It contains no GoodMusic-specific logic yet and will be **expanded upon in the coming branches**
> (e.g. connecting to the `Models`, `Services`, `DbRepos` layers, music group/album/artist views, authentication, etc.).

## Purpose

`AppMvc` is a server-side rendered web application built with the **Model-View-Controller (MVC)** pattern:

- **Model** – data passed to views (here only `ErrorViewModel`).
- **View** – Razor (`.cshtml`) templates that generate HTML.
- **Controller** – classes that handle HTTP requests, pick a view and pass it a model.

In the final solution it is one of the front-ends (alongside `AppRazor` and `AppWebApi`) of the layered GoodMusic solution.

## Project structure

```
AppMvc/
├── AppMvc.csproj              Project file (Microsoft.NET.Sdk.Web, net10.0, Nullable + ImplicitUsings enabled)
├── Program.cs                 Application startup, DI registration and HTTP request pipeline
├── appsettings.json           Configuration (logging levels, AllowedHosts)
├── appsettings.Development.json  Development-only configuration overrides
├── Properties/
│   └── launchSettings.json    Local run profiles (http :5081, https :7064), ASPNETCORE_ENVIRONMENT=Development
├── Controllers/
│   └── HomeController.cs      Actions: Index, Privacy, Error
├── Models/
│   └── ErrorViewModel.cs      View model for the error page (RequestId)
├── Views/
│   ├── _ViewImports.cshtml    Shared @using directives and tag helper registration
│   ├── _ViewStart.cshtml      Sets the default layout (_Layout)
│   ├── Home/
│   │   ├── Index.cshtml       Welcome page
│   │   └── Privacy.cshtml     Placeholder privacy page
│   └── Shared/
│       ├── _Layout.cshtml     Master page: navbar, footer, CSS/JS includes, @RenderBody()
│       ├── _Layout.cshtml.css Scoped CSS for the layout
│       ├── Error.cshtml       Error page (typed to ErrorViewModel)
│       └── _ValidationScriptsPartial.cshtml  jQuery validation scripts for forms
└── wwwroot/                   Static files served to the browser
    ├── css/site.css           Site styles
    ├── js/site.js             Empty placeholder for site scripts (js/seido/ is currently empty)
    ├── lib/                   Client libraries: Bootstrap, jQuery, jquery-validation(-unobtrusive)
    └── favicon.ico
```

`bin/` and `obj/` are build output and are not source code.

## Key files

### [Program.cs](AppMvc/Program.cs)
Uses the minimal hosting model:

1. `WebApplication.CreateBuilder(args)` creates the host and loads configuration.
2. `builder.Services.AddControllersWithViews()` registers MVC controllers, Razor views and related services in the DI container.
3. `builder.Build()` creates the app, then the middleware pipeline is configured:
   - Outside Development: `UseExceptionHandler("/Home/Error")` and `UseHsts()`.
   - `UseHttpsRedirection()` – redirects HTTP to HTTPS.
   - `UseRouting()` and `UseAuthorization()` – endpoint routing and authorization (no authentication configured yet).
   - `MapStaticAssets()` – serves `wwwroot` files (optimized, fingerprinted).
   - `MapControllerRoute("default", "{controller=Home}/{action=Index}/{id?}")` – conventional routing; `/` maps to `HomeController.Index`.
4. `app.Run()` starts the server.

### [HomeController.cs](AppMvc/Controllers/HomeController.cs)
- `Index()` → `Views/Home/Index.cshtml`
- `Privacy()` → `Views/Home/Privacy.cshtml`
- `Error()` → `Views/Shared/Error.cshtml` with an `ErrorViewModel` holding the current request id; response caching is disabled for this action.

### [ErrorViewModel.cs](AppMvc/Models/ErrorViewModel.cs)
Simple class with `RequestId` and a computed `ShowRequestId`. Note that this is a *view model* local to the web app, not one of the domain models in the separate `Models` project.

### Views
- `_ViewStart.cshtml` applies `_Layout` to every view.
- `_ViewImports.cshtml` imports namespaces and enables ASP.NET Core tag helpers (`asp-controller`, `asp-action`, `asp-append-version`, ...).
- `_Layout.cshtml` defines the common HTML shell: Bootstrap navbar (Home, Privacy), footer, stylesheet/script references and an optional `Scripts` section.

## Request flow (example)

`GET /Home/Privacy` → routing matches the default route → `HomeController.Privacy()` → `View()` renders `Views/Home/Privacy.cshtml` inside `_Layout.cshtml` → HTML returned to the browser.

## What comes next

Nothing in `AppMvc` depends on the other projects in the solution (`Models`, `Services`, `DbRepos`, `DbContext`, `Configuration`, ...) yet.
Coming branches will build on this template step by step, adding GoodMusic functionality; this document will be updated as the code evolves.
