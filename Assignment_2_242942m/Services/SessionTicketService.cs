using Assignment_2_242942m.Data;
using Assignment_2_242942m.Models;
using Microsoft.EntityFrameworkCore;

namespace Assignment_2_242942m.Services
{
    public class SessionTicketService
    {
        private readonly AppDbContext _db;
        public SessionTicketService(AppDbContext db) => _db = db;

        public async Task<string> CreateTicketAsync(int memberId)
        {
            // revoke any previous
            var old = await _db.SessionTickets.Where(s => s.MemberId == memberId).ToListAsync();
            _db.SessionTickets.RemoveRange(old);

            var ticket = new SessionTicket
            {
                MemberId = memberId,
                Ticket = Guid.NewGuid().ToString()
            };
            _db.SessionTickets.Add(ticket);
            await _db.SaveChangesAsync();
            return ticket.Ticket;
        }

        public async Task<bool> ValidateTicketAsync(int memberId, string ticket)
        {
            return await _db.SessionTickets
                             .AnyAsync(s => s.MemberId == memberId && s.Ticket == ticket);
        }
    }
}
