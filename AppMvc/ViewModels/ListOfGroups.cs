using Models.Interfaces;

namespace AppMvc.ViewModels
{
	public class ListOfGroups
    {
        public bool UseSeeds { get; set; } = true;
        public List<IMusicGroup> MusicGroups { get; set; }
        public int NrOfGroups { get; set; }
    }
}

