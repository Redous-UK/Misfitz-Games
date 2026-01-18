namespace Misfitz_Games.Models;

public sealed record RoomCreateRequest(string Name);

public sealed record RoomDto(
    Guid RoomId,
    string Name,
    DateTimeOffset CreatedAtUtc
);