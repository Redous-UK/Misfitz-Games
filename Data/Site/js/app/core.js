window.Misfitz = (() => {
    function el(id) {
        return document.getElementById(id);
    }

    function qs(sel, root = document) {
        return root.querySelector(sel);
    }

    function qsa(sel, root = document) {
        return Array.from(root.querySelectorAll(sel));
    }

    async function api(path, opts = {}) {
        const res = await fetch(path, {
            credentials: "include",
            headers: {
                ...(opts.body && !(opts.body instanceof FormData)
                    ? { "Content-Type": "application/json" }
                    : {}),
                ...(opts.headers || {})
            },
            ...opts
        });

        const text = await res.text();
        let json = null;

        try {
            json = text ? JSON.parse(text) : null;
        } catch {
            json = null;
        }

        if (!res.ok) {
            const err = new Error(json?.error || json?.detail || `HTTP ${res.status}`);
            err.status = res.status;
            err.payload = json;
            throw err;
        }

        return json;
    }

    async function getCurrentUser() {
        return await api("/account/me");
    }

    async function getPortalState() {
        return await api("/account/portal");
    }

    async function savePortalProfile(payload) {
        return await api("/account/portal/profile", {
            method: "POST",
            body: JSON.stringify(payload)
        });
    }

    async function savePortalRoom(payload) {
        return await api("/account/portal/room", {
            method: "POST",
            body: JSON.stringify(payload)
        });
    }

    async function savePortalPreferences(payload) {
        return await api("/account/portal/preferences", {
            method: "POST",
            body: JSON.stringify(payload)
        });
    }

    function setStatus(text, kind = "info") {
        const node = el("portalStatus");
        if (!node) return;
        node.textContent = text;
        node.dataset.kind = kind;
    }

    function requireAuthRedirect(err) {
        if (err?.status === 401) {
            window.location.href = "/login.html";
            return true;
        }
        return false;
    }

    return {
        el,
        qs,
        qsa,
        api,
        getCurrentUser,
        getPortalState,
        savePortalProfile,
        savePortalRoom,
        savePortalPreferences,
        setStatus,
        requireAuthRedirect
    };
})();