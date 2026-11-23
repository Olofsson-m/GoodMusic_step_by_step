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

        //Pagination
        public int NrOfPages { get; set; }
        public int PageSize { get; } = 10;

        public int ThisPageNr { get; set; } = 0;
        public int PrevPageNr { get; set; } = 0;
        public int NextPageNr { get; set; } = 0;
        public int NrVisiblePages { get; set; } = 0;

        //will execute on a Get request
        public async Task<IActionResult> OnGetAsync(int pagenr)
        {   
            ThisPageNr = pagenr;

            //Use the Service
            var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, null, ThisPageNr, PageSize);
            MusicGroups = resp.PageItems;
            NrOfGroups = resp.DbItemsCount;

            //Pagination
            UpdatePagination(resp.DbItemsCount);

            return Page();
        }

        private void UpdatePagination(int nrOfItems)
        {
            //Pagination
            NrOfPages = (int)Math.Ceiling((double)nrOfItems / PageSize);
            PrevPageNr = Math.Max(0, ThisPageNr - 1);
            NextPageNr = Math.Min(NrOfPages - 1, ThisPageNr + 1);
            NrVisiblePages = Math.Min(10, NrOfPages);
        }

        //Inject services just like in WebApi
        public ListOfGroupsModel(IMusicGroupsService service, ILogger<ListOfGroupsModel> logger)
        {
            _service = service;
            _logger = logger;
        }
    }
}
