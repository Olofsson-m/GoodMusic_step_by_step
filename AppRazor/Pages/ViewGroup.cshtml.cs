using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;

using Models.Interfaces;
using Services;

namespace AppRazor.Pages
{
	public class ViewGroupModel : PageModel
    {
        //Just like for WebApi
        readonly IMusicGroupsService _service = null;
        readonly ILogger<ViewGroupModel> _logger = null;

        public IMusicGroup MusicGroup  { get; set; }

        public async Task<IActionResult> OnGetAsync(Guid id)
        {
            MusicGroup = (await _service.ReadMusicGroupAsync(id, false)).Item;

            return Page();
        }

        //Inject services just like in WebApi
        public ViewGroupModel(IMusicGroupsService service, ILogger<ViewGroupModel> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
