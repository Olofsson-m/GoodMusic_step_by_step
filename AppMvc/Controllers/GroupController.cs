using System.Diagnostics;
using AppMvc.ViewModels;
using AppMvc.SeidoHelpers;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

using Models.DTO;
using Models.Interfaces;
using Services;
using Models.Authorization;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AppMvc.Controllers;

[Authorize] // Apply authorization to the entire controller
public class GroupController : Controller
{
    //Just like for WebApi
    readonly IMusicGroupsService _mg_service = null;
    readonly IAlbumsService _alb_service = null;
    readonly IArtistsService _art_service = null;
    readonly ILogger<GroupController> _logger = null;
    readonly IAuthorizationService _autService = null;

    //Inject services just like in WebApi
    public GroupController(IMusicGroupsService mg_service, IAlbumsService alb_service, IArtistsService art_service, 
    ILogger<GroupController> logger, IAuthorizationService autService)
    {
        _mg_service = mg_service;
        _alb_service = alb_service;
        _art_service = art_service;
        _logger = logger;
        _autService = autService;
    }

    #region ListOfGroups handling
    [AllowAnonymous] // Allow anonymous access to view the list
    public async Task<IActionResult> ListOfGroups(int pagenr, string search)
    {
        //Create the viewModel
        var vm = new ListOfGroups() { ThisPageNr = pagenr, SearchFilter = search };

        //Use the Service
        var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, vm.ThisPageNr, vm.PageSize);
        vm.MusicGroups = _resp.PageItems;
        vm.NrOfGroups = _resp.DbItemsCount;

        //Pagination
        vm.UpdatePagination(_resp.DbItemsCount);

        //Render the View
        return View(vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteGroup(Guid groupId, int pagenr, ListOfGroups vm)
    {
        //Use the Service
        var mg = (await _mg_service.ReadMusicGroupAsync(groupId, true)).Item;
        var result = await _autService.AuthorizeAsync(User, mg, CrudOperations.Delete);
        if (!result.Succeeded)
        {
            return Forbid();
        }

        await _mg_service.DeleteMusicGroupAsync(groupId);

        //Count what is left, so we never redirect to a page that no longer exists
        //(e.g. after deleting the only item on the last page)
        var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, 0, 1);
        int lastPageNr = Math.Max(0, (int)Math.Ceiling((double)_resp.DbItemsCount / vm.PageSize) - 1);
        pagenr = Math.Clamp(pagenr, 0, lastPageNr);

        //Post/Redirect/Get: a refresh of the browser will not post the delete again,
        //and the user stays on the same page of the (filtered) list
        return RedirectToAction(nameof(ListOfGroups), new { pagenr, search = vm.SearchFilter });
    }

    [AllowAnonymous] // Allow anonymous access to search
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SearchGroup(ListOfGroups vm)
    {
        //Use the Service
        var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, vm.SearchFilter, vm.ThisPageNr, vm.PageSize);
        vm.MusicGroups = _resp.PageItems;
        vm.NrOfGroups = _resp.DbItemsCount;

        //Pagination
        vm.UpdatePagination(_resp.DbItemsCount);

        //Render the View
        return View("ListOfGroups", vm);
    }
    #endregion

    #region ViewGroup handling
    [AllowAnonymous] // Allow anonymous access to view individual groups
    public async Task<IActionResult> ViewGroup(Guid id) 
    {
        Guid _groupId = id;
        //Guid _groupId = Guid.Parse(Request.Query["id"]);   //an alternative

        //use the Service
        var mg = await _mg_service.ReadMusicGroupAsync(_groupId, false);

        //Create the viewModel
        var vm = new ViewGroup() { MusicGroup = mg.Item };

        //Render the View
        return View(vm);
    }
    #endregion

    #region EditGroup Handling
    public async Task<IActionResult> EditGroup(Guid? id)
    {
        Guid? _groupId = id;
        if (_groupId.HasValue)
        {
            //Read a music group from 
            var mg = await _mg_service.ReadMusicGroupAsync(_groupId.Value, false);

            //Populate the InputModel from the music group
            //Create the viewModel
            var vm = new EditGroup()
            {
                MusicGroupInput = new EditGroup.MusicGroupIM(mg.Item),
                PageHeader = "Edit details of a music group"
            };


            return View(vm);
        }
        else
        {
            //Create an empty music group
            //Create the viewModel
            var vm = new EditGroup()
            {
                MusicGroupInput = new EditGroup.MusicGroupIM(),
                PageHeader = "Create a new a music group"
            };
            vm.MusicGroupInput.StatusIM = ViewModels.EditGroup.StatusIM.Inserted;
            vm.MusicGroupInput.Genre = null;

            return View(vm);
        }


    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteArtist(Guid artistId, EditGroup vm)
    {
        //Set the Artist as deleted, it will not be rendered
        vm.MusicGroupInput.Artists.First(a => a.ArtistId == artistId).StatusIM = ViewModels.EditGroup.StatusIM.Deleted;

        return View("EditGroup",vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult DeleteAlbum(Guid albumId, EditGroup vm)
    {
        //Set the Album as deleted, it will not be rendered
        vm.MusicGroupInput.Albums.First(a => a.AlbumId == albumId).StatusIM = ViewModels.EditGroup.StatusIM.Deleted;

        return View("EditGroup", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddArtist(EditGroup vm)
    {
        string[] keys = { "MusicGroupInput.NewArtist.FirstName",
                            "MusicGroupInput.NewArtist.LastName"};

        if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
        {
            vm.ValidationResult = validationResult;
            return View("EditGroup", vm);
        }

        //Set the Artist as Inserted, it will later be inserted in the database
        vm.MusicGroupInput.NewArtist.StatusIM = ViewModels.EditGroup.StatusIM.Inserted;

        //Need to add a temp Guid so it can be deleted and editited in the form
        //A correct Guid will be created by the DTO when Inserted into the database
        vm.MusicGroupInput.NewArtist.ArtistId = Guid.NewGuid();

        //Add it to the Input Models artists
        vm.MusicGroupInput.Artists.Add(new EditGroup.ArtistIM(vm.MusicGroupInput.NewArtist));

        //Clear the NewArtist so another album can be added
        vm.MusicGroupInput.NewArtist = new EditGroup.ArtistIM();

        return View("EditGroup", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult AddAlbum(EditGroup vm)
    {
        string[] keys = { "MusicGroupInput.NewAlbum.ReleaseYear",
                            "MusicGroupInput.NewAlbum.AlbumName"};

        if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
        {
            vm.ValidationResult = validationResult;
            return View("EditGroup", vm);
        }

        //Set the Album as Inserted, it will later be inserted in the database
        vm.MusicGroupInput.NewAlbum.StatusIM = ViewModels.EditGroup.StatusIM.Inserted;

        //Need to add a temp Guid so it can be deleted and editited in the form
        //A correct Guid will be created by the DTO when Inserted into the database
        vm.MusicGroupInput.NewAlbum.AlbumId = Guid.NewGuid();

        //Add it to the Input Models albums
        vm.MusicGroupInput.Albums.Add(new EditGroup.AlbumIM(vm.MusicGroupInput.NewAlbum));

        //Clear the NewAlbum so another album can be added
        vm.MusicGroupInput.NewAlbum = new EditGroup.AlbumIM();

        return View("EditGroup", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditArtist(Guid artistId, EditGroup vm)
    {
        int idx = vm.MusicGroupInput.Artists.FindIndex(a => a.ArtistId == artistId);
        string[] keys = { $"MusicGroupInput.Artists[{idx}].editFirstName",
                        $"MusicGroupInput.Artists[{idx}].editLastName"};

        if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
        {
            vm.ValidationResult = validationResult;
            return View("EditGroup", vm);
        }

        //Set the Album as Modified, it will later be updated in the database
        var a = vm.MusicGroupInput.Artists.First(a => a.ArtistId == artistId);
        if (a.StatusIM != ViewModels.EditGroup.StatusIM.Inserted)
        {
            a.StatusIM = ViewModels.EditGroup.StatusIM.Modified;
        }

        //Implement the changes
        a.FirstName = a.editFirstName;
        a.LastName = a.editLastName;

        return View("EditGroup", vm);
    }
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult EditAlbum(Guid albumId, EditGroup vm)
    {
        int idx = vm.MusicGroupInput.Albums.FindIndex(a => a.AlbumId == albumId);
        string[] keys = { $"MusicGroupInput.Albums[{idx}].editAlbumName",
                        $"MusicGroupInput.Albums[{idx}].editReleaseYear"};

        if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
        {
            vm.ValidationResult = validationResult;
            return View("EditGroup", vm);
        }

        //Set the Album as Modified, it will later be updated in the database
        var a = vm.MusicGroupInput.Albums.First(a => a.AlbumId == albumId);
        if (a.StatusIM != ViewModels.EditGroup.StatusIM.Inserted)
        {
            a.StatusIM = ViewModels.EditGroup.StatusIM.Modified;
        }

        //Implement the changes
        a.AlbumName = a.editAlbumName;
        a.ReleaseYear = a.editReleaseYear;

        return View("EditGroup", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Undo(EditGroup vm)
    {
        //Reload Music group from Database
        var mg = await _mg_service.ReadMusicGroupAsync(vm.MusicGroupInput.MusicGroupId, false);

        //Repopulate the InputModel
        vm.MusicGroupInput = new EditGroup.MusicGroupIM(mg.Item);

        //Clear ModelState to ensure the page displays the updated values
        ModelState.Clear();
        
        return View("EditGroup", vm);
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(EditGroup vm)
    {
        string[] keys = { "MusicGroupInput.Name",
                            "MusicGroupInput.EstablishedYear",
                            "MusicGroupInput.Genre"};

        if (!ModelState.IsValidPartially(out ModelValidationResult validationResult, keys))
        {
            vm.ValidationResult = validationResult;
            return View("EditGroup", vm);
        }

        //This is where the music plays
        //First, are we creating a new Music group or editing another
        if (vm.MusicGroupInput.StatusIM == ViewModels.EditGroup.StatusIM.Inserted)
        {
            var newMg = await _mg_service.CreateMusicGroupAsync(vm.MusicGroupInput.CreateCUdto());
            //get the newly created MusicGroupId
            vm.MusicGroupInput.MusicGroupId = newMg.Item.MusicGroupId;
        }

        //Do all updates for Albums
        await SaveAlbums(vm);

        // Do all updates for Artists
        var mg = await SaveArtists(vm);

        //Finally, update the MusicGroup itself
        mg = vm.MusicGroupInput.UpdateModel(mg);
        await _mg_service.UpdateMusicGroupAsync(new MusicGroupCUdto(mg));

        if (vm.MusicGroupInput.StatusIM == ViewModels.EditGroup.StatusIM.Inserted)
        {
            return RedirectToAction("ListOfGroups");
        }

        return RedirectToAction("ViewGroup", new { id = vm.MusicGroupInput.MusicGroupId });
    }
    #endregion

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Error { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    #region ViewModel InputModel Albums and Artists saved to database
    private async Task<IMusicGroup> SaveAlbums(EditGroup vm)
    {
        //Check if there are deleted albums, if so simply remove them
        var deletedAlbums = vm.MusicGroupInput.Albums.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Deleted));
        foreach (var item in deletedAlbums)
        {
            //Remove from the database
            await _alb_service.DeleteAlbumAsync(item.AlbumId);
        }

        //Note that now the deleted albums will be removed and I can focus on Album creation
        await _mg_service.ReadMusicGroupAsync(vm.MusicGroupInput.MusicGroupId, false);

        //Check if there are any new albums added, if so create them in the database
        var newAlbums = vm.MusicGroupInput.Albums.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Inserted));
        foreach (var item in newAlbums)
        {
            //Create the corresposning model and CUdto objects
            var cuDto = item.CreateCUdto();

            //Set the relationships of a newly created item and write to database
            cuDto.MusicGroupId = vm.MusicGroupInput.MusicGroupId;
            await _alb_service.CreateAlbumAsync(cuDto);
        }

        //Note that now the deleted albums will be removed and created albums added. I can focus on Album update
        var mg = await _mg_service.ReadMusicGroupAsync(vm.MusicGroupInput.MusicGroupId, false);


        //Check if there are any modified albums , if so update them in the database
        var modifiedAlbums = vm.MusicGroupInput.Albums.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Modified));
        foreach (var item in modifiedAlbums)
        {
            var model = mg.Item.Albums.First(a => a.AlbumId == item.AlbumId);

            //Update the model from the InputModel
            model = item.UpdateModel(model);

            //Updatet the model in the database
            model.MusicGroup = mg.Item;      //ensure that MusicGroupId ca be set, Album must belong to a music group
            await _alb_service.UpdateAlbumAsync(new AlbumCUdto(model));
        }

        return mg.Item;
    }
    private async Task<IMusicGroup> SaveArtists(EditGroup vm)
    {
        //Check if there are deleted artists, if so simply remove them
        var deletedArtists = vm.MusicGroupInput.Artists.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Deleted));
        foreach (var item in deletedArtists)
        {
            //Remove from the database
            await _art_service.DeleteArtistAsync(item.ArtistId);
        }

        //Check if there are any new artist added, if so create them in the database
        var newArtists = vm.MusicGroupInput.Artists.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Inserted));
        foreach (var item in newArtists)
        {
            //Create the corresposning model and CUdto objects
            var cuDto = item.CreateCUdto();

            //Set the relationships of a newly created item and write to database
            cuDto.MusicGroupsId = [vm.MusicGroupInput.MusicGroupId];

            //Create
            await _art_service.CreateArtistAsync(cuDto);
        }

        //To update modified and deleted Artists, lets first read the original
        //Note that now the deleted artists will be removed and created artists will be nicely included
        var mg = await _mg_service.ReadMusicGroupAsync(vm.MusicGroupInput.MusicGroupId, false);


        //Check if there are any modified artists , if so update them in the database
        var modifiedArtists = vm.MusicGroupInput.Artists.FindAll(a => (a.StatusIM == ViewModels.EditGroup.StatusIM.Modified));
        foreach (var item in modifiedArtists)
        {
            var model = mg.Item.Artists.First(a => a.ArtistId == item.ArtistId);

            //Update the model from the InputModel
            model = item.UpdateModel(model);

            //Updatet the model in the database
            await _art_service.UpdateArtistAsync(new ArtistCUdto(model));
        }

        return mg.Item;
    }
    #endregion
}

