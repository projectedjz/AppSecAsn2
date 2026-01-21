using Assignment_2_242942m.Services;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Assignment_2_242942m.Models
{
    public class Member
    {
        public int Id { get; set; }

        [MaxLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string LastName { get; set; } = string.Empty;

        [MaxLength(100)]
        public string Email { get; set; } = string.Empty;

        [MaxLength(100)]
        public string PasswordHash { get; set; } = string.Empty;

        [MaxLength(20)]
        public string MobileNo { get; set; } = string.Empty;

        public string BillingAddress { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;

        // Credit-card encrypted fields
        public byte[] CreditCardCipher { get; set; } = Array.Empty<byte>();
        public byte[] CreditCardIV { get; set; } = Array.Empty<byte>();
        public byte[] CreditCardTag { get; set; } = Array.Empty<byte>();

        public string? PhotoPath { get; set; }

        // 2FA
        public string? TwoFactorSecret { get; set; }
        public bool TwoFactorEnabled { get; set; }

        // Audit
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? LastLoginAt { get; set; }
        public int AccessFailedCount { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public DateTime? LastPasswordChange { get; set; }

        // Reset Password
        public string? ResetToken { get; set; }
        public DateTime? ResetTokenExpiry { get; set; }

        // ✅ ADD THESE PROPERTIES:
        [NotMapped] // Don't save to database
        public CryptoService? _crypto { get; set; }

        [NotMapped]
        public string CreditCardMasked
        {
            get
            {
                if (CreditCardCipher == null || CreditCardCipher.Length == 0 || _crypto == null)
                    return "****";

                try
                {
                    // Decrypt using your CryptoService
                    var decrypted = _crypto.Decrypt(CreditCardCipher, CreditCardIV, CreditCardTag);

                    // Remove any spaces
                    decrypted = decrypted.Replace(" ", "").Replace("-", "");

                    if (decrypted.Length < 4)
                        return new string('*', decrypted.Length);

                    // Get last 4 digits
                    var lastFour = decrypted.Substring(decrypted.Length - 4);

                    // Return formatted: **** **** **** 1234
                    return $"**** **** **** {lastFour}";
                }
                catch
                {
                    return "****";
                }
            }
        }
    }

    public class SessionTicket
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string Ticket { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    public class AuditLog
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string Action { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
    }

    public class PasswordHistory
    {
        public int Id { get; set; }
        public int MemberId { get; set; }
        public string PasswordHash { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
