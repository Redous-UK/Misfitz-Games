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

                <pre id="adminDebugOut" class="admin-output" style="margin-top:16px;">Debug output will appear here...</pre>
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
            const users = await adminApi(`/bootstrap/users?key=${encodeURIComponent(key)}`);
            const list = Array.isArray(users) ? users : users?.users || users?.items || [];
            const target = M.el("adminUsersList");

            if (!list.length) {
                target.innerHTML = `<div class="emptyState">No users returned.</div>`;
                setAdminOutput(users);
                return;
            }

            target.innerHTML = list.map(u => `
                <div class="list-row">
                    <div class="list-copy">
                        <strong>${escAdmin(u.displayName || u.username || u.email || "Unknown user")}</strong>
                        <span>${escAdmin(u.email || "-")} • ${escAdmin(u.role || "member")}</span>
                    </div>
                    <span class="pill ${String(u.role).toLowerCase() === "admin" ? "warn" : "good"}">
                        ${escAdmin(u.role || "member")}
                    </span>
                </div>
            `).join("");

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

    function bindActions() {
        M.el("btnBackToLobby")?.addEventListener("click", () => {
            window.location.href = "/lobby.html";
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

        bindAdminActions();
    }

    async function boot() {
        bindTabs();
        bindActions();

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
