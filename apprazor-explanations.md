# AppRazor – Branch `18-sec-resource-authorization`

This branch adds **resource-level authorization** for deleting a music group, and by that closes the gap left open by folder-level authorization: the delete handler lives on the public `/ListOfGroups` page, so a page-level rule could not protect it. Now the handler itself asks an authorization service "may this user perform *Delete* on *this music group*?".

Everything below is compared with the previous branch (`17-sec-authorization`).

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| Shared `Models/Authorization` | `User` only | New `CrudOperations` and `MusicGroupAuthorizationHandler` (`MusicGroupAuth.cs`) |
| `Program.cs` | – | `AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>()` |
| `ListOfGroupsModel` constructor | `IMusicGroupsService`, `ILogger` | Also receives `IAuthorizationService` |
| `OnPostDeleteGroupAsync` | Deleted for **any visitor** | Loads the group and asks `AuthorizeAsync(User, group, CrudOperations.Delete)`. `Forbid()` if refused |
| Folder rule `/Members` | Login required | Unchanged |

The handler and the operations live in the shared `Models` project, so AppMvc uses the same code.

---

## 2. The building blocks

### 2.1 Operations
```csharp
public static class CrudOperations
{
    public static OperationAuthorizationRequirement Create = new() { Name = nameof(Create) };
    public static OperationAuthorizationRequirement Read   = new() { Name = nameof(Read) };
    public static OperationAuthorizationRequirement Edit   = new() { Name = nameof(Edit) };
    public static OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}
```
An `OperationAuthorizationRequirement` is just a named operation. It is the "what" of the question.

### 2.2 The handler
```csharp
public class MusicGroupAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, IMusicGroup>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement, IMusicGroup resource) { … }
}
```
It is only run when the resource passed to `AuthorizeAsync` is an `IMusicGroup`. The rules coded in it:

| User | Create | Read | Edit | Delete |
|---|---|---|---|---|
| Anonymous | no | **yes** | no | no |
| Logged in | yes | yes | yes | yes |

`context.Succeed(requirement)` grants. Doing nothing means "not granted". A handler never denies on its own, so with no `Succeed` the check fails.

### 2.3 Registration
```csharp
builder.Services.AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>();
```
`IAuthorizationService` collects all registered `IAuthorizationHandler`s and runs those that match the resource type. A singleton is right, because the handler has no state or dependencies.

---

## 3. The delete flow

```csharp
public async Task<IActionResult> OnPostDeleteGroupAsync(Guid groupId, int pagenr)
{
    //Model level CRUD authorization
    var mg = (await _service.ReadMusicGroupAsync(groupId, true)).Item;
    var result = await _autService.AuthorizeAsync(User, mg, CrudOperations.Delete);
    if (!result.Succeeded)
    {
        return Forbid();
    }

    await _service.DeleteMusicGroupAsync(groupId);
    …clamp page number, RedirectToPage("/ListOfGroups", …)
}
```

`User` is the page model's `ClaimsPrincipal`, built from the cookie. For an anonymous visitor it is an unauthenticated principal, so the handler's anonymous branch applies and `Delete` is not granted. `Forbid()` makes the cookie handler answer with a redirect to `AccessDeniedPath`, which is `/Account/Login`.

### Why this is needed here
The folder convention from the previous branch protects pages. All handlers on one page share that page's rule, and `/ListOfGroups` is public so that everybody can browse. The delete handler is on that page, so it needs its own check inside the page model. That check works per operation and per resource, so it fits exactly where the page-level rule cannot reach.

**Fix applied while analysing this branch:** the commit did not compile. `ListOfGroupsModel` used `_autService` and `CrudOperations` but had no field, no constructor parameter and no `using` lines for them. `Program.cs` was also missing `using Microsoft.AspNetCore.Authorization;`. All were added: `using Microsoft.AspNetCore.Authorization; using Models.Authorization;`, the `IAuthorizationService _autService` field and the constructor parameter.

---

## 4. Verified behaviour

The solution builds with 0 errors (after the fix above). A test group ("ZZ Auth Test") was created through the Edit form, then deleted. The app was run against the SQL Server container:

| Request | Result |
|---|---|
| Anonymous `POST /ListOfGroups?handler=DeleteGroup` (valid token from the public page, real id) | **302 → `/Account/Login?ReturnUrl=…`. The group still exists**. This was the open gap on the previous branch |
| Logged-in `POST /ListOfGroups?handler=DeleteGroup`, real id | 302 → `/ListOfGroups?pagenr=0`. The group is gone |
| Logged-in `POST /ListOfGroups?handler=DeleteGroup`, unknown id | **500** (`ReadMusicGroupAsync` throws for an id that does not exist) |

All test groups were deleted afterwards.

---

## 5. Limitations to be aware of

1. **Only the delete handler uses the check.** The handlers on `/Members/EditGroup` (`Save`, `AddArtist`, `DeleteAlbum`, …) rely on the folder rule alone. `Create`, `Read` and `Edit` exist in the handler but are not called.
2. **The rule is "logged in = allowed".** There are no roles, claims or ownership checks, so every registered user can delete every group. The `Forbid()` branch is not reachable by a logged-in user today.
3. **Unknown id gives a 500**, not a 404.
4. **One extra database read per delete**, to get the resource for the check.
5. **The handler is only registered in `Program.cs`.** If it is removed, `AuthorizeAsync` fails for every user, so delete is refused for everyone (safe by default).
6. **UI still shows buttons to everyone.** The list page shows *New*, *Edit* and the delete dialog to anonymous visitors. The server refuses the action, but the buttons are not hidden.
7. **`Forbid()` redirects to the login page**, even for a logged-in user, with no "not allowed" message.
8. **Protection of other handlers on public pages still depends on remembering to add a check.** A new handler on a page outside `/Members` is open until it gets one.

---

## 6. Trying it

1. `dotnet run` in `AppRazor`, log in, and delete a group on the list page: it disappears.
2. Log out and try the same: the delete is refused and the browser is sent to the login page.
3. To see the fine gate say no, change the handler so that `Delete` is only allowed for a specific user or role, then delete as another user: `Forbid()` is returned.

---

## 7. Next steps

Show or hide the buttons depending on what the user may do (the same `AuthorizeAsync` can be called from the page), and move to roles or ownership in the handler.
