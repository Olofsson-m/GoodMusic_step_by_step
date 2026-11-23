using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using AppMvc.ViewModels;
using Services;

namespace AppMvc.Controllers;

public class SeedController : Controller
{
    readonly ILogger<SeedController> _logger;
    readonly IAdminService _admin_service = null;

    //Inject services just like in WebApi
    public SeedController(IAdminService admin_service, ILogger<SeedController> logger)
    {
        _admin_service = admin_service;
        _logger = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Seed()
    {
        var vm = new Seed() { NrOfGroups = await NrOfGroupsAsync() };
        return View("Seed", vm);
    }


    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Seed(Seed vm)
    {
        if (ModelState.IsValid)
        {
            if (vm.RemoveSeeds)
            {
                await _admin_service.RemoveSeedAsync(true);
                await _admin_service.RemoveSeedAsync(false);
            }

            await _admin_service.SeedAsync(vm.NrOfItemsToSeed);
            return RedirectToAction("DataSourceInfo", "Home");
        }

        vm.NrOfGroups = await NrOfGroupsAsync();
        return View(vm);
    }

    private async Task<int> NrOfGroupsAsync()
    {
        var info = await _admin_service.GuestInfoAsync();
        return info.Item.Db.NrSeededMusicGroups + info.Item.Db.NrUnseededMusicGroups;
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new Error { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}

