const el = (id) => document.getElementById(id);
const EFFECTS_BASE = "/api/effects";

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
    let payload = null;
    if (text) {
        try { payload = JSON.parse(text); }
        catch { payload = { _nonJson: true, raw: text }; }
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

const pretty = (x) => { try { return JSON.stringify(x, null, 2); } catch { return String(x); } };
const esc = (s) => String(s).replaceAll("&", "&amp;").replaceAll("<", "&lt;").replaceAll(">", "&gt;").replaceAll('"', "&quot;").replaceAll("'", "&#39;");

function setOut(obj) { el("out").textContent = typeof obj === "string" ? obj : pretty(obj); }

function normAction(a) {
    if (typeof a === "number") return a;
    const s = String(a).toLowerCase();
    if (s.includes("pulse")) return 1;
    if (s.includes("on")) return 2;
    if (s.includes("off")) return 3;
    return 1;
}
function actionLabel(a) {
    const n = normAction(a);
    if (n === 1) return "Pulse";
    if (n === 2) return "On";
    if (n === 3) return "Off";
    return String(a);
}

// state
let devices = [];
let groups = [];
let effects = [];
let selectedEffectId = null;
let selectedEffect = null;
let activity = [];

function addActivity(type, title, meta, raw) {
    activity.unshift({ type, title, meta, at: new Date(), raw });
    if (activity.length > 40) activity = activity.slice(0, 40);
    renderActivity();
}

function renderActivity() {
    const box = el("activity");
    box.innerHTML = "";
    if (!activity.length) {
        box.innerHTML = `<div class="muted">No activity yet.</div>`;
        return;
    }
    for (const a of activity) {
        const t = a.at.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
        const div = document.createElement("div");
        div.className = "fxActItem " + (a.type === "ok" ? "ok" : a.type === "bad" ? "bad" : "");
        div.innerHTML = `
      <div class="fxActTitle">${esc(a.title)}</div>
      <div class="fxActMeta">${esc(t)} • ${esc(a.meta || "")}</div>
    `;
        div.addEventListener("click", () => a.raw && setOut(a.raw));
        box.appendChild(div);
    }
}

// drawer
function openDrawer() {
    el("drawer").classList.remove("hidden");
    el("drawerBackdrop").classList.remove("hidden");
    document.body.classList.add("fxNoScroll");
}
function closeDrawer() {
    el("drawer").classList.add("hidden");
    el("drawerBackdrop").classList.add("hidden");
    document.body.classList.remove("fxNoScroll");
}

// health
async function loadTuyaBadge() {
    try {
        await api("/api/health/tuya");
        el("tuyaBadge").className = "badge ok";
        el("tuyaBadge").textContent = "Tuya: online";
    } catch (e) {
        el("tuyaBadge").className = "badge warn";
        el("tuyaBadge").textContent = "Tuya: offline";
        addActivity("bad", "Tuya offline", e.message, e.payload || { error: e.message });
    }
}

// loads
async function loadDevices() {
    const r = await api("/api/effects/devices");
    devices = r.devices || [];
    renderDevices();
    renderTargetPicker();
}
async function loadGroups() {
    const r = await api("/api/effects/groups");
    groups = r.groups || [];
    renderGroups();
    renderTargetPicker();
}
async function loadEffects() {
    const r = await api(EFFECTS_BASE);
    effects = r.effects || [];
    renderEffects();
}
async function loadEffectDetails(effectId) {
    const r = await api(`/api/effects/${effectId}`);
    selectedEffect = r.effect;
    renderSelected();
    renderTargets();
    renderTargetPicker();
}

// render lists
function renderDevices() {
    const box = el("devicesList");
    const q = (el("deviceSearch").value || "").toLowerCase().trim();
    const list = q ? devices.filter(d => (d.name || "").toLowerCase().includes(q)) : devices;

    box.innerHTML = "";
    if (!list.length) { box.innerHTML = `<div class="muted">No devices.</div>`; return; }

    for (const d of list) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(d.name)}</div>
        <div class="fxChips">
          <span class="chip ${d.isEnabled ? "" : "warn"}">${d.isEnabled ? "Enabled" : "Disabled"}</span>
          <span class="chip subtle">CD ${d.cooldownSeconds}s</span>
          <span class="chip subtle">Max ${d.maxPulseSeconds}s</span>
        </div>
      </div>
      <button class="btn" data-add="${d.id}">+ Target</button>
    `;
        row.querySelector("[data-add]").addEventListener("click", async () => {
            if (!selectedEffectId) return setOut("Select an effect first.");
            el("targetType").value = "1";
            renderTargetPicker();
            el("targetPicker").value = d.id;
            openDrawer();
        });
        box.appendChild(row);
    }
}

function renderGroups() {
    const box = el("groupsList");
    const q = (el("groupSearch").value || "").toLowerCase().trim();
    const list = q ? groups.filter(g => (g.name || "").toLowerCase().includes(q)) : groups;

    box.innerHTML = "";
    if (!list.length) { box.innerHTML = `<div class="muted">No groups.</div>`; return; }

    for (const g of list) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(g.name)}</div>
      </div>
      <button class="btn" data-add="${g.id}">+ Target</button>
    `;
        row.querySelector("[data-add]").addEventListener("click", async () => {
            if (!selectedEffectId) return setOut("Select an effect first.");
            el("targetType").value = "2";
            renderTargetPicker();
            el("targetPicker").value = g.id;
            openDrawer();
        });
        box.appendChild(row);
    }
}

function renderEffects() {
    const box = el("effectsList");
    const q = (el("effectSearch").value || "").toLowerCase().trim();
    const list = q ? effects.filter(e => (e.name || "").toLowerCase().includes(q)) : effects;

    box.innerHTML = "";
    if (!list.length) { box.innerHTML = `<div class="muted">No effects yet.</div>`; return; }

    for (const e of list) {
        const btn = document.createElement("button");
        btn.className = "listItem" + (e.id === selectedEffectId ? " selected" : "");
        btn.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(e.name)}</div>
        <div class="fxChips">
          <span class="chip">${actionLabel(e.action)}</span>
          <span class="chip subtle">${e.durationSeconds}s</span>
          <span class="chip subtle">CD ${e.cooldownSeconds}s</span>
        </div>
      </div>
    `;
        btn.addEventListener("click", async () => {
            selectedEffectId = e.id;
            renderEffects();
            await loadEffectDetails(e.id);
        });
        box.appendChild(btn);
    }
}

function syncDevices() {
    try {
        setOut("Syncing from Tuya…");
        const r = await api("/api/effects/devices/sync-tuya", { method: "POST" });
        setOut(r);
        await loadDevices();
    } catch (e) {
        setOut(e.payload || { error: e.message });
    }
}

function renderSelected() {
    if (!selectedEffect) {
        el("selName").textContent = "Select an effect";
        el("selMeta").textContent = "—";
        el("selChips").innerHTML = "";
        el("btnRun").disabled = true;
        el("btnRunSelectedMini").disabled = true;
        return;
    }

    el("selName").textContent = selectedEffect.name;
    el("selMeta").textContent = `${actionLabel(selectedEffect.action)} • ${selectedEffect.durationSeconds}s • CD ${selectedEffect.cooldownSeconds}s`;
    el("selChips").innerHTML = `
    <span class="chip ${selectedEffect.isEnabled ? "" : "warn"}">${selectedEffect.isEnabled ? "Enabled" : "Disabled"}</span>
    <span class="chip subtle">Targets ${selectedEffect.targets?.length ?? 0}</span>
  `;
    el("btnRun").disabled = false;
    el("btnRunSelectedMini").disabled = false;
}

function renderTargets() {
    const box = el("targetsList");
    box.innerHTML = "";

    const targets = selectedEffect?.targets || [];
    if (!targets.length) {
        box.innerHTML = `<div class="muted">No targets yet. Click “+ Add”.</div>`;
        return;
    }

    for (const t of targets) {
        const name = t.targetType === 1 ? (t.deviceName || t.deviceId) : (t.groupName || t.groupId);
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(name || "(unknown)")}</div>
        <div class="fxChips">
          <span class="chip">${t.targetType === 1 ? "Device" : "Group"}</span>
          ${t.durationSecondsOverride ? `<span class="chip warn">Override ${t.durationSecondsOverride}s</span>` : `<span class="chip subtle">No override</span>`}
          <span class="chip subtle">Sort ${t.sortOrder}</span>
        </div>
      </div>
      <button class="btn" data-del="${t.id}">Remove</button>
    `;
        row.querySelector("[data-del]").addEventListener("click", async () => {
            try {
                await api(`/api/effects/targets/${t.id}`, { method: "DELETE" });
                addActivity("ok", "Target removed", name, { ok: true, targetId: t.id });
                await loadEffectDetails(selectedEffectId);
            } catch (e) {
                addActivity("bad", "Remove failed", e.message, e.payload || { error: e.message });
                setOut(e.payload || { error: e.message });
            }
        });
        box.appendChild(row);
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
        opt.textContent = it.name;
        picker.appendChild(opt);
    }
}

// actions
async function createEffect() {
    const name = el("newEffectName").value.trim();
    const action = Number(el("newEffectAction").value);
    const durationSeconds = Number(el("newEffectDuration").value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown").value || 2);

    if (!name) return setOut("Name required");

    try {
        const r = await api(EFFECTS_BASE, {
            method: "POST",
            body: JSON.stringify({ name, action, durationSeconds, cooldownSeconds }),
        });
        addActivity("ok", "Effect created", name, r);
        el("newEffectName").value = "";
        await loadEffects();
        if (r.effectId) {
            selectedEffectId = r.effectId;
            await loadEffectDetails(r.effectId);
            renderEffects();
        }
        setOut(r);
    } catch (e) {
        addActivity("bad", "Create failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

async function addTarget() {
    if (!selectedEffectId) return setOut("Select an effect first.");

    const targetType = Number(el("targetType").value);
    const pickId = el("targetPicker").value;
    const durRaw = el("targetDurationOverride").value.trim();
    const durationSecondsOverride = durRaw ? Number(durRaw) : null;
    const sortOrder = Number(el("targetSort").value || 0);

    const body = {
        targetType,
        deviceId: targetType === 1 ? pickId : null,
        groupId: targetType === 2 ? pickId : null,
        durationSecondsOverride,
        sortOrder,
    };

    try {
        const r = await api(`/api/effects/${selectedEffectId}/targets`, {
            method: "POST",
            body: JSON.stringify(body),
        });
        addActivity("ok", "Target added", `Effect ${selectedEffectId}`, r);
        await loadEffectDetails(selectedEffectId);
        setOut(r);
        closeDrawer();
    } catch (e) {
        addActivity("bad", "Add target failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

async function runSelected() {
    if (!selectedEffectId) return;
    try {
        const r = await api(`/api/effects/${selectedEffectId}/run`, { method: "POST" });
        addActivity("ok", "Effect ran", selectedEffect?.name || selectedEffectId, r);
        setOut(r);
    } catch (e) {
        addActivity("bad", "Run failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

// boot
document.addEventListener("DOMContentLoaded", async () => {
    // drawer buttons
    el("btnOpenDrawer").addEventListener("click", openDrawer);
    el("btnOpenDrawer2").addEventListener("click", openDrawer);
    el("btnCloseDrawer").addEventListener("click", closeDrawer);
    el("drawerBackdrop").addEventListener("click", closeDrawer);

    // refresh
    el("btnRefreshEffects").addEventListener("click", () => loadEffects().catch(e => setOut(e.message)));
    el("btnRefreshDevices").addEventListener("click", () => loadDevices().catch(e => setOut(e.message)));
    el("btnRefreshGroups").addEventListener("click", () => loadGroups().catch(e => setOut(e.message)));
    el("btnSyncTuya").addEventListener("click", () => syncDevices().catch(e => setOut(e.message)));

    // searches
    el("effectSearch").addEventListener("input", renderEffects);
    el("deviceSearch").addEventListener("input", renderDevices);
    el("groupSearch").addEventListener("input", renderGroups);

    // drawer tools
    el("btnCreateEffect").addEventListener("click", () => createEffect().catch(() => { }));
    el("targetType").addEventListener("change", renderTargetPicker);
    el("btnAddTarget").addEventListener("click", () => addTarget().catch(() => { }));

    // run
    el("btnRun").addEventListener("click", () => runSelected().catch(() => { }));
    el("btnRunSelectedMini").addEventListener("click", () => runSelected().catch(() => { }));

    // activity
    el("btnClearActivity").addEventListener("click", () => { activity = []; renderActivity(); });

    setOut("Loading…");
    await loadTuyaBadge();
    await loadDevices();
    await loadGroups();
    await loadEffects();
    renderSelected();
    renderActivity();
    setOut("Ready.");
});
