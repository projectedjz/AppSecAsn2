using System.ComponentModel.DataAnnotations;
using Assignment_2_242942m.Attributes;

namespace Assignment_2_242942m.Models
{
    public class RegisterViewModel
    {
        [Required, MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required, MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Upload)]
        [AllowedExtensions(".jpg", ".jpeg")]
        public IFormFile Photo { get; set; } = null!;

        [Required, CreditCard, MaxLength(20)]
        public string CreditCardNo { get; set; } = string.Empty;

        [Required, RegularExpression(@"^[89]\d{7}$", ErrorMessage = "Invalid SG mobile number")]
        public string MobileNo { get; set; } = string.Empty;

        [Required]
        public string BillingAddress { get; set; } = string.Empty;

        [Required]
        public string ShippingAddress { get; set; } = string.Empty;

        [Required, EmailAddress, MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [Required, MinLength(12)]
        [RegularExpression(@"^(?=.*[a-z])(?=.*[A-Z])(?=.*\d)(?=.*[^A-Za-z0-9]).{12,}$",
         ErrorMessage = "Password must be 12+ chars with upper, lower, digit and special")]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Required, Compare(nameof(Password))]
        [DataType(DataType.Password)]
        public string ConfirmPassword { get; set; } = string.Empty;
    }
}
