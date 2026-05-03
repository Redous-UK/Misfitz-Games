// overlayApp.js
const POLL_MS = 2500;

const qs = new URLSearchParams(location.search);
const roomRef = qs.get("roomId") || qs.get("roomRef") || "";

async function api(path) {
    const res = await fetch(path, {
        cache: "no-store",
        credentials: "same-origin"
    });

    const text = await res.text();

    if (!res.ok) {
        throw new Error(`${res.status}: ${text}`);
    }

    return text ? JSON.parse(text) : null;
}

async function loadOverlay() {
    if (!roomRef) {
        renderError("Missing room. Use ?roomRef=YOUR_ROOM");
        return;
    }

    try {
        const data = await api(`/rooms/${encodeURIComponent(roomRef)}/state`);
        renderOverlay(data);
    } catch (err) {
        console.warn("[overlay] failed", err);
        renderError("Overlay feed unavailable. Retrying...");
    }
}

function renderOverlay(data) {
    const gs = data?.gameState || data?.state || data?.currentGame || {};
    const game = gs?.game || data?.game || "None";
    const active = gs?.isActive || data?.isActive;

    document.getElementById("roomBadge").textContent = `Room: ${roomRef}`;
    document.getElementById("gameBadge").textContent = `Game: ${game}`;
    document.getElementById("mainStatus").textContent = active ? "Live" : "Waiting";
    document.getElementById("mainMessage").textContent =
        gs?.question || gs?.riddle || gs?.prompt || "Waiting for game activity...";

    renderPlayers(data?.players || data?.leaderboard || []);
}

function renderPlayers(players) {
    const el = document.getElementById("boardList");

    if (!Array.isArray(players) || players.length === 0) {
        el.innerHTML = `<div class="emptyState">No players to display yet.</div>`;
        return;
    }

    el.innerHTML = players
        .slice(0, 8)
        .map((p, i) => `
            <div class="scoreRow">
                <div class="scoreRank">${i + 1}</div>
                <div class="scorePlayer">
                    <div class="scoreName">${escapeHtml(p.username || p.userName || p.name || "Unknown")}</div>
                    <div class="scoreMeta">${p.isHost ? "Host" : "Player"}</div>
                </div>
                <div class="scoreValue">
                    <div class="scoreNum">${p.score || p.points || 0}</div>
                    <div class="scorePts">Pts</div>
                </div>
            </div>
        `)
        .join("");
}

function renderError(message) {
    document.getElementById("mainStatus").textContent = "Offline";
    document.getElementById("mainMessage").textContent = message;
}

function escapeHtml(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

loadOverlay();
setInterval(loadOverlay, POLL_MS);