# AppRazor – Branch `19-apps-secure`

This branch makes the **user interface follow the user's permissions**. The server already refused unauthorized actions. Now the pages also stop *showing* the *New*, *Edit* and *Del* buttons and the edit icon to visitors who may not use them. Only two pages change: `ListOfGroups.cshtml` and `ViewGroup.cshtml`.

Everything below is compared with the previous branch (`18-sec-resource-authorization`).

---

## 1. What is new in this branch

| Area | Previous branch | This branch |
|---|---|---|
| `Pages/ListOfGroups.cshtml` | *New*, *Edit*, *Del* shown to everyone | Shown only if `AuthorizeAsync(..., Delete)` succeeds. Name column widens to the full row otherwise |
| `Pages/ViewGroup.cshtml` | Edit icon shown to everyone | Edit icon shown only if `AuthorizeAsync(..., Delete)` succeeds |
| Page models, `Program.cs`, handler | – | Unchanged |

The pages reuse the handler and `CrudOperations` from the previous branch. No new server code.

---

## 2. The pattern

Each page injects the authorization service directly into the `.cshtml`, asks one question at the top, and uses the answer:

```razor
@using Microsoft.AspNetCore.Authorization;
@using Models.Authorization;
@inject IAuthorizationService _authService;
@{
    var authResult = await _authService.AuthorizeAsync(User, <resource>, CrudOperations.Delete);
}
…
@if (authResult.Succeeded) { /* buttons */ }
```

`User` is the page's `ClaimsPrincipal`. `<resource>` is an `IMusicGroup`:

| Page | Resource passed |
|---|---|
| `ViewGroup` | `Model.MusicGroup` (the group being shown) |
| `ListOfGroups` | `Model.MusicGroups.FirstOrDefault()` (the first group on the page) for *Edit* and *Del*, and a new empty `MusicGroup` for *New* |

The service is injected into the view (`@inject`) and not into the page model. The page models are not touched. The handler is the one from the previous branch, so the UI and the server use **the same rule**. If the rule changes (for example to roles), the buttons follow automatically.

### 2.1 `ListOfGroups`

Two questions are asked at the top:

```razor
var authResult = await _authService.AuthorizeAsync(User, Model.MusicGroups.FirstOrDefault(), CrudOperations.Delete);
//Creating does not concern an existing group, so ask against an empty one. Works also when the list is empty
var canCreate = (await _authService.AuthorizeAsync(User, new global::Models.MusicGroup(), CrudOperations.Create)).Succeeded;
```

1. Header row: the *New* button and the width of the "Group Name" header (`col-md-10` with the button column, `col-md-12` without) follow `canCreate`.
2. Each row: the *Edit* and *Del* column, and the width of the name cell, follow `authResult`.

*New* is about creating a group, not about an existing one, so it is asked with the `Create` operation against a new empty `MusicGroup`. That keeps it working when the list has no rows (for example after a search with no matches). The rows only exist when there is at least one group, so `authResult` is only used when `FirstOrDefault()` is not null.

The grid keeps its shape in both states: 10+2 columns when logged in, one 12-column name when not.

### 2.2 `ViewGroup`

The edit icon link in the page heading is wrapped in `@if (authResult.Succeeded)`.

---

## 3. What this is and is not

Hiding a button is **presentation, not protection**. The protection is still on the server: the folder rule for `/Members` and the resource check in `OnPostDeleteGroupAsync`. A visitor who types `/Members/EditGroup` or posts the delete handler is still refused, whatever the page shows. The UI change only removes buttons that would lead to a refusal.

---

## 4. Verified behaviour

The solution builds with 0 errors. The app was run against the SQL Server container and the rendered HTML was inspected:

| Page | Anonymous | Logged in |
|---|---|---|
| `/ListOfGroups` | No *New*, no *Edit*, no *Del*. Eleven `col-md-12` cells | *New*, 10 *Edit*, 10 *Del*. Eleven `col-md-10` cells |
| `/ViewGroup?id=<id>` | No edit icon | Edit icon |
| `/ListOfGroups` after a search with **no matches** | No buttons | *New* only (no rows, so no *Edit* or *Del*) |

---

## 5. Limitations to be aware of

1. **One answer is used for every row.** The check is made once, against the first group, and applied to the whole page. This works because the rule does not look at the group's data. As soon as the rule depends on the group (owner, role), rows would need to be checked one by one.
2. **`Delete` is the question for *Edit* and *Del*.** *Edit* is shown because *Delete* is allowed. `CrudOperations.Edit` exists, and would be the accurate question for *Edit* (the view page also asks `Delete` for its edit icon).
3. **The delete dialog markup is still sent to anonymous visitors.** The modal and its form (with an antiforgery token) are in the HTML, but nothing can open it, because the *Del* buttons are not rendered.
4. **The rule is still "logged in = allowed"**, so the UI only distinguishes anonymous from logged in. Everything else in the earlier limitations (no roles, no ownership) is unchanged.

---

## 6. Trying it

1. `dotnet run` in `AppRazor`. Logged out, open *Famous groups* and a group: no buttons or edit icon.
2. Log in and reload: *New*, *Edit*, *Del* and the edit icon appear.
3. While logged in, search for a text that matches nothing: the *New* button stays, so a group can still be created.
4. Logged out, type `/Members/EditGroup`: you are sent to the login page, because the server still enforces the rule.

---

## 7. Next steps

Make *Edit* use `CrudOperations.Edit`, then add roles or ownership to the handler.
