using System.Security.Cryptography;
using System.Text;

namespace Assignment_2_242942m.Services
{
    public class CryptoService
    {
        private readonly IConfiguration _cfg;
        public CryptoService(IConfiguration cfg) => _cfg = cfg;

        private byte[] Key => SHA256.HashData(Encoding.UTF8.GetBytes(_cfg["Crypto:Key"]!));

        public (byte[] cipher, byte[] iv, byte[] tag) Encrypt(string plain)
        {
            using var aes = new AesGcm(Key);
            var iv = RandomNumberGenerator.GetBytes(12);
            var cipher = new byte[Encoding.UTF8.GetByteCount(plain)];
            var tag = new byte[16];
            aes.Encrypt(iv, Encoding.UTF8.GetBytes(plain), cipher, tag);
            return (cipher, iv, tag);
        }

        public string Decrypt(byte[] cipher, byte[] iv, byte[] tag)
        {
            using var aes = new AesGcm(Key);
            var plain = new byte[cipher.Length];
            aes.Decrypt(iv, cipher, tag, plain);
            return Encoding.UTF8.GetString(plain);
        }

        public string MaskCard(string card)
        {
            // Remove spaces/dashes
            card = card.Replace(" ", "").Replace("-", "");

            if (card.Length < 4)
                return "****";

            // Get last 4 digits
            var lastFour = card.Substring(card.Length - 4);

            // Return formatted: **** **** **** 1234
            return $"**** **** **** {lastFour}";
        }
    }
}
