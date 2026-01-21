using Assignment_2_242942m.Data;
using Assignment_2_242942m.Models;
using Assignment_2_242942m.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using QRCoder;
using System.Drawing;
using System.IO;
using OtpNet;
using System.Security.Claims;
using System.Text.Json;


namespace Assignment_2_242942m.Controllers
{


    public class AccountController : Controller
    {
        private readonly AppDbContext _db;
        private readonly CryptoService _crypto;
        private readonly PhotoService _photo;
        private readonly SessionTicketService _session;
        private readonly IHttpClientFactory _http;
        private readonly IConfiguration _cfg;
        private readonly ITokenService _token;
        private readonly IEmailService _email;
        private readonly IPasswordPolicyService _passwordPolicy;

        public AccountController(AppDbContext db, CryptoService crypto, PhotoService photo,
                         SessionTicketService session, IHttpClientFactory http,
                         IConfiguration cfg, ITokenService token, IEmailService email, IPasswordPolicyService passwordPolicy)
        {
            _db = db;
            _crypto = crypto;
            _photo = photo;
            _session = session;
            _http = http;
            _cfg = cfg;
            _token = token;
            _email = email;
            _passwordPolicy = passwordPolicy;
        }

        // ======== 2FA (TOTP) - CUSTOM MEMBER TABLE ========

        private const string TwoFactorIssuer = "Bookworms Online";

        // IMPORTANT: get logged-in member from COOKIE claims, not Session
        private async Task<Member?> GetCurrentMemberAsync()
        {
            var idStr = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(idStr)) return null;

            if (!int.TryParse(idStr, out var memberId)) return null;

            return await _db.Members.FirstOrDefaultAsync(m => m.Id == memberId);
        }

        private static bool VerifyTotpCode(string base32Secret, string code)
        {
            var secretBytes = Base32Encoding.ToBytes(base32Secret);
            var totp = new Totp(secretBytes);

            // Allow small time drift
            return totp.VerifyTotp(code, out _, VerificationWindow.RfcSpecifiedNetworkDelay);
        }

        private void RebuildEnable2FAViewBag(Member member)
        {
            if (string.IsNullOrWhiteSpace(member.TwoFactorSecret)) return;

            var qrUri = new OtpUri(
                OtpType.Totp,
                member.TwoFactorSecret,
                member.Email,
                TwoFactorIssuer
            ).ToString();

            ViewBag.QrUri = qrUri;
            ViewBag.Secret = member.TwoFactorSecret;
        }


        [HttpGet]
        public IActionResult Register() => View();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Register(RegisterViewModel vm)
        {
            // Additional file type validation by checking actual file content (MIME type)
            if (vm.Photo != null)
            {
                var allowedMimeTypes = new[] { "image/jpeg", "image/jpg" };
                if (!allowedMimeTypes.Contains(vm.Photo.ContentType.ToLower()))
                {
                    ModelState.AddModelError("Photo", "Only JPG/JPEG images are allowed.");
                    return View(vm);
                }
            }

            if (!ModelState.IsValid) return View(vm);

            if (await _db.Members.AnyAsync(m => m.Email == vm.Email))
            {
                ModelState.AddModelError(nameof(vm.Email), "Email already registered");
                return View(vm);
            }

            var fileName = await _photo.SavePhotoAsync(vm.Photo);

            var (cipher, iv, tag) = _crypto.Encrypt(vm.CreditCardNo.Replace(" ", ""));

            var member = new Member
            {
                FirstName = vm.FirstName,
                LastName = vm.LastName,
                Email = vm.Email,
                PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(vm.Password),
                MobileNo = vm.MobileNo,
                BillingAddress = vm.BillingAddress,
                ShippingAddress = vm.ShippingAddress,
                CreditCardCipher = cipher,
                CreditCardIV = iv,
                CreditCardTag = tag,
                PhotoPath = fileName
            };

            _db.Members.Add(member);
            await _db.SaveChangesAsync();
            await LogAsync(member.Id, "Registered");
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        public IActionResult Login()
        {
            ViewBag.SiteKey = _cfg["Recaptcha:SiteKey"];  // must be filled
            return View();
        }


        [HttpPost]
        public async Task<IActionResult> Login(LoginViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            // 1.  basic lookup
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == vm.Email);
            if (member == null)
            {
                ModelState.AddModelError("", "Invalid login");
                return View(vm);
            }

            // 2.  lock-out check
            if (member.LockoutEnd.HasValue && member.LockoutEnd > DateTime.UtcNow)
            {
                ModelState.AddModelError("", $"Account locked until {member.LockoutEnd:u}");
                return View(vm);
            }

            // 3.  password check  (increments fail count)
            if (!BCrypt.Net.BCrypt.EnhancedVerify(vm.Password, member.PasswordHash))
            {
                member.AccessFailedCount++;
                if (member.AccessFailedCount >= 2)
                {
                    member.LockoutEnd = DateTime.UtcNow.AddMinutes(5);
                    member.AccessFailedCount = 0;
                }
                await _db.SaveChangesAsync();
                await LogAsync(member.Id, "Failed login");
                ModelState.AddModelError("", "Invalid login");
                return View(vm);
            }

            // 4.  password was correct – reset counters FIRST
            member.AccessFailedCount = 0;
            member.LockoutEnd = null;
            member.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            // 5.  reCAPTCHA check only after password is correct
            using var http = _http.CreateClient();
            var secret = _cfg["Recaptcha:SecretKey"];
            var response = await http.PostAsync(
                $"https://www.google.com/recaptcha/api/siteverify?secret={secret}&response={vm.RecaptchaToken}", null);
            using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
            bool success = doc.RootElement.TryGetProperty("success", out var sEl) && sEl.GetBoolean();
            double score = doc.RootElement.TryGetProperty("score", out var scEl) ? scEl.GetDouble() : 0.0;

            if (!success || score < 0.5)
            {
                ModelState.AddModelError("", "Bot verification failed");
                return View(vm);
            }

            // 6.  success path
            var ticket = await _session.CreateTicketAsync(member.Id);
            var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, member.Id.ToString()),
        new(ClaimTypes.Email, member.Email),
        new("SessionTicket", ticket)
    };

            if (member.TwoFactorEnabled)
            {
                HttpContext.Session.SetInt32("Pending2FA_MemberId", member.Id);
                return RedirectToAction(nameof(Verify2FA));
            }


            await HttpContext.SignInAsync(new ClaimsPrincipal(new ClaimsIdentity(claims,
                CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = false });

            await LogAsync(member.Id, "Logged in");
            return RedirectToAction("Index", "Home");
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            var id = int.Parse(User.FindFirstValue(ClaimTypes.NameIdentifier)!);
            await LogAsync(id, "Logged out");
            var ticket = User.FindFirstValue("SessionTicket");
            if (ticket != null)
            {
                var t = await _db.SessionTickets.FirstOrDefaultAsync(s => s.Ticket == ticket);
                if (t != null) { _db.SessionTickets.Remove(t); await _db.SaveChangesAsync(); }
            }
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction(nameof(Login));
        }

        [HttpGet]
        [Authorize]
        public async Task<IActionResult> Profile()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);

            if (member == null) return RedirectToAction(nameof(Login));

            member._crypto = _crypto;

            // Decrypt and mask the credit card
            var decryptedCard = _crypto.Decrypt(member.CreditCardCipher, member.CreditCardIV, member.CreditCardTag);
            decryptedCard = decryptedCard.Replace(" ", "");
            var lastFour = decryptedCard.Length >= 4
                ? decryptedCard.Substring(decryptedCard.Length - 4)
                : decryptedCard;

            // Create ViewModel
            var viewModel = new MemberProfileViewModel
            {
                FirstName = member.FirstName,
                LastName = member.LastName,
                Email = member.Email,
                MobileNo = member.MobileNo,
                BillingAddress = member.BillingAddress,
                ShippingAddress = member.ShippingAddress,
                PhotoPath = member.PhotoPath,
                TwoFactorEnabled = member.TwoFactorEnabled,
                CreditCardMasked = $"**** **** **** {lastFour}"
            };

            return View(viewModel);
        }


        [HttpGet]
        [Authorize]
        public async Task<IActionResult> ChangePassword()
        {
            var email = User.FindFirstValue(ClaimTypes.Email);
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);

            if (member == null) return RedirectToAction(nameof(Login));

            // Check if password MUST be changed (max age exceeded)
            if (_passwordPolicy.MustChangePassword(member.LastPasswordChange))
            {
                ViewBag.Warning = "Your password has expired. You must change it now.";
            }

            return View();
        }

        [HttpPost]
        [Authorize]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ChangePassword(ChangePasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var email = User.FindFirstValue(ClaimTypes.Email);
            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == email);

            if (member == null) return RedirectToAction(nameof(Login));

            // Verify current password
            if (!BCrypt.Net.BCrypt.EnhancedVerify(vm.CurrentPassword, member.PasswordHash))
            {
                ModelState.AddModelError("CurrentPassword", "Current password is incorrect.");
                return View(vm);
            }

            // Check minimum password age
            if (!_passwordPolicy.CanChangePassword(member.LastPasswordChange, out string errorMessage))
            {
                ModelState.AddModelError("", errorMessage);
                return View(vm);
            }

            // Check password history (max 2)
            var hist = await _db.PasswordHistories
                                .Where(p => p.MemberId == member.Id)
                                .OrderByDescending(p => p.CreatedAt)
                                .Take(2)
                                .ToListAsync();

            if (hist.Any(p => BCrypt.Net.BCrypt.EnhancedVerify(vm.NewPassword, p.PasswordHash)))
            {
                ModelState.AddModelError("NewPassword", "Cannot reuse last 2 passwords.");
                return View(vm);
            }

            // Save old password to history
            _db.PasswordHistories.Add(new PasswordHistory
            {
                MemberId = member.Id,
                PasswordHash = member.PasswordHash,
                CreatedAt = DateTime.UtcNow
            });

            // Update password
            member.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(vm.NewPassword);
            member.LastPasswordChange = DateTime.UtcNow;
            await _db.SaveChangesAsync();
            await LogAsync(member.Id, "Password changed successfully");

            TempData["Success"] = "Password changed successfully!";
            return RedirectToAction("Index", "Home");
        }

        [HttpGet]
        public IActionResult ResetPassword() => View();


        [HttpGet]
        public IActionResult ConfirmReset(string email, string token)
        {
            if (string.IsNullOrWhiteSpace(email) || string.IsNullOrWhiteSpace(token))
                return BadRequest();

            return View(new ConfirmResetViewModel { Email = email, Token = token });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResetPassword(ResetPasswordViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == vm.Email);
            if (member != null)
            {
                var token = _token.GenerateToken();
                member.ResetToken = token;
                member.ResetTokenExpiry = DateTime.Now.AddMinutes(15);
                await _db.SaveChangesAsync();

                var callback = Url.Action("ConfirmReset", "Account",
                                          new { email = member.Email, token },
                                          protocol: Request.Scheme);
                await _email.SendResetLinkAsync(member.Email, callback!);
                await LogAsync(member.Id, "Password-reset link sent");
            }

            TempData["Success"] = "If the email exists, a reset link has been sent to your inbox.";
            return RedirectToAction(nameof(ResetPassword));
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ConfirmReset(ConfirmResetViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            if (!_token.ValidateToken(vm.Token, out _))
            {
                ModelState.AddModelError("", "Invalid or expired token");
                return View(vm);
            }

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Email == vm.Email);
            if (member == null) return BadRequest();

            // password history check (max 2)
            var hist = await _db.PasswordHistories
                                .Where(p => p.MemberId == member.Id)
                                .OrderByDescending(p => p.CreatedAt)
                                .Take(2)
                                .ToListAsync();
            if (hist.Any(p => BCrypt.Net.BCrypt.EnhancedVerify(vm.NewPassword, p.PasswordHash)))
            {
                ModelState.AddModelError("", "Cannot reuse last 2 passwords");
                return View(vm);
            }

            // save old hash into history
            _db.PasswordHistories.Add(new PasswordHistory
            {
                MemberId = member.Id,
                PasswordHash = member.PasswordHash,
                CreatedAt = DateTime.UtcNow
            });

            member.PasswordHash = BCrypt.Net.BCrypt.EnhancedHashPassword(vm.NewPassword);
            member.LastPasswordChange = DateTime.UtcNow;

            // prevent token reuse
            member.ResetToken = null;
            member.ResetTokenExpiry = null;

            await _db.SaveChangesAsync();
            await LogAsync(member.Id, "Password reset completed");

            return RedirectToAction(nameof(Login), new { message = "Password has been reset. Please log in." });
        }

 

        // ---------- ENABLE 2FA ----------
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Enable2FA()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return RedirectToAction(nameof(Login));

            // Generate secret (Base32) and store (do not mark enabled until verified)
            var key = KeyGeneration.GenerateRandomKey(20);
            var secretBase32 = Base32Encoding.ToString(key);

            member.TwoFactorSecret = secretBase32;
            member.TwoFactorEnabled = false;
            await _db.SaveChangesAsync();

            RebuildEnable2FAViewBag(member);
            return View(new Verify2FAViewModel());
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Enable2FA(Verify2FAViewModel vm)
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return RedirectToAction(nameof(Login));

            if (!ModelState.IsValid)
            {
                RebuildEnable2FAViewBag(member);
                return View(vm);
            }

            if (string.IsNullOrWhiteSpace(member.TwoFactorSecret))
            {
                ModelState.AddModelError("", "2FA secret missing. Please reload and try again.");
                RebuildEnable2FAViewBag(member);
                return View(vm);
            }

            if (!VerifyTotpCode(member.TwoFactorSecret, vm.Code))
            {
                ModelState.AddModelError(nameof(vm.Code), "Invalid code. Try again.");
                RebuildEnable2FAViewBag(member);
                return View(vm);
            }

            member.TwoFactorEnabled = true;
            await _db.SaveChangesAsync();
            await LogAsync(member.Id, "2FA enabled");

            TempData["Success"] = "Two-factor authentication enabled.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }

        // ---------- VERIFY 2FA DURING LOGIN ----------
        [HttpGet]
        public IActionResult Verify2FA()
        {
            if (HttpContext.Session.GetInt32("Pending2FA_MemberId") == null)
                return RedirectToAction(nameof(Login));

            return View(new Verify2FAViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Verify2FA(Verify2FAViewModel vm)
        {
            if (!ModelState.IsValid) return View(vm);

            var pendingId = HttpContext.Session.GetInt32("Pending2FA_MemberId");
            if (pendingId == null)
            {
                ModelState.AddModelError("", "2FA session expired. Please login again.");
                return RedirectToAction(nameof(Login));
            }

            var member = await _db.Members.FirstOrDefaultAsync(m => m.Id == pendingId.Value);
            if (member == null) return RedirectToAction(nameof(Login));

            if (!member.TwoFactorEnabled || string.IsNullOrWhiteSpace(member.TwoFactorSecret))
            {
                ModelState.AddModelError("", "2FA is not enabled for this account.");
                return RedirectToAction(nameof(Login));
            }

            if (!VerifyTotpCode(member.TwoFactorSecret, vm.Code))
            {
                ModelState.AddModelError(nameof(vm.Code), "Invalid code.");
                return View(vm);
            }

            // SUCCESS: issue auth cookie now (real login)
            HttpContext.Session.Remove("Pending2FA_MemberId");

            var ticket = await _session.CreateTicketAsync(member.Id);
            var claims = new List<Claim>
    {
        new(ClaimTypes.NameIdentifier, member.Id.ToString()),
        new(ClaimTypes.Email, member.Email),
        new("SessionTicket", ticket)
    };

            await HttpContext.SignInAsync(
                new ClaimsPrincipal(new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme)),
                new AuthenticationProperties { IsPersistent = false }
            );

            member.LastLoginAt = DateTime.UtcNow;
            await _db.SaveChangesAsync();

            await LogAsync(member.Id, "Logged in (2FA)");
            return RedirectToAction("Index", "Home");
        }

        // ---------- DISABLE 2FA ----------
        [Authorize]
        [HttpGet]
        public async Task<IActionResult> Disable2FA()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return RedirectToAction(nameof(Login));

            if (!member.TwoFactorEnabled)
            {
                ViewBag.Error = "2FA is already disabled.";
                return View();
            }

            ViewBag.MemberId = member.Id;
            ViewBag.Email = member.Email;
            ViewBag.TwoFactorEnabled = member.TwoFactorEnabled;

            return View();
        }

        [Authorize]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Disable2FAConfirmed()
        {
            var member = await GetCurrentMemberAsync();
            if (member == null) return RedirectToAction(nameof(Login));

            member.TwoFactorEnabled = false;
            member.TwoFactorSecret = null; // wipe secret
            await _db.SaveChangesAsync();

            await LogAsync(member.Id, "2FA disabled");

            TempData["Success"] = "Two-factor authentication disabled.";
            return RedirectToAction(nameof(HomeController.Index), "Home");
        }


        //[HttpGet]
        //public IActionResult TestRoute() => Content("route ok");

        private async Task LogAsync(int memberId, string action)
        {
            _db.AuditLogs.Add(new AuditLog { MemberId = memberId, Action = action });
            await _db.SaveChangesAsync();
        }
    }
}
