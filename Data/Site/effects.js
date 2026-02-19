const el = (id) => document.getElementById(id);

//
// ROUTES
// If you DID NOT change List/Create to /api/effects, set this to "/api/effects/effects"
//
const EFFECTS_BASE = "/api/effects"; // or "/api/effects/effects" if needed

async function api(path, opts = {}) {
    const res = await fetch(path, {
        credentials: "include",
        ...opts,
        headers: {
            ...(opts.headers || {}),
            ...(opts.body ? { "Content-Type": "application/json" } : {}),
        },
    });

    const text = await res.text();
    let data = null;
    if (text) {
        try { data = JSON.parse(text); }
        catch { data = { _nonJson: true, body: text }; }
    }

    if (!res.ok) {
        const msg = data?.error || data?.message || `${res.status} ${res.statusText}`;
        throw new Error(msg);
    }
    return data;
}

const pretty = (x) => {
    try { return JSON.stringify(x, null, 2); } catch { return String(x); }
};

let devices = [];
let groups = [];
let effects = [];
let selectedEffectId = null;

function setOut(obj) {
    el("out").textContent = typeof obj === "string" ? obj : pretty(obj);
}

async function loadTuyaHealth() {
    try {
        await api("/api/health/tuya");
        const b = el("tuyaBadge");
        b.className = "badge ok";
        b.textContent = "Tuya: online";
    } catch {
        const b = el("tuyaBadge");
        b.className = "badge warn";
        b.textContent = "Tuya: offline";
    }
}

function escapeHtml(s) {
    return String(s)
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");
}

function renderEffectsList() {
    const box = el("effectsList");
    box.innerHTML = "";

    if (!effects.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No effects yet. Create one above.";
        box.appendChild(empty);
        return;
    }

    for (const e of effects) {
        const btn = document.createElement("button");
        btn.className = "listItem" + (e.id === selectedEffectId ? " selected" : "");
        btn.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(e.name)}</div>
        <div class="muted">Action ${e.action ?? e.actionId ?? e.action} • ${e.durationSeconds}s • cd ${e.cooldownSeconds}s</div>
      </div>
    `;
        btn.addEventListener("click", () => selectEffect(e.id));
        box.appendChild(btn);
    }
}

function renderTargetPicker() {
    const type = Number(el("targetType").value);
    const picker = el("targetPicker");
    picker.innerHTML = "";

    const items = type === 1 ? devices : groups;

    for (const it of items) {
        const opt = document.createElement("option");
        opt.value = it.id;
        opt.textContent =
            type === 1
                ? `${it.name}${it.isEnabled ? "" : " (disabled)"}`
                : `${it.name}`;
        picker.appendChild(opt);
    }
}

function renderEffectDetails(effect) {
    if (!effect) {
        el("effectMeta").textContent = "Select an effect…";
        el("targetsList").innerHTML = "";
        el("btnRun").disabled = true;
        return;
    }

    el("effectMeta").textContent =
        `${effect.name} • Action ${effect.action} • ${effect.durationSeconds}s • cd ${effect.cooldownSeconds}s`;
    el("btnRun").disabled = false;

    const list = el("targetsList");
    list.innerHTML = "";

    const targets = effect.targets || [];
    if (!targets.length) {
        const empty = document.createElement("div");
        empty.className = "muted";
        empty.textContent = "No targets yet. Add one below.";
        list.appendChild(empty);
        return;
    }

    for (const t of targets) {
        const row = document.createElement("div");
        row.className = "listRow";

        const name = t.targetType === 1
            ? (t.deviceName || t.deviceId)
            : (t.groupName || t.groupId);

        row.innerHTML = `
      <div class="grow">
        <div class="strong">${escapeHtml(name || "(unknown)")}</div>
        <div class="muted">${t.targetType === 1 ? "Device" : "Group"} • override ${t.durationSecondsOverride ?? "-"} • sort ${t.sortOrder}</div>
      </div>
      <button class="btn" data-del="${t.id}">Remove</button>
    `;

        row.querySelector("[data-del]").addEventListener("click", async () => {
            try {
                setOut("Removing target…");
                await api(`/api/effects/targets/${t.id}`, { method: "DELETE" });
                await selectEffect(selectedEffectId);
                setOut({ ok: true });
            } catch (e) {
                setOut(String(e));
            }
        });

        list.appendChild(row);
    }
}

async function loadCatalogs() {
    const [d, g] = await Promise.all([
        api("/api/effects/devices"),
        api("/api/effects/groups").catch(() => ({ ok: true, groups: [] })),
    ]);

    devices = d.devices || [];
    groups = g.groups || [];
    renderTargetPicker();
}

async function loadEffects() {
    const r = await api(EFFECTS_BASE);
    effects = r.effects || [];
    // normalize action to int if it came back as enum name
    effects = effects.map(e => ({
        ...e,
        action: typeof e.action === "string" ? e.action : e.action, // keep as-is
    }));
    renderEffectsList();
}

async function selectEffect(effectId) {
    selectedEffectId = effectId;
    renderEffectsList();

    try {
        const r = await api(`/api/effects/${effectId}`);
        renderEffectDetails(r.effect);
    } catch (e) {
        setOut(String(e));
    }
}

async function createEffect() {
    const name = el("newEffectName").value.trim();
    const action = Number(el("newEffectAction").value);
    const durationSeconds = Number(el("newEffectDuration").value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown").value || 2);

    if (!name) {
        setOut("Name required");
        return;
    }

    setOut("Creating effect…");
    const r = await api(EFFECTS_BASE, {
        method: "POST",
        body: JSON.stringify({ name, action, durationSeconds, cooldownSeconds }),
    });

    await loadEffects();
    el("newEffectName").value = "";
    if (r.effectId) await selectEffect(r.effectId);
    setOut(r);
}

async function addTarget() {
    if (!selectedEffectId) return;

    const targetType = Number(el("targetType").value);
    const pickId = el("targetPicker").value;

    const durationRaw = el("targetDurationOverride").value.trim();
    const durationSecondsOverride = durationRaw ? Number(durationRaw) : null;
    const sortOrder = Number(el("targetSort").value || 0);

    const body = {
        targetType,
        deviceId: targetType === 1 ? pickId : null,
        groupId: targetType === 2 ? pickId : null,
        durationSecondsOverride,
        sortOrder,
    };

    setOut("Adding target…");
    const r = await api(`/api/effects/${selectedEffectId}/targets`, {
        method: "POST",
        body: JSON.stringify(body),
    });

    await selectEffect(selectedEffectId);
    setOut(r);
}

async function runEffect() {
    if (!selectedEffectId) return;

    setOut("Running…");
    try {
        const r = await api(`/api/effects/${selectedEffectId}/run`, { method: "POST" });
        setOut(r);
    } catch (e) {
        setOut(String(e));
    }
}

document.addEventListener("DOMContentLoaded", async () => {
    try {
        setOut("Loading…");
        await loadTuyaHealth();
        await loadCatalogs();
        await loadEffects();
        setOut("Ready.");
    } catch (e) {
        setOut(String(e));
    }

    el("btnNewEffect").addEventListener("click", () => createEffect().catch(e => setOut(String(e))));
    el("btnAddTarget").addEventListener("click", () => addTarget().catch(e => setOut(String(e))));
    el("btnRun").addEventListener("click", () => runEffect().catch(e => setOut(String(e))));
    el("targetType").addEventListener("change", () => renderTargetPicker());
});