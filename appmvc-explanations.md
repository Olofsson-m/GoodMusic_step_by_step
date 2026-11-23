# AppMvc – Branch `17-sec-authorization`

This branch adds **authorization at controller and action level**. Until now the app knew who the user was (login), but any visitor could open every page and run every action. Now `GroupController` and `SeedController` require a logged-in user, with three public actions in `GroupController` as exceptions.

Everything below is compared with the previous branch (`16-sec-authentication`).

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| `GroupController` | Every action open to anyone | `[Authorize]` on the class, `[AllowAnonymous]` on `ListOfGroups`, `SearchGroup` and `ViewGroup` |
| `SeedController` | Open to anyone | `[Authorize]` on the class |
| `AccountController`, `HomeController` | Open | Unchanged, still open |
| Views, layout, `Program.cs` | – | Unchanged |

The change is four attributes and two `using` lines. The mechanism that makes them work was already configured in the previous branch (the cookie's `LoginPath`).

---

## 2. How `[Authorize]` works here

The authorization middleware runs after authentication and before the action:

```
request → UseAuthentication (reads cookie GoodMusicAuth → HttpContext.User)
        → UseAuthorization  (reads [Authorize] / [AllowAnonymous] on the endpoint)
        → action
```

- No attribute: the action is open.
- `[Authorize]` on a class: **every action in the class** requires an authenticated user.
- `[AllowAnonymous]` on an action: overrides the class-level `[Authorize]` for that action.

If an anonymous user reaches a protected endpoint, the cookie handler **challenges**: it answers with a 302 to the `LoginPath` from the previous branch, with the original address in `ReturnUrl`:

```
GET /Group/EditGroup  →  302  /Account/Login?ReturnUrl=%2FGroup%2FEditGroup
```

A logged-in user passes straight through. Because `AccessDeniedPath` also points to the login page, a user who is logged in but lacks a required role would be sent there too (roles are not used yet).

---

## 3. Which action is protected

`GroupController` is `[Authorize]` as a whole. These actions opt out:

| Action | Verb | Access | Why |
|---|---|---|---|
| `ListOfGroups` | GET | **Public** | Browsing the list |
| `SearchGroup` | POST | **Public** | Searching the list |
| `ViewGroup` | GET | **Public** | Reading one group |
| `DeleteGroup` | POST | Login required | Changes data |
| `EditGroup` | GET | Login required | Edit form |
| `DeleteArtist`, `DeleteAlbum`, `AddArtist`, `AddAlbum`, `EditArtist`, `EditAlbum`, `Undo`, `Save` | POST | Login required | Edit form operations |
| `Error` | GET | Login required (inherited) | See limitation 5 |

`SeedController` is `[Authorize]` as a whole: all its actions need a login.

Putting `[Authorize]` on the class and opening the exceptions is the **safe default**: a new action added to `GroupController` later is protected unless someone deliberately marks it `[AllowAnonymous]`.

`AccountController` is deliberately **not** `[Authorize]`. It contains `Login`, `Register` and `ConfirmEmail`, which anonymous users must reach. Putting `[Authorize]` on it would make the login page itself redirect to the login page.

---

## 4. Verified behaviour

The solution builds with 0 errors (after adding the missing `using`, see below). The app was run and tested over HTTP:

| Request | Anonymous | Logged in |
|---|---|---|
| `GET /Group/ListOfGroups` | 200 | 200 |
| `GET /Group/EditGroup` | **302 → `/Account/Login?ReturnUrl=%2FGroup%2FEditGroup`** | 200 |
| `GET /Seed/Seed` | **302 → `/Account/Login?ReturnUrl=%2FSeed%2FSeed`** | 200 |
| `POST /Group/DeleteGroup` (valid antiforgery token) | **302 → `/Account/Login?ReturnUrl=%2FGroup%2FDeleteGroup`**. The delete is not executed | – |
| `POST /Seed/Seed` | **302 → login** | – |
| `GET /Home/DataSourceInfo`, `/Account/Login`, `/Account/Register` | 200 | 200 |

`GET /Group/ViewGroup?id=<unknown id>` returns 500 in both states. That is the existing behaviour for an id that does not exist, and is not related to authorization.

**Build fix applied while analysing this branch:** `GroupController` and `SeedController` use `[Authorize]` and `[AllowAnonymous]` but did not import their namespace, so the project did not compile. `using Microsoft.AspNetCore.Authorization;` was added to both [GroupController.cs](/Users/Martin/Development/projects/WebDevelopment_Dec_2026/GoodMusic_step_by_step/AppMvc/Controllers/GroupController.cs) and [SeedController.cs](/Users/Martin/Development/projects/WebDevelopment_Dec_2026/GoodMusic_step_by_step/AppMvc/Controllers/SeedController.cs).

---

## 5. Why MVC gets "Delete group" right

Delete is a separate action (`DeleteGroup`) on `GroupController`. `[Authorize]` is applied per action, so it is protected, while the list that contains the delete button (`ListOfGroups`) stays public. The protection follows the operation, not the page that shows its button.

---

## 6. Limitations to be aware of

1. **The buttons are still shown to anonymous visitors.** The list page renders **New**, **Edit** and the **Delete** confirmation dialog for everyone. Clicking one sends the visitor to the login page (or the POST is refused), so nothing is exposed, but the UI promises something that is not available. Hiding them depends on the login state, which is a later step.
2. **Anyone who logs in may edit and delete.** `[Authorize]` only checks "is logged in". There are no roles or ownership rules, so every registered user can change all groups.
3. **After login the user lands on `/`.** The `ReturnUrl` is in the address, but `LoginOk` ignores it, so the user must navigate back to the page they wanted.
4. **The pages ask the user to log in after the form was filled in.** A POST that is challenged is not replayed after login, so any typed-in data is lost.
5. **`Error()` in `GroupController` is behind the class-level `[Authorize]`.** An anonymous visitor who is sent to it would be redirected to the login page instead of seeing an error page.
6. **Authorization is attribute-only.** The services below the controller (`IMusicGroupsService` etc.) do not check who calls them. Anything that calls them from a non-protected entry point (for example a new controller action without an attribute) is unprotected.

---

## 7. Trying it

1. `dotnet run` in `AppMvc`. Log out.
2. Open *Famous groups*: the list, search and *View* work.
3. Click **Edit**, **New**, or open `/Seed/Seed` directly: you land on `/Account/Login?ReturnUrl=…`.
4. Log in, then repeat. The pages now open.

---

## 8. Next steps

Control access per user or per item (model-level authorization), and show or hide the buttons by login state.
