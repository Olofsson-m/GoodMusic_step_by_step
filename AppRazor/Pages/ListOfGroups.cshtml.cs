using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Models.Interfaces;
using Services;

namespace AppRazor.Pages
{
    public class ListOfGroupsModel : PageModel
    {
        readonly IMusicGroupsService _service = null;
        readonly ILogger<ListOfGroupsModel> _logger = null;

        public bool UseSeeds { get; set; } = true;
        
        public List<IMusicGroup> MusicGroups { get; set; }

        public int NrOfGroups { get; set; }

        //will execute on a Get request
        public async Task<IActionResult> OnGetAsync()
        {   
            //Read one group to get the total count of groups in the database
            var _resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, null, 0, 1);
            MusicGroups = _resp.PageItems;
            NrOfGroups = _resp.DbItemsCount;

            //Read all groups from the database 
            _resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, null, 0, NrOfGroups);
            MusicGroups = _resp.PageItems;
            NrOfGroups = _resp.DbItemsCount;

            return Page();
        }

        //Inject services just like in WebApi
        public ListOfGroupsModel(IMusicGroupsService service, ILogger<ListOfGroupsModel> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
