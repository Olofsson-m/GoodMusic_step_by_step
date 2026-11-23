# AppRazor explanations

> **Important:** `AppRazor` is the unmodified **Microsoft ASP.NET Core Razor Pages project template** (`dotnet new razor`, targeting .NET 10).
> It is used **only as a starting point**. It contains no GoodMusic-specific logic yet and will be **expanded upon in the coming branches**
> (e.g. connecting to the `Models`, `Services`, `DbRepos` layers, music group/album/artist pages, authentication, etc.).

## Purpose

`AppRazor` is a server-side rendered web application built with **Razor Pages**, a *page-centric* alternative to MVC (see [appmvc-explanations.md](appmvc-explanations.md)):

- Each page is a pair of files: a `.cshtml` Razor template (the markup) and a `.cshtml.cs` **PageModel** class (the handler code).
- There are no controllers. The URL is derived from the page's location under `Pages/`.
- The `@page` directive at the top of a `.cshtml` file is what turns it into a routable page.

In the final solution it is one of the front-ends (alongside `AppMvc` and `AppWebApi`) of the layered GoodMusic solution.

## Project structure

```
AppRazor/
├── AppRazor.csproj             Project file (Microsoft.NET.Sdk.Web, net10.0, Nullable + ImplicitUsings enabled)
├── Program.cs                  Application startup, DI registration and HTTP request pipeline
├── appsettings.json            Configuration (logging levels, AllowedHosts)
├── appsettings.Development.json  Development-only configuration overrides
├── Properties/
│   └── launchSettings.json     Local run profiles (http :5149, https :7141), ASPNETCORE_ENVIRONMENT=Development
├── Pages/
│   ├── _ViewImports.cshtml     Shared @using / @namespace directives and tag helper registration
│   ├── _ViewStart.cshtml       Sets the default layout (_Layout)
│   ├── Index.cshtml (+ .cs)    Welcome page          -> /
│   ├── Privacy.cshtml (+ .cs)  Placeholder privacy   -> /Privacy
│   ├── Error.cshtml (+ .cs)    Error page            -> /Error
│   └── Shared/
│       ├── _Layout.cshtml      Master page: navbar, footer, CSS/JS includes, @RenderBody()
│       ├── _Layout.cshtml.css  Scoped CSS for the layout
│       └── _ValidationScriptsPartial.cshtml  jQuery validation scripts for forms
└── wwwroot/                    Static files served to the browser
    ├── css/site.css            Site styles
    ├── js/site.js              Empty placeholder for site scripts (js/seido/ is currently empty)
    ├── lib/                    Client libraries: Bootstrap, jQuery, jquery-validation(-unobtrusive)
    └── favicon.ico
```

`bin/` and `obj/` are build output and are not source code.

## Key files

### [Program.cs](AppRazor/Program.cs)
Uses the minimal hosting model:

1. `WebApplication.CreateBuilder(args)` creates the host and loads configuration.
2. `builder.Services.AddRazorPages()` registers Razor Pages services in the DI container.
3. `builder.Build()` creates the app, then the middleware pipeline is configured:
   - Outside Development: `UseExceptionHandler("/Error")` and `UseHsts()`.
   - `UseHttpsRedirection()` – redirects HTTP to HTTPS.
   - `UseRouting()` and `UseAuthorization()` – endpoint routing and authorization (no authentication configured yet).
   - `MapStaticAssets()` – serves `wwwroot` files (optimized, fingerprinted).
   - `MapRazorPages()` – maps every page under `Pages/` to a URL.
4. `app.Run()` starts the server.

### Page models
- [IndexModel](AppRazor/Pages/Index.cshtml.cs) and [PrivacyModel](AppRazor/Pages/Privacy.cshtml.cs) – empty `OnGet()` handlers; the pages only render static markup.
- [ErrorModel](AppRazor/Pages/Error.cshtml.cs) – `OnGet()` sets `RequestId` (from `Activity.Current` or `HttpContext.TraceIdentifier`); caching is disabled and antiforgery validation is ignored for this page.

Handlers follow the `On<Verb>[Async]` naming convention (`OnGet`, `OnPost`, ...), and the `.cshtml` file accesses the model through `@model` / `Model`.

### Razor files
- `_ViewStart.cshtml` applies `_Layout` to every page.
- `_ViewImports.cshtml` imports namespaces, sets `@namespace AppRazor.Pages` and enables tag helpers (`asp-page`, `asp-append-version`, ...).
- `_Layout.cshtml` defines the common HTML shell: Bootstrap navbar (Home, Privacy) using `asp-page` links, footer, stylesheet/script references and an optional `Scripts` section.

## Request flow (example)

`GET /Privacy` → Razor Pages routing matches `Pages/Privacy.cshtml` → `PrivacyModel.OnGet()` runs → the page is rendered inside `_Layout.cshtml` → HTML returned to the browser.

## Comparison with AppMvc

| | AppMvc | AppRazor |
|---|---|---|
| Pattern | Model-View-Controller | Page-centric (PageModel per page) |
| Request handling | Controller actions | `OnGet` / `OnPost` handlers in PageModels |
| Routing | Conventional `{controller}/{action}/{id?}` | File-based, from `Pages/` folder |
| Navigation tag helpers | `asp-controller` / `asp-action` | `asp-page` |
| Startup registration | `AddControllersWithViews` / `MapControllerRoute` | `AddRazorPages` / `MapRazorPages` |
| Local ports | 5081 / 7064 | 5149 / 7141 |

## What comes next

Nothing in `AppRazor` depends on the other projects in the solution (`Models`, `Services`, `DbRepos`, `DbContext`, `Configuration`, ...) yet.
Coming branches will build on this template step by step, adding GoodMusic functionality; this document will be updated as the code evolves.
