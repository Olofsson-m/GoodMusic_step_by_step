# AppMvc – Branch `18-sec-resource-authorization`

This branch adds **resource-level authorization** for deleting a music group. Beyond "is the user logged in?", the controller now asks an authorization service "may this user perform *Delete* on *this music group*?", and the answer comes from a dedicated handler. Today the handler's rule is simple (logged-in users may do everything), but the structure allows rules based on roles, claims or the group's own properties.

Everything below is compared with the previous branch (`17-sec-authorization`).

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| Shared `Models/Authorization` | `User` only | New `CrudOperations` and `MusicGroupAuthorizationHandler` (`MusicGroupAuth.cs`) |
| `Program.cs` | – | `AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>()` |
| `GroupController` constructor | 4 services | Also receives `IAuthorizationService` |
| `DeleteGroup` | Class-level `[Authorize]`, then delete | Also loads the group and asks `AuthorizeAsync(User, group, CrudOperations.Delete)`. `Forbid()` if refused |

The handler and the operations live in the shared `Models` project, so AppRazor uses the same code.

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
The generic arguments say what it handles: an operation requirement on an `IMusicGroup` resource. It is only run when the resource passed to `AuthorizeAsync` is an `IMusicGroup`. The rules coded in it:

| User | Create | Read | Edit | Delete |
|---|---|---|---|---|
| Anonymous | no | **yes** | no | no |
| Logged in | yes | yes | yes | yes |

`context.Succeed(requirement)` grants. Doing nothing means "not granted". A handler never denies on its own, so with no `Succeed` the check fails.

### 2.3 Registration
```csharp
builder.Services.AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>();
```
`IAuthorizationService` collects all registered `IAuthorizationHandler`s and runs those that match the resource type. A singleton is right here, because the handler has no state or dependencies.

---

## 3. The delete flow

```
POST /Group/DeleteGroup   (class-level [Authorize] runs first)
  1. var mg = (await _mg_service.ReadMusicGroupAsync(groupId, true)).Item;
  2. var result = await _autService.AuthorizeAsync(User, mg, CrudOperations.Delete);
  3. if (!result.Succeeded) return Forbid();
  4. await _mg_service.DeleteMusicGroupAsync(groupId);
  5. clamp page number, RedirectToAction(ListOfGroups, { pagenr, search })
```

Why load the group first? A resource-based check needs the resource itself, so rules can depend on its data (for example "only the creator may delete"). The handler here does not use the group's data yet, but the call shape is ready for it.

`Forbid()` makes the cookie handler answer with a redirect to `AccessDeniedPath`, which is `/Account/Login`.

### Two layers
1. `[Authorize]` on `GroupController` is the **coarse gate**: no login, no action.
2. `AuthorizeAsync` is the **fine gate**: login is not enough, the handler must also allow *this operation on this resource*.

Today layer 2 never says no to a logged-in user, so the visible behaviour is identical to the previous branch. The difference is where the rule lives.

**Fix applied while analysing this branch:** the commit replaced the line `await _mg_service.DeleteMusicGroupAsync(groupId);` with the authorization check but did not put the delete call back, so *Delete* redirected to the list and deleted nothing (verified: a created test group was still there after 5 delete requests). The call was restored after the `Forbid()` check. Also, `Program.cs` was missing `using Microsoft.AspNetCore.Authorization;` (the project did not compile), which was added.

---

## 4. Verified behaviour

The solution builds with 0 errors (after the `using` fix). Test groups ("ZZ Auth Test") were created through the Edit form, then deleted. The app was run against the SQL Server container:

| Request | Result |
|---|---|
| Anonymous `POST /Group/DeleteGroup` (valid token, real id) | 302 → `/Account/Login?ReturnUrl=…`. The group still exists |
| Logged-in `POST /Group/DeleteGroup`, real id | 302 → `/Group/ListOfGroups?pagenr=0`. The group is gone (5 of 5 test groups deleted, 0 left) |
| Logged-in `POST /Group/DeleteGroup`, unknown id | **500** (`ReadMusicGroupAsync` throws for an id that does not exist) |

Anonymous requests are stopped by the class-level `[Authorize]` before `AuthorizeAsync` is reached, so the handler's anonymous branch was not exercised here. The `Forbid()` branch is not reachable today: a logged-in user always succeeds.

---

## 5. Limitations to be aware of

1. **Only `DeleteGroup` uses the handler.** `EditGroup`, `Save`, `AddArtist`, `AddAlbum` and the other edit actions rely on `[Authorize]` alone. `Create`, `Read` and `Edit` exist in `CrudOperations` and in the handler but are not called.
2. **The rule is "logged in = allowed".** There are no roles, claims or ownership checks, so every registered user can delete every group.
3. **Unknown id gives a 500**, not a 404. The service throws, and the controller does not catch it.
4. **The group is read before it is deleted**, which is one extra database call per delete. It is the price of resource-based authorization.
5. **The handler is only registered in `Program.cs`.** If it is removed, `AuthorizeAsync` fails for every user, and delete is refused for everyone (safe by default).
6. **UI still shows buttons to everyone.** The list page shows *New*, *Edit* and the delete dialog to anonymous visitors. The server refuses the action, but the buttons are not hidden.
7. **`Forbid()` redirects to the login page**, even for a logged-in user. The user sees the login form, with no "not allowed" message.

---

## 6. Trying it

1. `dotnet run` in `AppMvc`, log in, and delete a group on the list page: it disappears.
2. Log out and try the same: the page asks for a login (the delete dialog is shown, but the request is refused).
3. To see the fine gate say no, change the handler so that `Delete` is only allowed for a specific user or role, then delete as another user: `Forbid()` is returned.

---

## 7. Next steps

Show or hide buttons by what the user may do (the same `AuthorizeAsync` can be called from the views), and move to roles or ownership in the handler.
