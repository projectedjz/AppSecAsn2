using System.ComponentModel.DataAnnotations;

namespace Assignment_2_242942m.Models
{
    public class LoginViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required, DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public string RecaptchaToken { get; set; } = string.Empty;


    }
}
