using Assignment_2_242942m.Data;
using Assignment_2_242942m.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment_2_242942m.Services
{
    public class SessionTicketService
    {
        private readonly AppDbContext _db;
        private readonly int _expirySeconds;

        public SessionTicketService(AppDbContext db, IConfiguration cfg)
        {
            _db = db;
            _expirySeconds = int.TryParse(cfg["Session:TicketExpirySeconds"], out var s) ? s : 30;
        }

        public async Task<string> CreateTicketAsync(int memberId)
        {
            // revoke any previous
            var old = await _db.SessionTickets.Where(s => s.MemberId == memberId).ToListAsync();
            _db.SessionTickets.RemoveRange(old);

            var ticket = new SessionTicket
            {
                MemberId = memberId,
                Ticket = Guid.NewGuid().ToString(),
                CreatedAt = DateTime.UtcNow // use CreatedAt as last-activity timestamp
            };
            _db.SessionTickets.Add(ticket);
            await _db.SaveChangesAsync();
            return ticket.Ticket;
        }

        public async Task<bool> ValidateTicketAsync(int memberId, string ticket)
        {
            var st = await _db.SessionTickets
                               .FirstOrDefaultAsync(s => s.MemberId == memberId && s.Ticket == ticket);

            if (st == null)
                return false;

            // expire by inactivity
            if (DateTime.UtcNow - st.CreatedAt > TimeSpan.FromSeconds(_expirySeconds))
            {
                _db.SessionTickets.Remove(st);
                await _db.SaveChangesAsync();
                return false;
            }

            return true;
        }

        // Call this on each validated request to extend activity
        public async Task TouchTicketAsync(int memberId, string ticket)
        {
            var st = await _db.SessionTickets
                               .FirstOrDefaultAsync(s => s.MemberId == memberId && s.Ticket == ticket);
            if (st == null) return;

            st.CreatedAt = DateTime.UtcNow;
            _db.SessionTickets.Update(st);
            await _db.SaveChangesAsync();
        }
    }
}
