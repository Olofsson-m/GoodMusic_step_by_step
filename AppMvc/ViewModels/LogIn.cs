using System.ComponentModel.DataAnnotations;
using AppMvc.SeidoHelpers;
using Microsoft.AspNetCore.Mvc;

namespace AppMvc.ViewModels
{
	public class Login
    {
        [BindProperty]
        public LoginIM LoginCreds { get; set; }
        public ModelValidationResult ValidationResult { get; set; } = new ModelValidationResult(false, null, null);
        
        public class LoginIM
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            public bool RememberMe { get; set; }
        }

    }
}