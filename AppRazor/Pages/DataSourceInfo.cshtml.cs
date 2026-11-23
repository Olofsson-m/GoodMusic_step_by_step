using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Services;
namespace AppRazor.Pages
{
	public class DataSourceInfoModel : PageModel
    {
        public Models.DTO.GstUsrInfoAllDto Info { get; set; }

        readonly ILogger<DataSourceInfoModel> _logger;
        readonly IAdminService _service = null;

        public DataSourceInfoModel(ILogger<DataSourceInfoModel> logger, IAdminService service)
        {
            _logger = logger;
            _service = service;
        }

        public async Task<IActionResult> OnGetAsync()
        {
            Info = (await _service.GuestInfoAsync()).Item;
            return Page();
        }
    }
}
