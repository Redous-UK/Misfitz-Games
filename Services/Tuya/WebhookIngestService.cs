using System.Text.Json;

namespace Misfitz_Games.Services.Tuya;

public class WebhookIngestService(EffectsService effects, ILogger<WebhookIngestService> log)
{
    public record IngestResult(bool Handled, string Message);

    public async Task<IngestResult> ProcessAsync(string source, JsonElement payload, CancellationToken _)
    {
        // Minimal “gift trigger” normalization:
        // We try to extract:
        // - streamerHandle (who)
        // - giftName (what)
        // - count (how many)
        // If your tools send different fields, we can adjust quickly.
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

        log.LogInformation("Webhook ingest {Source}: streamer={Streamer} gift={Gift} count={Count}", source, streamer, gift, count);

        // TEMP: hardcode mapping while you build UI/DB mapping.
        // Later: lookup (streamer, gift) -> EffectId or EffectGroup.
        // For now, example: “Rose” pulses plug1 for 2 seconds.
        if (gift.Equals("Rose", StringComparison.OrdinalIgnoreCase))
        {
            await effects.PulsePlugAsync("plug1", seconds: 2);
            return new IngestResult(true, "Triggered plug1 pulse");
        }

        return new IngestResult(false, "No mapping for gift");
    }

    private static string? TryGetString(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.String
            ? p.GetString()
            : null;

    private static int? TryGetInt(JsonElement e, string name)
        => e.ValueKind == JsonValueKind.Object && e.TryGetProperty(name, out var p) && p.ValueKind == JsonValueKind.Number && p.TryGetInt32(out var v)
            ? v
            : null;
}