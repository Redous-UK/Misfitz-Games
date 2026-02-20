// effects-dashboard.js — matches effects-dashboard.html IDs exactly

const el = (id) => document.getElementById(id);
const EFFECTS_BASE = "/api/effects";
let effectSort = { key: "name", dir: "asc" }; // asc|desc
let filteredEffects = [];                    // keep a rendered list for keyboard nav

// Safe event binder (prevents null.addEventListener crashes)
function on(id, evt, handler) {
    const node = el(id);
    if (!node) return false;
    node.addEventListener(evt, handler);
    return true;
}

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
        try {
            payload = JSON.parse(text);
        } catch {
            payload = { _nonJson: true, raw: text };
        }
    }

    if (!res.ok) {
        const msg =
            payload?.error ||
            payload?.message ||
            `HTTP ${res.status} ${res.statusText} @ ${path}`;
        const err = new Error(msg);
        err.status = res.status;
        err.path = path;
        err.payload = payload;
        err.raw = text;
        throw err;
    }

    return payload;
}

const pretty = (x) => {
    try {
        return JSON.stringify(x, null, 2);
    } catch {
        return String(x);
    }
};

const esc = (s) =>
    String(s ?? "")
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#39;");

function setOut(obj) {
    const out = el("out");
    if (!out) return;
    out.textContent = typeof obj === "string" ? obj : pretty(obj);
}

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

// ---------- State ----------
let devices = [];
let groups = [];
let effects = [];
let selectedEffectId = null;
let selectedEffect = null;
let activity = [];

// ---------- Activity ----------
function addActivity(type, title, meta, raw) {
    activity.unshift({ type, title, meta, at: new Date(), raw });
    if (activity.length > 60) activity = activity.slice(0, 60);
    renderActivity();
}

function renderActivity() {
    const box = el("activity");
    if (!box) return;

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

// ---------- Drawer ----------
function openDrawer() {
    el("drawer")?.classList.remove("hidden");
    el("drawerBackdrop")?.classList.remove("hidden");
    document.body.classList.add("fxNoScroll");
}
function closeDrawer() {
    el("drawer")?.classList.add("hidden");
    el("drawerBackdrop")?.classList.add("hidden");
    document.body.classList.remove("fxNoScroll");
}

// ---------- Health ----------
async function loadTuyaBadge() {
    const badge = el("tuyaBadge");
    if (!badge) return;

    try {
        await api("/api/health/tuya");
        badge.className = "badge ok";
        badge.textContent = "Tuya: online";
    } catch (e) {
        badge.className = "badge warn";
        badge.textContent = "Tuya: offline";
        addActivity("bad", "Tuya offline", e.message, e.payload || { error: e.message });
    }
}

// ---------- Loads ----------
async function loadDevices() {
    const r = await api("/api/effects/devices");
    devices = r?.devices || [];
    renderDevices();
    renderTargetPicker();
}

async function loadGroups() {
    const r = await api("/api/effects/groups");
    groups = r?.groups || [];
    renderGroups();
    renderTargetPicker();
}

async function loadEffects() {
    const r = await api(EFFECTS_BASE);
    effects = r.effects || [];
    renderEffectsTable();
}

async function loadEffectDetails(effectId) {
    const r = await api(`/api/effects/${effectId}`);
    selectedEffect = r?.effect || null;
    renderSelected();
    renderTargets();
    renderTargetPicker();
}

// ---------- Render: Devices ----------
function renderDevices() {
    const box = el("devicesList");
    if (!box) return;

    const q = (el("deviceSearch")?.value || "").toLowerCase().trim();
    const list = q
        ? devices.filter((d) => (d.name || "").toLowerCase().includes(q))
        : devices;

    box.innerHTML = "";
    if (!list.length) {
        box.innerHTML = `<div class="muted">No devices.</div>`;
        return;
    }

    for (const d of list) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(d.name)}</div>
        <div class="fxChips">
          <span class="chip ${d.isEnabled ? "" : "warn"}">${d.isEnabled ? "Enabled" : "Disabled"}</span>
          <span class="chip subtle">CD ${esc(d.cooldownSeconds ?? 0)}s</span>
          <span class="chip subtle">Max ${esc(d.maxPulseSeconds ?? 0)}s</span>
        </div>
      </div>
      <button class="btn" data-add="${esc(d.id)}">+ Target</button>
    `;

        row.querySelector("[data-add]")?.addEventListener("click", () => {
            if (!selectedEffectId) return setOut("Select an effect first.");
            const tt = el("targetType");
            if (tt) tt.value = "1";
            renderTargetPicker();
            const picker = el("targetPicker");
            if (picker) picker.value = d.id;
            openDrawer();
        });

        box.appendChild(row);
    }
}

// ---------- Render: Groups ----------
function renderGroups() {
    const box = el("groupsList");
    if (!box) return;

    const q = (el("groupSearch")?.value || "").toLowerCase().trim();
    const list = q
        ? groups.filter((g) => (g.name || "").toLowerCase().includes(q))
        : groups;

    box.innerHTML = "";
    if (!list.length) {
        box.innerHTML = `<div class="muted">No groups.</div>`;
        return;
    }

    for (const g of list) {
        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(g.name)}</div>
      </div>
      <button class="btn" data-add="${esc(g.id)}">+ Target</button>
    `;

        row.querySelector("[data-add]")?.addEventListener("click", () => {
            if (!selectedEffectId) return setOut("Select an effect first.");
            const tt = el("targetType");
            if (tt) tt.value = "2";
            renderTargetPicker();
            const picker = el("targetPicker");
            if (picker) picker.value = g.id;
            openDrawer();
        });

        box.appendChild(row);
    }
}

// ---------- Render: Effects ----------
function renderEffectsTable() {
    const tbody = el("effectsTbody");
    if (!tbody) return;

    const q = (el("effectSearch")?.value || "").toLowerCase().trim();

    let list = q
        ? effects.filter(e =>
            ((e.name || "").toLowerCase().includes(q)) ||
            String(e.id || "").toLowerCase().includes(q) ||
            actionLabel(e.action).toLowerCase().includes(q))
        : effects;

    // Update count
    const count = el("effectsCount");
    if (count) count.textContent = String(list.length);

    const rows = [];

    if (!list.length) {
        const tr = document.createElement("tr");
        const td = document.createElement("td");

        td.colSpan = 7;
        td.className = "muted";
        td.textContent = "No effects yet.";

        tr.appendChild(td);
        rows.push(tr);
    } else {
        list.forEach((e, idx) => {
            const isSel = e.id === selectedEffectId;

            const tr = document.createElement("tr");
            tr.dataset.effectId = e.id;
            if (isSel) tr.classList.add("isSelected");

            // # column
            const tdIdx = document.createElement("td");
            tdIdx.className = "muted";
            tdIdx.style.width = "34px";
            tdIdx.textContent = String(idx + 1);
            tr.appendChild(tdIdx);

            // Name + id
            const tdName = document.createElement("td");

            const nameDiv = document.createElement("div");
            nameDiv.className = "strong";
            nameDiv.textContent = e.name ?? "";

            const idDiv = document.createElement("div");
            idDiv.className = "muted";
            idDiv.style.fontSize = "12px";
            idDiv.textContent = e.id ?? "";

            tdName.appendChild(nameDiv);
            tdName.appendChild(idDiv);
            tr.appendChild(tdName);

            // Action
            const tdAction = document.createElement("td");
            tdAction.style.width = "140px";
            tdAction.textContent = actionLabel(e.action);
            tr.appendChild(tdAction);

            // Duration
            const tdDur = document.createElement("td");
            tdDur.style.width = "90px";
            tdDur.textContent = `${e.durationSeconds ?? 0}s`;
            tr.appendChild(tdDur);

            // Cooldown
            const tdCd = document.createElement("td");
            tdCd.style.width = "90px";
            tdCd.textContent = `${e.cooldownSeconds ?? 0}s`;
            tr.appendChild(tdCd);

            // Enabled chip
            const tdEnabled = document.createElement("td");
            tdEnabled.style.width = "90px";

            const chip = document.createElement("span");
            chip.className = "chip" + (e.isEnabled ? "" : " warn");
            chip.textContent = e.isEnabled ? "Enabled" : "Disabled";

            tdEnabled.appendChild(chip);
            tr.appendChild(tdEnabled);

            // Load button
            const tdBtn = document.createElement("td");
            tdBtn.style.width = "160px";
            tdBtn.style.textAlign = "right";

            const btn = document.createElement("button");
            btn.className = "btn btnTiny" + (isSel ? " primary" : "");
            btn.dataset.load = e.id;
            btn.textContent = isSel ? "Loaded" : "Load";

            tdBtn.appendChild(btn);
            tr.appendChild(tdBtn);

            rows.push(tr);
        });
    }

    // 🔥 Replace all rows at once
    tbody.replaceChildren(...rows);
}



// ---------- Selected effect header (right) ----------
function renderSelected() {
    const btnRun = el("btnRun");
    const btnRunMini = el("btnRunSelectedMini");

    if (!selectedEffect) {
        el("selName") && (el("selName").textContent = "Select an effect");
        el("selMeta") && (el("selMeta").textContent = "—");
        el("selChips") && (el("selChips").innerHTML = "");
        if (btnRun) btnRun.disabled = true;
        if (btnRunMini) btnRunMini.disabled = true;
        return;
    }

    el("selName") && (el("selName").textContent = selectedEffect.name);
    el("selMeta") &&
        (el("selMeta").textContent =
            `${actionLabel(selectedEffect.action)} • ${selectedEffect.durationSeconds}s • CD ${selectedEffect.cooldownSeconds}s`);
    el("selChips") &&
        (el("selChips").innerHTML = `
      <span class="chip ${selectedEffect.isEnabled ? "" : "warn"}">${selectedEffect.isEnabled ? "Enabled" : "Disabled"}</span>
      <span class="chip subtle">Targets ${selectedEffect.targets?.length ?? 0}</span>
    `);

    if (btnRun) btnRun.disabled = false;
    if (btnRunMini) btnRunMini.disabled = false;
}

// ---------- Targets list (right) ----------
function renderTargets() {
    const box = el("targetsList");
    if (!box) return;

    box.innerHTML = "";

    const targets = selectedEffect?.targets || [];
    if (!targets.length) {
        box.innerHTML = `<div class="muted">No targets yet. Click “+ Add”.</div>`;
        return;
    }

    for (const t of targets) {
        const name =
            t.targetType === 1
                ? (t.deviceName || t.deviceId)
                : (t.groupName || t.groupId);

        const row = document.createElement("div");
        row.className = "listRow";
        row.innerHTML = `
      <div class="grow">
        <div class="strong">${esc(name || "(unknown)")}</div>
        <div class="fxChips">
          <span class="chip">${t.targetType === 1 ? "Device" : "Group"}</span>
          ${t.durationSecondsOverride
                ? `<span class="chip warn">Override ${esc(t.durationSecondsOverride)}s</span>`
                : `<span class="chip subtle">No override</span>`}
          <span class="chip subtle">Sort ${esc(t.sortOrder ?? 0)}</span>
        </div>
      </div>
      <button class="btn" data-del="${esc(t.id)}">Remove</button>
    `;

        row.querySelector("[data-del]")?.addEventListener("click", async () => {
            try {
                await api(`/api/effects/targets/${t.id}`, { method: "DELETE" });
                addActivity("ok", "Target removed", name, { ok: true, targetId: t.id });
                await loadEffectDetails(selectedEffectId);
            } catch (e) {
                addActivity("bad", "Remove failed", e.message, e.payload || { error: e.message });
                setOut(e.payload || { error: e.message, raw: e.raw });
            }
        });

        box.appendChild(row);
    }
}

// ---------- Target picker (drawer) ----------
function renderTargetPicker() {
    const typeEl = el("targetType");
    const picker = el("targetPicker");
    if (!typeEl || !picker) return;

    const type = Number(typeEl.value);
    picker.innerHTML = "";

    const items = type === 1 ? devices : groups;

    for (const it of items) {
        const opt = document.createElement("option");
        opt.value = it.id;
        opt.textContent = it.name;
        picker.appendChild(opt);
    }
}

// ---------- Actions ----------
async function syncDevices() {
    try {
        setOut("Syncing from Tuya…");
        const r = await api("/api/effects/devices/sync", { method: "POST" });
        setOut(r);
        addActivity("ok", "Tuya sync complete", `${r.added} added, ${r.updated} updated`, r);
        await loadDevices();
    } catch (e) {
        addActivity("bad", "Tuya sync failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message, raw: e.raw });
    }
}

async function createEffect() {
    const name = el("newEffectName")?.value?.trim() || "";
    const action = Number(el("newEffectAction")?.value || 1);
    const durationSeconds = Number(el("newEffectDuration")?.value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown")?.value || 0);

    if (!name) return setOut("Name required");

    try {
        const r = await api(EFFECTS_BASE, {
            method: "POST",
            body: JSON.stringify({ name, action, durationSeconds, cooldownSeconds }),
        });

        addActivity("ok", "Effect created", name, r);
        if (el("newEffectName")) el("newEffectName").value = "";

        await loadEffects();

        if (r.effectId) {
            selectedEffectId = r.effectId;
            await loadEffectDetails(r.effectId);
            renderEffectsTable();
        }

        setOut(r);
    } catch (e) {
        addActivity("bad", "Create failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message, raw: e.raw });
    }
}

async function addTarget() {
    if (!selectedEffectId) return setOut("Select an effect first.");

    const targetType = Number(el("targetType")?.value || 1);
    const pickId = el("targetPicker")?.value;
    const durRaw = (el("targetDurationOverride")?.value || "").trim();
    const durationSecondsOverride = durRaw ? Number(durRaw) : null;
    const sortOrder = Number(el("targetSort")?.value || 0);

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
        setOut(e.payload || { error: e.message, raw: e.raw });
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
        setOut(e.payload || { error: e.message, raw: e.raw });
    }
}



// ---------- Boot ----------
document.addEventListener("DOMContentLoaded", async () => {

    // ----- Basic DOM sanity check -----
    if (!el("effectsTbody") || !el("devicesList") || !el("groupsList")) {
        setOut({
            error: "Dashboard HTML mismatch",
            missing: {
                effectsTbody: !el("effectsTbody"),
                devicesList: !el("devicesList"),
                groupsList: !el("groupsList"),
            },
            path: location.pathname,
        });
        return;
    }

    // ----- Drawer controls -----
    on("btnOpenDrawer", "click", openDrawer);
    on("btnOpenDrawer2", "click", openDrawer);
    on("btnCloseDrawer", "click", closeDrawer);
    on("drawerBackdrop", "click", closeDrawer);

    // ----- Refresh buttons -----
    on("btnRefreshDevices", "click", () =>
        loadDevices().catch(e => setOut(e.payload || e.message))
    );

    on("btnRefreshGroups", "click", () =>
        loadGroups().catch(e => setOut(e.payload || e.message))
    );

    on("btnRefreshEffects", "click", () =>
        loadEffects().catch(e => setOut(e.payload || e.message))
    );

    on("btnSyncTuya", "click", () =>
        syncDevices().catch(e => setOut(e.payload || e.message))
    );

    // ----- Search boxes -----
    on("deviceSearch", "input", renderDevices);
    on("groupSearch", "input", renderGroups);
    on("effectSearch", "input", renderEffectsTable);

    // ----- Drawer tools -----
    on("btnCreateEffect", "click", () =>
        createEffect().catch(() => { })
    );

    on("targetType", "change", renderTargetPicker);

    on("btnAddTarget", "click", () =>
        addTarget().catch(() => { })
    );

    // ----- Run buttons -----
    on("btnRun", "click", () =>
        runSelected().catch(() => { })
    );

    on("btnRunSelectedMini", "click", () =>
        runSelected().catch(() => { })
    );

    // ----- Activity -----
    on("btnClearActivity", "click", () => {
        activity = [];
        renderActivity();
    });

    // =========================================================
    // EFFECTS TABLE — Delegated click handler (Load buttons)
    // =========================================================
    on("effectsTbody", "click", async (ev) => {
        const btn = ev.target.closest("button[data-load]");
        if (!btn) return;

        const id = btn.dataset.load;
        if (!id) return;

        try {
            selectedEffectId = id;

            renderEffectsTable();        // highlight selection
            await loadEffectDetails(id); // load targets + meta

            const badge = el("effectLoadedBadge");
            if (badge) {
                badge.textContent = `Loaded: ${selectedEffect?.name || id}`;
            }

            addActivity("ok", "Effect loaded", selectedEffect?.name || id, { effectId: id });

        } catch (err) {
            addActivity("bad", "Load failed", err.message, err.payload || { error: err.message });
            setOut(err.payload || { error: err.message });
        }
    });

    // =========================================================
    // INITIAL LOAD SEQUENCE
    // =========================================================

    setOut("Loading…");

    try {
        await loadTuyaBadge();
        await loadDevices();
        await loadGroups();
        await loadEffects();

        renderSelected();
        renderActivity();

        setOut(
            `Ready. devices=${devices.length} groups=${groups.length} effects=${effects.length}`
        );

    } catch (e) {
        console.error(e);
        addActivity("bad", "Boot failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
});
