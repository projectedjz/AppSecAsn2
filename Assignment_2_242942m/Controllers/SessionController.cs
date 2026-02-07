using Assignment_2_242942m.Data;
using Assignment_2_242942m.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Assignment_2_242942m.Controllers
{
    [Authorize]
    public class SessionController : Controller
    {
        private readonly AppDbContext _db;
        private readonly SessionTicketService _session;
        private readonly IConfiguration _cfg;

        public SessionController(AppDbContext db, SessionTicketService session, IConfiguration cfg)
        {
            _db = db;
            _session = session;
            _cfg = cfg;
        }

        // Returns remaining seconds before the ticket expires (0 if expired/missing)
        [HttpGet]
        public async Task<IActionResult> Remaining()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ticket = User.FindFirstValue("SessionTicket");
            if (!int.TryParse(idStr, out var memberId) || string.IsNullOrWhiteSpace(ticket))
                return Json(new { remaining = 0 });

            var st = await _db.SessionTickets.FirstOrDefaultAsync(s => s.MemberId == memberId && s.Ticket == ticket);
            if (st == null) return Json(new { remaining = 0 });

            var expiry = _cfg.GetValue<int>("Session:TicketExpirySeconds", 30);
            var elapsed = (int)(DateTime.UtcNow - st.CreatedAt).TotalSeconds;
            var remaining = Math.Max(0, expiry - elapsed);
            return Json(new { remaining });
        }

        // Touch the ticket (extend last-activity). Returns remaining seconds after touching.
        [HttpGet]
        public async Task<IActionResult> KeepAlive()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var ticket = User.FindFirstValue("SessionTicket");
            if (!int.TryParse(idStr, out var memberId) || string.IsNullOrWhiteSpace(ticket))
                return Json(new { ok = false, remaining = 0 });

            await _session.TouchTicketAsync(memberId, ticket);

            var st = await _db.SessionTickets.FirstOrDefaultAsync(s => s.MemberId == memberId && s.Ticket == ticket);
            if (st == null) return Json(new { ok = false, remaining = 0 });

            var expiry = _cfg.GetValue<int>("Session:TicketExpirySeconds", 30);
            var remaining = Math.Max(0, expiry - (int)(DateTime.UtcNow - st.CreatedAt).TotalSeconds);
            return Json(new { ok = true, remaining });
        }
    }
}
