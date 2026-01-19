el("btnLogin").onclick = async () => {
    try {
        const password = el("adminPassword").value;
        const resp = await api("/admin/login", {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify({ password })
        });
        el("authOut").textContent = pretty(resp);
    } catch (e) { el("authOut").textContent = String(e); }
};

el("btnLogout").onclick = async () => {
    try {
        const resp = await api("/admin/logout", { method: "POST" });
        el("authOut").textContent = pretty(resp);
    } catch (e) { el("authOut").textContent = String(e); }
};

el("btnMe").onclick = async () => {
    try {
        const resp = await api("/admin/me");
        el("authOut").textContent = pretty(resp);
    } catch (e) { el("authOut").textContent = String(e); }
};
