using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Misfitz_Games.Services.Effects;

public sealed class WebhookIngestService(IServiceScopeFactory scopeFactory, ILogger<WebhookIngestService> log)
{
    private readonly IServiceScopeFactory _scopeFactory = scopeFactory;
    private readonly ILogger<WebhookIngestService> _log = log;

    public sealed record IngestResult(bool Handled, string Message);

    public async Task<IngestResult> ProcessAsync(
        string source,
        JsonElement payload,
        CancellationToken ct = default)
    {
        // Minimal “gift trigger” normalization:
        var streamer = TryGetString(payload, "streamer")
                    ?? TryGetString(payload, "username")
                    ?? TryGetString(payload, "tiktokUser")
                    ?? "unknown";

        var gift = TryGetString(payload, "gift")
                ?? TryGetString(payload, "giftName")
                ?? TryGetString(payload, "event")
                ?? "unknown";

        var count = TryGetInt(payload, "count")
                 ?? TryGetInt(payload, "repeatCount")
                 ?? 1;

        _log.LogInformation(
            "Webhook ingest {Source}: streamer={Streamer} gift={Gift} count={Count}",
            source, streamer, gift, count);

        // TEMP: hardcode mapping while you build UI/DB mapping.
        if (gift.Equals("Rose", StringComparison.OrdinalIgnoreCase))
        {
            using var scope = _scopeFactory.CreateScope();
            var effects = scope.ServiceProvider.GetRequiredService<EffectsService>();

            await effects.PulsePlugAsync("plug1", seconds: 2, ct);
            return new IngestResult(true, "Triggered plug1 pulse");
        }

        return new IngestResult(false, "No mapping for gift");
    }

    private static string? TryGetString(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(name, out var p)
        && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static int? TryGetInt(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object
        && e.TryGetProperty(name, out var p)
        && p.ValueKind == JsonValueKind.Number
        && p.TryGetInt32(out var v)
            ? v
            : null;
}