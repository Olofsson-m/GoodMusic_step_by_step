# Exercise: Turn the Microsoft templates into applications connected to the software stack

## Goal
The Microsoft templates are generic. In this exercise you change them into the two applications that the rest of the course builds on:

- They **reference the software stack** (`Configuration`, `Encryptions`, `Models`, `Services`) and register its services in `Program.cs`.
- They **read configuration and secrets** from `appsettings.json` and the user secrets of the stack.
- They get the **course look and feel**: a new layout, CSS, JavaScript, icons and a static page.
- The template samples that are not needed (Privacy page, template error model, template CSS) are **removed**.

The outcome is the code of branch **`3-seido-apps-foundation`** (the applications only show a home page, and do not yet show any data).

## Prerequisites
- You are on branch `3ex-seido-apps-foundation`. It contains the software stack and the two **unchanged Microsoft templates**, `AppMvc` (MVC) and `AppRazor` (Razor Pages), exactly as created with `dotnet new mvc` and `dotnet new razor` (the code of branch `2-ms-apps-template`).
- The branch `3-seido-apps-foundation` exists locally. You will compare with it, and copy files that are not worth typing (CSS, icons, JavaScript, seed data, and two large blocks of layout markup) from it. Use `git fetch origin` if you do not have it.
- The development secrets (database connections) are already in your user secrets, because the stack project `Configuration` holds the `UserSecretsId`.

## How to work
- Do the steps for **AppMvc first**, then **AppRazor**. They are similar, and AppRazor is shorter.
- File contents are shown in full. Differences in blank lines, line endings or the byte order mark (BOM) do not matter.
- `git show 3-seido-apps-foundation:<path>` prints a file from the target branch, for you to read or copy from. Never copy a whole file that the exercise asks you to write.

---

## Step 1: Check the starting point

```bash
git branch --show-current          # 3ex-seido-apps-foundation
git status --short                 # nothing to commit
dotnet build GoodMusic.slnx        # 0 errors
```

Look at what the templates contain, and at what they do **not** have:

| | AppMvc | AppRazor |
|---|---|---|
| Project references | none | none |
| Packages | none | none |
| Configuration | `appsettings.json` with only logging | the same |
| Layout | Microsoft's layout with a Privacy link | the same |
| Samples | `Privacy` page, `Models/ErrorViewModel.cs` | `Privacy` and `Error` pages |
| Static files | `site.css`, `site.js`, `favicon.ico`, the full Bootstrap and jQuery distributions | the same |

Run each template once (`cd AppMvc && dotnet run`, then `cd ../AppRazor && dotnet run`) so that you recognize them afterwards. Stop with `Ctrl+C`.

Also note the folder `AppWebApi`. It is not part of `GoodMusic.slnx`, and the applications do not use it. It is removed in Step 2.

**Question:** which of the stack projects must the applications reference, and which can they reach indirectly? (You will find out in Step 3.)

---

## Step 2: Remove what the real applications do not use

The templates contain samples that you will replace.

**AppMvc**
```bash
rm AppMvc/Models/ErrorViewModel.cs
rmdir AppMvc/Models
rm AppMvc/Views/Home/Privacy.cshtml
rm AppMvc/wwwroot/css/site.css AppMvc/wwwroot/js/site.js AppMvc/wwwroot/favicon.ico
rm AppMvc/appsettings.Development.json
```

**AppRazor**
```bash
rm AppRazor/Pages/Error.cshtml AppRazor/Pages/Error.cshtml.cs
rm AppRazor/Pages/Privacy.cshtml AppRazor/Pages/Privacy.cshtml.cs
rm AppRazor/Pages/Shared/_Layout.cshtml.css
rm AppRazor/wwwroot/css/site.css AppRazor/wwwroot/js/site.js AppRazor/wwwroot/favicon.ico
rm AppRazor/appsettings.Development.json
```

(On Windows use `del` or delete the files in your editor.)

**Keep `wwwroot/lib`.** The templates include more Bootstrap and jQuery files than the layout uses (minified, right-to-left, slim and bundled variants). Leave them as they are. They do no harm, and keeping them is less confusing. The target branch has only the plain files, so the comparison in Step 11 leaves this folder out.

**Question:** why can `appsettings.Development.json` go? (Hint: look at what `appsettings.json` will contain in Step 5.)

**Remove the unused `AppWebApi` folder.** It is a separate web API that is not in `GoodMusic.slnx` and that the applications do not use. Delete the folder, and remove its debug configuration from `.vscode/launch.json` (the last object, `"name": "AppWebApi"`, and the comma before it):

```bash
rm -r AppWebApi
```

`.vscode/launch.json` then ends with:

```json
                "ASPNETCORE_URLS": "https://localhost:5101;http://localhost:5100"
            }
        }
    ]
}
```

---

## Step 3: Connect the projects to the software stack

Replace the content of the project files. Four project references to the stack are added, nullable reference types are switched off, and the package `Microsoft.AspNetCore.Identity.UI` (used by later security branches) is added.

**File: `AppMvc/AppMvc.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(RunConfiguration)' == 'https' " />
  <PropertyGroup Condition=" '$(RunConfiguration)' == 'http' " />
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\Encryptions\Encryptions.csproj" />
    <ProjectReference Include="..\Models\Models.csproj" />
    <ProjectReference Include="..\Services\Services.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.13" />
  </ItemGroup>
  
</Project>
```

**File: `AppRazor/AppRazor.csproj`**
```xml
<Project Sdk="Microsoft.NET.Sdk.Web">

  <PropertyGroup>
    <TargetFramework>net10.0</TargetFramework>
    <Nullable>disable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
  </PropertyGroup>

  <PropertyGroup Condition=" '$(RunConfiguration)' == 'https' " />
  <PropertyGroup Condition=" '$(RunConfiguration)' == 'http' " />
  <ItemGroup>
    <ProjectReference Include="..\Configuration\Configuration.csproj" />
    <ProjectReference Include="..\Encryptions\Encryptions.csproj" />
    <ProjectReference Include="..\Models\Models.csproj" />
    <ProjectReference Include="..\Services\Services.csproj" />
  </ItemGroup>

  <ItemGroup>
    <PackageReference Include="Microsoft.AspNetCore.Identity.UI" Version="10.0.13" />
  </ItemGroup>
  
</Project>
```

**Question:** the apps reference `Services`, but not `DbContext`, `DbRepos` or `DbModels`. How can `Program.cs` (Step 6) still register `AdminDbRepos`? (Hint: look at the references of `Services`.)

---

## Step 4: Copy the course resources

CSS, icons, JavaScript, the static page and the seed data are resources, not code to practice on. Copy them from the target branch:

```bash
git checkout 3-seido-apps-foundation -- \
  AppMvc/wwwroot/css AppMvc/wwwroot/favicon_io AppMvc/wwwroot/js/seido AppMvc/wwwroot/index.html AppMvc/app-seeds.json \
  AppRazor/wwwroot/css AppRazor/wwwroot/favicon_io AppRazor/wwwroot/js/seido AppRazor/wwwroot/index.html AppRazor/app-seeds.json
```

This adds, to each app, five CSS files (`features`, `grid`, `modals`, `navbars-offcanvas`, `sign-in`), the folder `favicon_io`, `js/seido/modals.js` and `select-list-items.js`, the static page `index.html`, and `app-seeds.json` (216 lines of seed data for later branches).

---

## Step 5: Configuration

The applications read their settings from `appsettings.json`: where the secrets are kept, which database to use, and which data service to use.

**File: `AppMvc/appsettings.json`** (and the same content in `AppRazor/appsettings.json`)
```json
{
  "Logging": {
    "LogLevel": {
      "Default": "Information",
      "Microsoft.AspNetCore": "Warning"
    }
  },
  "AllowedHosts": "*",
  

  "ApplicationSecrets": {
      "SecretStorage": "UserSecrets" // Options: "UserSecrets", "AzureKeyVault"
  },

  "DatabaseConnections": {
    "DefaultDataUser": "dbo",    //"gstusr" | "usr" | "supusr" | "dbo" | "root"
    "MigrationUser": "root",    //migration is done by root user
     "UseDataSetWithTag": "sql-music.sqlserver.docker" //"sql-music.sqlserver.docker" | "sql-music.mysql.docker" | "sql-music.postgresql.docker"
   },

  "DataService": 
  {
    "DataSource": "Database",    //"Database" | "WebApi"
    //"WebApiBaseUri": "https://localhost:7066/api/"
    "WebApiBaseUri": "https://music.api.public.seido.se/api/"
  }
}
```

(Comments are allowed in `appsettings.json`.)

**Launch settings.** Replace the generated files, so that the applications always start on the same ports.

**File: `AppMvc/Properties/launchSettings.json`**
```json
{
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:17866",
      "sslPort": 44380
    }
  },
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5204",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true
    },
    "https": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "https://localhost:7029;http://localhost:5204",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

**File: `AppRazor/Properties/launchSettings.json`**
```json
{
  "iisSettings": {
    "windowsAuthentication": false,
    "anonymousAuthentication": true,
    "iisExpress": {
      "applicationUrl": "http://localhost:61423",
      "sslPort": 44352
    }
  },
  "profiles": {
    "http": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "http://localhost:5183",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "dotnetRunMessages": true
    },
    "https": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "https://localhost:5001;http://localhost:5000",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Production",
        "MyOwn": "Hello from Martin"
      },
      "dotnetRunMessages": true
    },
    "IIS Express": {
      "commandName": "IISExpress",
      "launchBrowser": true,
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    },
    "AppMusicRazor": {
      "commandName": "Project",
      "launchBrowser": true,
      "applicationUrl": "https://localhost:11774;http://localhost:29556",
      "environmentVariables": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      }
    }
  }
}
```

---

## Step 6: `Program.cs`

This is where the application is connected to the stack. Read the comments: each block registers something from the stack with dependency injection.

**File: `AppMvc/Program.cs`**
```csharp
using Services;
using Configuration.Extensions;
using DbContext.Extensions;
using DbRepos;
using Encryption.Extensions;
using Microsoft.Extensions.Options;
using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddControllersWithViews();

//adding support for several secret sources and database sources
//to use either user secrets or azure key vault depending on UseAzureKeyVault tag in appsettings.json
builder.Configuration.AddSecrets(builder.Environment);

//use encryption and multiple Database connections and their respective DbContexts
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddUserBasedDbContext();
builder.Services.AddVersionInfo();
builder.Services.AddEnvironmentInfo();

#region Injecting a dependency service to read MusicWebApi
builder.Services.AddHttpClient(name: "MusicWebApi", configureClient: options =>
{
    options.BaseAddress = new Uri(builder.Configuration["DataService:WebApiBaseUri"]);
    options.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
            mediaType: "application/json",
            quality: 1.0));
});


//Inject DbRepos and Services
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();
builder.Services.AddScoped<ArtistsDbRepos>();

builder.Services.AddScoped<IAdminService, AdminServiceDb>();
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();
builder.Services.AddScoped<IArtistsService, ArtistsServiceDb>();

#endregion

var app = builder.Build();

// Configure the HTTP request pipeline.
if (!app.Environment.IsDevelopment())
{
    //https://en.wikipedia.org/wiki/HTTP_Strict_Transport_Security
    //https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl
    app.UseHsts();
    app.UseExceptionHandler("/Home/Error");
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

app.UseAuthentication();
app.UseAuthorization();

app.MapDefaultControllerRoute(); // enables conventional routing: {controller=Home}/{action=Index}/{id?}

//Route aliases
app.MapGet("/version", (IOptions<VersionOptions> versionOptions) => Results.Json(versionOptions.Value));
app.MapGet("/static", () => Results.Redirect("/index.html"));

app.Run();
```

**File: `AppRazor/Program.cs`**
```csharp
using Services;
using Configuration.Extensions;
using DbContext.Extensions;
using DbRepos;
using Encryption.Extensions;
using Microsoft.Extensions.Options;
using Configuration.Options;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddRazorPages();

//adding support for several secret sources and database sources
//to use either user secrets or azure key vault depending on UseAzureKeyVault tag in appsettings.json
builder.Configuration.AddSecrets(builder.Environment);

//use encryption and multiple Database connections and their respective DbContexts
builder.Services.AddEncryptions(builder.Configuration);
builder.Services.AddDatabaseConnections(builder.Configuration);
builder.Services.AddUserBasedDbContext();

// adding version and environment info
builder.Services.AddVersionInfo();
builder.Services.AddEnvironmentInfo();

#region Injecting a dependency service to read MusicWebApi
builder.Services.AddHttpClient(name: "MusicWebApi", configureClient: options =>
{
    options.BaseAddress = new Uri(builder.Configuration["DataService:WebApiBaseUri"]);
    options.DefaultRequestHeaders.Accept.Add(
        new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue(
            mediaType: "application/json",
            quality: 1.0));
});


//Inject DbRepos and Services
builder.Services.AddScoped<AdminDbRepos>();
builder.Services.AddScoped<MusicGroupsDbRepos>();
builder.Services.AddScoped<AlbumsDbRepos>();
builder.Services.AddScoped<ArtistsDbRepos>();

builder.Services.AddScoped<IAdminService, AdminServiceDb>();
builder.Services.AddScoped<IMusicGroupsService, MusicGroupsServiceDb>();
builder.Services.AddScoped<IAlbumsService, AlbumsServiceDb>();
builder.Services.AddScoped<IArtistsService, ArtistsServiceDb>();
#endregion

var app = builder.Build();

//using Hsts and https to secure the site
if (!app.Environment.IsDevelopment())
{
    //https://en.wikipedia.org/wiki/HTTP_Strict_Transport_Security
    //https://learn.microsoft.com/en-us/aspnet/core/security/enforcing-ssl
    app.UseHsts();
    app.UseDeveloperExceptionPage();
}
app.UseHttpsRedirection();

//Use static files css, html, and js
app.UseStaticFiles();

//Use endpoint routing
app.UseRouting();

//Use Authentication and Authorization
app.UseAuthentication();
app.UseAuthorization();

//This is the endpoint, Map Razorpages into Pages folder
app.MapRazorPages();

//Route aliases
app.MapGet("/version", (IOptions<VersionOptions> versionOptions) => Results.Json(versionOptions.Value));
app.MapGet("/static", () => Results.Redirect("/index.html"));

app.Run();
```

Compare each with the template `Program.cs` that you replaced (`git diff` before you save, or in the editor). Note what changed:

| Template | Now |
|---|---|
| `MapStaticAssets()` / `.WithStaticAssets()` | `UseStaticFiles()` (plain static files from `wwwroot`) |
| No authentication | `UseAuthentication()` before `UseAuthorization()` |
| No stack | `AddSecrets`, `AddEncryptions`, `AddDatabaseConnections`, `AddUserBasedDbContext`, `AddVersionInfo`, `AddEnvironmentInfo` |
| No services | The `DbRepos` and the `Service` interfaces registered as scoped |
| `MapControllerRoute(...)` (MVC) | `MapDefaultControllerRoute()` |
| – | Route aliases `/version` and `/static` |

---

## Step 7: Helpers shared by the views

Both applications get a small helper class for validation and drop-down lists. It is not used on the home page, but later branches depend on it.

**File: `AppMvc/SeidoHelpers/SeidoHelpers.cs`** (create the folder)
```csharp
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AppMvc.SeidoHelpers
{
    #region Seido Helpers for Model Validation
    public record ModelValidationResult
    (
        bool HasErrors,
        IEnumerable<string> ErrorMsgs,
        IEnumerable<KeyValuePair<string, ModelStateEntry>> InvalidKeys
    );

    public static class SeidoExtension
    {
        //Model state Validations
        public static bool IsValidPartially(this ModelStateDictionary model, out ModelValidationResult validationResult, string[] validateOnlyKeys = null)
        {
            var _invalidKeys = model
               .Where(s => s.Value.ValidationState == ModelValidationState.Invalid);

            if (validateOnlyKeys != null)
            {
                _invalidKeys = _invalidKeys.Where(s => validateOnlyKeys.Any(vk => vk == s.Key));
            }

            var _errorMsgs = _invalidKeys.SelectMany(e => e.Value.Errors).Select(e => e.ErrorMessage);
            var _hasErrors = _invalidKeys.Count() != 0;

            validationResult = new ModelValidationResult(_hasErrors, _errorMsgs, _invalidKeys);

            return !_hasErrors;
        }


        //Populate SelectLists with Enum Values and Text
        public static List<SelectListItem> PopulateSelectList<TEnum>(this List<SelectListItem> selectList) where TEnum : struct
        {
            if (!typeof(TEnum).IsEnum)
            {
                throw new ArgumentException("Not an enum type");
            }

            //Populate populate select tag with options using tag helpers
            foreach (var item in typeof(TEnum).GetEnumValues())
            {
                selectList.Add(new SelectListItem
                {
                    Value = item.ToString(),
                    Text = item.ToString()
                });
            }

            return selectList;
        }
    }
    #endregion
}
```

**AppRazor:** create `AppRazor/SeidoHelpers/SeidoHelpers.cs` with the same code. Change the namespace to `AppRazor.SeidoHelpers`. (The target uses the local variable names `invalidKeys`, `errorMsgs` and `hasErrors` without underscore, which does not matter.)

`IsValidPartially` is an extension method on `ModelStateDictionary`: it checks only the form fields you name, which is needed when one form contains several sub-forms. You will use it in the edit branches.

---

## Step 8: AppMvc – controller, view model, views

### 8.1 Error view model
The template's `Models/ErrorViewModel.cs` is replaced by a view model in a folder named `ViewModels`.

**File: `AppMvc/ViewModels/Error.cs`** (create the folder)
```csharp
namespace AppMvc.ViewModels;

public class Error
{
    public string RequestId { get; set; }

    public bool ShowRequestId => !string.IsNullOrEmpty(RequestId);
}
```

### 8.2 `HomeController`
The controller now gets the `IAdminService` from the stack through its constructor (dependency injection), and `Privacy` is gone.

**File: `AppMvc/Controllers/HomeController.cs`**
```csharp
using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AppMvc.ViewModels;
using Services;

namespace AppMvc.Controllers;

public class HomeController : Controller
{
    readonly ILogger<HomeController> _logger;
    readonly IAdminService _service = null;

    public HomeController(ILogger<HomeController> logger, IAdminService service)
    {
        _logger = logger;
        _service = service;
    }

    public async Task<IActionResult> Index()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Error { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
```

### 8.3 View imports and view start

**File: `AppMvc/Views/_ViewImports.cshtml`**
```razor
@using AppMvc
@using AppMvc.ViewModels
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

**File: `AppMvc/Views/_ViewStart.cshtml`**
```razor
@{
    Layout = "_Layout";
}
```

Because `AppMvc.ViewModels` is imported, a view can write `@model Error` without a namespace.

### 8.4 Home page

**File: `AppMvc/Views/Home/Index.cshtml`**
```razor
@{
    ViewData["Title"] = "Home";
}
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
<!-- Individual pages, I place them with <main> tag. Will become separate Razor Pages -->
<!-- Page Welcome -->
<div class="container px-4 py-4" id="home">
    <div class="bg-body-tertiary p-5 rounded">
        <div class="col-sm-8 py-5 mx-auto">
            <h1 class="display-5 fw-normal">Welcome to AppMusic example</h1>
            <p class="fs-5">
                This example is part of the ASP.NET Web Development course, and shows how to combine ASP.NET Razorpages and MVC.
                Pages are rendered in ASP.NET backend using C# and Razor syntax. Frontend is using css, javascript, Bootstrap and JQuery.
                Backend architecture is MVC and MVVM.
            </p>
            <p>Enjoy!</p>
        </div>
    </div>
</div>
```

### 8.5 Error view

**File: `AppMvc/Views/Shared/Error.cshtml`**
```razor
@model Error
@{
    ViewData["Title"] = "Error";
}

<h1 class="text-danger">Error.</h1>
<h2 class="text-danger">An error occurred while processing your request.</h2>

@if (Model.ShowRequestId)
{
    <p>
        <strong>Request ID:</strong> <code>@Model.RequestId</code>
    </p>
}

<h3>Development Mode</h3>
<p>
    Swapping to <strong>Development</strong> environment will display more detailed information about the error that occurred.
</p>
<p>
    <strong>The Development environment shouldn't be enabled for deployed applications.</strong>
    It can result in displaying sensitive information from exceptions to end users.
    For local debugging, enable the <strong>Development</strong> environment by setting the <strong>ASPNETCORE_ENVIRONMENT</strong> environment variable to <strong>Development</strong>
    and restarting the app.
</p>
```

### 8.6 Validation scripts partial

**File: `AppMvc/Views/Shared/_ValidationScriptsPartial.cshtml`**
```razor
<script src="/lib/jquery-validation/dist/jquery.validate.js"></script>
<script src="/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.js"></script>

<script>var settings = {
        validClass: "is-valid",
        errorClass: "is-invalid"

    };
    $.validator.setDefaults(settings);
    $.validator.unobtrusive.options = settings;
</script>
```

The validation classes are changed to Bootstrap's `is-valid` and `is-invalid`.

### 8.7 The layout

The layout is the frame around every page. Replace the template's `Views/Shared/_Layout.cshtml` with the following. It has two large blocks that you copy from the target branch instead of typing them. The marked lines tell you which:

- **The icon block** (`<svg xmlns=...>` … `</svg>`): SVG symbols for buttons (home, edit, undo, save, boombox, album, artist).
- **The modal block**: three Bootstrap dialogs (`softModal`, `hardModal`, `dangerModal`) with the comment lines above them.

```bash
git show 3-seido-apps-foundation:AppMvc/Views/Shared/_Layout.cshtml
```

**File: `AppMvc/Views/Shared/_Layout.cshtml`**
```razor
<!doctype html>
@using Configuration.Options
@using Microsoft.Extensions.Options
@inject IConfiguration configuration;
@inject IOptions<EnvironmentOptions> envOptions;
@{}

<html lang="en" data-bs-theme="auto">
<!-- Part of page Frame -->
<!-- html-head with all resources -->
<head>
    <!--From index.cshtml-->
    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="description" content="">
    <meta name="author" content="Martin Lenart, Seido AB">

    <!-- Modified from index.cshtml-->
    <title>AppMusicMVC - @ViewData["Title"]</title>

    <!--From index.cshtml-->
    <link rel="apple-touch-icon" sizes="180x180" href="~/favicon_io/apple-touch-icon.png">
    <link rel="icon" type="image/png" sizes="32x32" href="~/favicon_io/favicon-32x32.png">
    <link rel="icon" type="image/png" sizes="16x16" href="~/favicon_io/favicon-16x16.png">
    <link rel="manifest" href="~/favicon_io/site.webmanifest">


    <!--From index.cshtml-->
    <!-- Bootstrap styles from their respective templates  -->
    <link href="~/lib/bootstrap/dist/css/bootstrap.css" rel="stylesheet">
    <link href="~/css/navbars-offcanvas.css" rel="stylesheet">
    <link href="~/css/features.css" rel="stylesheet">
    <link href="~/css/grid.css" rel="stylesheet">
    <link href="~/css/sign-in.css" rel="stylesheet">
    <link href="~/css/modals.css" rel="stylesheet">

</head>


<!-- Part of page Frame -->
<!-- Header and Menu -->
<body>
    <!-- SVG icons for buttons etc-->
    <!-- SVG icons for buttons etc-->
    <!-- PASTE HERE: the icon block -->

    <div class="container">
        <header class="d-flex flex-wrap justify-content-center py-3 mb-4 border-bottom">
            <a href="/" class="d-flex align-items-center mb-3 mb-md-0 me-md-auto link-body-emphasis text-decoration-none">
                <span class="fs-4">AppMusicMVC - @ViewData["Title"]</span>
            </a>

            <ul class="nav nav-pills">
                <li class="nav-item"><a asp-area="" asp-controller="Home" asp-action="Index" class="nav-link" aria-current="page">Home</a></li>
                <li class="nav-item"><a href="~/index.html" class="nav-link" aria-current="page">Static</a></li>
            </ul>
        </header>
    </div>

    <main>
        @RenderBody()
    </main>

    <!-- PASTE HERE: the modal block -->

    <!-- Part of page Frame -->
    <!-- Footer with copyright notice -->
    <div class="container">
      <footer class="row row-cols-1 row-cols-sm-2 row-cols-md-5 py-5 my-5 border-top">
        <div class="col mb-2">
            <a asp-area="" asp-controller="Home" asp-action="Index" class="mb-3 me-2 mb-md-0 text-body-secondary text-decoration-none lh-1">
                <span class="mb-3 mb-md-0 text-body-secondary">&copy;&nbsp;2026&nbsp;Seido&nbsp;AB</span>
            </a>
        </div>
      </footer>
    </div>
 
    <!-- Part of page Frame -->
    <!-- Bootstrap Javascript -->
    <script src="~/lib/bootstrap/dist/js/bootstrap.js"></script>

    <!-- .NET unobstrusive validation -->
    <script src="~/lib/jquery/dist/jquery.js"></script>

    <!-- SeidoAB script helpers -->
    <script src="~/js/seido/modals.js" asp-append-version="true"></script>
    <script src="~/js/seido/select-list-items.js" asp-append-version="true"></script>

    @await RenderSectionAsync("script", false)
</body>
</html>
```

Things to notice in the layout:
- `@inject` gives the layout the configuration and the `EnvironmentOptions` from the stack.
- `@RenderBody()` is where each page is placed, and `@await RenderSectionAsync("script", false)` lets a page add its own scripts.
- `asp-controller` / `asp-action` build the links from the routes.
- The template's `~/css/site.css` and `~/js/site.js` are gone, and the layout no longer links the CSS isolation file (`AppMvc.styles.css`). It uses the CSS you copied in Step 4. (`Views/Shared/_Layout.cshtml.css` is still in the folder in AppMvc, as in the target branch, but nothing uses it.)

Build AppMvc now:

```bash
dotnet build AppMvc/AppMvc.csproj
```

---

## Step 9: AppRazor – pages

### 9.1 Imports and start

**File: `AppRazor/Pages/_ViewImports.cshtml`** (replace)
```razor
@using AppRazor
@namespace AppRazor.Pages
@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers
```

**File: `AppRazor/Pages/_ViewStart.cshtml`** (replace)
```razor
@{
    #region L1.6 Using shared layouts with Razor Pages
    //Template: Razor View Start page
    Layout = "_Layout";
    #endregion
}
```

### 9.2 Home page and page model

**File: `AppRazor/Pages/Index.cshtml`**
```razor
@page
@model IndexModel
@{
    ViewData["Title"] = "Home";
}
<!-- Individual pages, I place them with <main> tag. Will become separate Razor Pages -->
<!-- Page Welcome -->
<div class="container px-4 py-4" id="home">
    <div class="bg-body-tertiary p-5 rounded">
        <div class="col-sm-8 py-5 mx-auto">
            <h1 class="display-5 fw-normal">Welcome to AppMusic example</h1>
            <p class="fs-5">
                This example is part of the ASP.NET Web Development course, and shows how to combine ASP.NET Razorpages and MVC.
                Pages are rendered in ASP.NET backend using C# and Razor syntax. Frontend is using css, javascript, Bootstrap and JQuery.
                Backend architecture is MVC and MVVM.
            </p>
            <p>Enjoy!</p>
        </div>
    </div>
</div>
```

**File: `AppRazor/Pages/Index.cshtml.cs`**
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AppRazor.Pages
{
	public class IndexModel : PageModel
    {
        readonly ILogger<IndexModel> _logger;

        public IndexModel(ILogger<IndexModel> logger)
        {
            _logger = logger;
        }
    }
}
```

### 9.3 Partials

**File: `AppRazor/Pages/Shared/_ValidationScriptsPartial.cshtml`** (replace, same content as in AppMvc)
```razor
<script src="/lib/jquery-validation/dist/jquery.validate.js"></script>
<script src="/lib/jquery-validation-unobtrusive/dist/jquery.validate.unobtrusive.js"></script>

<script>var settings = {
        validClass: "is-valid",
        errorClass: "is-invalid"

    };
    $.validator.setDefaults(settings);
    $.validator.unobtrusive.options = settings;
</script>
```

**File: `AppRazor/Pages/Shared/_LoginPartial.cshtml`** (new, empty file)
```razor

```

(An empty `_LoginPartial.cshtml` is created now, so the default Identity user interface can be used in the security branches.)

### 9.4 The layout

The Razor layout is **the MVC layout with five changes**. Copy the MVC layout you just wrote into `AppRazor/Pages/Shared/_Layout.cshtml` (replace the template file), and make these changes:

1. Replace the two `@inject` lines with nothing, and add `@using Microsoft.AspNetCore.Identity;` after `@using Configuration.Options`. The top of the file becomes:
   ```razor
   <!doctype html>
   @using Configuration.Options
   @using Microsoft.AspNetCore.Identity;
   @using Microsoft.Extensions.Options

   @{}
   ```
2. Change `AppMusicMVC` to `AppMusicRazor` in the `<title>` and in the `<span class="fs-4">`.
3. Change both Home links from `asp-controller="Home" asp-action="Index"` to `asp-page="/Index"` (one in the menu, one in the footer).
4. **Delete the modal block** (the three Bootstrap dialogs). AppRazor does not need them yet.
5. Nothing else changes. The icon block, the CSS and the scripts stay.

Build AppRazor:

```bash
dotnet build AppRazor/AppRazor.csproj
```

**Question:** the Razor page links with `asp-page="/Index"`, and MVC with `asp-controller` and `asp-action`. What does each tag helper use to find the URL?

---

## Step 10: Build and run

```bash
dotnet build GoodMusic.slnx
```

Expected: 0 errors.

Run AppMvc, in one terminal:

```bash
cd AppMvc
dotnet run --launch-profile http
```

and AppRazor in another:

```bash
cd AppRazor
dotnet run --launch-profile http
```

Check:

| Address | Expected |
|---|---|
| `http://localhost:5204/` (AppMvc) | Home page: "Welcome to AppMusic example", title "AppMusicMVC - Home" |
| `http://localhost:5183/` (AppRazor) | The same page, title "AppMusicRazor - Home" |
| `/static` (both) | Redirects to `/index.html` |
| `/version` (both) | JSON with version information from the stack |
| Click **Static** in the menu | The static page |

**Notes:**
- The console output says `Using Secret Storage: UserSecrets` and `Using User Secrets in Development environment.` That is `AddSecrets` in the stack reading `appsettings.json`.
- The applications do not show any data yet. The home page does not use the database, but the controller and the page get their services through dependency injection. If a service could not be created, the page would fail. That is a good test that `Program.cs` is correct.

**Question:** `HomeController` gets `IAdminService` but `Index()` does not use it. Why is the parameter there?

---

## Step 11: Check your result against the branch

```bash
git add -A AppMvc AppRazor AppWebApi .vscode GoodMusic.slnx
git diff --cached 3-seido-apps-foundation --stat -w --ignore-blank-lines -- AppMvc AppRazor AppWebApi .vscode GoodMusic.slnx ':!AppMvc/wwwroot/lib' ':!AppRazor/wwwroot/lib'
```

Expected: no differences, except in the project order in `GoodMusic.slnx` (the target lists AppMvc and AppRazor last), and possibly the byte order mark and the line endings. A difference in `AppMvc/Views/Shared/_Layout.cshtml.css` (only a comment URL) can be ignored.

The two `':!…/wwwroot/lib'` parts leave out the client libraries. You kept all the template files there, and the target branch has fewer (only the plain Bootstrap and jQuery files). The files the layout uses are in both.

If a file differs, show it:

```bash
git diff --cached 3-seido-apps-foundation -w --ignore-blank-lines -- AppMvc/Program.cs
```

Common mistakes:
- A missing `UseAuthentication()` or `UseStaticFiles()` in `Program.cs`.
- `Nullable` left as `enable` in a `.csproj`.
- A page or view that still refers to `ErrorViewModel` or `Privacy`.
- Forgetting to delete the template files, or the `AppWebApi` folder, in Step 2.

---

## Step 12: Commit

```bash
git status --short
git commit -m "Change the templates into applications connected to the software stack"
```

---

## Done when
- [ ] `dotnet build GoodMusic.slnx` gives 0 errors.
- [ ] Both applications start and show the home page and the static page.
- [ ] `/version` returns JSON in both applications.
- [ ] No Privacy page, `ErrorViewModel`, `site.css`, `site.js` or `AppWebApi` folder remains.
- [ ] Your files match `3-seido-apps-foundation` (see Step 11).

## Reflection
1. Which lines of `Program.cs` connect the application to the stack, and which one tells the application where to find its secrets?
2. Why do the applications only reference four of the seven stack projects?
3. What is the difference between `MapStaticAssets()` in the template and `UseStaticFiles()` now?
4. In which two places is the difference between MVC and Razor Pages most visible in the code you wrote? (Hint: `Program.cs` and the home page.)
5. The apps contain an `app-seeds.json` and `SeidoHelpers` that nothing uses yet. Why add them now?
