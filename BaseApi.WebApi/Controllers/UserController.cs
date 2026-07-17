namespace BaseApi.WebApi.Controllers
{
    using System.IdentityModel.Tokens.Jwt;
    using System.Net.Http;
    using System.Net.Http.Json;
    using System.Security;
    using System.Text;
    using BaseApi.Data.Contexts;
    using BaseApi.Model;
    using BaseApi.Model.DTOs;
    using BaseApi.Service;
    using Microsoft.AspNetCore.Http;
    using Microsoft.AspNetCore.Mvc;
    using Microsoft.EntityFrameworkCore;
    using MongoDB.Driver;

    [Route("api/[controller]")]
    [ApiController]
    public class UserController : ControllerBase
    {
        private readonly UserDbContext _context;
        private readonly AuthService _authService;
        private readonly ApplicationDbContext _applicationDbContext;
        private readonly EmailService _emailService;
        

        public UserController(UserDbContext context, AuthService authService, ApplicationDbContext applicationDbContext, EmailService emailService)
        {
            _context = context;
            _authService = authService;
            _applicationDbContext = applicationDbContext;
            _emailService = emailService;
        }

        


        [HttpPost("register")]
        public async Task<IActionResult> RegisterUser(UserRegisterDto registerDto)
        {
            try
            {

                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var existingUser = await _context.Users.FirstOrDefaultAsync(u => u.EmailId == registerDto.Email);
                if(existingUser != null)
                {
                    return BadRequest("Email already registered");
                }


                var hashedPassword = HashPassword(registerDto.Password);

                var user = new User
                {
                    Username = registerDto.Username,
                    EmailId = registerDto.Email,
                    Password = hashedPassword,
                };

                _context.Users.Add(user);
                await _context.SaveChangesAsync();

                return Ok(new { message = "User registered successfully" });
            }
            catch (Exception ex) 
            {
                // Log the exception (if you have logging set up)
                // logger.LogError(ex, "An error occurred while registering a user.");

                throw ex;
            }



        }

        [HttpPost("login")]
        public async Task<IActionResult> LoginUser(UserLoginDto loginDto)
        {
            try
            {
                if (!ModelState.IsValid)
                {
                    return BadRequest(ModelState);
                }

                var user = await _context.Users
                    .FirstOrDefaultAsync(u => u.EmailId == loginDto.Email);

                if (user == null || !VerifyPassword(loginDto.Password, user.Password, out bool needsUpgrade))
                {
                    return Unauthorized("Invalid credentials");
                }

                if (needsUpgrade)
                {
                    user.Password = HashPassword(loginDto.Password);
                    await _context.SaveChangesAsync();
                }

                //Generating JWT Token
                var token = _authService.GenerateJwtToken(user);

                if (token == null)
                {
                    return StatusCode(500, "Error generating token.");
                }

                //Fetch Resume
                var resume = await _applicationDbContext.Resumes
                                                        .Find(r => r.Id == loginDto.Email)
                                                        .FirstOrDefaultAsync();

                return Ok(new { token, Resume = resume });// Sending token to the fronend
            }
            catch (Exception ex)
            {
                throw ex;
            }

        }


        [HttpPost("forgot-password")]
        public async Task<IActionResult> ForgotPassword(ForgotPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailId == dto.Email);
            if (user == null)
                // Don't reveal if email exists — but still return OK
                return Ok(new { message = "If that email is registered, a reset token has been sent." });

            // Invalidate old tokens for this email
            var oldTokens = _context.PasswordResetTokens
                .Where(t => t.UserEmail == dto.Email && !t.Used);
            await oldTokens.ForEachAsync(t => t.Used = true);

            var resetToken = new PasswordResetToken
            {
                UserEmail = dto.Email,
                Token = Guid.NewGuid().ToString("N"),
                ExpiresAt = DateTime.UtcNow.AddHours(1)
            };

            _context.PasswordResetTokens.Add(resetToken);
            await _context.SaveChangesAsync();

            // Send reset email via EmailService
            bool isSmtpSent = await _emailService.SendResetTokenEmailAsync(dto.Email, resetToken.Token);

            if (isSmtpSent)
            {
                return Ok(new {
                    message = "Reset token has been sent to your email address."
                });
            }
            else
            {
                // In dev mode (SMTP not configured), return token directly so the frontend can auto-fill
                return Ok(new {
                    message = "Reset token generated (dev mode — SMTP not configured).",
                    token = resetToken.Token
                });
            }
        }

        [HttpPost("reset-password")]
        public async Task<IActionResult> ResetPassword(ResetPasswordDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            var resetToken = await _context.PasswordResetTokens
                .FirstOrDefaultAsync(t => t.Token == dto.Token && !t.Used);

            if (resetToken == null)
                return BadRequest(new { message = "Invalid or expired reset token." });

            if (resetToken.ExpiresAt < DateTime.UtcNow)
            {
                resetToken.Used = true;
                await _context.SaveChangesAsync();
                return BadRequest(new { message = "Reset token has expired. Please request a new one." });
            }

            var user = await _context.Users
                .FirstOrDefaultAsync(u => u.EmailId == resetToken.UserEmail);

            if (user == null)
                return NotFound(new { message = "User not found." });

            user.Password = HashPassword(dto.NewPassword!);
            resetToken.Used = true;
            await _context.SaveChangesAsync();

            return Ok(new { message = "Password reset successfully. You can now log in." });
        }

        [HttpPost("google-login")]
        public async Task<IActionResult> GoogleLogin(GoogleLoginDto dto)
        {
            if (!ModelState.IsValid) return BadRequest(ModelState);

            // Verify the Google ID token via Google's tokeninfo endpoint
            using var httpClient = new HttpClient();
            var googleResponse = await httpClient.GetAsync(
                $"https://oauth2.googleapis.com/tokeninfo?id_token={dto.Credential}");

            if (!googleResponse.IsSuccessStatusCode)
                return Unauthorized(new { message = "Invalid Google token." });

            var payload = await googleResponse.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            if (payload == null || !payload.ContainsKey("email"))
                return Unauthorized(new { message = "Could not extract email from Google token." });

            var email = payload["email"];
            var name = payload.GetValueOrDefault("name") ?? payload.GetValueOrDefault("given_name") ?? "Google User";

            // Find or create user
            var user = await _context.Users.FirstOrDefaultAsync(u => u.EmailId == email);
            if (user == null)
            {
                user = new User
                {
                    EmailId = email,
                    Username = name,
                    Password = HashPassword(Guid.NewGuid().ToString()) // random password for OAuth users
                };
                _context.Users.Add(user);
                await _context.SaveChangesAsync();
            }

            var token = _authService.GenerateJwtToken(user);
            var resume = await _applicationDbContext.Resumes
                .Find(r => r.Id == email)
                .FirstOrDefaultAsync();

            return Ok(new { token, Resume = resume });
        }

        private static string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.EnhancedHashPassword(password);
        }

        private static bool VerifyPassword(string enteredPassword, string storedPassword, out bool needsUpgrade)
        {
            needsUpgrade = false;

            // Check if it is a BCrypt hash (starts with $2)
            if (storedPassword.StartsWith("$2"))
            {
                return BCrypt.Net.BCrypt.EnhancedVerify(enteredPassword, storedPassword);
            }

            // Legacy Base64 check
            var hashedEnteredPassword = Convert.ToBase64String(Encoding.UTF8.GetBytes(enteredPassword));
            if (hashedEnteredPassword == storedPassword)
            {
                needsUpgrade = true; // Flag for auto-upgrade to BCrypt
                return true;
            }

            return false;
        }
    }
}
