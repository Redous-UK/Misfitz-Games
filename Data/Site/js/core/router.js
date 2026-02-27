import { el, pretty, setText } from "../core/dom.js";
try { await postPresence(state.joinedRef); }
catch (e) { console.warn("presence tick failed", e); }
  };

await tick();
state.presenceHandle = setInterval(tick, PRESENCE_MS);
}

async function join() {
    const ref = normalizeRef(el("roomRef")?.value);
    if (!isRoomRef(ref)) return alert("Enter a valid room code (8 digits) or custom code (4-12 A-Z0-9).");

    state.joinedRef = (/^\d{8}$/.test(ref) || ref.includes("-")) ? ref : ref.toUpperCase();

    el("btnJoin") && (el("btnJoin").disabled = true);
    el("btnLeave") && (el("btnLeave").disabled = false);

    const overlayLink = el("overlayLink");
    if (overlayLink) overlayLink.href = `/overlay.html?roomId=${encodeURIComponent(state.joinedRef)}&game=${encodeURIComponent(state.selectedGame)}`;

    await startPresence();
    await refreshAll();

    if (state.pollTimer) clearInterval(state.pollTimer);
    state.pollTimer = setInterval(refreshAll, POLL_MS);
}

function leave() {
    state.joinedRef = "";

    if (state.pollTimer) clearInterval(state.pollTimer);
    state.pollTimer = null;

    if (state.presenceHandle) clearInterval(state.presenceHandle);
    state.presenceHandle = null;

    el("btnJoin") && (el("btnJoin").disabled = false);
    el("btnLeave") && (el("btnLeave").disabled = true);
    el("btnGuess") && (el("btnGuess").disabled = true);

    el("roomLine") && (el("roomLine").textContent = "—");
    setStatus("Not connected", "warn");
    setGameBadge(state.selectedGame, "No game", "warn");

    // hide all panels
    showOnlyPanel("none");
}

function wireTopLevel() {
    el("btnJoin") && (el("btnJoin").onclick = join);
    el("btnLeave") && (el("btnLeave").onclick = leave);
    el("btnRefresh") && (el("btnRefresh").onclick = refreshAll);

    document.addEventListener("visibilitychange", () => {
        if (!document.hidden && state.joinedRef) {
            startPresence();
            refreshAll();
        }
    });
}

window.addEventListener("DOMContentLoaded", async () => {
    try {
        const me = await refreshMe();
        if (!me) return;

        wireTopLevel();
        bindAllGames();

        const qs = new URLSearchParams(location.search);
        const qRoom = qs.get("roomId");
        const qGame = (qs.get("game") || "contexto").toLowerCase();

        state.selectedGame = qGame;
        showOnlyPanel(state.selectedGame);

        if (qRoom) {
            const roomRef = el("roomRef");
            if (roomRef) roomRef.value = qRoom;
            await join();
        }
    } catch {
        location.href = "/user.html";
    }
});