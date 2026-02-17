using System.Text.Json;

namespace Misfitz_Games.Services.Room;

public static class GameStateJson
{
    public static readonly JsonSerializerOptions Options = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public static bool TryDeserialize<T>(object? obj, out T value)
    {
        value = default!;

        if (obj is T t)
        {
            value = t;
            return true;
        }

        if (obj is JsonElement je)
        {
            try
            {
                var v = je.Deserialize<T>(Options);
                if (v is null) return false;
                value = v;
                return true;
            }
            catch
            {
                return false;
            }
        }

        return false;
    }
}