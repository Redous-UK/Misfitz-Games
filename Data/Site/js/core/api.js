export async function api(path, opts = {}) {
    console.log("API:", path, opts);

    const fetchOpts = {
        credentials: "include",
        ...opts,
        headers: {
            ...(opts.headers || {}),
            ...(opts.body ? { "Content-Type": "application/json" } : {}),
        },
    };

    const res = await fetch(path, fetchOpts);
    console.log("FETCH RES", res.status, res.statusText, "for", path);

    const text = await res.text();
    let payload = null;

    if (text) {
        try { payload = JSON.parse(text); }
        catch { payload = { _nonJson: true, raw: text }; }
    }

    if (!res.ok) {
        console.error("API ERROR:", { path, status: res.status, statusText: res.statusText, payload, text });
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