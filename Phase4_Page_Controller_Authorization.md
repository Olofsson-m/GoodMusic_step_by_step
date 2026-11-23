# Phase 4: Page and Controller Level Authorization

## Purpose
Restrict who can open which page (Razor Pages) or run which action (MVC), using the login from Phase 3. After this phase an anonymous visitor can browse and read, but cannot reach the pages and actions that change data.

## Prerequisite
Phase 3 (`16-sec-authentication`) is in place. In particular `ConfigureApplicationCookie` sets `LoginPath = "/Account/Login"`. That setting is what turns a refused request into a redirect to the login page.

## What this phase delivers
- AppRazor: every page in `/Members` requires a login.
- AppMvc: `GroupController` and `SeedController` require a login, except three public actions in `GroupController`.

Not part of this phase: roles, per-item rules (model-level authorization), hiding buttons by login state, returning to the requested page after login.

## How it works
```
request → UseAuthentication  (reads cookie GoodMusicAuth → HttpContext.User)
        → UseAuthorization   (checks [Authorize] / [AllowAnonymous] / folder convention)
        → page handler / action
```

An anonymous request to a protected endpoint is answered with a 302 to `LoginPath`, with the original address in `ReturnUrl`:

```
GET /Members/EditGroup  →  302  /Account/Login?ReturnUrl=%2FMembers%2FEditGroup
```

`Program.cs` already contains `app.UseAuthentication()` and `app.UseAuthorization()` (see Phase 1).

---

## AppRazor

### 1. Authorize a folder
In `Program.cs`, replace `builder.Services.AddRazorPages();` with:

```csharp
builder.Services.AddRazorPages(option =>
{
    option.Conventions.AuthorizeFolder("/Members");
});
```

The convention adds an authorize filter to every page under `Pages/Members` (`EditGroup`, `Seed`, and any page added there later), including all of their handlers. No page file changes.

Pages outside `Members` (`ListOfGroups`, `ViewGroup`, `DataSourceInfo`, `Index`, `Account/*`) stay public.

### 2. Testing
Anonymous:
- `/ListOfGroups`, `/ViewGroup?id=…`, `/DataSourceInfo`, `/Account/Login`, `/Account/Register` → 200.
- `/Members/EditGroup` and `/Members/Seed` → 302 to `/Account/Login?ReturnUrl=…`.

Logged in:
- `/Members/EditGroup` and `/Members/Seed` → 200.

### 3. Important: "Delete group" is not protected
The delete handler (`OnPostDeleteGroupAsync`) lives on `/ListOfGroups`, which is public. Folder authorization works per page, so it cannot protect one handler on a public page.

Verified: an anonymous `POST /ListOfGroups?handler=DeleteGroup` with a valid antiforgery token (taken from the public page) is **not** refused and reaches the handler. In the test an id that does not exist was used, so the handler threw `Item … is not existing` and nothing was deleted. With a real id the group would be deleted.

This is closed in the next step, with an authorization check inside the page model (model-level authorization).

---

## AppMvc

### 1. Add the namespace
Both controllers need:

```csharp
using Microsoft.AspNetCore.Authorization;
```

Without it `[Authorize]` and `[AllowAnonymous]` do not compile.

### 2. `GroupController`: protect the class, open three actions

```csharp
[Authorize] // Apply authorization to the entire controller
public class GroupController : Controller
{
    [AllowAnonymous] // Allow anonymous access to view the list
    public async Task<IActionResult> ListOfGroups(int pagenr, string search) { … }

    [AllowAnonymous] // Allow anonymous access to search
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchGroup(ListOfGroups vm) { … }

    [AllowAnonymous] // Allow anonymous access to view individual groups
    public async Task<IActionResult> ViewGroup(Guid id) { … }

    // Every other action (DeleteGroup, EditGroup, Save, Undo, AddArtist, …) inherits [Authorize]
}
```

`[Authorize]` on the class protects every action. `[AllowAnonymous]` on an action overrides it. A new action is therefore protected by default.

### 3. `SeedController`: protect the whole class

```csharp
[Authorize]
public class SeedController : Controller { … }
```

### 4. Do **not** put `[Authorize]` on `AccountController`
`AccountController` holds `Login`, `LoginOk`, `Register`, `RegisterSave` and `ConfirmEmail`, which anonymous visitors must reach. With `[Authorize]` on the class, the login page would redirect to the login page. Leave it without an attribute (and `Logout` already needs a posted antiforgery token).

### 5. Testing
Anonymous:
- `/Group/ListOfGroups`, `/Home/DataSourceInfo`, `/Account/Login`, `/Account/Register` → 200.
- `/Group/EditGroup` and `/Seed/Seed` → 302 to `/Account/Login?ReturnUrl=…`.
- `POST /Group/DeleteGroup` with a valid token → 302 to login. The delete is **not** executed.
- `POST /Seed/Seed` → 302 to login.

Logged in:
- `/Group/EditGroup` and `/Seed/Seed` → 200.

### 6. Authorization advantage
Delete is a separate action, and `[Authorize]` applies per action. So "Delete group" is protected, even though the public list page contains its button.

---

## Authorization levels comparison

| | Razor Pages | MVC |
|---|---|---|
| Where it is set | `Program.cs` convention (`AuthorizeFolder`) | Attributes on controller and actions |
| Unit of protection | A page or a folder | An individual action |
| Public exception | Place the page outside the folder, or `AllowAnonymousToPage` | `[AllowAnonymous]` on the action |
| One page, several operations | Cannot separate them (all handlers share the page's rule) | Each operation is its own action |
| Delete group (verified) | **Not protected** | **Protected** |

---

## Testing checklist
1. Anonymous: public pages open, protected pages redirect to `/Account/Login?ReturnUrl=…`.
2. Logged in: protected pages open.
3. Logged out again: protected pages redirect again.
4. AppMvc: an anonymous `POST /Group/DeleteGroup` is redirected and nothing is deleted.
5. AppRazor: an anonymous `POST /ListOfGroups?handler=DeleteGroup` is still accepted (known gap, next step).

## Known limitations of this phase
- **AppRazor: delete is open to anonymous visitors** (see above).
- **Buttons are shown to everyone.** The list page shows *New*, *Edit* and the delete dialog to anonymous visitors in both apps.
- **Any logged-in user can edit and delete anything.** There are no roles or ownership rules.
- **`ReturnUrl` is ignored.** After login the user lands on `/`, not on the page they asked for. Data typed into a form is lost if the POST was challenged.
- **Folder protection depends on file placement** (AppRazor): a page placed outside `/Members` is public.
- **MVC `GroupController.Error()`** is behind the class-level `[Authorize]`.
- Authorization is only checked at the page or controller. The services below do not check the caller.
- Both MVC controllers needed `using Microsoft.AspNetCore.Authorization;` to compile.
