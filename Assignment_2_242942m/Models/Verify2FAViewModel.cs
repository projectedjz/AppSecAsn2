using System.ComponentModel.DataAnnotations;

namespace Assignment_2_242942m.Models
{
    public class Verify2FAViewModel
    {
        [Required]
        [RegularExpression(@"^\d{6}$", ErrorMessage = "Enter a 6-digit code.")]
        public string Code { get; set; } = string.Empty;
    }
}
