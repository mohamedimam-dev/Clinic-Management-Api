using ClinicManagementApi.DTOS.Auth;
using ClinicManagementBusiness;
using ClinicManagementDataAccess;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using static ClinicManagementBusiness.clsSecurityLog;

namespace ClinicManagementApi.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly IConfiguration _configuration;

        public AuthController(IConfiguration configuration)
        {
            _configuration = configuration;
        }

        private void LogSecurity(enSecurityEventType eventType, int? userId)
        {
            clsSecurityLog.LogEvent(
                eventType.ToString(),
                userId,
                HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown",
                HttpContext.Request.Path);
        }

        [HttpPost("Login")]
        [EnableRateLimiting("AuthLimiter")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Login([FromBody] LoginDTO loginDto)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string endpoint = HttpContext.Request.Path;

            clsUser? user = clsUser.FindByUserName(loginDto.UserName);

            if (user == null)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.LoginFailed.ToString(),
                    null,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid credentials.");
            }

            bool isValidPassword =
                BCrypt.Net.BCrypt.Verify(loginDto.Password, user.Password);

            if (!isValidPassword)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.LoginFailed.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid credentials.");
            }

            if (!user.IsActive)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.InactiveAccount.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid credentials.");
            }

            // Claims
            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Role,
            ((clsUser.enRole)user.RoleID).ToString())
            };

            var secretKey = _configuration["JWT:SecretKey"];
            var issuer = _configuration["JWT:Issuer"];
            var audience = _configuration["JWT:Audience"];
            var expirationInMinutes =
                int.Parse(_configuration["JWT:ExpirationInMinutes"]!);

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey!));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var token = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationInMinutes),
                signingCredentials: credentials);

            string accessToken =
                new JwtSecurityTokenHandler().WriteToken(token);

            // إنشاء Refresh Token
            string refreshToken = GenerateRefreshToken();

            // تخزين الـ Hash فقط
            user.RefreshTokenHash =
                BCrypt.Net.BCrypt.HashPassword(refreshToken);

            user.RefreshTokenExpiresAt =
                DateTime.UtcNow.AddDays(7);

            user.RefreshTokenRevokedAt = null;

            // حفظ بيانات الـ Refresh Token
            if (!user.UpdateRefreshToken())
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Failed to save refresh token.");
            }

            clsSecurityLog.LogEvent(
                enSecurityEventType.LoginSucceeded.ToString(),
                user.UserID,
                ipAddress,
                endpoint);

            return Ok(new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = refreshToken
            });
        }
       
        private static string GenerateRefreshToken()
        {
            var bytes = new byte[64];
            using var rng = RandomNumberGenerator.Create();
            rng.GetBytes(bytes);
            return Convert.ToBase64String(bytes);
        }

        [HttpPost("Refresh")]
        [EnableRateLimiting("AuthLimiter")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status500InternalServerError)]
        public IActionResult Refresh([FromBody] RefreshRequest request)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string endpoint = HttpContext.Request.Path;

            if (string.IsNullOrWhiteSpace(request.UserName) ||
                string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Invalid data.");
            }

            request.UserName = request.UserName.Trim();

            clsUser? user = clsUser.FindByUserName(request.UserName);

            if (user == null)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.RefreshTokenFailed.ToString(),
                    null,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid refresh request.");
            }

            if (user.RefreshTokenRevokedAt != null)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.RefreshTokenRevoked.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid refresh request.");
            }

            if (!user.RefreshTokenExpiresAt.HasValue ||
                user.RefreshTokenExpiresAt.Value <= DateTime.UtcNow)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.RefreshTokenExpired.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid refresh request..");
            }

            if (string.IsNullOrWhiteSpace(user.RefreshTokenHash))
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.RefreshTokenFailed.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid refresh request.");
            }

            bool isValidRefreshToken =
                BCrypt.Net.BCrypt.Verify(
                    request.RefreshToken,
                    user.RefreshTokenHash);

            if (!isValidRefreshToken)
            {
                clsSecurityLog.LogEvent(
                    enSecurityEventType.RefreshTokenFailed.ToString(),
                    user.UserID,
                    ipAddress,
                    endpoint);

                return Unauthorized("Invalid refresh request.");
            }

            // إنشاء Claims
            var claims = new[]
            {
        new Claim(ClaimTypes.NameIdentifier, user.UserID.ToString()),
        new Claim(ClaimTypes.Name, user.UserName),
        new Claim(ClaimTypes.Role,
            ((clsUser.enRole)user.RoleID).ToString())
            };

            var secretKey = _configuration["JWT:SecretKey"];
            var issuer = _configuration["JWT:Issuer"];
            var audience = _configuration["JWT:Audience"];
            var expirationInMinutes =
                int.Parse(_configuration["JWT:ExpirationInMinutes"]!);

            var key = new SymmetricSecurityKey(
                Encoding.UTF8.GetBytes(secretKey!));

            var credentials = new SigningCredentials(
                key,
                SecurityAlgorithms.HmacSha256);

            var jwt = new JwtSecurityToken(
                issuer: issuer,
                audience: audience,
                claims: claims,
                expires: DateTime.UtcNow.AddMinutes(expirationInMinutes),
                signingCredentials: credentials);

            string accessToken =
                new JwtSecurityTokenHandler().WriteToken(jwt);

            // ===== Refresh Token Rotation =====

            string newRefreshToken = GenerateRefreshToken();

            user.RefreshTokenHash =
                BCrypt.Net.BCrypt.HashPassword(newRefreshToken);

            user.RefreshTokenExpiresAt =
                DateTime.UtcNow.AddDays(7);

            user.RefreshTokenRevokedAt = null;

            if (!user.UpdateRefreshToken())
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Failed to update refresh token.");
            }

            clsSecurityLog.LogEvent(
                enSecurityEventType.RefreshTokenSucceeded.ToString(),
                user.UserID,
                ipAddress,
                endpoint);

            return Ok(new TokenResponse
            {
                AccessToken = accessToken,
                RefreshToken = newRefreshToken
            });
        }

        [HttpPost("Logout")]
        [ProducesResponseType(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status400BadRequest)]
        public IActionResult Logout([FromBody] LogoutRequest request)
        {
            string ipAddress = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "Unknown";
            string endpoint = HttpContext.Request.Path;

            if (string.IsNullOrWhiteSpace(request.UserName) ||
                string.IsNullOrWhiteSpace(request.RefreshToken))
            {
                return BadRequest("Invalid data.");
            }

            request.UserName = request.UserName.Trim();

            clsUser? user =
                clsUser.FindByUserName(request.UserName);

            if (user == null)
                return Ok();

            if (string.IsNullOrWhiteSpace(user.RefreshTokenHash))
                return Ok();

            bool isValidRefreshToken =
                BCrypt.Net.BCrypt.Verify(
                    request.RefreshToken,
                    user.RefreshTokenHash);

            if (!isValidRefreshToken)
                return Ok();

            if (!user.RevokeRefreshToken())
            {
                return StatusCode(
                    StatusCodes.Status500InternalServerError,
                    "Failed to logout.");
            }

            clsSecurityLog.LogEvent(
                enSecurityEventType.LogoutSucceeded.ToString(),
                user.UserID,
                ipAddress, 
                endpoint);

            return Ok("Logged out successfully.");
        }


    }
}
