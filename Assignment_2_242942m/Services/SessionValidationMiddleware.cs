using Assignment_2_242942m.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Claims;

namespace Assignment_2_242942m.Services
{
    public class SessionValidationMiddleware
    {
        private readonly RequestDelegate _next;

        public SessionValidationMiddleware(RequestDelegate next) => _next = next;

        public async Task InvokeAsync(HttpContext ctx, SessionTicketService session)
        {
            if (ctx.User?.Identity?.IsAuthenticated == true)
            {
                var idStr = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                var ticket = ctx.User.FindFirstValue("SessionTicket");

                if (int.TryParse(idStr, out var memberId))
                {
                    var valid = !string.IsNullOrWhiteSpace(ticket) && await session.ValidateTicketAsync(memberId, ticket);

                    if (!valid)
                    {
                        // invalidate cookie and redirect to login
                        await ctx.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        ctx.Response.Redirect("/Account/Login");
                        return;
                    }
                }
            }

            await _next(ctx);
        }
    }
}