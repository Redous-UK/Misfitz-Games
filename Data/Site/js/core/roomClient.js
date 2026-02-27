import { api } from "./api.js";
import { normalizeGameId, mapGameType } from "./router.js";

export function normalizeRoomState(raw) {
    const state = raw?.state ?? raw;

    const roomName = state?.roomName ?? state?.RoomName ?? "Room";

    // Projector style
    if (state?.game?.id) {
        return {
            roomName,
            gameId: normalizeGameId(state.game.id),
            gameState: state.game.state ?? null,
            raw: state,
        };
    }

    // Legacy style
    if (state?.activeGame != null) {
        const mapped = mapGameType(state.activeGame);
        const gameId = mapped ? normalizeGameId(mapped) : "none";

        let gs = state?.gameState ?? null;
        // optional unwrap if your server nests
        gs = gs?.higherLower ?? gs?.HigherLower ?? gs?.hl ?? gs?.HL ?? gs;

        return { roomName, gameId, gameState: gs, raw: state };
    }

    return { roomName, gameId: "none", gameState: null, raw: state };
}

export async function fetchRoomState(joinedRef) {
    const raw = await api(`/rooms/${encodeURIComponent(joinedRef)}/state`);
    return normalizeRoomState(raw);
}

export async function postPresence(joinedRef) {
    await api(`/rooms/${encodeURIComponent(joinedRef)}/presence`, { method: "POST" });
}

export async function fetchStats(joinedRef) {
    const res = await api(`/rooms/${encodeURIComponent(joinedRef)}/stats`);
    return res?.stats ?? res?.data ?? res;
}

export async function fetchLeaderboard(joinedRef) {
    const res = await api(`/rooms/${encodeURIComponent(joinedRef)}/leaderboard`);
    return res?.leaderboard ?? res?.data ?? res;
}