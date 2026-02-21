namespace Misfitz_Games.Models
{
    public sealed class TikTokAccountLink
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }                 // Misfitz user
        public string TikTokOpenId { get; set; } = "";   // TikTok identity key (depends on API product)
        public string? TikTokUsername { get; set; }

        public string AccessTokenEnc { get; set; } = "";
        public string RefreshTokenEnc { get; set; } = "";
        public DateTimeOffset AccessTokenExpiresUtc { get; set; }

        public string Scopes { get; set; } = "";         // store granted scopes
        public DateTimeOffset CreatedUtc { get; set; } = DateTimeOffset.UtcNow;
        public DateTimeOffset UpdatedUtc { get; set; } = DateTimeOffset.UtcNow;
    }
}
