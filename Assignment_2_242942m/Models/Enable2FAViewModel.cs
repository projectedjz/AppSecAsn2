namespace Assignment_2_242942m.Models
{
    public class Enable2FAViewModel
    {
        public string QrCodeUri { get; set; } = string.Empty;
        public string ManualKey { get; set; } = string.Empty;
        public string QrImageBase64 { get; set; } = string.Empty;

        // binds the form input (preserve leading zeros)
        public string Code { get; set; } = string.Empty;

        // TEST ONLY: current TOTP value generated server-side for quick testing
        public string CurrentCode { get; set; } = string.Empty;
    }
}
