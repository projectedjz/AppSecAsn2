using Assignment_2_242942m.Data;
using Assignment_2_242942m.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

var builder = WebApplication.CreateBuilder(args);
builder.Services.AddHttpClient();



// DB
builder.Services.AddDbContext<AppDbContext>(opt =>
    opt.UseSqlServer(builder.Configuration.GetConnectionString("Default")));

// Services
builder.Services.AddScoped<CryptoService>();
builder.Services.AddScoped<PhotoService>();
builder.Services.AddScoped<SessionTicketService>();
builder.Services.AddScoped<ITokenService, TokenService>();
builder.Services.AddScoped<IEmailService, EmailService>();
builder.Services.AddScoped<IPasswordPolicyService, PasswordPolicyService>();

builder.Services.AddAuthentication(CookieAuthenticationDefaults.AuthenticationScheme)
    .AddCookie(opt =>
    {
        opt.LoginPath = "/Account/Login";
        opt.AccessDeniedPath = "/Error/Error403";
        opt.ExpireTimeSpan = TimeSpan.FromMinutes(20);
        opt.SlidingExpiration = false;
        opt.Cookie.HttpOnly = true;
        opt.Cookie.SecurePolicy = CookieSecurePolicy.Always;
        opt.Cookie.SameSite = SameSiteMode.Strict;

        // Validate principal on every request by checking the session ticket in DB.
        opt.Events = new CookieAuthenticationEvents
        {
            OnValidatePrincipal = async ctx =>
            {
                try
                {
                    var user = ctx.Principal;
                    if (user == null)
                    {
                        ctx.RejectPrincipal();
                        return;
                    }

                    var idStr = user.FindFirst(System.Security.Claims.ClaimTypes.NameIdentifier)?.Value;
                    var ticket = user.FindFirst("SessionTicket")?.Value;
                    if (!int.TryParse(idStr, out var memberId) || string.IsNullOrWhiteSpace(ticket))
                    {
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    // Resolve the SessionTicketService from DI and validate ticket
                    var session = ctx.HttpContext.RequestServices.GetService<Assignment_2_242942m.Services.SessionTicketService>();
                    if (session == null)
                    {
                        // If service unavailable, be conservative and reject principal
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                        return;
                    }

                    var valid = await session.ValidateTicketAsync(memberId, ticket);
                    if (!valid)
                    {
                        ctx.RejectPrincipal();
                        await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                    }
                }
                catch
                {
                    // On error, reject the principal to be safe
                    ctx.RejectPrincipal();
                    await ctx.HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
                }
            }
        };
    });

// MVC + Antiforgery global filter
builder.Services.AddControllersWithViews(options =>
{
    options.Filters.Add(new AutoValidateAntiforgeryTokenAttribute());
});

// reCAPTCHA settings
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));

// Session
builder.Services.AddDistributedMemoryCache();

builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(20);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
    options.Cookie.SameSite = SameSiteMode.Strict;
});


var app = builder.Build();

app.UseExceptionHandler("/Error/Error500");
app.UseStatusCodePagesWithReExecute("/Error/Error{0}");

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseSession();
app.UseRouting();
app.UseAuthentication();

// after app.UseAuthentication();
app.UseMiddleware<Assignment_2_242942m.Services.SessionValidationMiddleware>();

app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();

public class RecaptchaSettings
{
    public string SiteKey { get; set; } = string.Empty;
    public string SecretKey { get; set; } = string.Empty;
}
