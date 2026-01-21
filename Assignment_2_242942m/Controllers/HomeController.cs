using Assignment_2_242942m.Data;
using Assignment_2_242942m.Models;
using Assignment_2_242942m.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Diagnostics;
using System.Security.Claims;

namespace Assignment_2_242942m.Controllers
{
    [Authorize]
    public class HomeController : Controller
    {
        private readonly AppDbContext _db;
        private readonly CryptoService _crypto;

        public HomeController(AppDbContext db, CryptoService crypto)
        {
            _db = db; _crypto = crypto;
        }

        public async Task<IActionResult> Index()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            var member = await _db.Members.FindAsync(id);
            var card = _crypto.Decrypt(member!.CreditCardCipher, member.CreditCardIV, member.CreditCardTag);
            var vm = new HomeIndexViewModel
            {
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = member.Email,
                MobileNo = member.MobileNo,
                BillingAddress = member.BillingAddress,
                ShippingAddress = member.ShippingAddress,
                CreditCardMasked = _crypto.MaskCard(card),
                PhotoPath = member.PhotoPath,
                TwoFactorEnabled = member.TwoFactorEnabled
            };
            return View(vm);
        }
    }

    public class HomeIndexViewModel
    {
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string MobileNo { get; set; } = string.Empty;
        public string BillingAddress { get; set; } = string.Empty;
        public string ShippingAddress { get; set; } = string.Empty;
        public string CreditCardMasked { get; set; } = string.Empty;
        public string? PhotoPath { get; set; }
        public bool TwoFactorEnabled { get; set; }
    }
}
