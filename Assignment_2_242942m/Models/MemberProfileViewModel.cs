namespace Assignment_2_242942m.Models
{
    public class MemberProfileViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string PhotoPath { get; set; } = string.Empty;
        public bool TwoFactorEnabled { get; set; }
        public string CreditCardMasked { get; set; } = string.Empty;
    }
}
