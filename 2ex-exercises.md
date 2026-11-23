# Exercise: Create AppMvc and AppRazor from the Microsoft templates

## Goal
Start from the software stack only (class libraries, no web application) and add two web applications generated from the Microsoft templates:

- **AppMvc**: ASP.NET Core Web App (Model-View-Controller)
- **AppRazor**: ASP.NET Core Web App (Razor Pages)

The outcome is the code of branch **`2-ms-apps-template`**. The applications are the unchanged templates. They are a starting point only, and are not yet connected to the stack. That comes in later branches.

## Prerequisites
- .NET 10 SDK (`dotnet --version` shows `10.x`).
- A terminal in the repository root (the folder that contains `GoodMusic.slnx`).
- You are on branch `2ex-ms-apps-template`.

---

## Step 1: Inspect the starting point

```bash
git branch --show-current     # 2ex-ms-apps-template
ls
```

You should see the stack projects `Configuration`, `Encryptions`, `Models`, `DbModels`, `DbContext`, `DbRepos`, `Services`, the solution file `GoodMusic.slnx`, and **no** `AppMvc` or `AppRazor`.

```bash
cat GoodMusic.slnx
```

It lists only the seven stack projects.

**Question:** which of the projects will the web applications eventually need to reference? (Not needed yet. Just think about it.)

---

## Step 2: See which templates are available

```bash
dotnet new list mvc
dotnet new list razor
```

Note the short names `mvc` and `razor` (also `webapp`). Look at the options of one of them:

```bash
dotnet new mvc --help
```

Find `--framework` (`-f`) and `--auth` (`-au`, default `None`). You will use `-f net10.0` and keep the default authentication.

---

## Step 3: Create AppMvc

```bash
dotnet new mvc -n AppMvc -f net10.0
```

- `-n AppMvc` creates a folder `AppMvc/` and names the project and its namespace `AppMvc`.
- `-f net10.0` targets .NET 10.

Look at what was generated:

```
AppMvc/
├─ AppMvc.csproj
├─ Program.cs
├─ appsettings.json, appsettings.Development.json
├─ Properties/launchSettings.json
├─ Controllers/HomeController.cs
├─ Models/ErrorViewModel.cs
├─ Views/Home/Index.cshtml, Privacy.cshtml
├─ Views/Shared/_Layout.cshtml, Error.cshtml, _ValidationScriptsPartial.cshtml
├─ Views/_ViewImports.cshtml, _ViewStart.cshtml
└─ wwwroot/ (css, js, lib/bootstrap, lib/jquery, favicon.ico)
```

---

## Step 4: Create AppRazor

```bash
dotnet new razor -n AppRazor -f net10.0
```

```
AppRazor/
├─ AppRazor.csproj
├─ Program.cs
├─ appsettings.json, appsettings.Development.json
├─ Properties/launchSettings.json
├─ Pages/Index.cshtml(.cs), Privacy.cshtml(.cs), Error.cshtml(.cs)
├─ Pages/Shared/_Layout.cshtml, _ValidationScriptsPartial.cshtml
├─ Pages/_ViewImports.cshtml, _ViewStart.cshtml
└─ wwwroot/ (css, js, lib/bootstrap, lib/jquery, favicon.ico)
```

---

## Step 5: Add both projects to the solution

```bash
dotnet sln GoodMusic.slnx add AppMvc/AppMvc.csproj AppRazor/AppRazor.csproj
cat GoodMusic.slnx
```

The two projects are now listed together with the stack projects.

---

## Step 6: Build the whole solution

```bash
dotnet build GoodMusic.slnx
```

Expected: `0 Warning(s)`, `0 Error(s)`.

---

## Step 7: Run and explore each application

Start AppMvc:

```bash
cd AppMvc
dotnet run
```

The output shows the address, for example `Now listening on: http://localhost:5081` (the port number is chosen when the project is created, so yours differs). Open it in a browser and click **Home** and **Privacy**. Stop with `Ctrl+C`, then do the same for AppRazor:

```bash
cd ../AppRazor
dotnet run
```

Compare how the two work:

| | AppMvc | AppRazor |
|---|---|---|
| Request handling | `Controllers/HomeController.cs` actions return views | A page `Pages/Index.cshtml` with its page model `Index.cshtml.cs` |
| Where the HTML is | `Views/Home/Index.cshtml` | `Pages/Index.cshtml` |
| Registered in `Program.cs` with | `AddControllersWithViews()` and `MapControllerRoute(...)` | `AddRazorPages()` and `MapRazorPages()` |
| URL of Privacy | `/Home/Privacy` (`{controller}/{action}`) | `/Privacy` (the file path) |

Also read both `Program.cs` files and find: the error handling in production, `UseHttpsRedirection`, `UseAuthorization`, `MapStaticAssets`.

**Question:** the templates have `app.UseAuthorization()`, but nothing is protected. Why? (Hint: nothing is marked with `[Authorize]` and no authentication is configured.)

---

## Step 8: Check your result against the branch

Your result should equal branch `2-ms-apps-template`. Compare:

```bash
git add AppMvc AppRazor GoodMusic.slnx
git diff --cached 2-ms-apps-template --stat -- AppMvc AppRazor GoodMusic.slnx
```

Expected differences only:
- `Properties/launchSettings.json`: the port numbers (they are random per project).
- `GoodMusic.slnx`: the order of the project lines and the path separators (`/` or `\`).
- Possibly line endings (CRLF or LF).

If other files differ, check that you used `mvc` and `razor`, `-f net10.0`, and the exact names `AppMvc` and `AppRazor`.

> The branch `2-ms-apps-template` also contains an `AppWebApi` folder. It is not part of this exercise.

---

## Step 9: Commit

Generated `bin/` and `obj/` folders are ignored by `.gitignore`. Check, then commit:

```bash
git status --short
git commit -m "Add AppMvc and AppRazor from the Microsoft templates"
```

---

## Done when
- [ ] `AppMvc/` and `AppRazor/` exist and are listed in `GoodMusic.slnx`.
- [ ] `dotnet build GoodMusic.slnx` gives 0 warnings and 0 errors.
- [ ] Both applications start, and show Home and Privacy.
- [ ] Your files match `2-ms-apps-template`, apart from the ports and line endings.

## Reflection
1. What is the difference in where the HTML for the home page lives in MVC and in Razor Pages?
2. Open `AppMvc.csproj` and `AppRazor.csproj`. What do they reference? (Nothing from the stack yet.)
3. Which of the stack projects would you reference from the applications first, and why?

---

## Alternative: use the IDE instead of the command line
In Visual Studio: *Add → New Project → ASP.NET Core Web App (Model-View-Controller)* with name `AppMvc`, and *ASP.NET Core Web App (Razor Pages)* with name `AppRazor`. Choose **.NET 10**, **Authentication type: None**, **Configure for HTTPS**: your choice, and no Docker. The result is the same as with `dotnet new`.
