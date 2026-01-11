using Checkers.Core;
using Checkers.Entities;
using Checkers.Models;
using Checkers.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using MongoDB.Driver;
using System.Security.Claims;
namespace Checkers.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : Controller
    {
        private readonly IMongoCollection<User> _users;
        private readonly PasswordHasher<string> _hasher = new PasswordHasher<string>();
        private readonly IJWTService _jwtService;
        private readonly IPasswordService _passwordService;
        private readonly IEmailService _emailService;

        public AuthController(AppDbContext context, IJWTService jWTService, IPasswordService passwordService, IEmailService emailService)
        {
            this._jwtService = jWTService;
            this._users = context.Users;
            _passwordService = passwordService;
            _emailService = emailService;
        }

        [HttpPost("register")]
        public async Task<IActionResult> Register([FromBody] RegisterRequest dto)
        {
            User existingUser = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
            if (existingUser != null)
            {
                return BadRequest("User with this email already exists");
            }
            var (passwordHash, passwordSalt) = _passwordService.HashPassword(dto.Password);
            var activationCode = Guid.NewGuid().ToString();
            User user = new User
            {
                Email = dto.Email,
                PasswordHash = passwordHash,
                PasswordSalt = passwordSalt,
                ActivationLink = activationCode,
            };

            string accessToken = _jwtService.GenerateAccessToken(user);
            string refreshToken = _jwtService.GenerateRefreshToken();

            RefreshToken refreshTokenEnity = new RefreshToken
            {
                Token = refreshToken,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            user.RefreshToken = refreshTokenEnity;
            await _users.InsertOneAsync(user);
            Response.Cookies.Append("accessToken", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,  // false for HTTP (development)
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,  // false for HTTP (development)
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            var activationLink = $"http://localhost:5239/auth/activate?code={activationCode}";
            _emailService.SendActivationEmailAsync(user.Email, activationLink);
            return Ok(new
            {
                accessToken,
                refreshToken,
                user = new
                {
                    email = user.Email
                }
            });
        }

        [HttpGet("activate")]
        public async Task<IActionResult> ActivateAccount([FromQuery] string code)
        {
            if(string.IsNullOrEmpty(code))
            {
                return BadRequest("Code is empty");
            }
            User user = await _users.Find(u => u.ActivationLink == code).FirstOrDefaultAsync();
            if (user == null) {
                return BadRequest("Invalid activation code");
            }
            if (user.IsActivated)
                return Content(GetAlreadyActivatedHtml(), "text/html");

            // 1. Створюємо "інструкцію", що саме треба змінити
            var update = Builders<User>.Update
                .Set(u => u.IsActivated, true)        
                .Set(u => u.ActivationLink, null);  

            // 2. Виконуємо оновлення в базі
            await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            return Content(GetActivationSuccessHtml(), "text/html");
        }


        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest dto)
        {
            User user = await _users.Find(u => u.Email == dto.Email).FirstOrDefaultAsync();
            if (user == null)
                return Unauthorized("Invalid username");
            Console.WriteLine("PasswordHash: " + user.PasswordHash + " Length of hash: " + user.PasswordHash.Length + "  PasswordSalt: " + user.PasswordSalt + " Salt length: " + user.PasswordSalt.Length);
            if (!_passwordService.VerifyPassword(dto.Password, user.PasswordHash, user.PasswordSalt))
                return Unauthorized(new { message = "Invalid credentials" });

            if (!user.IsActivated)
            {
                return Unauthorized(new
                {
                    message = "Please activate your account before logging in. Check your email.",
                    accountNotActivated = true
                });
            }
            string accessToken = _jwtService.GenerateAccessToken(user);
            string refreshToken = _jwtService.GenerateRefreshToken();
            RefreshToken refreshTokenEnity = new RefreshToken
            {
                Token = refreshToken,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            await _users.UpdateOneAsync(u => u.Id == user.Id, Builders<User>.Update.Set(u => u.RefreshToken, refreshTokenEnity));
            Response.Cookies.Append("accessToken", accessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,  // false for HTTP (development)
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });
            Response.Cookies.Append("refreshToken", refreshToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,  // false for HTTP (development)
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return Ok(new
            {
                accessToken,
                refreshToken,
                user = new
                {
                    email = user.Email,
                    isActivated = user.IsActivated,
                }
            });
        }

        [HttpPost("refresh")]
        public async Task<IActionResult> RefreshToken()
        {
            var refreshToken = Request.Cookies["refreshToken"];
            Console.WriteLine("Refresh Token: " + refreshToken);
            if (string.IsNullOrEmpty(refreshToken))
                return Unauthorized(new { message = "Token not found in cookies" });
            var user = await _users.Find(u => u.RefreshToken.Token == refreshToken).FirstOrDefaultAsync();
            if (user == null || user.RefreshToken == null || user.RefreshToken.Token != refreshToken || user.RefreshToken.Expires <= DateTime.UtcNow)
            {
                Response.Cookies.Delete("accessToken");
                Response.Cookies.Delete("refreshToken");
                return Unauthorized(new { message = "Invalid or expired refresh token" });
            }
            var newAccessToken = _jwtService.GenerateAccessToken(user);
            var newRefreshTokenString = _jwtService.GenerateRefreshToken();
            var newRefreshTokenEntity = new RefreshToken
            {
                Token = newRefreshTokenString,
                Expires = DateTime.UtcNow.AddDays(7)
            };
            var update = Builders<User>.Update.Set(u => u.RefreshToken, newRefreshTokenEntity);
            await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            Response.Cookies.Append("accessToken", newAccessToken, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddMinutes(15)
            });

            Response.Cookies.Append("refreshToken", newRefreshTokenString, new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/",
                Expires = DateTimeOffset.UtcNow.AddDays(7)
            });
            return Ok(new { message = "Tokens refreshed successfully" });
        }
        [Authorize]
        [HttpPost("logout")]
        public async Task<IActionResult> Logout()
        {
            var refreshToken = Request.Cookies["refreshToken"];  // Read from cookie
            if (!string.IsNullOrEmpty(refreshToken))
            {
                var email = User.FindFirst(ClaimTypes.Email)?.Value;
                var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();
                if (user == null)
                    return Unauthorized();
                var update = Builders<User>.Update.Set(u => u.RefreshToken, null);
                await _users.UpdateOneAsync(u => u.Id == user.Id, update);
            }
            var cookieOptions = new CookieOptions
            {
                HttpOnly = true,
                Secure = true,
                SameSite = SameSiteMode.None,
                Path = "/"
            };
            Response.Cookies.Delete("accessToken", cookieOptions);
            Response.Cookies.Delete("refreshToken", cookieOptions);
            return Ok(new { message = "Logged out successfully" });
        }

        [Authorize]
        [HttpGet("me")]
        public async Task<IActionResult> GetCurrentUser()
        {
            var email = User.FindFirst(ClaimTypes.Email)?.Value;
            var user = await _users.Find(u => u.Email == email).FirstOrDefaultAsync();

            if (user == null)
                return NotFound();

            return Ok(new
            {
                id = user.Id,
                Email = user.Email
            });
        }

        private string GetActivationSuccessHtml()
        {
            return @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Account Activated</title>
                    <style>
                        body { font-family: Arial; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f5f5f5; }
                        .container { text-align: center; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
                        .success { color: #28a745; font-size: 60px; margin-bottom: 20px; }
                        h1 { color: #333; margin: 20px 0; }
                        p { color: #666; margin: 20px 0; }
                        .button { background: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='success'>Congratulations</div>
                        <h1>Account Activated!</h1>
                        <p>Your account has been successfully activated.</p>
                        <p>You can now login to your account.</p>
                        <a href='http://localhost:3000/login' class='button'>Go to Login</a>
                    </div>
                </body>
                </html>
            ";
        }

        private string GetAlreadyActivatedHtml()
        {
            return @"
                <!DOCTYPE html>
                <html>
                <head>
                    <title>Already Activated</title>
                    <style>
                        body { font-family: Arial; display: flex; justify-content: center; align-items: center; height: 100vh; margin: 0; background: #f5f5f5; }
                        .container { text-align: center; background: white; padding: 40px; border-radius: 10px; box-shadow: 0 2px 10px rgba(0,0,0,0.1); }
                        .info { color: #17a2b8; font-size: 60px; margin-bottom: 20px; }
                        h1 { color: #333; margin: 20px 0; }
                        p { color: #666; margin: 20px 0; }
                        .button { background: #007bff; color: white; padding: 12px 30px; text-decoration: none; border-radius: 5px; display: inline-block; margin-top: 20px; }
                    </style>
                </head>
                <body>
                    <div class='container'>
                        <div class='info'>ℹ</div>
                        <h1>Already Activated</h1>
                        <p>Your account is already activated.</p>
                        <p>You can login now.</p>
                        <a href='http://localhost:3000/login' class='button'>Go to Login</a>
                    </div>
                </body>
                </html>
            ";
        }
    }
}
