using AppMvc.SeidoHelpers;

namespace AppMvc.ViewModels
{
	public class ConfirmEmail
    {        
        public ModelValidationResult ValidationResult { get; set; } = new ModelValidationResult(false, null, null);
    }
}
