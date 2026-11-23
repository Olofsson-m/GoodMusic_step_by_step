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

        [BindProperty]
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

        //ModelBinding for the form
        [BindProperty]
        public string SearchFilter { get; set; } = null;

        //will execute on a Get request
        public async Task<IActionResult> OnGetAsync(int pagenr, string search)
        {   
            ThisPageNr = pagenr;
            SearchFilter = search;

            //Use the Service
            var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, ThisPageNr, PageSize);
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

        public async Task<IActionResult> OnPostSearchAsync()
        {
            //Use the Service
            var resp = await _service.ReadMusicGroupsAsync(UseSeeds, false, SearchFilter, ThisPageNr, PageSize);
            MusicGroups = resp.PageItems;
            NrOfGroups = resp.DbItemsCount;

            //Pagination
            UpdatePagination(resp.DbItemsCount);

            //Page is rendered as the postback is part of the form tag
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
