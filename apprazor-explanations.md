# AppRazor – Branch `17-sec-authorization`

This branch adds **authorization at page level**. Until now the app knew who the user was (login), but any visitor could open every page. Now every page in the `/Members` folder requires a logged-in user.

Everything below is compared with the previous branch (`16-sec-authentication`).

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| `Program.cs` | `AddRazorPages()` | `AddRazorPages(option => option.Conventions.AuthorizeFolder("/Members"))` |
| Pages in `/Members` (`EditGroup`, `Seed`) | Open to anyone | Login required |
| Pages outside `/Members` (`ListOfGroups`, `ViewGroup`, `DataSourceInfo`, `Index`, `Account/*`) | Open | Unchanged, still open |
| Page models, views, layout | – | Unchanged |

The whole change is one convention in `Program.cs`. No page file was edited.

---

## 2. The folder convention

```csharp
builder.Services.AddRazorPages(option =>
{
    option.Conventions.AuthorizeFolder("/Members");
});
```

This is a Razor Pages **convention**: at startup it applies an authorize filter to every page found under `/Pages/Members`, including pages added there later. It has the same effect as putting `[Authorize]` on each page model, without touching them.

Other conventions that work the same way (not used here): `AuthorizePage("/Some/Page")`, `AllowAnonymousToPage(...)`, `AllowAnonymousToFolder(...)`.

If an anonymous user requests a protected page, the cookie handler **challenges**: it answers with a 302 to the `LoginPath` configured in the previous branch, with the original address in `ReturnUrl`:

```
GET /Members/EditGroup  →  302  /Account/Login?ReturnUrl=%2FMembers%2FEditGroup
```

The folder is the unit of protection. That is why `EditGroup` and `Seed` live under `/Members`, and why `ListOfGroups` and `ViewGroup` stay in the root: they are meant to be public.

---

## 2.1 What is protected

| Page | Path | Access |
|---|---|---|
| Group list, search, delete handler | `/ListOfGroups` | **Public** (see limitation 1) |
| View one group | `/ViewGroup` | **Public** |
| Edit / new group, with all its handlers (`DeleteArtist`, `AddAlbum`, `Save`, `Undo`, …) | `/Members/EditGroup` | Login required |
| Seed | `/Members/Seed` | Login required |
| Data overview, Home, Login, Register, ConfirmEmail | `/…` | Public |

All handlers of a protected page (`OnGet…` and `OnPost…`) are protected by the same filter, because the check runs before the handler is selected.

---

## 3. Verified behaviour

The solution builds with 0 errors (after the `using` fix in AppMvc, which does not affect this app). The app was run and tested over HTTP:

| Request | Anonymous | Logged in |
|---|---|---|
| `GET /ListOfGroups` | 200 | 200 |
| `GET /Members/EditGroup` | **302 → `/Account/Login?ReturnUrl=%2FMembers%2FEditGroup`** | 200 |
| `GET /Members/Seed` | **302 → `/Account/Login?ReturnUrl=%2FMembers%2FSeed`** | 200 |
| `POST /Members/Seed` (valid token) | **302 → login** | – |
| `GET /DataSourceInfo`, `/Account/Login`, `/Account/Register` | 200 | 200 |
| `POST /ListOfGroups?handler=DeleteGroup` (valid token) | **Not refused.** The request reaches the handler (see below) | – |

For the last row, the test used an id that does not exist, so no data was deleted. The handler was run and then threw `Item 00000000-0000-0000-0000-000000000001 is not existing` (500, in the application log at `OnPostDeleteGroupAsync`). With a real id the group would have been deleted by an anonymous visitor.

`GET /ViewGroup?id=<unknown id>` also returns 500 in both states. That is the existing behaviour for an id that does not exist, and is not related to authorization.

---

## 4. Limitations to be aware of

1. **"Delete group" is not protected.** The delete handler `OnPostDeleteGroupAsync` is on `/ListOfGroups`, which is public, so folder authorization does not cover it. Any visitor can delete a group by posting to `/ListOfGroups?handler=DeleteGroup` with a valid antiforgery token (the token is on the public page). Page-level authorization cannot separate one handler from another on the same page. It needs a check inside the page model (model-level authorization), or the delete must move to a protected page. This is the main difference from AppMvc, where delete is its own action.
2. **The buttons are still shown to anonymous visitors.** The list page renders **New**, **Edit** and the **Delete** dialog for everyone. **New** and **Edit** lead to the login page. **Delete** actually works (limitation 1).
3. **Anyone who logs in may edit and delete.** The folder rule only checks "is logged in". There are no roles or ownership rules.
4. **After login the user lands on `/`.** The `ReturnUrl` is in the address, but the login page ignores it.
5. **Folder-based protection depends on file placement.** A new page added outside `/Members` is public unless it is explicitly authorized. Moving a page between folders changes who can open it.
6. **Authorization is on the page only.** The services below the pages (`IMusicGroupsService` etc.) do not check who calls them.

---

## 5. Trying it

1. `dotnet run` in `AppRazor`. Log out.
2. Open *Famous groups*: the list, search and *View* work.
3. Click **Edit**, **New**, or open `/Members/Seed` directly: you land on `/Account/Login?ReturnUrl=…`.
4. Log in, then repeat. The pages now open.

---

## 6. Next steps

Close the gap in `OnPostDeleteGroupAsync` with a check inside the page model, then control access per user or per item (model-level authorization), and show or hide the buttons by login state.
