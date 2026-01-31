using Microsoft.AspNetCore.Mvc;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

namespace Misfitz_Games.Controllers;

[ApiController]
public sealed class MemberAuthController(IConfiguration config) : ControllerBase
{
    public sealed record MemberLoginRequest(string Username, string Key);

    [HttpPost("/member/login")]
    public IActionResult Login([FromBody] MemberLoginRequest req)
    {
        var expected = config["MEMBER_LOGIN_KEY"] ?? "";
        if (string.IsNullOrWhiteSpace(expected))
            return StatusCode(500, new { ok = false, error = "MEMBER_LOGIN_KEY not configured" });

        if (string.IsNullOrWhiteSpace(req.Username))
            return BadRequest(new { ok = false, error = "Username required" });

        if (!string.Equals(req.Key ?? "", expected, StringComparison.Ordinal))
            return Unauthorized(new { ok = false, error = "Invalid member key" });

        var jwtSecret = config["JWT_SECRET"] ?? "dev-secret-change-me";
        var keyBytes = Encoding.UTF8.GetBytes(jwtSecret);
        var creds = new SigningCredentials(new SymmetricSecurityKey(keyBytes), SecurityAlgorithms.HmacSha256);

        var claims = new[]
        {
            new Claim("role", "member"),
            new Claim(ClaimTypes.NameIdentifier, req.Username.Trim()),
            new Claim(ClaimTypes.Name, req.Username.Trim())
        };

        var token = new JwtSecurityToken(
            claims: claims,
            expires: DateTime.UtcNow.AddHours(12),
            signingCredentials: creds
        );

        var jwt = new JwtSecurityTokenHandler().WriteToken(token);

        Response.Cookies.Append("mf_member", jwt, new CookieOptions
        {
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.None,
            Path = "/",
            Expires = DateTimeOffset.UtcNow.AddHours(12)
        });

        return Ok(new { ok = true });
    }

    [HttpPost("/member/logout")]
    public IActionResult Logout()
    {
        Response.Cookies.Delete("mf_member", new CookieOptions
        {
            Path = "/",
            Secure = true,
            SameSite = SameSiteMode.None
        });

        return Ok(new { ok = true });
    }

    [HttpGet("/member/me")]
    public IActionResult Me()
    {
        var isMember = User?.Claims?.Any(c => c.Type == "role" && c.Value == "member") == true;

        return Ok(new
        {
            ok = true,
            isAuth = User?.Identity?.IsAuthenticated == true,
            isMember,
            name = User?.Identity?.Name,
            claims = User?.Claims?.Select(c => new { type = c.Type, value = c.Value }).ToArray()
        });
    }
}