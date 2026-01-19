using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Misfitz_Games.Controllers;

public sealed record AdminLoginRequest(string Password);

[ApiController]
public class AdminAuthController(IConfiguration config, IWebHostEnvironment env) : ControllerBase
{
    [HttpPost("/admin/login")]
    public IActionResult Login([FromBody] AdminLoginRequest req)
    {
        var expected = config["ADMIN_PASSWORD"];
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(500, new { ok = false, error = "ADMIN_PASSWORD not set" });

        if (!string.Equals(req.Password, expected, StringComparison.Ordinal))
            return Unauthorized(new { ok = false, error = "Invalid password" });

        var secret = config["JWT_SECRET"];
        if (string.IsNullOrWhiteSpace(secret))
            return StatusCode(500, new { ok = false, error = "JWT_SECRET not set" });

        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("role", "admin"),
            new Claim("sub", "admin"),
        };

        var token = new JwtSecurityToken(
            claims: claims,
            notBefore: DateTime.UtcNow,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("mf_admin", jwt, new CookieOptions
        {
            HttpOnly = true,

            // ✅ only secure cookies in production (Render = HTTPS)
            Secure = !env.IsDevelopment(),

            // ✅ Lax works well for same-site admin panel
            SameSite = SameSiteMode.Lax,

            Expires = DateTimeOffset.UtcNow.AddHours(12),
            Path = "/",
        });

        return Ok(new { ok = true });
    }

    [HttpPost("/admin/logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("mf_admin", new CookieOptions
        {
            Path = "/",
            Secure = !env.IsDevelopment(),
            SameSite = SameSiteMode.Lax
        });

        return Ok(new { ok = true });
    }

    [Authorize(Policy = "AdminOnly")]
    [HttpGet("/admin/me")]
    public IActionResult Me()
        => Ok(new { ok = true, role = "admin" });
}