# Exercise: Repeat it for the album carousel

This exercise continues from [4ex-exercises.md](4ex-exercises.md) (Exercises 1 and 2: the album **cards** as a static page, a Razor Page and an MVC view, with menu items). Here you do the same work again for a second snippet, `_exercises-assets/album_carousel.html`, with less help.

A carousel needs Bootstrap's JavaScript. That shows an important difference between a static page and a page in the layout.

## Prerequisites
- You have finished Exercises 1 and 2 in `4ex-exercises.md`. In particular:
  - `wwwroot/album_cards.html` and the four pictures in `wwwroot/img` exist in both `AppMvc` and `AppRazor`.
  - The pages `AlbumCards` (Razor Page and MVC view, with the action `HomeController.AlbumCards`) exist.
  - Both menus have the items **Static cards** and **Album cards**.
- `AppMvc` and `AppRazor` build and run (`dotnet build GoodMusic.slnx` gives 0 errors).
- The folder `_exercises-assets/` contains `album_carousel.html`.
- Ports when you run with `dotnet run --launch-profile http`: AppMvc `http://localhost:5204`, AppRazor `http://localhost:5183`.

## Goal
Use the snippet `_exercises-assets/album_carousel.html` (a Bootstrap carousel with three slides) and create, in **both** applications:

1. A **static page** `wwwroot/album_carousel.html`, and the menu item **Static carousel**.
2. A **Razor Page** `AlbumCarousel` (AppRazor) and an **MVC view** `AlbumCarousel` (AppMvc), and the menu item **Album carousel**.
3. Finally, let the **Go somewhere** buttons of the cards lead to the carousel (as the original snippet intended).

You have done every part of this in Exercises 1 and 2. Try each step yourself first. A complete solution for each step is in a "Solution" section that you can open when you need it.

## Step 1: Study the snippet

```bash
cat _exercises-assets/album_carousel.html
```

Compare it with `album_cards.html`. Write down:

1. How many `<div class="container">` does it have, and which Bootstrap component does it use?
2. Which pictures does it use? Are they already in `wwwroot/img`? (They are, from Exercise 1. You do **not** copy any pictures.)
3. Which attributes show that the component needs **JavaScript** (`data-bs-...`)?
4. Does the static `index.html` / `album_cards.html` load any Bootstrap JavaScript? Does `_Layout.cshtml`? (Look at the end of the layout.)

**Prediction:** will the carousel work in a static page that only has Bootstrap's CSS? Decide, then test it in Step 2.

## Step 2: The static page

Create `AppMvc/wwwroot/album_carousel.html`, in the same way as `album_cards.html`: copy that file, change the title, and replace the content of `<main>` with the snippet. Remember:

- the title is `Album carousel`,
- the container id is `album-carousel`,
- the pictures are `./img/jupiter1.jpg` and so on, which is correct from the root of the site.

**First test it without any JavaScript** (the page then only loads the Bootstrap CSS). Start AppMvc, open `/album_carousel.html`, and look at the page. Try the two arrow buttons. What happens?

Then **add Bootstrap's JavaScript** at the end of `<body>`, like `index.html` does:

```html
<script src="/lib/bootstrap/dist/js/bootstrap.js"></script>
```

Test again. Now the slides change, and the arrows work. (The carousel also advances by itself, because of `data-bs-ride="carousel"`.)

<details>
<summary>Solution: <code>AppMvc/wwwroot/album_carousel.html</code></summary>

**File: `AppMvc/wwwroot/album_carousel.html`**
```html
<!doctype html>
<html lang="en" data-bs-theme="auto">
<head>

    <meta charset="utf-8">
    <meta name="viewport" content="width=device-width, initial-scale=1">
    <meta name="description" content="">
    <meta name="author" content="Martin Lenart, Seido AB">
    <title>Album carousel</title>

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
      <!-- Page Album carousel -->
      <div class="container px-4 py-4" id="album-carousel">
        <div id="carouselExampleCaptions" class="carousel slide" data-bs-ride="carousel">
          <div class="carousel-inner">
            <div class="carousel-item active">
              <img src="./img/jupiter1.jpg" class="d-block w-100" alt="...">
              <div class="carousel-caption d-none d-md-block">
                <h5>First slide label</h5>
                <p>Some representative placeholder content for the first slide.</p>
              </div>
            </div>
            <div class="carousel-item">
              <img src="./img/jupiter2.jpg" class="d-block w-100" alt="...">
              <div class="carousel-caption d-none d-md-block">
                <h5>Second slide label</h5>
                <p>Some representative placeholder content for the second slide.</p>
              </div>
            </div>
            <div class="carousel-item">
              <img src="./img/jupiter3.jpg" class="d-block w-100" alt="...">
              <div class="carousel-caption d-none d-md-block">
                <h5>Third slide label</h5>
                <p>Some representative placeholder content for the third slide.</p>
              </div>
            </div>
          </div>
          <button class="carousel-control-prev" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="prev">
            <span class="carousel-control-prev-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Previous</span>
          </button>
          <button class="carousel-control-next" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="next">
            <span class="carousel-control-next-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Next</span>
          </button>
        </div>
      </div>
    </main>

    <script src="/lib/bootstrap/dist/js/bootstrap.js"></script>
  </body>
</html>
```

</details>

Copy the page to AppRazor (it is identical):

```bash
cp AppMvc/wwwroot/album_carousel.html AppRazor/wwwroot/album_carousel.html
```

## Step 3: The menu item for the static page

In **both** layouts, add an item after **Static cards**: the text **Static carousel**, linking to `~/album_carousel.html`. Use the **Static cards** item as a model.

<details>
<summary>Solution</summary>

```html
<li class="nav-item"><a href="~/album_carousel.html" class="nav-link">Static carousel</a></li>
```

</details>

## Step 4: Test the static page

Build and run both applications. Check that **Static carousel** opens `/album_carousel.html` in AppMvc and in AppRazor, that the slides change, and that the page has no menu.

## Step 5: The Razor Page in AppRazor

Create the page `AlbumCarousel`: a page model `AlbumCarouselModel` in `Pages/AlbumCarousel.cshtml.cs` and the page `Pages/AlbumCarousel.cshtml`. Use `AlbumCards` as the model. What must change in the markup, compared with the static page?

Hints:
- Only the **content** goes in the page: the `<div class="container">` and what is inside it.
- `src="./img/..."` becomes `src="~/img/..."`.
- The page does **not** need its own `<script src=".../bootstrap.js">`. The layout already loads Bootstrap's JavaScript. (Check this in `_Layout.cshtml`.)
- Set `ViewData["Title"]`.

<details>
<summary>Solution</summary>

**File: `AppRazor/Pages/AlbumCarousel.cshtml.cs`**
```csharp
using Microsoft.AspNetCore.Mvc.RazorPages;
namespace AppRazor.Pages
{
    public class AlbumCarouselModel : PageModel
    {
        readonly ILogger<AlbumCarouselModel> _logger;

        public AlbumCarouselModel(ILogger<AlbumCarouselModel> logger)
        {
            _logger = logger;
        }
    }
}
```

**File: `AppRazor/Pages/AlbumCarousel.cshtml`**
```razor
@page
@model AlbumCarouselModel
@{
    ViewData["Title"] = "Album carousel";
}
<!-- Individual pages -->
<!-- Page Album carousel -->
<div class="container px-4 py-4" id="album-carousel">
    <div id="carouselExampleCaptions" class="carousel slide" data-bs-ride="carousel">
        <div class="carousel-inner">
            <div class="carousel-item active">
                <img src="~/img/jupiter1.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>First slide label</h5>
                    <p>Some representative placeholder content for the first slide.</p>
                </div>
            </div>
            <div class="carousel-item">
                <img src="~/img/jupiter2.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>Second slide label</h5>
                    <p>Some representative placeholder content for the second slide.</p>
                </div>
            </div>
            <div class="carousel-item">
                <img src="~/img/jupiter3.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>Third slide label</h5>
                    <p>Some representative placeholder content for the third slide.</p>
                </div>
            </div>
        </div>
        <button class="carousel-control-prev" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="prev">
            <span class="carousel-control-prev-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Previous</span>
        </button>
        <button class="carousel-control-next" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="next">
            <span class="carousel-control-next-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Next</span>
        </button>
    </div>
</div>
```

</details>

## Step 6: The MVC view in AppMvc

Add the action `AlbumCarousel()` to `HomeController`, after `AlbumCards()`, and create the view `Views/Home/AlbumCarousel.cshtml`. The view is the same as the Razor Page, except for the first lines (no `@page`, no `@model`).

<details>
<summary>Solution</summary>

**File: `AppMvc/Controllers/HomeController.cs`** (add this method after `AlbumCards()`)
```csharp
    public IActionResult AlbumCarousel()
    {
        return View();
    }
```

**File: `AppMvc/Views/Home/AlbumCarousel.cshtml`**
```razor
@{
    ViewData["Title"] = "Album carousel";
}
<!-- Individual pages -->
<!-- Page Album carousel -->
<div class="container px-4 py-4" id="album-carousel">
    <div id="carouselExampleCaptions" class="carousel slide" data-bs-ride="carousel">
        <div class="carousel-inner">
            <div class="carousel-item active">
                <img src="~/img/jupiter1.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>First slide label</h5>
                    <p>Some representative placeholder content for the first slide.</p>
                </div>
            </div>
            <div class="carousel-item">
                <img src="~/img/jupiter2.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>Second slide label</h5>
                    <p>Some representative placeholder content for the second slide.</p>
                </div>
            </div>
            <div class="carousel-item">
                <img src="~/img/jupiter3.jpg" class="d-block w-100" alt="...">
                <div class="carousel-caption d-none d-md-block">
                    <h5>Third slide label</h5>
                    <p>Some representative placeholder content for the third slide.</p>
                </div>
            </div>
        </div>
        <button class="carousel-control-prev" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="prev">
            <span class="carousel-control-prev-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Previous</span>
        </button>
        <button class="carousel-control-next" type="button" data-bs-target="#carouselExampleCaptions" data-bs-slide="next">
            <span class="carousel-control-next-icon" aria-hidden="true"></span>
            <span class="visually-hidden">Next</span>
        </button>
    </div>
</div>
```

</details>

## Step 7: The menu item for the new pages

Add the item **Album carousel** after **Album cards**, in both layouts. Link it to the page (Razor) and to the controller and action (MVC), as you did for **Album cards**.

<details>
<summary>Solution</summary>

**AppRazor: `Pages/Shared/_Layout.cshtml`**
```html
<li class="nav-item"><a asp-area="" asp-page="/AlbumCarousel" class="nav-link">Album carousel</a></li>
```

**AppMvc: `Views/Shared/_Layout.cshtml`**
```html
<li class="nav-item"><a asp-area="" asp-controller="Home" asp-action="AlbumCarousel" class="nav-link">Album carousel</a></li>
```

</details>

The menus are now: Home, Static, Static cards, **Static carousel**, Album cards, **Album carousel** (AppRazor and AppMvc). The static pages come first and the Razor/MVC pages after them.

## Step 8: Let the cards lead to the carousel

The original snippet `album_cards.html` had its buttons link to `../album_carousel.html`. In Exercises 1 and 2 you pointed them at the Home page, because the carousel did not exist. Now it does. Change the **Go somewhere** button in all four cards, in three places in each application:

| Page | Old link | New link |
|---|---|---|
| `wwwroot/album_cards.html` (both apps) | `href="/index.html"` | `href="/album_carousel.html"` |
| `AppRazor/Pages/AlbumCards.cshtml` | `asp-page="/Index"` | `asp-page="/AlbumCarousel"` |
| `AppMvc/Views/Home/AlbumCards.cshtml` | `asp-controller="Home" asp-action="Index"` | `asp-controller="Home" asp-action="AlbumCarousel"` |

Use find and replace in your editor, so you change all four buttons in each file at once.

## Step 9: Test and commit

```bash
dotnet build GoodMusic.slnx
```

Run both applications, and check:

| Check | Expected |
|---|---|
| Menu | Home, Static, Static cards, Static carousel, Album cards, Album carousel |
| **Static carousel** | `/album_carousel.html`: the carousel works, there is no menu |
| **Album carousel** | AppRazor `/AlbumCarousel`, AppMvc `/Home/AlbumCarousel`: the carousel works **inside the layout**, with menu and footer |
| **Static cards**, then **Go somewhere** | `/album_carousel.html` (the static carousel) |
| **Album cards**, then **Go somewhere** | The Razor/MVC carousel (`/AlbumCarousel` or `/Home/AlbumCarousel`) |
| View source of the Razor/MVC carousel | Only **one** `bootstrap.js` script, loaded by the layout |

**Questions**
1. Why does the static carousel need its own `<script src="/lib/bootstrap/dist/js/bootstrap.js">`, and the Razor/MVC page not?
2. The cards in the static page and in the Razor/MVC page lead to different carousels. Why? Which line decides it?
3. All the pages use `id="carouselExampleCaptions"` for the carousel. What would happen if you put two carousels with the same id on one page?
4. You now have six pages in the menu, and the same markup twice (static and Razor/MVC). If a designer asks for a different menu, how many files change? And if they ask for a different slide text?

```bash
git status --short
git add AppMvc AppRazor
git commit -m "Add album carousel static page, Razor Page and MVC view, and link the cards to it"
```

## Done when
- [ ] `album_carousel.html` exists in both `wwwroot` folders and loads Bootstrap's JavaScript.
- [ ] `AppRazor/Pages/AlbumCarousel.cshtml(.cs)`, `AppMvc/Views/Home/AlbumCarousel.cshtml` and the action `HomeController.AlbumCarousel` exist.
- [ ] Both menus have **Static carousel** and **Album carousel**, and both carousels work.
- [ ] The **Go somewhere** buttons of the static cards lead to the static carousel, and those of the Razor/MVC cards lead to the Razor/MVC carousel.
- [ ] `dotnet build GoodMusic.slnx` gives 0 errors.
