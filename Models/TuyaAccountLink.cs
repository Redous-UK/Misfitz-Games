using System;

namespace Misfitz_Games.Models;

public class TuyaAccountLink
{
    public Guid Id { get; set; } = Guid.NewGuid();

    // Your Misfitz user
    public Guid UserId { get; set; }

    // Tuya user id (uid returned by token exchange)
    public string TuyaUid { get; set; } = "";

    // Which Tuya DC this user belongs to (their code is only valid in that DC)
    public string ApiBase { get; set; } = "https://openapi.tuyaeu.com";

    // Store tokens ENCRYPTED (DataProtection)
    public string AccessTokenEnc { get; set; } = "";
    public string RefreshTokenEnc { get; set; } = "";

    public DateTimeOffset AccessTokenExpiresUtc { get; set; }

    public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
    public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
}