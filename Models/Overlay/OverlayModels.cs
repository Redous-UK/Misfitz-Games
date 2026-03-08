public sealed class RoomOverlayDto
{
    public string RoomCode { get; set; } = "";
    public string Title { get; set; } = "Misfitz Gaming";
    public string Game { get; set; } = "None";
    public string Status { get; set; } = "Waiting";
    public string Message { get; set; } = "";
    public List<OverlayPlayerDto> Players { get; set; } = new();
    public Dictionary<string, object?> Meta { get; set; } = new();
}

public sealed class OverlayPlayerDto
{
    public string UserId { get; set; } = "";
    public string Username { get; set; } = "";
    public int Score { get; set; }
    public bool IsHost { get; set; }
}

public sealed class OverlayEventDto
{
    public string Type { get; set; } = "";
    public string Message { get; set; } = "";
    public DateTimeOffset Timestamp { get; set; }
}

public sealed class OverlayLeaderboardDto
{
    public List<OverlayPlayerDto> TopPlayers { get; set; } = new();
}