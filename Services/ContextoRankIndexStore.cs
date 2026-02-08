namespace Misfitz_Games.Services;

public sealed class ContextoRankIndexStore
{
    private readonly Dictionary<Guid, ContextoRankIndex> _byRoom = [];

    public void Set(Guid roomId, ContextoRankIndex index)
        => _byRoom[roomId] = index;

    public bool TryGet(Guid roomId, out ContextoRankIndex index)
        => _byRoom.TryGetValue(roomId, out index!);

    public void Remove(Guid roomId)
        => _byRoom.Remove(roomId);
}