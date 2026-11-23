# Phase 5: Resource (Model-Level) Authorization

## Purpose
Decide per **operation** and per **resource** whether a user may act, not just whether the user is logged in. In this phase the question is "may this user *Delete* this music group?", and the answer comes from a dedicated handler.

## Prerequisite
Phase 4 (`17-sec-authorization`) is in place: folder-level authorization in AppRazor, and `[Authorize]` / `[AllowAnonymous]` in AppMvc.

## What this phase delivers
- `CrudOperations` and `MusicGroupAuthorizationHandler` in `Models/Authorization` (shared by both apps).
- The handler registered in both apps.
- Delete group checked through `IAuthorizationService` in both apps. In AppRazor this **closes the gap** that Phase 4 left open.

Not part of this phase: roles or ownership rules, hiding buttons by permission, using the check for create and edit.

---

## Common

### 1. Namespaces
Wherever the types below are used, import:

```csharp
using Microsoft.AspNetCore.Authorization;   // IAuthorizationService, IAuthorizationHandler
using Models.Authorization;                 // CrudOperations, MusicGroupAuthorizationHandler
```

Both `Program.cs` files, `ListOfGroups.cshtml.cs` and `GroupController.cs` need them. Without them the projects do not compile.

### 2. `CrudOperations` (`Models/Authorization/MusicGroupAuth.cs`)

```csharp
public static class CrudOperations
{
    public static OperationAuthorizationRequirement Create = new() { Name = nameof(Create) };
    public static OperationAuthorizationRequirement Read   = new() { Name = nameof(Read) };
    public static OperationAuthorizationRequirement Edit   = new() { Name = nameof(Edit) };
    public static OperationAuthorizationRequirement Delete = new() { Name = nameof(Delete) };
}
```

The operation is named `Edit` in the code (the original guide called it `Update`).

### 3. `MusicGroupAuthorizationHandler`

```csharp
public class MusicGroupAuthorizationHandler
    : AuthorizationHandler<OperationAuthorizationRequirement, IMusicGroup>
{
    protected override Task HandleRequirementAsync(AuthorizationHandlerContext context,
        OperationAuthorizationRequirement requirement, IMusicGroup resource)
    {
        //Non authenticated users can only read
        if (!context.User.Identity.IsAuthenticated)
        {
            if (requirement.Name == CrudOperations.Read.Name) context.Succeed(requirement);
            return Task.CompletedTask;
        }

        //Authenticated users can create, read, edit, delete
        if (requirement.Name == CrudOperations.Create.Name) context.Succeed(requirement);
        if (requirement.Name == CrudOperations.Read.Name)   context.Succeed(requirement);
        if (requirement.Name == CrudOperations.Edit.Name)   context.Succeed(requirement);
        if (requirement.Name == CrudOperations.Delete.Name) context.Succeed(requirement);
        return Task.CompletedTask;
    }
}
```

The resource type is the interface `IMusicGroup`, so the handler is run for any implementation. The original guide used the concrete `MusicGroup`.

| User | Create | Read | Edit | Delete |
|---|---|---|---|---|
| Anonymous | no | yes | no | no |
| Logged in | yes | yes | yes | yes |

A handler only grants (`Succeed`). If it does nothing, the check fails. Nothing here calls `Fail()`.

### 4. Register the handler (both `Program.cs`)

```csharp
//Model Authorization
builder.Services.AddSingleton<IAuthorizationHandler, MusicGroupAuthorizationHandler>();
```

---

## AppRazor

### 5. Inject `IAuthorizationService`
In `Pages/ListOfGroups.cshtml.cs`:

```csharp
readonly IAuthorizationService _autService = null;

public ListOfGroupsModel(IMusicGroupsService service, ILogger<ListOfGroupsModel> logger,
                         IAuthorizationService autService)
{
    _service = service;
    _logger = logger;
    _autService = autService;
}
```

### 6. Check in `OnPostDeleteGroupAsync`

```csharp
//Model level CRUD authorization
var mg = (await _service.ReadMusicGroupAsync(groupId, true)).Item;
var result = await _autService.AuthorizeAsync(User, mg, CrudOperations.Delete);
if (!result.Succeeded)
{
    return Forbid();
}

await _service.DeleteMusicGroupAsync(groupId);
```

The resource (`mg`) must be loaded before the check, because a resource-based check needs the resource. `Forbid()` redirects to `AccessDeniedPath`, which is `/Account/Login`.

### 7. Testing
- Anonymous `POST /ListOfGroups?handler=DeleteGroup` with a valid token and a real id → 302 to `/Account/Login?ReturnUrl=…`. **The group still exists.** (In Phase 4 this request was accepted.)
- Logged in → the group is deleted and the list is shown again.

---

## AppMvc

### 5. Inject `IAuthorizationService`
In `Controllers/GroupController.cs`:

```csharp
readonly IAuthorizationService _autService = null;

public GroupController(IMusicGroupsService mg_service, IAlbumsService alb_service,
    IArtistsService art_service, ILogger<GroupController> logger, IAuthorizationService autService)
{ …  _autService = autService; }
```

### 6. Check in `DeleteGroup`

```csharp
var mg = (await _mg_service.ReadMusicGroupAsync(groupId, true)).Item;
var result = await _autService.AuthorizeAsync(User, mg, CrudOperations.Delete);
if (!result.Succeeded)
{
    return Forbid();
}

await _mg_service.DeleteMusicGroupAsync(groupId);   // must stay after the check

…count what is left, clamp the page number, RedirectToAction(ListOfGroups, …)
```

**Pitfall:** in the commit this branch started from, the delete call was replaced by the authorization lines and not put back. *Delete* then redirected to the list but deleted nothing. Keep the `DeleteMusicGroupAsync` line after the check.

### 7. Testing
- Anonymous `POST /Group/DeleteGroup` → 302 to login (stopped by `[Authorize]` on the class). The group still exists.
- Logged in → the group is deleted.

---

## Two layers, compared

| | Page / controller authorization (Phase 4) | Resource authorization (Phase 5) |
|---|---|---|
| Question | May this user reach this page or action? | May this user do this operation on this resource? |
| Declared with | `[Authorize]`, `AuthorizeFolder` | `IAuthorizationService.AuthorizeAsync(user, resource, operation)` and a handler |
| Knows the resource | No | Yes |
| Covers one handler on a public page | No | Yes |
| Where the rule lives | Attributes and conventions at each endpoint | One handler class, shared by both apps |

Both layers stay: the first is a cheap gate in front of everything, the second is the precise rule.

---

## Verified behaviour (both apps)

| Request | AppMvc | AppRazor |
|---|---|---|
| Anonymous delete (valid token, real id) | 302 to login, group kept | 302 to login, group kept |
| Logged-in delete, real id | group deleted | group deleted |
| Logged-in delete, unknown id | 500 | 500 |

Test groups were created through the Edit form and deleted. None remained.

## Known limitations of this phase
- Only *Delete* is checked. `Create`, `Read` and `Edit` are defined and handled but not called anywhere. The edit actions and handlers rely on Phase 4 authorization alone.
- The rule is "logged in = allowed". The `Forbid()` branch cannot be reached by a logged-in user yet.
- An unknown id gives a 500, because `ReadMusicGroupAsync` throws.
- One extra database read per delete.
- Buttons (*New*, *Edit*, delete dialog) are still shown to anonymous visitors.
- `Forbid()` sends the user to the login page, with no "not allowed" message.
- The branch as committed needed several fixes to compile and to delete (namespaces, the AppRazor injection, and the restored delete call in MVC).
