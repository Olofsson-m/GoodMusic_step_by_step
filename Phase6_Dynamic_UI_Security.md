# Phase 6: UI Authorization – Dynamic Element Visibility

## Purpose
Make the user interface show only what the current user may do. The server already refuses unauthorized requests (Phases 4 and 5). This phase hides the buttons and links that would lead to a refusal, using the **same** authorization handler.

## Prerequisite
Phase 5 (`18-sec-resource-authorization`) is in place: `CrudOperations` and `MusicGroupAuthorizationHandler` exist and are registered in both apps.

## What this phase delivers
- *New*, *Edit* and *Del* buttons on the list page are hidden unless the user is authorized.
- The edit icon on the view page is hidden unless the user is authorized.
- The layout of the list adapts (name column widens when there are no buttons).

Only two views per app change. No controller, page model or `Program.cs` change.

---

## The pattern (both apps)

### 1. Directives
At the top of `ListOfGroups` and `ViewGroup`:

```razor
@using Microsoft.AspNetCore.Authorization;
@using Microsoft.AspNetCore.Identity;
@using Models.Authorization;

@inject IAuthorizationService _authService;
```

(`Microsoft.AspNetCore.Identity` is not used by the code and can be left out.)

### 2. Ask once, at the top
`ViewGroup.cshtml`:

```razor
@{
    ViewData["Title"] = "View group";
    var authResult = await _authService.AuthorizeAsync(User, Model.MusicGroup, CrudOperations.Delete);
}
```

`ListOfGroups.cshtml`:

```razor
@{
    ViewData["Title"] = "List groups";
    var authResult = await _authService.AuthorizeAsync(User, Model.MusicGroups.FirstOrDefault(), CrudOperations.Delete);
    //Creating does not concern an existing group, so ask against an empty one. Works also when the list is empty
    var canCreate = (await _authService.AuthorizeAsync(User, new global::Models.MusicGroup(), CrudOperations.Create)).Succeeded;
}
```

Use `FirstOrDefault()`, not `First()` (as the original guide had it), because `First()` throws on an empty list.

**Pitfall: *New* must not depend on an existing group.** With an empty list (for example after a search with no matches), `FirstOrDefault()` returns `null`. The handler only runs for an `IMusicGroup` resource, so a check against `null` fails for everybody, and a logged-in user would lose the *New* button. That is why *New* is authorized with `CrudOperations.Create` against a new empty `MusicGroup`. `new global::Models.MusicGroup()` is written fully qualified because the name `MusicGroup` is not imported in the views. *Edit* and *Del* are only rendered for existing rows, so `authResult` is enough for them.

### 3. Use the answer
`@if (authResult.Succeeded) { … }` around whatever must be hidden, and a conditional CSS class for the layout.

---

## AppRazor

`Pages/ListOfGroups.cshtml`:

```razor
<div class="@(canCreate ? "col-md-10" : "col-md-12") themed-grid-head-col">Group Name</div>
@if (canCreate)
{
    <div class="col-md-2 themed-grid-head-col">
        <a asp-page="/Members/EditGroup" class="btn btn-success btn-sm m-1" type="button">New</a>
    </div>
}
…
@foreach (var item in Model.MusicGroups)
{
    <div class="@(authResult.Succeeded ? "col-md-10" : "col-md-12") themed-grid-col">
        <a asp-page="/ViewGroup" asp-route-id="@item.MusicGroupId">@item.Name</a>
    </div>
    @if (authResult.Succeeded)
    {
        <div class="col-md-2 themed-grid-col">
            <a asp-page="/Members/EditGroup" asp-route-id="@item.MusicGroupId" class="btn btn-secondary btn-sm m-1">Edit</a>
            <button type="button" class="btn btn-danger btn-sm m-1" data-bs-toggle="modal" data-bs-target="#delModal" …>Del</button>
        </div>
    }
}
```

`Pages/ViewGroup.cshtml`: the edit icon link in the heading:

```razor
@if (authResult.Succeeded)
{
    <a asp-page="/Members/EditGroup" asp-route-id="@Model.MusicGroup.MusicGroupId">
        <svg …><use xlink:href="#edit" /></svg>
    </a>
}
```

## AppMvc

Same structure, with `asp-controller="Group" asp-action="EditGroup"` links in `Views/Group/ListOfGroups.cshtml` and `Views/Group/ViewGroup.cshtml`.

The buttons in the code are the existing ones (*New*, *Edit*, *Del* that opens the delete dialog, and the edit icon). The original guide showed example buttons (`CreateGroup`, an inline Delete form) that do not exist in this project.

---

## Verified behaviour (both apps)

| Page | Anonymous | Logged in |
|---|---|---|
| List | No *New*, *Edit*, *Del*. 11 `col-md-12` cells | *New*, 10 *Edit*, 10 *Del*. 11 `col-md-10` cells |
| View one group | No edit icon | Edit icon |
| List after a search with no matches | No buttons | *New* only (no rows, so no *Edit* or *Del*) |

## Security note
Hiding a button is **not** protection. The server still refuses an anonymous `GET` of the edit page, and an anonymous delete `POST` (Phases 4 and 5). Test this: type the edit URL while logged out, and expect the login page.

## Known limitations of this phase
- **One answer for the whole list.** *Edit* and *Del* are decided once against the first group. It is correct only while the rule does not depend on the group's data.
- **`Delete` is the question for *Edit* and *Del*** (and for the edit icon on the view page). `CrudOperations.Edit` would be more precise.
- **The delete dialog markup** is still in the HTML for anonymous visitors, but cannot be opened.
- The rule is still "logged in = allowed". Roles and ownership are not implemented.

## Testing checklist
1. Logged out: no buttons on the list, no edit icon on a group.
2. Logged in: all buttons and the edit icon appear, and the grid keeps its layout.
3. Logged out, type an edit URL or post a delete: still refused.
4. Logged in, search for something that matches nothing: *New* is still shown.
