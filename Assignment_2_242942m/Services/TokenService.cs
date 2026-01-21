using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Assignment_2_242942m.Services
{
    public interface ITokenService
    {
        string GenerateToken();
        bool ValidateToken(string token, out ClaimsPrincipal cp);
    }

    public class TokenService : ITokenService
    {
        private readonly IConfiguration _cfg;
        public TokenService(IConfiguration cfg) => _cfg = cfg;

        public string GenerateToken()
        {
            var key = Encoding.ASCII.GetBytes(_cfg["Crypto:Key"]!);
            var tokenHandler = new JwtSecurityTokenHandler();
            var tokenDescriptor = new SecurityTokenDescriptor
            {
                Subject = new ClaimsIdentity(new[] { new Claim("reset", "true") }),
                Expires = DateTime.UtcNow.AddMinutes(int.Parse(_cfg["Reset:TokenExpiryMinutes"]!)),
                SigningCredentials = new SigningCredentials(new SymmetricSecurityKey(key), SecurityAlgorithms.HmacSha256Signature)
            };
            return tokenHandler.CreateEncodedJwt(tokenDescriptor);
        }

        public bool ValidateToken(string token, out ClaimsPrincipal cp)
        {
            var key = Encoding.ASCII.GetBytes(_cfg["Crypto:Key"]!);
            var handler = new JwtSecurityTokenHandler();
            try
            {
                cp = handler.ValidateToken(token, new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(key),
                    ValidateIssuer = false,
                    ValidateAudience = false,
                    ClockSkew = TimeSpan.Zero
                }, out _);
                return true;
            }
            catch { cp = null!; return false; }
        }
    }
}