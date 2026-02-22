// lobby.js (fixed for lobby2.html)
// Works with the IDs in lobby2.html you pasted.
// Safe to include on other pages: all bindings are null-guarded.

// ----------------------------
// Helpers
// ----------------------------
const el = (id) => document.getElementById(id);

function onClick(id, handler) {
    const node = el(id);
    if (!node) return false;
    node.addEventListener("click", handler);
    return true;
}

const pretty = (x) => JSON.stringify(x, null, 2);

function esc(s) {
    return String(s ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}

// Normalize casing differences between System.Text.Json camelCase vs PascalCase
function pick(o, ...keys) {
    for (const k of keys) {
        if (!o) break;
        if (o[k] !== undefined) return o[k];
    }
    return undefined;
}

function initials(name) {
    const parts = String(name || "").trim().split(/\s+/).filter(Boolean);
    const a = parts[0]?.[0] ?? "?";
    const b = parts.length > 1 ? parts[parts.length - 1][0] : "";
    return (a + b).toUpperCase();
}

function setText(id, text) {
    const n = el(id);
    if (n) n.textContent = text;
}

function setHtml(id, html) {
    const n = el(id);
    if (n) n.innerHTML = html;
}

async function api(path, opts = {}) {
    const fetchOpts = {
        credentials: "include",
        ...opts,
        headers: {
            ...(opts.headers || {}),
            ...(opts.body ? { "Content-Type": "application/json" } : {}),
        },
    };

    const res = await fetch(path, fetchOpts);
    const text = await res.text();
    let payload = null;

    if (text) {
        try {
            payload = JSON.parse(text);
        } catch {
            payload = { _nonJson: true, raw: text };
        }
    }

    if (!res.ok) {
        const msg = payload?.error || payload?.message || `HTTP ${res.status} ${res.statusText} @ ${path}`;
        const err = new Error(msg);
        err.status = res.status;
        err.path = path;
        err.payload = payload;
        err.raw = text;
        throw err;
    }

    return payload;
}

// ----------------------------
// State
// ----------------------------
let myUserId = null;
let myName = null;
let myRole = null;

let activeRoomCode = null;
let currentRoomState = null;

let selectedGameId = null;
let pollHandle = null;

// To prevent duplicate init if script is included twice
let __didInit = false;

// ----------------------------
// Drawer (settings panel)
// ----------------------------
function wireSettingsPanel() {
    const btn = el("settingsBtn");
    const overlay = el("settingsOverlay");
    const panel = el("settingsPanel");
    const closeBtn = el("settingsCloseBtn");

    if (!btn || !overlay || !panel || !closeBtn) return false;

    const open = () => {
        overlay.classList.remove("hidden");
        overlay.setAttribute("aria-hidden", "false");
        btn.setAttribute("aria-expanded", "true");
        // focus panel for accessibility
        panel.focus?.();
    };

    const close = () => {
        overlay.classList.add("hidden");
        overlay.setAttribute("aria-hidden", "true");
        btn.setAttribute("aria-expanded", "false");
        btn.focus?.();
    };

    btn.addEventListener("click", () => {
        const isHidden = overlay.classList.contains("hidden");
        isHidden ? open() : close();
    });

    closeBtn.addEventListener("click", close);

    // click outside panel closes
    overlay.addEventListener("click", (e) => {
        if (e.target === overlay) close();
    });

    // ESC closes
    document.addEventListener("keydown", (e) => {
        if (e.key === "Escape" && !overlay.classList.contains("hidden")) close();
    });

    // Settings options
    const optAutoJoin = el("optAutoJoin");
    if (optAutoJoin) {
        const v = localStorage.getItem("opt:autoJoin");
        optAutoJoin.checked = v === "1";
        optAutoJoin.addEventListener("change", () => {
            localStorage.setItem("opt:autoJoin", optAutoJoin.checked ? "1" : "0");
        });
    }

    const optReconnect = el("optReconnect");
    if (optReconnect) {
        const v = localStorage.getItem("opt:reconnect");
        optReconnect.checked = v !== "0"; // default on
        optReconnect.addEventListener("change", () => {
            localStorage.setItem("opt:reconnect", optReconnect.checked ? "1" : "0");
        });
    }

    el("btnClearLocal")?.addEventListener("click", () => {
        try {
            localStorage.removeItem("roomId");
            localStorage.removeItem("username");
            localStorage.removeItem("activeRoomCode");
        } catch { }
        setText("out", "Cleared local data.");
    });

    el("btnLogout")?.addEventListener("click", async () => {
        try {
            await api("/member/logout", { method: "POST" });
        } catch { }
        location.href = "/user.html";
    });

    return true;
}

// ----------------------------
// Auth
// ----------------------------
function applyRoleUI(role) {
    // lobby2.html doesn’t currently have admin/start/stop buttons,
    // but keep this for future expansion.
    const r = String(role || "").toLowerCase();
    const isAdmin = r === "admin";
    const roleLabel = el("roleLabel");
    if (roleLabel) roleLabel.textContent = role ? String(role) : "—";

    // If you add an admin link later, it can be toggled here.
    const adminLink = el("adminLink");
    if (adminLink) adminLink.classList.toggle("hidden", !isAdmin);
}

async function refreshMe() {
    const me = await api("/member/me");

    // Lobby2 uses #out for quick output
    setText("out", pretty(me));

    const meLabel = el("meLabel");
    if (meLabel) {
        meLabel.textContent = me.isAuth
            ? `👤 ${me.name ?? "Signed in"}`
            : "Not logged in";
    }

    if (!me.isAuth) return me;

    myUserId = me.userId || null;
    myName = me.name || null;
    myRole = me.role || null;

    // keep username box in sync (no forced overwrite if user typed)
    const u = el("username");
    if (u && (!u.value || u.value.trim().length === 0)) {
        // Prefer stored username (guest style) then account name
        u.value = localStorage.getItem("username") || myName || "";
    }

    applyRoleUI(myRole);
    return me;
}

// ----------------------------
// Rooms
// ----------------------------
function roomId() {
    return activeRoomCode;
}

function setActiveRoom(code) {
    const c = (code || "").trim();
    activeRoomCode = c || null;

    // Persist under the key your settings text mentions
    if (activeRoomCode) localStorage.setItem("roomId", activeRoomCode);

    // Update UI
    const badge = el("roomBadge");
    if (badge) badge.textContent = activeRoomCode ? activeRoomCode : "—";

    const stateLine = el("roomStateLine");
    if (stateLine) stateLine.textContent = activeRoomCode ? `Active room: ${activeRoomCode}` : "—";

    // Toggle join/leave
    const btnLeave = el("btnLeave");
    const btnJoin = el("btnJoin");
    if (btnLeave) btnLeave.disabled = !activeRoomCode;
    if (btnJoin) btnJoin.disabled = false;

    // Refresh panels
    refreshState().catch(() => { });
    startPolling();
}

function autoLoadRoomFromStorage() {
    const allow = localStorage.getItem("opt:autoJoin") === "1";
    if (!allow) return false;

    const saved = localStorage.getItem("roomId") || localStorage.getItem("activeRoomCode");
    if (!saved) return false;

    const roomRef = el("roomRef");
    if (roomRef && !roomRef.value) roomRef.value = saved;

    setActiveRoom(saved);
    return true;
}

function filterList(containerId, query) {
    const host = el(containerId);
    if (!host) return;
    const q = String(query || "").trim().toLowerCase();
    host.querySelectorAll("[data-filter]").forEach((row) => {
        const text = String(row.getAttribute("data-filter") || "").toLowerCase();
        row.style.display = !q || text.includes(q) ? "" : "none";
    });
}

async function loadRooms() {
    const host = el("roomsList");
    if (!host) return;

    host.innerHTML = `<div class="muted">Loading rooms…</div>`;

    try {
        const payload = await api("/rooms");
        const rooms = Array.isArray(payload) ? payload : (payload?.rooms ?? []);

        if (!Array.isArray(rooms) || rooms.length === 0) {
            host.innerHTML = `<div class="muted">No rooms yet.</div>`;
            return;
        }

        rooms.sort((a, b) => String(b.createdAtUtc || "").localeCompare(String(a.createdAtUtc || "")));

        host.innerHTML = rooms
            .map((r) => {
                const code = r.roomCode ?? r.code ?? "";
                const name = r.name ?? "Room";
                const isActive = activeRoomCode && activeRoomCode === code;

                const players = Number.isFinite(r.playerCount) ? r.playerCount : null;
                const pText = players === null ? "" : ` • 👥 ${players}`;

                const hasActiveGame = !!r.hasActiveGame;
                const gameText = hasActiveGame
                    ? `🎮 ${esc(String(r.activeGame || "Active"))}`
                    : "No game";

                return `
          <div class="mgRow" data-code="${esc(code)}" data-filter="${esc(name)} ${esc(code)}">
            <div style="display:flex;justify-content:space-between;gap:12px;align-items:center;">
              <div>
                <div style="font-weight:800">${esc(name)}</div>
                <div class="muted" style="font-size:12px">Code: <b>${esc(code)}</b>${pText}</div>
              </div>
              <div style="display:flex;gap:8px;align-items:center;">
                <span class="chip ${isActive ? "" : "subtle"}">${isActive ? "ACTIVE" : "JOIN"}</span>
                <span class="chip subtle">${gameText}</span>
              </div>
            </div>
          </div>
        `;
            })
            .join("");

        host.querySelectorAll("[data-code]").forEach((row) => {
            row.addEventListener("click", () => {
                const code = row.getAttribute("data-code");
                if (!code) return;
                setActiveRoom(code);
            });
        });

        // apply search filter if present
        const q = el("roomSearch")?.value;
        filterList("roomsList", q);
    } catch (e) {
        host.innerHTML = `<div class="muted">Failed to load rooms: ${esc(String(e?.message || e))}</div>`;
    }
}

async function joinRoom() {
    const code = String(el("roomRef")?.value || "").trim();
    const name = String(el("username")?.value || "").trim();

    if (!code) return alert("Enter a room code.");
    if (!name) return alert("Enter a name.");

    localStorage.setItem("roomId", code);
    localStorage.setItem("username", name);

    // Best-effort join endpoint (if present). Even if it fails, we still set active room.
    try {
        await api(`/rooms/${encodeURIComponent(code)}/join`, {
            method: "POST",
            body: JSON.stringify({ name }),
        });
    } catch (e) {
        // Not all deployments have /join; don’t block UX.
        console.warn("join endpoint failed (continuing):", e);
    }

    setActiveRoom(code);
}

async function leaveRoom() {
    const code = roomId();
    if (!code) return;

    try {
        await api(`/rooms/${encodeURIComponent(code)}/leave`, { method: "POST" });
    } catch (e) {
        console.warn("leave endpoint failed:", e);
    }

    activeRoomCode = null;
    setText("roomStateLine", "—");
    setText("roomBadge", "—");
    setHtml("playersList", "");

    const btnLeave = el("btnLeave");
    if (btnLeave) btnLeave.disabled = true;
}

// ----------------------------
// Presence ping
// ----------------------------
async function sendPresenceForRoom(code) {
    if (!code) return;
    try {
        await api(`/rooms/${encodeURIComponent(code)}/presence`, { method: "POST" });
    } catch (e) {
        console.warn("presence ping failed", e);
    }
}

// ----------------------------
// Players render
// ----------------------------
function renderPlayers(roomState, currentUserId) {
    const container = el("playersList");
    if (!container) return;

    const hostId = pick(roomState, "hostUserId", "HostUserId");
    const players = pick(roomState, "players", "Players");
    const arr = Array.isArray(players) ? players : [];

    if (!arr.length) {
        container.innerHTML = `<div class="muted">No players yet.</div>`;
        return;
    }

    const sorted = [...arr].sort((p1, p2) => {
        const aHost = p1.userId === hostId ? -1 : 0;
        const bHost = p2.userId === hostId ? -1 : 0;
        if (aHost !== bHost) return aHost - bHost;

        return String(p1.name || "").localeCompare(String(p2.name || ""));
    });

    container.innerHTML = sorted
        .map((p) => {
            const name = p.name ?? "Player";
            const isHost = p.userId === hostId;
            const isYou = currentUserId && p.userId === currentUserId;
            const connected = p.isConnected !== false;

            const badges = [
                isHost ? `<span class="chip">HOST</span>` : "",
                isYou ? `<span class="chip subtle">YOU</span>` : "",
                !connected ? `<span class="chip subtle">OFFLINE</span>` : "",
            ]
                .filter(Boolean)
                .join(" ");

            const avatar = p.avatarUrl
                ? `<img src="${esc(p.avatarUrl)}" alt="avatar" />`
                : `<span>${esc(initials(name))}</span>`;

            return `
        <div class="mgRow" data-filter="${esc(name)} ${esc(p.userId || "")}">
          <div style="display:flex;gap:10px;align-items:center;">
            <div class="avatar">${avatar}</div>
            <div style="flex:1;min-width:0;">
              <div style="display:flex;justify-content:space-between;gap:10px;align-items:center;">
                <div style="font-weight:800;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${esc(name)}</div>
                <div style="display:flex;gap:6px;flex-wrap:wrap;justify-content:flex-end;">${badges}</div>
              </div>
              <div class="muted" style="font-size:12px;white-space:nowrap;overflow:hidden;text-overflow:ellipsis;">${esc(p.userId || "")}</div>
            </div>
          </div>
        </div>
      `;
        })
        .join("");
}

// ----------------------------
// Room state
// ----------------------------
async function refreshState() {
    const code = roomId();
    if (!code) {
        setHtml("playersList", `<div class="muted">Pick a room to see players.</div>`);
        return;
    }

    const raw = await api(`/rooms/${encodeURIComponent(code)}/state`);
    const state = raw?.state ?? raw;

    currentRoomState = state;

    // Update room line
    const game = pick(state, "activeGame", "ActiveGame");
    const updated = pick(state, "updatedAtUtc", "UpdatedAtUtc");
    const t = updated ? new Date(updated).toLocaleString() : "";
    setText("roomStateLine", `Active room: ${code}${game ? ` • Game: ${game}` : ""}${t ? ` • Updated: ${t}` : ""}`);

    renderPlayers(state, myUserId);
}

// ----------------------------
// Games
// ----------------------------
function setSelectedGame(id, gameObj) {
    selectedGameId = id;

    document.querySelectorAll(".game-tile.is-selected").forEach((x) => x.classList.remove("is-selected"));
    const tile = document.querySelector(`.game-tile[data-game="${CSS.escape(id)}"]`);
    if (tile) tile.classList.add("is-selected");

    // Update Active Game card
    const card = el("activeGameCard");
    if (card) {
        if (!gameObj) {
            card.textContent = `Selected: ${id}`;
        } else {
            card.innerHTML = `
        <div style="font-weight:900">${esc(gameObj.name || id)}</div>
        <div class="muted" style="margin-top:4px;">${esc(gameObj.description || "")}</div>
        <div class="muted" style="margin-top:8px;font-size:12px;">Game id: <b>${esc(id)}</b></div>
      `;
        }
    }
}

async function loadGames() {
    const grid = el("gamesGrid");
    if (!grid) return;

    grid.innerHTML = `<div class="muted">Loading games…</div>`;

    const payload = await api("/catalog/games");
    const games = payload?.games;
    if (!Array.isArray(games)) {
        grid.innerHTML = `<div class="muted">Invalid games payload.</div>`;
        return;
    }

    grid.innerHTML = "";

    for (const item of games) {
        const tile = document.createElement("div");
        tile.className = "game-tile";
        tile.dataset.game = item.id;

        if (!item.enabled) {
            tile.setAttribute("aria-disabled", "true");
            tile.classList.add("is-disabled");
        }

        const bg = document.createElement("div");
        bg.className = "game-bg";
        const imageUrl = item.image || "/assets/Games/placeholder.png";
        bg.style.backgroundImage = `url('${imageUrl}')`;

        const inner = document.createElement("div");
        inner.className = "game-inner";

        const left = document.createElement("div");

        const title = document.createElement("div");
        title.className = "game-title";
        title.textContent = item.name || item.id;

        const desc = document.createElement("div");
        desc.className = "game-desc";
        desc.textContent = item.description || "";

        left.appendChild(title);
        left.appendChild(desc);

        const badge = document.createElement("span");
        badge.className = "tile-badge " + (item.enabled ? "ok" : "soon");
        badge.textContent = item.enabled ? "Ready" : "Soon";

        inner.appendChild(left);
        inner.appendChild(badge);

        tile.appendChild(bg);
        tile.appendChild(inner);

        tile.addEventListener("click", () => {
            if (!item.enabled) return;
            setSelectedGame(item.id, item);
        });

        grid.appendChild(tile);
    }

    // select default (first enabled)
    const firstEnabled = games.find((g) => g.enabled);
    if (firstEnabled) setSelectedGame(firstEnabled.id, firstEnabled);

    // apply game search filter if present
    const q = el("gameSearch")?.value;
    filterGames(q);
}

function filterGames(query) {
    const grid = el("gamesGrid");
    if (!grid) return;
    const q = String(query || "").trim().toLowerCase();
    grid.querySelectorAll(".game-tile").forEach((tile) => {
        const id = tile.getAttribute("data-game") || "";
        const title = tile.querySelector(".game-title")?.textContent || "";
        const desc = tile.querySelector(".game-desc")?.textContent || "";
        const hay = `${id} ${title} ${desc}`.toLowerCase();
        tile.style.display = !q || hay.includes(q) ? "" : "none";
    });
}

// ----------------------------
// Polling
// ----------------------------
function startPolling() {
    if (pollHandle) return;
    pollHandle = setInterval(() => {
        if (document.hidden) return;
        const code = roomId();
        if (!code) return;

        sendPresenceForRoom(code);
        refreshState().catch(() => { });
    }, 5000);
}

// ----------------------------
// Wiring / init
// ----------------------------
async function init() {
    if (__didInit) return;
    __didInit = true;

    wireSettingsPanel();

    onClick("btnRefreshMe", () => refreshMe().catch((e) => setText("out", String(e?.message || e))));
    onClick("btnRefreshRooms", () => loadRooms().catch((e) => setText("out", String(e?.message || e))));
    onClick("btnRefreshGames", () => loadGames().catch((e) => setText("out", String(e?.message || e))));
    onClick("btnJoin", () => joinRoom().catch((e) => alert(String(e?.message || e))));
    onClick("btnLeave", () => leaveRoom().catch((e) => alert(String(e?.message || e))));

    // Search wiring
    el("roomSearch")?.addEventListener("input", (e) => filterList("roomsList", e.target.value));
    el("gameSearch")?.addEventListener("input", (e) => filterGames(e.target.value));

    // Status badge (we don’t have SignalR wiring in this snippet, so keep it honest)
    const status = el("statusBadge");
    if (status) {
        status.textContent = "Connected";
        status.classList.remove("warn");
        status.classList.add("ok");
    }

    // Auth must be valid
    let me;
    try {
        me = await refreshMe();
    } catch (e) {
        console.warn("refreshMe failed:", e);
        location.href = "/user.html";
        return;
    }

    if (!me.isAuth) {
        location.href = "/user.html";
        return;
    }

    // Restore stored username if present
    const storedName = localStorage.getItem("username");
    const u = el("username");
    if (u && storedName && (!u.value || !u.value.trim())) u.value = storedName;

    // Games + rooms
    await Promise.all([loadGames(), loadRooms()]);

    // Auto-join last room if enabled
    autoLoadRoomFromStorage();

    startPolling();
}

document.addEventListener("DOMContentLoaded", () => {
    init().catch((e) => {
        console.error("Lobby init failed:", e);
        location.href = "/user.html";
    });
});
