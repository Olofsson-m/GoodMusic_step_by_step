using System.Diagnostics;
using AppMvc.ViewModels;
using AppMvc.SeidoHelpers;
using Microsoft.AspNetCore.Mvc;

using Models.DTO;
using Models.Interfaces;
using Services;

// For more information on enabling MVC for empty projects, visit https://go.microsoft.com/fwlink/?LinkID=397860

namespace AppMvc.Controllers;

public class GroupController : Controller
{
    //Just like for WebApi
    readonly IMusicGroupsService _mg_service = null;
    readonly IAlbumsService _alb_service = null;
    readonly IArtistsService _art_service = null;
    readonly ILogger<GroupController> _logger = null;

    //Inject services just like in WebApi
    public GroupController(IMusicGroupsService mg_service, IAlbumsService alb_service, IArtistsService art_service, ILogger<GroupController> logger)
    {
        _mg_service = mg_service;
        _alb_service = alb_service;
        _art_service = art_service;
        _logger = logger;
    }

    #region ListOfGroups handling
    public async Task<IActionResult> ListOfGroups()
    {
        //Create the viewModel
        var vm = new ListOfGroups();

        //Read one group to get the total count of groups in the database
        var _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, null, 0, 1);
        vm.MusicGroups = _resp.PageItems;
        vm.NrOfGroups = _resp.DbItemsCount;

        //Read all groups from the database 
        _resp = await _mg_service.ReadMusicGroupsAsync(vm.UseSeeds, false, null, 0, vm.NrOfGroups);
        vm.MusicGroups = _resp.PageItems;
        vm.NrOfGroups = _resp.DbItemsCount;

        //Render the View
        return View(vm);
    }
    #endregion

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Error { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

