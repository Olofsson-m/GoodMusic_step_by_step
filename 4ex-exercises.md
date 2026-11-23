# Exercises: From a static page to a Razor Page and an MVC view

In these exercises you take a ready-made piece of HTML, `_exercises-assets/album_cards.html` (four Bootstrap cards with album pictures), and use it in **both** applications. Each exercise ends with a **menu item** that reaches the new page:

1. **Exercise 1:** the cards as a **static HTML page** in `wwwroot`, and a menu item **Static cards** for it.
2. **Exercise 2:** the cards as a **Razor Page** in AppRazor and an **MVC view** in AppMvc, inside the common layout, and a menu item **Album cards** for each.

The point is to see what the layout, routing and tag helpers add to plain HTML. Do the exercises in order. Exercise 2 continues from the result of Exercise 1.

A third exercise repeats the work for a second snippet, `album_carousel.html`, with less help: [4ex-exercises-carousel.md](4ex-exercises-carousel.md).

## Prerequisites
- You are on branch `4ex-apps-info`. `AppMvc` and `AppRazor` build and run (`dotnet build GoodMusic.slnx` gives 0 errors).
- The folder `_exercises-assets/` contains `album_cards.html` and four pictures in `_exercises-assets/img/` (`jupiter1.jpg`, `jupiter2.jpg`, `jupiter3.jpg`, `jupiter5.jpg`).
- Ports when you run with `dotnet run --launch-profile http`: AppMvc `http://localhost:5204`, AppRazor `http://localhost:5183`.

## How the two kinds of pages differ
| | Static page (`wwwroot`) | Razor Page / MVC view |
|---|---|---|
| Served by | `app.UseStaticFiles()` straight from the file | Routing, then Razor renders it on the server |
| URL | The file name, e.g. `/album_cards.html` | Razor: `/AlbumCards`. MVC: `/Home/AlbumCards` |
| Frame (head, menu, footer) | Must be written in the file itself | Comes from `_Layout.cshtml` |
| Links and images | Plain `href` and `src` | Tag helpers: `asp-page`, `asp-controller`, `asp-action`, and `~/` |
| Can use C# | No | Yes |

---

# Exercise 1: A static page `album_cards.html`

## Goal
Create `album_cards.html` in the `wwwroot` folder of **both** applications. It should look like `wwwroot/index.html`, but show the cards from `_exercises-assets/album_cards.html`. Then add a **menu item for the static page** (`Static cards`) in both applications, next to the existing `Static` item.

## Step 1: Look at what you have

```bash
cat AppMvc/wwwroot/index.html
cat _exercises-assets/album_cards.html
ls _exercises-assets/img
```

`index.html` is a complete HTML document: a `<head>` with the favicon links and Bootstrap's CSS, and a `<body>` with `<main>` and the content. `album_cards.html` is only the **content** (the `<div class="container">` with four cards), without `<html>`, `<head>` or `<body>`.

Run AppMvc, and open `http://localhost:5204/index.html` and `http://localhost:5204/static`. Note that `/static` redirects to `/index.html`. Stop the application.

**Questions**
1. Which line in `Program.cs` makes `wwwroot/index.html` available?
2. In the snippet, each `<img>` has `src="./img/jupiter1.jpg"`. Which folder must the pictures be in, for a page at `/album_cards.html`?

## Step 2: Copy the pictures into `wwwroot`

The pictures must be served by the web server, so they go in `wwwroot`. Both applications have their own `wwwroot`:

```bash
mkdir AppMvc/wwwroot/img AppRazor/wwwroot/img
cp _exercises-assets/img/*.jpg AppMvc/wwwroot/img/
cp _exercises-assets/img/*.jpg AppRazor/wwwroot/img/
```

(On Windows, copy the files in your file explorer.)

Check: start an application, and open `/img/jupiter1.jpg`. The picture is shown.

## Step 3: Create the static page in AppMvc

Create the file `AppMvc/wwwroot/album_cards.html`. Start from a copy of `index.html`, and change these things:

- The `<title>`: `Album cards`.
- The content of `<main>`: replace the content with the snippet from `_exercises-assets/album_cards.html`.
- Remove the `<script>` that fills in the Bootstrap version. The cards do not need it. (The Bootstrap JavaScript is not needed for cards either, so it can be removed too.)
- Change the button link from `../album_carousel.html` to `/index.html`. There is no `album_carousel.html` yet, and `../` would point outside the site.

The result:

**File: `AppMvc/wwwroot/album_cards.html`**
```html
<!doctype html>
<html lang="en" data-bs-theme="auto">
<head>

    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="description" content="">
    <meta name="author" content="Martin Lenart, Seido AB">
    <title>Album cards</title>

    <link rel="apple-touch-icon" sizes="180x180" href="/favicon_io/apple-touch-icon.png">
    <link rel="icon" type="image/png" sizes="32x32" href="/favicon_io/favicon-32x32.png">
    <link rel="icon" type="image/png" sizes="16x16" href="/favicon_io/favicon-16x16.png">
    <link rel="manifest" href="/favicon_io/site.webmanifest">


    <!-- Bootstrap styles from their website -->
    <link href="/lib/bootstrap/dist/css/bootstrap.css" rel="stylesheet">
</head>

  <body>
    <main>
      <!-- Individual pages -->
      <!-- Page Album cards -->
      <div class="container px-4 py-4" id="album-cards">
        <div class="d-flex flex-wrap justify-content-between p-5 rounded">
          <div class="card m-3" style="width: 18rem;">
            <img src="./img/jupiter1.jpg" class="card-img-top" alt="...">
            <div class="card-body">
              <h5 class="card-title">Card title</h5>
              <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
              <a href="/index.html" class="btn btn-primary">Go somewhere</a>
            </div>
          </div>
          <div class="card m-3" style="width: 18rem;">
            <img src="./img/jupiter2.jpg" class="card-img-top" alt="...">
            <div class="card-body">
              <h5 class="card-title">Card title</h5>
              <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
              <a href="/index.html" class="btn btn-primary">Go somewhere</a>
            </div>
          </div>
          <div class="card m-3" style="width: 18rem;">
            <img src="./img/jupiter3.jpg" class="card-img-top" alt="...">
            <div class="card-body">
              <h5 class="card-title">Card title</h5>
              <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
              <a href="/index.html" class="btn btn-primary">Go somewhere</a>
            </div>
          </div>
          <div class="card m-3" style="width: 18rem;">
            <img src="./img/jupiter5.jpg" class="card-img-top" alt="...">
            <div class="card-body">
              <h5 class="card-title">Card title</h5>
              <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
              <a href="/index.html" class="btn btn-primary">Go somewhere</a>
            </div>
          </div>
        </div>
      </div>
    </main>
  </body>
</html>
```

The container has the id `album-cards` instead of `home` (an `id` should describe the page).

## Step 4: Create the static page in AppRazor

The static page does not use anything from the application, so it is **identical**. Copy it:

```bash
cp AppMvc/wwwroot/album_cards.html AppRazor/wwwroot/album_cards.html
```

## Step 5: Add the menu item for the static page, in both applications

The menu is in the layout, in the list `<ul class="nav nav-pills">`. There is already an item **Static** that links to `~/index.html`. Add an item after it, in both layouts:

**AppMvc: `Views/Shared/_Layout.cshtml`** and **AppRazor: `Pages/Shared/_Layout.cshtml`**

```html
<li class="nav-item"><a href="~/index.html" class="nav-link" aria-current="page">Static</a></li>
<li class="nav-item"><a href="~/album_cards.html" class="nav-link">Static cards</a></li>
```

The link is a plain `href`: the page is a file, not a route. The `~/` is the application root, and Razor converts it to the right path (also if the application is hosted below a sub path).

## Step 6: Test

```bash
dotnet build GoodMusic.slnx
cd AppMvc && dotnet run --launch-profile http
```

Check, in AppMvc (`http://localhost:5204`) and then in AppRazor (`http://localhost:5183`):

| Check | Expected |
|---|---|
| Menu | Home, Static, **Static cards** |
| Click **Static cards** | `/album_cards.html` shows four cards with Jupiter pictures |
| Click **Go somewhere** | You are taken to `/index.html` |
| Menu on the static page | **There is none**. The static page is outside the layout. The browser's back button is the only way back |

**Questions**
1. Why is there no menu or footer on `/album_cards.html`?
2. What would you have to do to get the menu on the static page? (Think about how many files you would have to change when the menu changes.)

## Step 7: Commit

```bash
git status --short
git add AppMvc AppRazor
git commit -m "Add static album cards page with menu item"
```

## Done when (Exercise 1)
- [ ] `AppMvc/wwwroot` and `AppRazor/wwwroot` have `album_cards.html` and `img/` with four pictures.
- [ ] Both menus have the item **Static cards**, and it opens the page with the four cards.
- [ ] The static page is shown without the layout (no menu).
- [ ] `dotnet build GoodMusic.slnx` gives 0 errors.

---

# Exercise 2: The same page as a Razor Page and an MVC view

## Goal
Move the content of the static page into the application's own page system, so that it is shown **inside the layout** (menu, footer, title). Then add a **menu item for the new page** (`Album cards`) in both applications, next to `Static cards` from Exercise 1.

- AppRazor: a Razor Page `AlbumCards` with the URL `/AlbumCards`.
- AppMvc: a view `AlbumCards` with the action `Home/AlbumCards` and the URL `/Home/AlbumCards`.

Compared to the static page you keep only the **content** (the `<div class="container">`). The `<html>`, `<head>`, `<body>`, the Bootstrap link and the favicons are already in `_Layout.cshtml`.

## Step 1: Look at an existing page

Open `AppRazor/Pages/Index.cshtml` and `Index.cshtml.cs`, and `AppMvc/Controllers/HomeController.cs` with `Views/Home/Index.cshtml`.

Note how they are built:

| | Razor Page | MVC |
|---|---|---|
| The page | `Pages/Index.cshtml` starts with `@page` and `@model IndexModel` | `Views/Home/Index.cshtml` has no `@page` |
| The code | `IndexModel` in `Index.cshtml.cs` | The action `Index()` in `HomeController` |
| The title | `ViewData["Title"] = "Home"` | the same |
| The URL | from the file name: `/Index` (also `/`) | from `{controller}/{action}`: `/Home/Index` (also `/`) |

## Step 2: AppRazor: the page model

Create `AppRazor/Pages/AlbumCards.cshtml.cs`. It follows the pattern of `IndexModel`:

**File: `AppRazor/Pages/AlbumCards.cshtml.cs`**
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AppRazor.Pages
{
    public class AlbumCardsModel : PageModel
    {
        readonly ILogger<AlbumCardsModel> _logger;

        public AlbumCardsModel(ILogger<AlbumCardsModel> logger)
        {
            _logger = logger;
        }
    }
}
```

The name must be the file name followed by `Model`. The namespace `AppRazor.Pages` is the one given in `Pages/_ViewImports.cshtml`.

## Step 3: AppRazor: the page

Create `AppRazor/Pages/AlbumCards.cshtml`. Copy the `<div class="container">` from `wwwroot/album_cards.html` (not the `<html>`, `<head>` or `<body>`), and add the Razor parts at the top. Make two changes to the content:

1. The pictures: `src="./img/jupiter1.jpg"` becomes `src="~/img/jupiter1.jpg"`. The page is not served from the root any more, and `~/` always means the application root.
2. The buttons: `href="/index.html"` becomes `asp-page="/Index"`, so the link is built from the **page name** and not from a file name.

**File: `AppRazor/Pages/AlbumCards.cshtml`**
```razor
@page
@model AlbumCardsModel
@{
    ViewData["Title"] = "Album cards";
}
<!-- Individual pages -->
<!-- Page Album cards -->
<div class="container px-4 py-4" id="album-cards">
    <div class="d-flex flex-wrap justify-content-between p-5 rounded">
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter1.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-page="/Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter2.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-page="/Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter3.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-page="/Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter5.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-page="/Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
    </div>
</div>
```

`ViewData["Title"]` is shown by the layout in the browser tab and in the page header ("AppMusicRazor - Album cards").

## Step 4: AppMvc: the action

In `AppMvc/Controllers/HomeController.cs`, add an action. Put it after `Index()`:

**File: `AppMvc/Controllers/HomeController.cs`** (add this method)
```csharp
    public IActionResult AlbumCards()
    {
        return View();
    }
```

By convention `View()` looks for `Views/Home/AlbumCards.cshtml`, the view with the same name as the action.

## Step 5: AppMvc: the view

Create `AppMvc/Views/Home/AlbumCards.cshtml`. It has the same content as the Razor Page, except for the first lines (no `@page` and no `@model`) and the buttons, which use the controller and the action:

**File: `AppMvc/Views/Home/AlbumCards.cshtml`**
```razor
@{
    ViewData["Title"] = "Album cards";
}
<!-- Individual pages -->
<!-- Page Album cards -->
<div class="container px-4 py-4" id="album-cards">
    <div class="d-flex flex-wrap justify-content-between p-5 rounded">
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter1.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-controller="Home" asp-action="Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter2.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-controller="Home" asp-action="Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter3.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-controller="Home" asp-action="Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
        <div class="card m-3" style="width: 18rem;">
            <img src="~/img/jupiter5.jpg" class="card-img-top" alt="...">
            <div class="card-body">
                <h5 class="card-title">Card title</h5>
                <p class="card-text">Some quick example text to build on the card title and make up the bulk of the card's content.</p>
                <a asp-controller="Home" asp-action="Index" class="btn btn-primary">Go somewhere</a>
            </div>
        </div>
    </div>
</div>
```

The tag helpers work because `Views/_ViewImports.cshtml` has `@addTagHelper *, Microsoft.AspNetCore.Mvc.TagHelpers`.

## Step 6: Add the menu item for the new pages, in both applications

Add one more item to each layout, after **Static cards**. The page is reached through routing, so the link is built from the page (Razor) or from the controller and action (MVC), not from a file name (as it was for the static page):

**AppRazor: `Pages/Shared/_Layout.cshtml`**
```html
<li class="nav-item"><a asp-area="" asp-page="/AlbumCards" class="nav-link">Album cards</a></li>
```

**AppMvc: `Views/Shared/_Layout.cshtml`**
```html
<li class="nav-item"><a asp-area="" asp-controller="Home" asp-action="AlbumCards" class="nav-link">Album cards</a></li>
```

The menus are now:

| | Menu |
|---|---|
| AppRazor | Home, Static, Static cards, **Album cards** |
| AppMvc | Home, Static, Static cards, **Album cards** |

## Step 7: Test

```bash
dotnet build GoodMusic.slnx
```

Run both applications, and check:

| Check | Expected |
|---|---|
| Click **Album cards** | AppRazor: `/AlbumCards`. AppMvc: `/Home/AlbumCards`. The four cards are shown **inside the layout**: with the menu, the title "AppMusicRazor - Album cards" (or "AppMusicMVC - ...") and the footer |
| Click **Go somewhere** | You are taken to the Home page (`/`), through the link built by the tag helper |
| Page source (View Source) of the page | One complete `<html>` document, where the head and the menu come from the layout. The links are `/` and the pictures `/img/jupiter1.jpg` |
| Click **Static cards** | The static page, without the menu |
| `/AlbumCards` in AppMvc, or `/Home/AlbumCards` in AppRazor | 404: the page exists in the other application only |

**Questions**
1. Compare **Static cards** and **Album cards**. What do you get for free when the page is in the layout, and what do you give up?
2. Change the text in the menu, in `_Layout.cshtml`. How many pages are affected, in the static version and in the Razor/MVC version?
3. Why does `<img src="~/img/jupiter1.jpg">` work, without any C#? (Hint: the tag helper `UrlResolutionTagHelper` changes `~/`.)
4. What is the URL of the Razor Page when the file is `Pages/AlbumCards.cshtml`? What would it be in `Pages/Members/AlbumCards.cshtml`?
5. The four cards are almost the same. How would you avoid repeating the markup four times? (Hint: a list of picture names in the page model or in `ViewData`, and `@foreach`.)

## Step 8: Commit

```bash
git status --short
git add AppMvc AppRazor
git commit -m "Add album cards Razor Page and MVC view with menu items"
```

---

## Done when (Exercise 2)
- [ ] `AppRazor/Pages/AlbumCards.cshtml(.cs)`, `AppMvc/Views/Home/AlbumCards.cshtml` and the action `HomeController.AlbumCards` exist.
- [ ] Both menus have the item **Album cards** (after **Static cards**), and it opens the page with the four cards.
- [ ] The Razor/MVC page is shown inside the layout (menu, title, footer), and **Go somewhere** leads to the Home page.
- [ ] `dotnet build GoodMusic.slnx` gives 0 errors.

The next exercise, [Exercise 3: Repeat it for the album carousel](4ex-exercises-carousel.md), is in its own file.
