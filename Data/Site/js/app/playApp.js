import { el, pretty, setText } from "../core/dom.js";
import { api } from "../core/api.js";
import { setStatus, setGameBadge } from "../core/badges.js";
import { showOnlyPanel, panelExists, normalizeGameId } from "../core/router.js";
import { fetchRoomState, fetchStats, fetchLeaderboard, postPresence } from "../core/roomClient.js";

import { bindContexto, renderContexto } from "../games/contexto.js";
import { bindHangman, renderHangman } from "../games/hangman.js";
import { bindTrivia, renderTrivia } from "../games/trivia.js";
import { bindHigherLower, renderHigherLower } from "../games/higherlower.js";
import { bindRiddleMeThis, renderRiddleMeThis } from "../games/riddle.js";


const POLL_MS = 1200;
const PRESENCE_MS = 5000;

const state = {
    myUserId: null,
    myName: null,
    myRole: null,
    joinedRef: "",
    selectedGame: "contexto",
    pollTimer: null,
    presenceHandle: null,
};

function normalizeRef(s) { return (s || "").trim(); }
function isRoomRef(s) {
    s = normalizeRef(s);
    if (!s) return false;
    if (/^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[1-5][0-9a-fA-F]{3}-[89abAB][0-9a-fA-F]{3}-[0-9a-fA-F]{12}$/.test(s)) return true;
    if (/^\d{8}$/.test(s)) return true;
    if (/^[A-Za-z0-9]{4,12}$/.test(s)) return true;
    return false;
}

async function refreshMe() {
    const me = await api("/member/me");
    if (!me.isAuth) {
        location.href = "/user.html";
        return null;
    }

    state.myUserId = me.userId || null;
    state.myName = me.name || "Player";
    state.myRole = me.role || "Guest";

    const meLabel = el("meLabel");
    if (meLabel) meLabel.textContent = `👤 ${state.myName} (${state.myRole})`;

    const input = el("username");
    if (input) {
        input.value = state.myName;
        input.readOnly = true;
    }

    localStorage.setItem("myName", state.myName);
    localStorage.setItem("myUserId", state.myUserId);
    localStorage.setItem("myRole", state.myRole);

    return me;
}

// Actions used by games
const actions = {
    async sendContextoGuess() {
        if (!state.joinedRef) return;
        const message = (el("guess")?.value || "").trim();
        if (!message) return;

        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/contexto/guess`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ guess: message })
        });

        const guessEl = el("guess");
        if (guessEl) guessEl.value = "";

        await refreshAll();
    },

    async sendHangmanGuess() {
        if (!state.joinedRef) return;
        const value = (el("hangmanGuessInput")?.value || "").trim();
        if (!value) return;

        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/hangman/guess`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ value })
        });

        el("hangmanGuessInput").value = "";
        await refreshAll();
    },

    async hlStart() {
        if (!state.joinedRef) return;
        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/higher_lower/start`, { method: "POST" });
        await refreshAll();
    },
    async hlGuess(choice) {
        if (!state.joinedRef) return;
        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/higher_lower/guess`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ choice })
        });
        await refreshAll();
    },
    async hlContinue() {
        if (!state.joinedRef) return;
        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/higher_lower/continue`, { method: "POST" });
        await refreshAll();
    },
    async hlStop() {
        if (!state.joinedRef) return;
        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/stop`, { method: "POST" });
        await refreshAll();
    },
    async rmtLoadCategories() {
        if (!state.joinedRef) return;

        const data = await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/riddle_me_this/categories`);
        const select = el("rmtCategory");
        if (!select) return;

        const categories = Array.isArray(data?.categories) ? data.categories : [];
        const saved = localStorage.getItem("rmtCategory") || "";

        select.innerHTML = `<option value="">Any Category</option>`;

        for (const cat of categories) {
            const opt = document.createElement("option");
            opt.value = cat;
            opt.textContent = cat;
            select.appendChild(opt);
        }

        if ([...select.options].some(o => o.value === saved)) {
            select.value = saved;
        }
    },

    async rmtStart() {
        if (!state.joinedRef) return;

        const category = (el("rmtCategory")?.value || "").trim();

        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/riddle_me_this/start`, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ category: category || null })
        });

        await refreshAll();
    },

    async rmtNext() {
        if (!state.joinedRef) return;

        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/riddle_me_this/next`, {
            method: "POST"
        });

        await refreshAll();
    },

    async rmtReveal() {
        if (!state.joinedRef) return;

        await api(`/rooms/${encodeURIComponent(state.joinedRef)}/games/riddle_me_this/reveal`, {
            method: "POST"
        });

        await refreshAll();
    },

    async rmtRefresh() {
        if (!state.joinedRef) return;
        await refreshAll();
    }
};

// Game registry
const games = {
    contexto: { bind: bindContexto, render: renderContexto },
    hangman: { bind: bindHangman, render: renderHangman },
    trivia: { bind: bindTrivia, render: renderTrivia },
    higher_lower: { bind: bindHigherLower, render: (gs, raw) => renderHigherLower(gs, raw, state.joinedRef) },
    riddle: { bind: bindRiddle, render: renderRiddle }
};

function bindAllGames() {
    const ctx = { actions };
    Object.values(games).forEach(g => g.bind?.(ctx));
}

async function refreshAll() {
    if (!state.joinedRef) return;

    try {
        const normalized = await fetchRoomState(state.joinedRef);

        el("roomLine").textContent = `${normalized.roomName} (${state.joinedRef})`;

        const gameIdFromServer = normalized.gameId;
        const gs = normalized.gameState;

        const gameIdToRender = (gameIdFromServer && gameIdFromServer !== "none")
            ? gameIdFromServer
            : state.selectedGame;

        state.selectedGame = gameIdToRender;

        showOnlyPanel(gameIdToRender);

        // enable/disable Contexto guess button only
        el("btnGuess") && (el("btnGuess").disabled = !(state.joinedRef && gameIdToRender === "contexto"));

        if (!gameIdFromServer || gameIdFromServer === "none") {
            setGameBadge(state.selectedGame, `No game (selected: ${gameIdToRender})`, "warn");
        } else {
            setGameBadge(state.selectedGame, gameIdToRender, "ok");
        }

        const key = normalizeGameId(gameIdToRender);
        if (panelExists(key)) {
            const renderer = games[key]?.render;
            if (renderer) renderer(gs, normalized.raw);
        }

        const [lb, stats] = await Promise.all([
            fetchLeaderboard(state.joinedRef),
            fetchStats(state.joinedRef)
        ]);

        setText("statsOut", pretty(stats));
        renderLeaderboard(lb);

        setStatus("Connected", "ok");
    } catch (e) {
        console.error("refreshAll failed", e);
        setStatus("Offline / Room not found", "bad");
    }
}

function renderLeaderboard(lb) {
    const ul = el("leaderboard");
    if (!ul) return;
    ul.innerHTML = "";

    const top = lb?.top ?? lb?.Top ?? lb?.leaderboard?.top ?? lb?.leaderboard?.Top ?? [];
    const arr = Array.isArray(top) ? top : [];

    if (arr.length === 0) {
        ul.innerHTML = `<li class="small">No scores yet…</li>`;
        return;
    }

    for (const row of arr.slice(0, 10)) {
        const userId = (row.userId ?? row.UserId ?? "user").toString();
        const score = row.score ?? row.Score ?? 0;

        const li = document.createElement("li");
        li.className = "item";
        li.innerHTML = `
      <div>
        <div class="who">${userId}</div>
        <div class="small">score</div>
      </div>
      <div class="right">
        <div class="small">&nbsp;</div>
        <div class="who">${score}</div>
      </div>
    `;
        ul.appendChild(li);
    }
}

function onPanelChanged(panelId) {
    if (panelId === "panel-riddlemethis" && window.riddleMeThisGame) {
        window.riddleMeThisGame.init();
    }
}

async function startPresence() {
    if (!state.joinedRef) return;
    if (state.presenceHandle) clearInterval(state.presenceHandle);

    const tick = async () => {
        if (document.hidden) return;
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