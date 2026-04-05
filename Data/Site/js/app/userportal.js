(() => {
    const M = window.Misfitz;

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

    function getInitials(name) {
        const parts = (name || "").trim().split(/\s+/).filter(Boolean);
        if (!parts.length) return "MG";
        if (parts.length === 1) return parts[0].slice(0, 2).toUpperCase();
        return (parts[0][0] + parts[1][0]).toUpperCase();
    }

    function openTab(tab) {
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
            roomSlug: M.el("roomSlug")?.value?.trim() ?? "",
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

    function renderOverview(data) {
        const displayName = data.user?.displayName || data.user?.username || "User";
        const bio = data.user?.bio || "Owner of one persistent room with account-linked settings, stream profile, and gameplay preferences.";
        const email = data.user?.email || "-";
        const roomPath = data.room?.portalPath || `/rooms/${data.room?.roomSlug || ""}`;
        const role = data.user?.role || "member";
        const initials = getInitials(displayName);

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
        setValue("profileUsername", data.user?.username);
        setValue("profileBio", data.user?.bio);
        setValue("profileAvatarUrl", data.user?.avatarUrl);

        setChecked("profileIsPublic", data.user?.isProfilePublic);
        setChecked("profileShowAvatar", data.user?.showAvatarInRoom);
        setChecked("profileShowOnline", data.user?.showOnlineStatus);
    }

    function renderRoom(data) {
        setValue("roomName", data.room?.roomName);
        setValue("roomSlug", data.room?.roomSlug);
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

    function renderPortal(data) {
        renderOverview(data);
        renderProfile(data);
        renderRoom(data);
        renderPreferences(data);
    }

    function bindActions() {
        M.el("btnBackToLobby")?.addEventListener("click", () => {
            window.location.href = "/lobby.html";
        });

        const openRoom = () => {
            const path = M.el("heroRoomPath")?.textContent?.trim();
            if (path) {
                window.location.href = path;
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