using System.ComponentModel.DataAnnotations;

namespace Assignment_2_242942m.Models
{
    public class ResetPasswordViewModel
    {
        [Required, EmailAddress]
        public string Email { get; set; } = string.Empty;
    }
}
