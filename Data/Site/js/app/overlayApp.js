const qs = new URLSearchParams(location.search);
const roomRef = (qs.get("roomRef") || qs.get("room") || qs.get("code") || "").trim();
const debug = qs.get("debug") === "1";
const pollMs = Math.max(1000, Number(qs.get("poll") || 2500));

const els = {
    brandSub: document.getElementById("brandSub"),
    roomBadge: document.getElementById("roomBadge"),
    gameBadge: document.getElementById("gameBadge"),
    statusBadge: document.getElementById("statusBadge"),
    mainStatus: document.getElementById("mainStatus"),
    mainMessage: document.getElementById("mainMessage"),
    mainSub: document.getElementById("mainSub"),
    metaGrid: document.getElementById("metaGrid"),
    boardList: document.getElementById("boardList"),
    playerCountPill: document.getElementById("playerCountPill"),
    feedText: document.getElementById("feedText"),
    refreshPill: document.getElementById("refreshPill"),
    lastUpdated: document.getElementById("lastUpdated")
};

function esc(value) {
    return String(value ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

function titleCase(value) {
    const text = String(value ?? "").trim();
    if (!text) return "";

    return text
        .replaceAll("_", " ")
        .toLowerCase()
        .replace(/\b\w/g, m => m.toUpperCase());
}

function fmtTime(value) {
    const d = value ? new Date(value) : new Date();
    if (Number.isNaN(d.getTime())) return "--";

    return d.toLocaleTimeString([], {
        hour: "2-digit",
        minute: "2-digit",
        second: "2-digit"
    });
}

function setBadge(status) {
    const s = String(status || "waiting").toLowerCase();
    let cls = "waiting";

    if (s.includes("live") || s.includes("active") || s.includes("playing")) cls = "live";
    if (s.includes("offline") || s.includes("error")) cls = "offline";

    els.statusBadge.className = `badge ${cls}`;
    els.statusBadge.innerHTML = `<span class="dot"></span><span>${esc(status)}</span>`;
}

async function api(path) {
    const res = await fetch(path, {
        cache: "no-store",
        credentials: "same-origin"
    });

    const text = await res.text();

    if (!res.ok) {
        throw new Error(`HTTP ${res.status}: ${text.slice(0, 180)}`);
    }

    if (!text.trim()) return null;
    return JSON.parse(text);
}

function normaliseRoomState(data) {
    // Your actual response is: { state: { players: [], game: { id, state: {} } } }
    const roomState = data?.state ?? data;
    const gameWrapper = roomState?.game ?? {};
    const gameState = gameWrapper?.state ?? {};

    const activeGame = gameWrapper?.id
        || roomState?.activeGame
        || "none";

    const players = Array.isArray(roomState?.players)
        ? roomState.players.map(p => ({
            userId: p.userId,
            username: p.name || p.username || p.userName || "Unknown",
            score: p.score || p.points || 0,
            isHost: p.userId && p.userId === roomState.hostUserId,
            isConnected: Boolean(p.isConnected),
            isReady: Boolean(p.isReady)
        }))
        : [];

    let status = "Waiting";
    let message = "Room is idle. Start a game to go live.";
    const meta = {};

    if (gameWrapper?.id && gameState) {
        status = gameState.isSolved ? "Solved" : "Live";

        if (gameWrapper.id === "riddle_me_this") {
            message = gameState.riddle || "Riddle is active.";
            meta.round = gameState.round;
            meta.category = gameState.category;
            meta.guesses = Array.isArray(gameState.recentGuesses) ? gameState.recentGuesses.length : 0;
            meta.solved = gameState.isSolved ? "Yes" : "No";
        } else {
            message = gameState.question || gameState.prompt || gameState.message || `${titleCase(gameWrapper.id)} is active.`;
        }
    }

    return {
        roomName: roomState?.roomName || "Room",
        roomRef,
        activeGame,
        gameId: gameWrapper?.id || "none",
        status,
        message,
        players,
        meta,
        updatedAtUtc: roomState?.updatedAtUtc || roomState?.utc
    };
}

function renderMeta(meta) {
    const entries = Object.entries(meta || {})
        .filter(([_, v]) => v !== null && v !== undefined && String(v).trim() !== "");

    if (!entries.length) {
        els.metaGrid.style.display = "none";
        els.metaGrid.innerHTML = "";
        return;
    }

    els.metaGrid.style.display = "grid";
    els.metaGrid.innerHTML = entries.map(([key, value]) => `
        <div class="metaChip">
            <div class="metaKey">${esc(titleCase(key))}</div>
            <div class="metaVal">${esc(value)}</div>
        </div>
    `).join("");
}

function renderPlayers(players) {
    els.playerCountPill.textContent = `${players.length} Player${players.length === 1 ? "" : "s"}`;

    if (!players.length) {
        els.boardList.innerHTML = `<div class="emptyState">No players to display yet.</div>`;
        return;
    }

    els.boardList.innerHTML = players.map((p, index) => `
        <div class="scoreRow">
            <div class="scoreRank">${index + 1}</div>
            <div class="scorePlayer">
                <div class="scoreName">${esc(p.username)}</div>
                <div class="scoreMeta">${p.isHost ? "Host" : "Player"} • ${p.isConnected ? "Online" : "Offline"}</div>
            </div>
            <div class="scoreValue">
                <div class="scoreNum">${Number(p.score || 0)}</div>
                <div class="scorePts">Pts</div>
            </div>
        </div>
    `).join("");
}

function renderOverlay(data) {
    const vm = normaliseRoomState(data);

    if (debug) {
        console.log("[overlay raw]", data);
        console.log("[overlay vm]", vm);
    }

    document.title = `${vm.roomName} — Misfitz Overlay`;

    els.brandSub.textContent = `${vm.roomName} • ${titleCase(vm.gameId)} overlay`;
    els.roomBadge.textContent = `Room: ${vm.roomRef}`;
    els.gameBadge.textContent = `Game: ${titleCase(vm.gameId)}`;

    setBadge(vm.status);

    els.mainStatus.textContent = vm.status;
    els.mainMessage.textContent = vm.message;
    els.mainSub.textContent = `Tracking ${vm.players.length} player${vm.players.length === 1 ? "" : "s"} in ${vm.roomName}.`;

    renderMeta(vm.meta);
    renderPlayers(vm.players);

    els.feedText.textContent = `${titleCase(vm.gameId)} • ${vm.status}`;
    els.refreshPill.textContent = `${Math.round(pollMs / 1000)}s Poll`;
    els.lastUpdated.textContent = `Last update: ${fmtTime(vm.updatedAtUtc || Date.now())}`;
}

function renderError(message) {
    setBadge("Offline");
    els.mainStatus.textContent = "Offline";
    els.mainMessage.textContent = "Overlay feed unavailable";
    els.mainSub.textContent = message;
    els.feedText.textContent = "Retrying…";
    els.lastUpdated.textContent = `Last update: ${fmtTime(Date.now())}`;
}

async function loadOverlay() {
    if (!roomRef) {
        renderError("Missing roomRef. Use /overlay.html?roomRef=AVVVVSL7");
        return;
    }

    try {
        const data = await api(`/rooms/${encodeURIComponent(roomRef)}/state`);
        renderOverlay(data);
    } catch (err) {
        console.warn("[overlay] failed", err);
        renderError(`Could not load /rooms/${roomRef}/state (${err.message}).`);
    }
}

loadOverlay();
setInterval(loadOverlay, pollMs);
