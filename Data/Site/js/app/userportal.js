(() => {
    const M = window.Misfitz;

    function isAdmin(data) {
        return String(data?.user?.role || "").toLowerCase() === "admin";
    }

    function setValue(id, value) {
        const node = M.el(id);
        if (!node) return;
        node.value = value ?? "";
    }

    function setChecked(id, value) {
        const node = M.el(id);
        if (!node) return;
        node.checked = !!value;
    }

    function setText(id, value) {
        const node = M.el(id);
        if (!node) return;
        node.textContent = value ?? "";
    }

    function setAdminOutput(value) {
        const node = M.el("adminOutput");
        if (!node) return;

        node.textContent = typeof value === "string"
            ? value
            : JSON.stringify(value, null, 2);
    }

    function escAdmin(value) {
        return String(value ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#39;");
    }

    function prettyAdmin(value) {
        return typeof value === "string" ? value : JSON.stringify(value, null, 2);
    }

    function getInitials(name) {
        const parts = (name || "").trim().split(/\s+/).filter(Boolean);
        if (!parts.length) return "MG";
        if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
        return (parts[0][0] + parts[1][0]).toUpperCase();
    }

    function openTab(tab) {
        if (tab === "admin" && !document.body.classList.contains("is-admin")) {
            tab = "overview";
        }

        M.qsa("#portalNav button[data-tab]").forEach(btn => {
            btn.classList.toggle("active", btn.dataset.tab === tab);
        });

        M.qsa(".tab-panel").forEach(panel => {
            panel.classList.toggle("active", panel.dataset.panel === tab);
        });

        window.scrollTo({ top: 0, behavior: "smooth" });
    }

    function bindTabs() {
        M.qsa("#portalNav button[data-tab]").forEach(btn => {
            btn.addEventListener("click", () => openTab(btn.dataset.tab));
        });

        M.qsa("[data-jump]").forEach(btn => {
            btn.addEventListener("click", () => openTab(btn.dataset.jump));
        });
    }

    function readProfileForm() {
        return {
            displayName: M.el("profileDisplayName")?.value?.trim() ?? "",
            email: M.el("profileEmail")?.value?.trim() ?? "",
            username: M.el("profileUsername")?.value?.trim() ?? "",
            bio: M.el("profileBio")?.value?.trim() ?? "",
            avatarUrl: M.el("profileAvatarUrl")?.value?.trim() ?? "",
            isProfilePublic: !!M.el("profileIsPublic")?.checked,
            showAvatarInRoom: !!M.el("profileShowAvatar")?.checked,
            showOnlineStatus: !!M.el("profileShowOnline")?.checked
        };
    }

    function readRoomForm() {
        return {
            roomName: M.el("roomName")?.value?.trim() ?? "",
            description: M.el("roomDescription")?.value?.trim() ?? "",
            defaultGame: M.el("roomDefaultGame")?.value?.trim() ?? "None",
            autoRestore: !!M.el("roomAutoRestore")?.checked,
            allowGuests: !!M.el("roomAllowGuests")?.checked,
            overlaysEnabled: !!M.el("roomOverlaysEnabled")?.checked,
            isPrivate: !!M.el("roomIsPrivate")?.checked
        };
    }

    function readPreferencesForm() {
        return {
            emailAlerts: !!M.el("prefEmailAlerts")?.checked,
            securityAlerts: !!M.el("prefSecurityAlerts")?.checked,
            gameReminders: !!M.el("prefGameReminders")?.checked,
            digestFrequency: M.el("prefDigestFrequency")?.value?.trim() ?? "Weekly",
            timezone: M.el("prefTimezone")?.value?.trim() ?? "Europe/London",
            theme: M.el("prefTheme")?.value?.trim() ?? "Dark",
            accent: M.el("prefAccent")?.value?.trim() ?? "Misfitz",
            compactLayout: !!M.el("prefCompactLayout")?.checked,
            showTips: !!M.el("prefShowTips")?.checked,
            publicRoomListing: !!M.el("prefPublicRoomListing")?.checked,
            showGameplayStats: !!M.el("prefShowGameplayStats")?.checked
        };
    }

    let portalPath = "";
    let latestPortalData = null;

    function renderOverview(data) {
        const displayName = data.user?.displayName || data.user?.username || "User";
        const bio = data.user?.bio || "Owner of one persistent room with account-linked settings, stream profile, and gameplay preferences.";
        const email = data.user?.email || "-";
        const roomRef = data.room?.roomRef || "";
        const roomPath = data.room?.portalPath || `/play.html?roomRef=${encodeURIComponent(roomRef)}`;
        const role = data.user?.role || "member";
        const initials = getInitials(displayName);

        portalPath = roomPath;

        setText("heroDisplayName", displayName);
        setText("heroBio", bio);
        setText("heroEmail", email);
        setText("heroRoomPath", roomPath);
        setText("overviewEmail", email);
        setText("overviewRoomPath", roomPath);
        setText("overviewAccountType", `${role.charAt(0).toUpperCase()}${role.slice(1)} with room ownership enabled`);
        setText("overviewDefaultGame", `Default game: ${data.room?.defaultGame || "None"}`);
        setText("overviewVisibility", data.user?.isProfilePublic ? "Public profile" : "Private profile");
        setText("heroRoleChip", `${role.charAt(0).toUpperCase()}${role.slice(1)}`);
        setText("sidebarRole", `${role.charAt(0).toUpperCase()}${role.slice(1)} / Host`);
        setText("overviewRoomStatus", data.room?.isPrivate ? "Private" : "Public");
        setText("overviewPortalState", "Live");
        setText("heroAvatar", initials);
        setText("heroInitials", initials);
    }

    function renderProfile(data) {
        setValue("profileDisplayName", data.user?.displayName);
        setValue("profileEmail", data.user?.email);
        setValue("profileUsername", data.user?.username);
        setValue("profileBio", data.user?.bio);
        setValue("profileAvatarUrl", data.user?.avatarUrl);

        setChecked("profileIsPublic", data.user?.isProfilePublic);
        setChecked("profileShowAvatar", data.user?.showAvatarInRoom);
        setChecked("profileShowOnline", data.user?.showOnlineStatus);
    }

    function renderRoom(data) {
        setValue("roomName", data.room?.roomName);
        setValue("roomDescription", data.room?.description);
        setValue("roomDefaultGame", data.room?.defaultGame);

        setChecked("roomAutoRestore", data.room?.autoRestore);
        setChecked("roomAllowGuests", data.room?.allowGuests);
        setChecked("roomOverlaysEnabled", data.room?.overlaysEnabled);
        setChecked("roomIsPrivate", data.room?.isPrivate);
    }

    function renderPreferences(data) {
        setChecked("prefEmailAlerts", data.preferences?.emailAlerts);
        setChecked("prefSecurityAlerts", data.preferences?.securityAlerts);
        setChecked("prefGameReminders", data.preferences?.gameReminders);

        setValue("prefDigestFrequency", data.preferences?.digestFrequency);
        setValue("prefTimezone", data.preferences?.timezone);
        setValue("prefTheme", data.preferences?.theme);
        setValue("prefAccent", data.preferences?.accent);

        setChecked("prefCompactLayout", data.preferences?.compactLayout);
        setChecked("prefShowTips", data.preferences?.showTips);
        setChecked("prefPublicRoomListing", data.preferences?.publicRoomListing);
        setChecked("prefShowGameplayStats", data.preferences?.showGameplayStats);
    }

    function renderAdmin(data) {
        const admin = isAdmin(data);

        document.body.classList.toggle("is-admin", admin);

        M.qsa("[data-admin-only]").forEach(el => {
            el.hidden = !admin;
        });

        if (!admin) return;

        setText("adminStatus", "Admin tools enabled.");
    }

    function renderPortal(data) {
        latestPortalData = data;
        renderOverview(data);
        renderProfile(data);
        renderRoom(data);
        renderPreferences(data);
        renderAdmin(data);
    }

    async function adminApi(path, options = {}) {
        if (typeof M.api === "function") {
            return await M.api(path, options);
        }

        const res = await fetch(path, {
            credentials: "same-origin",
            cache: "no-store",
            ...options
        });

        const text = await res.text();

        if (!res.ok) {
            throw new Error(`${res.status}: ${text}`);
        }

        return text ? JSON.parse(text) : null;
    }

    function requireAdminAction() {
        if (document.body.classList.contains("is-admin")) return true;
        M.setStatus("Admin access required.", "bad");
        setAdminOutput("Admin access required.");
        return false;
    }

    function normaliseGameId(value) {
        const game = String(value || "")
            .trim()
            .toLowerCase()
            .replaceAll("-", "_")
            .replaceAll(" ", "_");

        if (game === "riddlemethis" || game === "riddle_me_this") return "riddle_me_this";
        if (game === "higherlower" || game === "higher_or_lower") return "higher_lower";
        if (game === "daily_trivia") return "trivia";
        if (game === "deal_or_no_deal") return "deal";

        return game || "contexto";
    }

    function getAdminRoomRef() {
        return M.el("adminRoomRef")?.value?.trim()
            || latestPortalData?.room?.roomRef
            || latestPortalData?.room?.code
            || "";
    }

    async function postRoomGame(roomRef, gamePath, body) {
        return await adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/${gamePath}`, {
            method: "POST",
            headers: body ? { "Content-Type": "application/json" } : undefined,
            body: body ? JSON.stringify(body) : undefined
        });
    }

    function showAdminGameSettings(gameId) {
        const selected = normaliseGameId(gameId);
        const ids = ["contexto", "hangman", "trivia", "higher_lower", "deal", "riddle_me_this"];

        for (const id of ids) {
            const node = M.el(`adminSettings_${id}`);
            if (!node) continue;
            node.hidden = id !== selected;
        }

        setText("adminSelectedGamePill", `Selected: ${selected}`);
    }

    function getAdminGameConfig(roomRef) {
        const game = normaliseGameId(M.el("adminGameSelect")?.value);

        if (game === "contexto") {
            return {
                start: async () => {
                    const secretWord = M.el("adminContextoSecretWord")?.value?.trim();
                    if (secretWord) return postRoomGame(roomRef, "contexto/start", { secretWord });
                    return postRoomGame(roomRef, "contexto/next");
                },
                newGame: async () => postRoomGame(roomRef, "contexto/next"),
                stop: async () => adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/stop`, { method: "POST" })
            };
        }

        if (game === "hangman") {
            return {
                start: async () => {
                    const word = M.el("adminHangmanWord")?.value?.trim() || prompt("Hangman word:")?.trim();
                    if (!word) throw new Error("Word required.");
                    const maxWrong = Number(M.el("adminHangmanMaxWrong")?.value || 6);
                    return postRoomGame(roomRef, "hangman/start", {
                        word,
                        maxWrong: Number.isFinite(maxWrong) ? maxWrong : 6
                    });
                },
                stop: async () => adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/stop`, { method: "POST" })
            };
        }

        if (game === "higher_lower") {
            return {
                start: async () => postRoomGame(roomRef, "higher_lower/start"),
                higher: async () => postRoomGame(roomRef, "higher_lower/guess", { choice: "higher" }),
                lower: async () => postRoomGame(roomRef, "higher_lower/guess", { choice: "lower" }),
                cont: async () => postRoomGame(roomRef, "higher_lower/continue"),
                stop: async () => adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/stop`, { method: "POST" })
            };
        }

        if (game === "riddle_me_this") {
            return {
                start: async () => postRoomGame(roomRef, "riddle_me_this/start"),
                showAnswer: async () => postRoomGame(roomRef, "riddle_me_this/answer"),
                stop: async () => adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/stop`, { method: "POST" })
            };
        }

        return {
            start: async () => postRoomGame(roomRef, `${game}/start`),
            stop: async () => adminApi(`/rooms/${encodeURIComponent(roomRef)}/games/stop`, { method: "POST" })
        };
    }

    async function runAdminGameAction(actionName) {
        const roomRef = getAdminRoomRef();
        if (!roomRef) {
            setAdminOutput("Enter a room ref first.");
            return;
        }

        try {
            const cfg = getAdminGameConfig(roomRef);
            const action = cfg[actionName];

            if (!action) {
                const msg = `${actionName} is not implemented for ${normaliseGameId(M.el("adminGameSelect")?.value)}.`;
                setAdminOutput({ ok: false, error: msg });
                setText("adminRoomStateOut", msg);
                return;
            }

            M.setStatus(`Running ${actionName}...`);
            const result = await action();
            setAdminOutput(result);
            setText("adminRoomStateOut", prettyAdmin(result));
            M.setStatus("Game action complete.", "good");
        } catch (err) {
            setAdminOutput(err.message || String(err));
            setText("adminRoomStateOut", err.message || String(err));
            M.setStatus("Game action failed.", "bad");
        }
    }

    async function loadAdminRoomStateInline() {
        const roomRef = getAdminRoomRef();
        if (!roomRef) {
            setAdminOutput("Enter a room ref first.");
            return;
        }

        try {
            const state = await adminApi(`/rooms/${encodeURIComponent(roomRef)}/state`);
            setText("adminRoomStateOut", prettyAdmin(state));
            setAdminOutput(state);
        } catch (err) {
            setText("adminRoomStateOut", err.message || String(err));
            setAdminOutput(err.message || String(err));
        }
    }

    function renderAdminRoomSection() {
        const host = M.el("adminSectionHost");
        if (!host) return;

        const roomRef = getAdminRoomRef();

        host.innerHTML = `
            <div class="card span-12">
                <div class="section-head">
                    <div>
                        <h4>Room Admin</h4>
                        <p>Open/close rooms, view state, start games, and show game-specific tools.</p>
                    </div>
                    <span class="pill" id="adminSelectedGamePill">Selected: contexto</span>
                </div>

                <div class="portal-grid">
                    <div class="card span-6">
                        <h4>Room</h4>
                        <div class="field">
                            <label for="adminRoomRef">Room Ref</label>
                            <input id="adminRoomRef" value="${escAdmin(roomRef)}" placeholder="AVVVVSL7" />
                        </div>

                        <div class="admin-grid">
                            <button class="btn" id="btnAdminOpenRoom" type="button">Open Room</button>
                            <button class="btn" id="btnAdminOpenOverlay" type="button">Open Overlay</button>
                            <button class="btn" id="btnAdminOpenRoomState" type="button">View State</button>
                            <button class="btn" id="btnAdminRefreshRoomState" type="button">Load State Below</button>
                        </div>
                    </div>

                    <div class="card span-6">
                        <h4>Game Controls</h4>
                        <div class="field">
                            <label for="adminGameSelect">Game</label>
                            <select id="adminGameSelect">
                                <option value="contexto">Contexto</option>
                                <option value="hangman">Hangman</option>
                                <option value="trivia">Daily Trivia</option>
                                <option value="higher_lower">Higher or Lower</option>
                                <option value="deal">Deal or No Deal</option>
                                <option value="riddle_me_this">Riddle Me This</option>
                                <option value="Riddle-me-this">Riddle-me-this</option>
                            </select>
                        </div>

                        <div class="admin-grid">
                            <button class="btn primary" id="btnAdminStartGame" type="button">Start</button>
                            <button class="btn" id="btnAdminStopGame" type="button">Stop</button>
                            <button class="btn" id="btnAdminNewGame" type="button">New Game</button>
                            <button class="btn" id="btnAdminLeaderboard" type="button">Leaderboard</button>
                        </div>
                    </div>
                </div>

                <div id="adminGameSettings" style="margin-top:16px;">
                    <div id="adminSettings_contexto" class="card">
                        <h4>Contexto Settings</h4>
                        <div class="field">
                            <label for="adminContextoSecretWord">Secret word optional</label>
                            <input id="adminContextoSecretWord" placeholder="Leave blank for random/next" />
                        </div>
                    </div>

                    <div id="adminSettings_hangman" class="card" hidden>
                        <h4>Hangman Settings</h4>
                        <div class="field">
                            <label for="adminHangmanWord">Word</label>
                            <input id="adminHangmanWord" placeholder="Hangman word" />
                        </div>
                        <div class="field">
                            <label for="adminHangmanMaxWrong">Max wrong guesses</label>
                            <input id="adminHangmanMaxWrong" type="number" min="1" max="10" value="6" />
                        </div>
                    </div>

                    <div id="adminSettings_trivia" class="card" hidden>
                        <h4>Daily Trivia Settings</h4>
                        <p class="muted">No settings yet. Start will call <span class="kbd">/rooms/{roomRef}/games/trivia/start</span>.</p>
                    </div>

                    <div id="adminSettings_higher_lower" class="card" hidden>
                        <h4>Higher or Lower Controls</h4>
                        <p class="muted">Use these after starting Higher or Lower.</p>
                        <div class="admin-grid">
                            <button class="btn" id="btnAdminHigher" type="button">Higher</button>
                            <button class="btn" id="btnAdminLower" type="button">Lower</button>
                            <button class="btn" id="btnAdminHigherLowerContinue" type="button">Continue</button>
                        </div>
                    </div>

                    <div id="adminSettings_deal" class="card" hidden>
                        <h4>Deal or No Deal Settings</h4>
                        <p class="muted">No settings yet. Start will call <span class="kbd">/rooms/{roomRef}/games/deal/start</span>.</p>
                    </div>

                    <div id="adminSettings_riddle_me_this" class="card" hidden>
                        <div class="section-head">
                            <div>
                                <h4>Riddle Me This</h4>
                                <p>Import riddles before starting the game, then use riddle-specific controls.</p>
                            </div>
                        </div>

                        <div class="field">
                            <label for="adminRiddleCategory">Import Category</label>
                            <select id="adminRiddleCategory">
                                <option value="science">science</option>
                                <option value="animal">animal</option>
                                <option value="food">food</option>
                                <option value="history">history</option>
                                <option value="general">general</option>
                                <option value="logic">logic</option>
                                <option value="math">math</option>
                                <option value="funny">funny</option>
                            </select>
                        </div>

                        <div class="admin-grid">
                            <button class="btn primary" id="btnAdminImportRiddleCategory" type="button">Import Category</button>
                            <button class="btn" id="btnAdminImportRiddlesAll" type="button">Import All Categories</button>
                            <button class="btn" id="btnAdminShowRiddleAnswer" type="button">Show Answer</button>
                        </div>

                        <div id="adminRiddleImportOut" class="muted" style="margin-top:10px;">No imports yet.</div>
                    </div>
                </div>

                <pre id="adminRoomStateOut" class="admin-output" style="margin-top:16px;">Room output will appear here...</pre>
            </div>
        `;

        wireAdminRoomSectionEvents();
        showAdminGameSettings("contexto");
    }

    function wireAdminRoomSectionEvents() {
        M.el("adminGameSelect")?.addEventListener("change", e => {
            showAdminGameSettings(e.target.value);
        });

        M.el("btnAdminOpenRoom")?.addEventListener("click", () => {
            const roomRef = getAdminRoomRef();
            if (!roomRef) return setAdminOutput("Enter a room ref first.");
            window.open(`/play.html?roomRef=${encodeURIComponent(roomRef)}`, "_blank", "noopener,noreferrer");
        });

        M.el("btnAdminOpenOverlay")?.addEventListener("click", () => {
            const roomRef = getAdminRoomRef();
            if (!roomRef) return setAdminOutput("Enter a room ref first.");
            window.open(`/overlay.html?roomRef=${encodeURIComponent(roomRef)}&debug=1`, "_blank", "noopener,noreferrer");
        });

        M.el("btnAdminOpenRoomState")?.addEventListener("click", () => {
            const roomRef = getAdminRoomRef();
            if (!roomRef) return setAdminOutput("Enter a room ref first.");
            window.open(`/rooms/${encodeURIComponent(roomRef)}/state`, "_blank", "noopener,noreferrer");
        });

        M.el("btnAdminRefreshRoomState")?.addEventListener("click", loadAdminRoomStateInline);

        M.el("btnAdminStartGame")?.addEventListener("click", () => runAdminGameAction("start"));
        M.el("btnAdminStopGame")?.addEventListener("click", () => runAdminGameAction("stop"));
        M.el("btnAdminNewGame")?.addEventListener("click", () => runAdminGameAction("newGame"));
        M.el("btnAdminHigher")?.addEventListener("click", () => runAdminGameAction("higher"));
        M.el("btnAdminLower")?.addEventListener("click", () => runAdminGameAction("lower"));
        M.el("btnAdminHigherLowerContinue")?.addEventListener("click", () => runAdminGameAction("cont"));
        M.el("btnAdminShowRiddleAnswer")?.addEventListener("click", () => runAdminGameAction("showAnswer"));

        M.el("btnAdminLeaderboard")?.addEventListener("click", async () => {
            const roomRef = getAdminRoomRef();
            if (!roomRef) return setAdminOutput("Enter a room ref first.");

            try {
                const result = await adminApi(`/rooms/${encodeURIComponent(roomRef)}/leaderboard`);
                setAdminOutput(result);
                setText("adminRoomStateOut", prettyAdmin(result));
            } catch (err) {
                setAdminOutput(err.message || String(err));
            }
        });

        M.el("btnAdminImportRiddleCategory")?.addEventListener("click", async () => {
            const category = M.el("adminRiddleCategory")?.value?.trim() || "science";

            try {
                M.setStatus(`Importing ${category} riddles...`);
                const result = await adminApi(`/admin/games/riddle_me_this/import/${encodeURIComponent(category)}`, {
                    method: "POST"
                });
                setAdminOutput(result);
                setText("adminRiddleImportOut", prettyAdmin(result));
                setText("adminRoomStateOut", prettyAdmin(result));
                M.setStatus("Riddle import complete.", "good");
            } catch (err) {
                setAdminOutput(err.message || String(err));
                setText("adminRiddleImportOut", err.message || String(err));
                M.setStatus("Riddle import failed.", "bad");
            }
        });

        M.el("btnAdminImportRiddlesAll")?.addEventListener("click", async () => {
            try {
                M.setStatus("Importing all riddle categories...");
                const result = await adminApi("/admin/games/riddle_me_this/import", {
                    method: "POST"
                });
                setAdminOutput(result);
                setText("adminRiddleImportOut", prettyAdmin(result));
                setText("adminRoomStateOut", prettyAdmin(result));
                M.setStatus("Riddle imports complete.", "good");
            } catch (err) {
                setAdminOutput(err.message || String(err));
                setText("adminRiddleImportOut", err.message || String(err));
                M.setStatus("Riddle imports failed.", "bad");
            }
        });
    }

    async function renderAdminDebugSection() {
        const host = M.el("adminSectionHost");
        if (!host) return;

        host.innerHTML = `
            <div class="card span-12">
                <div class="section-head">
                    <div>
                        <h4>Debug Tools</h4>
                        <p>Check auth, static paths, portal state, and current room state.</p>
                    </div>
                </div>

                <div class="admin-grid">
                    <button class="btn" id="btnDebugWhoami" type="button">Who Am I</button>
                    <button class="btn" id="btnDebugStatic" type="button">Static Debug</button>
                    <button class="btn" id="btnDebugPortal" type="button">Portal State</button>
                    <button class="btn" id="btnDebugRoomState" type="button">Room State</button>
                </div>

                <!--<pre id="adminDebugOut" class="admin-output" style="margin-top:16px;">Debug output will appear here...</pre> -->
            </div>
        `;

        const writeDebug = value => {
            setAdminOutput(value);
            setText("adminDebugOut", prettyAdmin(value));
        };

        M.el("btnDebugWhoami")?.addEventListener("click", async () => {
            try { writeDebug(await adminApi("/debug/whoami")); }
            catch (err) { writeDebug(err.message || String(err)); }
        });

        M.el("btnDebugStatic")?.addEventListener("click", async () => {
            try { writeDebug(await adminApi("/debug/static")); }
            catch (err) { writeDebug(err.message || String(err)); }
        });

        M.el("btnDebugPortal")?.addEventListener("click", async () => {
            try { writeDebug(await M.getPortalState()); }
            catch (err) { writeDebug(err.message || String(err)); }
        });

        M.el("btnDebugRoomState")?.addEventListener("click", async () => {
            const roomRef = getAdminRoomRef();
            if (!roomRef) return writeDebug("No room ref found.");
            try { writeDebug(await adminApi(`/rooms/${encodeURIComponent(roomRef)}/state`)); }
            catch (err) { writeDebug(err.message || String(err)); }
        });
    }

    async function renderAdminUsers() {
        const host = M.el("adminSectionHost");
        if (!host) return;

        host.innerHTML = `
        <div class="card span-12">
            <div class="section-head">
                <div>
                    <h4>Admin Users</h4>
                    <p>Manage registered users.</p>
                </div>
                <button class="btn" id="btnReloadAdminUsers" type="button">Reload Users</button>
            </div>
            <div id="adminUsersList" class="stack">Loading users...</div>
        </div>
    `;

        M.el("btnReloadAdminUsers")?.addEventListener("click", renderAdminUsers);

        try {
            const users = await adminApi("/admin/users");
            const list = Array.isArray(users) ? users : users?.users || users?.items || [];
            const target = M.el("adminUsersList");

            if (!list.length) {
                target.innerHTML = `<div class="emptyState">No users returned.</div>`;
                setAdminOutput(users);
                return;
            }

            target.innerHTML = list.map(u => {
                const userId = u.id || u.userId || u.accountId || "";
                const role = String(u.role || "member").toLowerCase();

                return `
                <div class="list-row">
                    <div class="list-copy">
                        <strong>${escAdmin(u.displayName || u.username || u.email || "Unknown user")}</strong>
                        <span>${escAdmin(u.email || "-")} • ${escAdmin(role)}</span>
                    </div>
                    <span class="pill ${role === "admin" ? "warn" : "good"}">
                        ${escAdmin(role)}
                    </span>
                </div>

                <div class="admin-grid" style="margin: -6px 0 14px 0;">
                    <button class="btn" data-user-role="${escAdmin(userId)}|admin" type="button">Make Admin</button>
                    <button class="btn" data-user-role="${escAdmin(userId)}|member" type="button">Make Member</button>
                    <button class="btn" data-user-role="${escAdmin(userId)}|guest" type="button">Make Guest</button>
                </div>
            `;
            }).join("");

            M.qsa("[data-user-role]").forEach(btn => {
                btn.addEventListener("click", async () => {
                    if (!requireAdminAction()) return;

                    const [userId, role] = String(btn.dataset.userRole || "").split("|");

                    if (!userId || !role) {
                        setAdminOutput("Missing user id or role.");
                        return;
                    }

                    if (!confirm(`Change this user to ${role}?`)) return;

                    try {
                        M.setStatus(`Updating user role to ${role}...`);

                        const result = await adminApi(`/admin/users/${encodeURIComponent(userId)}/role`, {
                            method: "POST",
                            headers: { "Content-Type": "application/json" },
                            body: JSON.stringify({ role })
                        });

                        setAdminOutput(result);
                        M.setStatus("User role updated.", "good");
                        await renderAdminUsers();
                    } catch (err) {
                        setAdminOutput(err.message || String(err));
                        M.setStatus("Failed to update user role.", "bad");
                    }
                });
            });

            setAdminOutput(users);
        } catch (err) {
            M.el("adminUsersList").innerHTML = `<div class="emptyState">Failed to load users.</div>`;
            setAdminOutput(err.message || String(err));
        }
    }


    function bindAdminActions() {
        M.el("btnAdminSiteEditor")?.addEventListener("click", () => {
            if (!requireAdminAction()) return;
            window.open("/admin", "_blank", "noopener,noreferrer");
        });

        M.el("btnAdminSql")?.addEventListener("click", () => {
            if (!requireAdminAction()) return;
            window.open("/admin/sql", "_blank", "noopener,noreferrer");
        });

        M.qsa("[data-admin-section]").forEach(btn => {
            btn.addEventListener("click", async () => {
                if (!requireAdminAction()) return;

                const section = btn.dataset.adminSection;

                M.qsa("[data-admin-section]").forEach(x => {
                    x.classList.toggle("primary", x === btn);
                });

                if (section === "room") renderAdminRoomSection();
                if (section === "debug") await renderAdminDebugSection();
                if (section === "users") await renderAdminUsers();
            });
        });
    }


    // ========== Battle Management ==========

    let battleCalendarDate = new Date();

    let currentBattle = null;
    let latestBattles = [];

    function getBattleStart(b) {
        return new Date(b.startsAtUtc || b.StartsAtUtc);
    }

    function getBattleEnd(b) {
        const start = getBattleStart(b);
        const rawEnd = b.endsAtUtc || b.EndsAtUtc;

        if (rawEnd) return new Date(rawEnd);

        // Default battle length if no end time exists
        return new Date(start.getTime() + 60 * 60 * 1000);
    }

    function battlesOverlap(a, b) {
        const aStart = getBattleStart(a);
        const aEnd = getBattleEnd(a);
        const bStart = getBattleStart(b);
        const bEnd = getBattleEnd(b);

        return aStart < bEnd && bStart < aEnd;
    }

    function findBattleConflicts(battles) {
        const conflictIds = new Set();

        for (let i = 0; i < battles.length; i++) {
            for (let j = i + 1; j < battles.length; j++) {
                if (battlesOverlap(battles[i], battles[j])) {
                    conflictIds.add(battles[i].id);
                    conflictIds.add(battles[j].id);
                }
            }
        }

        return conflictIds;
    }

    async function renderBattleCalendar() {
        const host = M.el("battleViewHost");
        if (!host) return;

        host.innerHTML = `<div class="emptyState">Loading battle calendar...</div>`;

        try {
            const result = await battleApi("/api/battles");
            const battles = result?.battles || [];
            latestBattles = battles;

            const conflictIds = findBattleConflicts(battles);

            const year = battleCalendarDate.getFullYear();
            const month = battleCalendarDate.getMonth();

            const firstDay = new Date(year, month, 1);
            const lastDay = new Date(year, month + 1, 0);
            const startOffset = firstDay.getDay();
            const daysInMonth = lastDay.getDate();

            const monthLabel = battleCalendarDate.toLocaleString([], {
                month: "long",
                year: "numeric"
            });

            let cells = "";

            for (let i = 0; i < startOffset; i++) {
                cells += `<div class="battle-cal-cell empty"></div>`;
            }

            for (let day = 1; day <= daysInMonth; day++) {
                const dateKey = new Date(year, month, day).toDateString();

                const dayBattles = battles.filter(b =>
                    getBattleStart(b).toDateString() === dateKey
                );

                cells += `
                <div class="battle-cal-cell">
                    <div class="battle-cal-day">${day}</div>

                    ${dayBattles.map(b => {
                    const hasConflict = conflictIds.has(b.id);

                    return `
                        <div class="battle-cal-event ${hasConflict ? "conflict" : ""}" data-battle-id="${escAdmin(b.id)}">
                            <strong>${escAdmin(b.title || "Battle")}</strong>

                            <span>
                                ${getBattleStart(b).toLocaleTimeString([], {
                                    hour: "2-digit",
                                    minute: "2-digit"
                                })}
                            </span>

                            <span class="battle-status-badge ${statusClass(b.status)}">
                                ${formatStatus(b.status)}
                            </span>

                            ${hasConflict ? `<em>Conflict</em>` : ""}
                        </div>
                        `;
                }).join("")}
                </div>
            `;
            }

            host.innerHTML = `
            <div class="card span-12">
                <div class="section-head">
                    <div>
                        <h4>${monthLabel}</h4>
                        <p>Monthly battle schedule with conflict warnings.</p>
                    </div>
                    <div class="admin-grid">
                        <button class="btn" id="btnBattlePrevMonth" type="button">Previous</button>
                        <button class="btn" id="btnBattleNextMonth" type="button">Next</button>
                    </div>
                </div>

                <div class="battle-calendar">
                    <div class="battle-cal-head">Sun</div>
                    <div class="battle-cal-head">Mon</div>
                    <div class="battle-cal-head">Tue</div>
                    <div class="battle-cal-head">Wed</div>
                    <div class="battle-cal-head">Thu</div>
                    <div class="battle-cal-head">Fri</div>
                    <div class="battle-cal-head">Sat</div>
                    ${cells}
                </div>
            </div>
        `;

            M.el("btnBattlePrevMonth")?.addEventListener("click", async () => {
                battleCalendarDate = new Date(year, month - 1, 1);
                await renderBattleCalendar();
            });

            M.el("btnBattleNextMonth")?.addEventListener("click", async () => {
                battleCalendarDate = new Date(year, month + 1, 1);
                await renderBattleCalendar();
            });

        } catch (err) {
            host.innerHTML = `<div class="emptyState">Failed to load battle calendar.</div>`;
            setAdminOutput(err.message || String(err));
        }
    }

    async function battleApi(path, options = {}) {
        return await adminApi(path, options);
    }

    function formatBattleDate(value) {
        if (!value) return "-";

        const d = new Date(value);
        if (Number.isNaN(d.getTime())) return "-";

        return d.toLocaleString([], {
            dateStyle: "medium",
            timeStyle: "short"
        });
    }

    function renderBattleRows(battles, isAdminView) {
        if (!Array.isArray(battles) || !battles.length) {
            return `<div class="emptyState">No battles found.</div>`;
        }

        return battles.map(b => `
        <div class="list-row battle-clickable" data-battle-id="${escAdmin(b.id)}">
            <div class="list-copy">
                <strong>${escAdmin(b.title || "Untitled Battle")}</strong>
                <span>
                    ${escAdmin(b.opponentName || "No opponent")} •
                    ${formatBattleDate(b.startsAtUtc)} •
                    ${formatStatus(b.status)}
                </span>
                ${b.description ? `<span>${escAdmin(b.description)}</span>` : ""}
            </div>

            <span class="pill ${statusClass(b.status)}">
                ${formatStatus(b.status)}
            </span>
        </div>

        ${isAdminView ? `
            <div class="admin-grid" style="margin:-6px 0 14px 0;">
                <button class="btn" data-battle-status="${escAdmin(b.id)}|approved" type="button">Approve</button>
                <button class="btn" data-battle-status="${escAdmin(b.id)}|declined" type="button">Decline</button>
                <button class="btn" data-battle-status="${escAdmin(b.id)}|completed" type="button">Complete</button>
            </div>
        ` : ""}
    `).join("");
    }

    async function renderAllBattles() {
        const host = M.el("battleViewHost");
        if (!host) return;

        host.innerHTML = `<div class="emptyState">Loading all battles...</div>`;

        try {
            const result = await battleApi("/api/battles");
            const battles = result?.battles || [];
            latestBattles = battles;

            host.innerHTML = `
            <div class="card span-12">
                <h4>All Battles</h4>
                <div class="stack">
                    ${renderBattleRows(battles, true)}
                </div>
            </div>
        `;

            bindBattleStatusButtons();
        } catch (err) {
            host.innerHTML = `<div class="emptyState">Failed to load all battles.</div>`;
            setAdminOutput(err.message || String(err));
        }
    }

    async function renderMyBattles() {
        const host = M.el("battleViewHost");
        if (!host) return;

        host.innerHTML = `<div class="emptyState">Loading your battles...</div>`;

        try {
            const result = await battleApi("/api/battles/me");
            const battles = result?.battles || [];
            latestBattles = battles;

            host.innerHTML = `
            <div class="card span-12">
                <h4>My Battles</h4>
                <div class="stack">
                    ${renderBattleRows(battles, false)}
                </div>
            </div>
        `;
        } catch (err) {
            host.innerHTML = `<div class="emptyState">Failed to load your battles.</div>`;
            setAdminOutput(err.message || String(err));
        }
    }

    function renderBattleRequestForm() {
        const host = M.el("battleViewHost");
        if (!host) return;

        host.innerHTML = `
        <div class="card span-12">
            <div class="section-head">
                <div>
                    <h4>Request Battle</h4>
                    <p>Submit a battle request for review.</p>
                </div>
            </div>

            <div class="field">
                <label for="battleTitle">Title</label>
                <input id="battleTitle" placeholder="Friday Night Battle" />
            </div>

            <div class="field">
                <label for="battleOpponent">Opponent</label>
                <input id="battleOpponent" placeholder="Opponent username or team" />
            </div>

            <div class="field">
                <label for="battleStartsAt">Date and time</label>
                <input id="battleStartsAt" type="datetime-local" />
            </div>

            <div class="field">
                <label for="battleDescription">Description</label>
                <textarea id="battleDescription" placeholder="Battle notes, rules, game mode, etc."></textarea>
            </div>

            <button class="btn primary" id="btnSubmitBattleRequest" type="button">Submit Battle Request</button>
        </div>
    `;

        M.el("btnSubmitBattleRequest")?.addEventListener("click", submitBattleRequest);
    }

    async function submitBattleRequest() {
        const title = M.el("battleTitle")?.value?.trim() || "";
        const opponentName = M.el("battleOpponent")?.value?.trim() || "";
        const startsAtRaw = M.el("battleStartsAt")?.value || "";
        const description = M.el("battleDescription")?.value?.trim() || "";

        if (!title) {
            M.setStatus("Battle title is required.", "bad");
            return;
        }

        if (!startsAtRaw) {
            M.setStatus("Battle date/time is required.", "bad");
            return;
        }

        const startsAtUtc = new Date(startsAtRaw).toISOString();

        try {
            M.setStatus("Submitting battle request...");

            const result = await battleApi("/api/battles/request", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    title,
                    opponentName,
                    description,
                    startsAtUtc
                })
            });

            setAdminOutput(result);
            M.setStatus("Battle request submitted.", "good");
            await renderMyBattles();
        } catch (err) {
            setAdminOutput(err.message || String(err));
            M.setStatus("Failed to submit battle request.", "bad");
        }
    }

    function bindBattleStatusButtons() {
        M.qsa("[data-battle-status]").forEach(btn => {
            btn.addEventListener("click", async () => {
                e.stopPropagation();
                if (!requireAdminAction()) return;

                const [id, status] = String(btn.dataset.battleStatus || "").split("|");

                if (!id || !status) {
                    setAdminOutput("Missing battle id or status.");
                    return;
                }

                try {
                    M.setStatus(`Updating battle to ${status}...`);

                    const result = await battleApi(`/api/battles/${encodeURIComponent(id)}/status`, {
                        method: "POST",
                        headers: { "Content-Type": "application/json" },
                        body: JSON.stringify({ status })
                    });

                    setAdminOutput(result);
                    M.setStatus("Battle updated.", "good");
                    await renderAllBattles();
                } catch (err) {
                    setAdminOutput(err.message || String(err));
                    M.setStatus("Failed to update battle.", "bad");
                }
            });
        });
    }

    // let currentBattle = null;

    document.addEventListener("click", async (e) => {
        const item = e.target.closest("[data-battle-id]");
        if (!item) return;

        await openBattleDetails(item.dataset.battleId);
    });

    document.addEventListener("click", async (e) => {
        const item = e.target.closest("[data-tournament-id]");
        if (!item) return;

        await openTournamentDetails(item.dataset.tournamentId);
    });

    async function openBattleDetails(battleId) {
        const battle = latestBattles.find(b => String(b.id) === String(battleId));

        if (!battle) {
            M.setStatus("Battle details not found in loaded list.", "bad");
            return;
        }

        currentBattle = battle;

        document.getElementById("battleModalTitle").textContent = battle.title ?? "Scheduled Battle";

        document.getElementById("battleModalBody").innerHTML = `
        <div><strong>Room:</strong> ${escapeHtml(battle.roomRef ?? "")}</div>
        <div><strong>Starts:</strong> ${escapeHtml(battle.startsAtLocal ?? battle.startsAtUtc ?? "")}</div>
        <div><strong>Ends:</strong> ${escapeHtml(battle.endsAtLocal ?? battle.endsAtUtc ?? "")}</div>
        <div><strong>Status:</strong> ${escapeHtml(battle.status ?? "Scheduled")}</div>
        <div><strong>Description:</strong><br>${escapeHtml(battle.description ?? "")}</div>
    `;

        document.getElementById("battleViewMode").classList.remove("hidden");
        document.getElementById("battleEditMode").classList.add("hidden");
        document.getElementById("battleDetailsModal").classList.remove("hidden");
    }

    document.getElementById("battleEditBtn")?.addEventListener("click", () => {
        if (!currentBattle) return;

        document.getElementById("battleEditTitle").value = currentBattle.title ?? "";
        document.getElementById("battleEditRoomRef").value = currentBattle.roomRef ?? "";
        document.getElementById("battleEditStartsAt").value = toDateTimeLocal(currentBattle.startsAtUtc);
        document.getElementById("battleEditEndsAt").value = toDateTimeLocal(currentBattle.endsAtUtc);
        document.getElementById("battleEditDescription").value = currentBattle.description ?? "";

        document.getElementById("battleViewMode").classList.add("hidden");
        document.getElementById("battleEditMode").classList.remove("hidden");
    });

    document.getElementById("battleEditMode")?.addEventListener("submit", async (e) => {
        e.preventDefault();
        if (!currentBattle) return;

        const payload = {
            title: document.getElementById("battleEditTitle").value.trim(),
            roomRef: document.getElementById("battleEditRoomRef").value.trim(),
            startsAtUtc: new Date(document.getElementById("battleEditStartsAt").value).toISOString(),
            endsAtUtc: new Date(document.getElementById("battleEditEndsAt").value).toISOString(),
            description: document.getElementById("battleEditDescription").value.trim()
        };

        await battleApi(`/api/battles/${encodeURIComponent(currentBattle.id)}`, {
            method: "PUT",
            body: JSON.stringify(payload)
        });

        closeBattleModal();
        await renderBattleCalendar();
        //await renderAllBattles(); -- render calender or all battles (switch between views)
    });

    function closeBattleModal() {
        document.getElementById("battleDetailsModal").classList.add("hidden");
        currentBattle = null;
    }

    document.getElementById("battleModalClose")?.addEventListener("click", closeBattleModal);
    document.getElementById("battleCloseBtn")?.addEventListener("click", closeBattleModal);
    document.getElementById("battleCancelEditBtn")?.addEventListener("click", () => {
        document.getElementById("battleEditMode").classList.add("hidden");
        document.getElementById("battleViewMode").classList.remove("hidden");
    });

    function toDateTimeLocal(value) {
        if (!value) return "";
        const d = new Date(value);
        d.setMinutes(d.getMinutes() - d.getTimezoneOffset());
        return d.toISOString().slice(0, 16);
    }

    function escapeHtml(s) {
        return String(s ?? "")
            .replaceAll("&", "&amp;")
            .replaceAll("<", "&lt;")
            .replaceAll(">", "&gt;")
            .replaceAll('"', "&quot;")
            .replaceAll("'", "&#039;");
    }

    let currentTournament = null;

    async function loadTournaments() {
        const listEl = M.el("tournamentList");
        if (!listEl) return;

        listEl.innerHTML = "Loading tournaments...";

        try {
            const result = await battleApi("/api/tournaments");
            const list = result?.tournaments || [];

            if (!list.length) {
                listEl.innerHTML = `<div class="emptyState">No tournaments yet.</div>`;
                return;
            }

            listEl.innerHTML = list.map(t => `
            <div class="list-row tournament-clickable" data-tournament-id="${escAdmin(t.id)}">
                <div class="list-copy">
                    <strong>${escAdmin(t.title || "Untitled Tournament")}</strong>
                    <span>
                        ${formatBattleDate(t.startsAtUtc)} → ${formatBattleDate(t.endsAtUtc)}
                    </span>
                    <span>
                        Signups: ${t.signupCount ?? 0}/${t.requiredSignups ?? 0}
                        ${t.prize ? ` • Prize: ${escAdmin(t.prize)}` : ""}
                    </span>
                </div>
                <span class="pill ${statusClass(t.status)}">${formatStatus(t.status)}</span>
            </div>
        `).join("");

        } catch (err) {
            listEl.innerHTML = `<div class="emptyState">Failed to load tournaments.</div>`;
            setAdminOutput(err.message || String(err));
        }
    }

    function renderTournamentSection() {
        const host = M.el("battleViewHost");
        if (!host) return;

        const isAdminUser = document.body.classList.contains("is-admin");

        host.innerHTML = `
        <div class="card span-12">
            <div class="section-head">
                <div>
                    <h4>Tournaments</h4>
                    <p>Admin-created tournaments that members can sign up for.</p>
                </div>
                ${isAdminUser ? `<button class="btn primary" id="btnCreateTournament" type="button">Create Tournament</button>` : ""}
            </div>

            ${isAdminUser ? `
                <div id="tournamentCreateBox" class="card hidden">
                    <div class="field">
                        <label for="tournamentTitle">Tournament title</label>
                        <input id="tournamentTitle" placeholder="Friday Night Showdown" />
                    </div>

                    <div class="field">
                        <label for="tournamentRequiredSignups">Required signups</label>
                        <input id="tournamentRequiredSignups" type="number" min="1" value="8" />
                    </div>

                    <div class="field">
                        <label for="tournamentPrize">Prize</label>
                        <input id="tournamentPrize" placeholder="Coins, gift, shoutout, etc." />
                    </div>

                    <div class="field">
                        <label for="tournamentStartsAt">Start date</label>
                        <input id="tournamentStartsAt" type="datetime-local" />
                    </div>

                    <div class="field">
                        <label for="tournamentEndsAt">End date</label>
                        <input id="tournamentEndsAt" type="datetime-local" />
                    </div>

                    <div class="field">
                        <label for="tournamentStatus">Status</label>
                        <select id="tournamentStatus">
                            <option value="draft">Draft</option>
                            <option value="open">Open</option>
                            <option value="active">Active</option>
                            <option value="completed">Completed</option>
                            <option value="cancelled">Cancelled</option>
                        </select>
                    </div>

                    <div class="field">
                        <label for="tournamentDescription">Description</label>
                        <textarea id="tournamentDescription"></textarea>
                    </div>

                    <button class="btn primary" id="btnSaveNewTournament" type="button">Save Tournament</button>
                </div>
            ` : ""}

            <div id="tournamentList" class="stack" style="margin-top:16px;"></div>
        </div>
    `;

        M.el("btnCreateTournament")?.addEventListener("click", () => {
            M.el("tournamentCreateBox")?.classList.toggle("hidden");
        });

        M.el("btnSaveNewTournament")?.addEventListener("click", createTournament);

        loadTournaments();
    }

    async function createTournament() {
        const title = M.el("tournamentTitle")?.value?.trim() || "";
        const requiredSignups = Number(M.el("tournamentRequiredSignups")?.value || 0);
        const prize = M.el("tournamentPrize")?.value?.trim() || "";
        const startsRaw = M.el("tournamentStartsAt")?.value || "";
        const endsRaw = M.el("tournamentEndsAt")?.value || "";
        const status = M.el("tournamentStatus")?.value || "draft";
        const description = M.el("tournamentDescription")?.value?.trim() || "";

        if (!title) return M.setStatus("Tournament title is required.", "bad");
        if (!requiredSignups || requiredSignups <= 0) return M.setStatus("Required signups must be greater than zero.", "bad");
        if (!startsRaw || !endsRaw) return M.setStatus("Start and end dates are required.", "bad");

        try {
            M.setStatus("Creating tournament...");

            const result = await battleApi("/api/tournaments", {
                method: "POST",
                headers: { "Content-Type": "application/json" },
                body: JSON.stringify({
                    title,
                    requiredSignups,
                    prize,
                    description,
                    startsAtUtc: new Date(startsRaw).toISOString(),
                    endsAtUtc: new Date(endsRaw).toISOString(),
                    status
                })
            });

            setAdminOutput(result);
            M.setStatus("Tournament created.", "good");
            await loadTournaments();

        } catch (err) {
            setAdminOutput(err.message || String(err));
            M.setStatus("Failed to create tournament.", "bad");
        }
    }



    function bindBattleActions() {
        M.qsa("[data-battle-view]").forEach(btn => {
            btn.addEventListener("click", async () => {
                const view = btn.dataset.battleView;

                M.qsa("[data-battle-view]").forEach(x => {
                    x.classList.toggle("primary", x === btn);
                });

                if (view === "all") await renderAllBattles();
                if (view === "mine") await renderMyBattles();
                if (view === "request") renderBattleRequestForm();
                if (view === "calendar") await renderBattleCalendar();
                if (view === "tournaments") renderTournamentSection();
            });
        });

        M.el("btnReloadBattles")?.addEventListener("click", async () => {
            await renderMyBattles();
        });
    }

    function bindActions() {
        M.el("btnBackToLobby")?.addEventListener("click", () => {
            window.location.href = "/userportal.html";
        });

        const openRoom = () => {
            if (portalPath) {
                window.location.href = portalPath;
            }
        };

        M.el("btnOpenRoom")?.addEventListener("click", openRoom);
        M.el("btnOpenRoomInline")?.addEventListener("click", openRoom);

        M.el("btnSaveProfile")?.addEventListener("click", async () => {
            try {
                M.setStatus("Saving profile...");
                await M.savePortalProfile(readProfileForm());
                M.setStatus("Profile saved.", "good");
            } catch (err) {
                if (!M.requireAuthRedirect(err)) {
                    M.setStatus(err.message || "Failed to save profile.", "bad");
                }
            }
        });

        M.el("btnSaveRoom")?.addEventListener("click", async () => {
            try {
                M.setStatus("Saving room settings...");
                await M.savePortalRoom(readRoomForm());
                M.setStatus("Room settings saved.", "good");
            } catch (err) {
                if (!M.requireAuthRedirect(err)) {
                    M.setStatus(err.message || "Failed to save room settings.", "bad");
                }
            }
        });

        const savePreferences = async () => {
            try {
                M.setStatus("Saving preferences...");
                await M.savePortalPreferences(readPreferencesForm());
                M.setStatus("Preferences saved.", "good");
            } catch (err) {
                if (!M.requireAuthRedirect(err)) {
                    M.setStatus(err.message || "Failed to save preferences.", "bad");
                }
            }
        };

        M.el("btnSavePreferences")?.addEventListener("click", savePreferences);
        M.el("btnSavePreferencesTop")?.addEventListener("click", savePreferences);


    }

    function formatStatus(status) {
        switch ((status || "").toLowerCase()) {
            case "pending": return "Pending";
            case "approved": return "Approved";
            case "declined": return "Declined";
            case "completed": return "Completed";
            default: return status;
        }
    }

    function statusClass(status) {
        switch ((status || "").toLowerCase()) {
            case "approved": return "good";
            case "declined": return "bad";
            case "completed": return "neutral";
            default: return "warn";
        }
    }

    async function boot() {
        bindTabs();
        bindActions();
        bindAdminActions();
        bindBattleActions();

        try {
            M.setStatus("Loading portal...");
            const data = await M.getPortalState();
            renderPortal(data);
            M.setStatus("Portal ready.", "good");
            openTab("overview");
        } catch (err) {
            console.error("Portal load failed", err);
            if (!M.requireAuthRedirect(err)) {
                M.setStatus(err.message || "Failed to load portal.", "bad");
            }
        }
    }

    document.addEventListener("DOMContentLoaded", boot);
})();
