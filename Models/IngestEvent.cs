namespace Misfitz_Games.Models;

public sealed record IngestEvent(
    string RoomId,
    string Platform,
    string ChannelId,
    string UserId,
    string Username,
    string Type,
    string Message,
    DateTimeOffset TsUtc
);