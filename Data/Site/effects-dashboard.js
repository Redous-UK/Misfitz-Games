// effects-dashboard.js — matches effects-dashboard.html IDs exactly

const el = (id) => document.getElementById(id);
const EFFECTS_BASE = "/api/effects";
let effectSort = { key: "name", dir: "asc" }; // asc|desc
let filteredEffects = [];                    // keep a rendered list for keyboard nav
let liveMode = false;
let liveTimer = null;

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
function mk(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text !== undefined) n.textContent = text;
    return n;
}

function renderEffectsTable() {
    const tbody = el("effectsTbody");
    if (!tbody) return;

    const q = (el("effectSearch")?.value || "").toLowerCase().trim();

    let list = q
        ? effects.filter(e =>
            (e.name || "").toLowerCase().includes(q) ||
            String(e.id || "").toLowerCase().includes(q) ||
            actionLabel(e.action).toLowerCase().includes(q))
        : effects;

    list = sortEffects(list);
    filteredEffects = list;

    const count = el("effectsCount");
    if (count) count.textContent = String(list.length);

    const rows = [];

    if (!list.length) {
        const tr = mk("tr");
        const td = mk("td", "muted", "No effects yet.");
        td.colSpan = 7;
        tr.appendChild(td);
        rows.push(tr);
        tbody.replaceChildren(...rows);
        return;
    }

    list.forEach((e, idx) => {
        const isSel = e.id === selectedEffectId;

        const tr = mk("tr", isSel ? "isSelected" : "");
        tr.dataset.effectId = e.id;

        // #
        const tdIdx = mk("td", "muted", String(idx + 1));
        tdIdx.style.width = "34px";
        tr.appendChild(tdIdx);

        // Name + id
        const tdName = mk("td");
        const nameDiv = mk("div", "strong", e.name ?? "");
        const idDiv = mk("div", "muted", e.id ?? "");
        idDiv.style.fontSize = "12px";
        tdName.appendChild(nameDiv);
        tdName.appendChild(idDiv);
        tr.appendChild(tdName);

        // Action
        const tdAction = mk("td", null, actionLabel(e.action));
        tdAction.style.width = "140px";
        tr.appendChild(tdAction);

        // Duration
        const tdDur = mk("td", null, `${e.durationSeconds ?? 0}s`);
        tdDur.style.width = "90px";
        tr.appendChild(tdDur);

        // Cooldown
        const tdCd = mk("td", null, `${e.cooldownSeconds ?? 0}s`);
        tdCd.style.width = "90px";
        tr.appendChild(tdCd);

        // Enabled
        const tdEnabled = mk("td");
        tdEnabled.style.width = "90px";
        const chip = mk("span", "chip" + (e.isEnabled ? "" : " warn"), e.isEnabled ? "Enabled" : "Disabled");
        tdEnabled.appendChild(chip);
        tr.appendChild(tdEnabled);

        // Actions
        const tdAct = mk("td");
        tdAct.style.width = "240px";
        tdAct.style.textAlign = "right";

        const btnLoad = mk("button", "btn btnTiny" + (isSel ? " primary" : ""), isSel ? "Loaded" : "Load");
        btnLoad.dataset.load = e.id;

        const btnRun = mk("button", "btn btnTiny", "Run");
        btnRun.dataset.run = e.id;

        const btnToggle = mk("button", "btn btnTiny", e.isEnabled ? "Disable" : "Enable");
        btnToggle.dataset.toggle = e.id;

        const btnEdit = mk("button", "btn btnTiny", "Edit");
        btnEdit.dataset.edit = e.id;

        const btnDel = mk("button", "btn btnTiny warn", "Delete");
        btnDel.dataset.del = e.id;

        // spacing
        tdAct.appendChild(btnLoad);
        tdAct.appendChild(document.createTextNode(" "));
        tdAct.appendChild(btnRun);
        tdAct.appendChild(document.createTextNode(" "));
        tdAct.appendChild(btnToggle);
        tdAct.appendChild(document.createTextNode(" "));
        tdAct.appendChild(btnEdit);
        tdAct.appendChild(document.createTextNode(" "));
        tdAct.appendChild(btnDel);

        tr.appendChild(tdAct);

        rows.push(tr);
    });

    tbody.replaceChildren(...rows);
}

function effectKeyVal(e, key) {
    switch (key) {
        case "name": return (e.name || "").toLowerCase();
        case "action": return normAction(e.action);
        case "duration": return e.durationSeconds ?? 0;
        case "cooldown": return e.cooldownSeconds ?? 0;
        case "enabled": return e.isEnabled ? 1 : 0;
        default: return 0;
    }
}

function sortEffects(list) {
    const dir = effectSort.dir === "asc" ? 1 : -1;
    const key = effectSort.key;
    return [...list].sort((a, b) => {
        const av = effectKeyVal(a, key);
        const bv = effectKeyVal(b, key);
        if (av < bv) return -1 * dir;
        if (av > bv) return 1 * dir;
        return 0;
    });
}

function fillEditEffectForm(effect) {
    if (!effect) return;
    el("newEffectName").value = effect.name ?? "";
    el("newEffectAction").value = String(normAction(effect.action));
    el("newEffectDuration").value = String(effect.durationSeconds ?? 2);
    el("newEffectCooldown").value = String(effect.cooldownSeconds ?? 0);

    // Change button label to make it obvious
    const btn = el("btnCreateEffect");
    if (btn) btn.textContent = "Save";
}

function resetCreateEffectForm() {
    el("newEffectName").value = "";
    el("newEffectAction").value = "1";
    el("newEffectDuration").value = "2";
    el("newEffectCooldown").value = "2";
    const btn = el("btnCreateEffect");
    if (btn) btn.textContent = "Create";
}

async function toggleEffectEnabled(effectId) {
    const cur = effects.find(e => e.id === effectId);
    const next = !(cur?.isEnabled ?? true);
    const r = await patchEffect(effectId, { isEnabled: next });
    addActivity("ok", "Effect toggled", `${cur?.name || effectId} → ${next ? "Enabled" : "Disabled"}`, r);
    return r;
}

async function deleteEffect(effectId) {
    return await api(`${EFFECTS_BASE}/${effectId}`, { method: "DELETE" });
}

async function saveEffectEdits(effectId, patch) {
    const r = await api(`${EFFECTS_BASE}/${effectId}`, {
        method: "PATCH",
        body: JSON.stringify(patch)
    });
    addActivity("ok", "Effect saved", patch.name || effectId, r);
    return r;
}

async function deleteEffect(effectId) {
    const r = await api(`${EFFECTS_BASE}/${effectId}`, { method: "DELETE" });
    return r;
}

function setLiveMode(on) {
    liveMode = on;
    if (liveTimer) { clearInterval(liveTimer); liveTimer = null; }
    if (liveMode) {
        liveTimer = setInterval(() => loadEffects().catch(() => { }), 5000);
    }
}

let editMode = false; // true when drawer is editing an existing effect

function setEditMode(on) {
    editMode = on;

    const btn = el("btnCreateEffect");
    if (btn) btn.textContent = editMode ? "Save" : "Create";

    const title = document.querySelector("#drawer .sectionTitle");
    // optional: if you want the drawer section title to reflect mode
    // (only safe if the first sectionTitle in drawer is CREATE EFFECT)
    if (title && title.textContent?.trim().toLowerCase() === "create effect") {
        title.textContent = editMode ? "EDIT EFFECT" : "CREATE EFFECT";
    }
}

function fillEditEffectForm(effect) {
    if (!effect) return;

    el("newEffectName").value = effect.name ?? "";
    el("newEffectAction").value = String(normAction(effect.action));
    el("newEffectDuration").value = String(effect.durationSeconds ?? 2);
    el("newEffectCooldown").value = String(effect.cooldownSeconds ?? 0);

    setEditMode(true);
}

function resetEffectForm() {
    el("newEffectName").value = "";
    el("newEffectAction").value = "1";
    el("newEffectDuration").value = "2";
    el("newEffectCooldown").value = "2";
    setEditMode(false);
}

async function patchEffect(effectId, patch) {
    return await api(`${EFFECTS_BASE}/${effectId}`, {
        method: "PATCH",
        body: JSON.stringify(patch),
    });
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

async function createOrSaveEffect() {
    const name = el("newEffectName").value.trim();
    const action = Number(el("newEffectAction").value);
    const durationSeconds = Number(el("newEffectDuration").value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown").value || 0);

    if (!name) return setOut("Name required");

    // common payload fields
    const payload = { name, action, durationSeconds, cooldownSeconds };

    try {
        // SAVE (PATCH)
        if (editMode && selectedEffectId) {
            setOut("Saving…");

            const r = await patchEffect(selectedEffectId, payload);

            addActivity("ok", "Effect saved", name, r);

            await loadEffects();
            await loadEffectDetails(selectedEffectId);
            renderEffectsTable();
            renderSelected();
            setOut(r);

            closeDrawer();
            return;
        }

        // CREATE (POST)
        setOut("Creating…");

        const r = await api(EFFECTS_BASE, {
            method: "POST",
            body: JSON.stringify(payload),
        });

        addActivity("ok", "Effect created", name, r);

        await loadEffects();

        if (r.effectId) {
            selectedEffectId = r.effectId;
            await loadEffectDetails(r.effectId);
            renderEffectsTable();
            renderSelected();
            const badge = el("effectLoadedBadge");
            if (badge) badge.textContent = `Loaded: ${selectedEffect?.name || r.effectId}`;
        }

        resetEffectForm();
        setOut(r);
        closeDrawer();
    } catch (e) {
        addActivity("bad", editMode ? "Save failed" : "Create failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

function updateLoadedBadge() {
    const badge = el("effectLoadedBadge");
    if (!badge) return;
    if (!selectedEffect) { badge.textContent = ""; return; }
    badge.textContent = `Loaded: ${selectedEffect.name} (${selectedEffectId})`;
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
    on("btnCreateEffect", "click", () => createOrSaveEffect().catch(() => { }));

    on("targetType", "change", renderTargetPicker);

    on("btnAddTarget", "click", () =>
        addTarget().catch(() => { })
    );
    on("btnOpenDrawer", "click", openDrawerForCreate);

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
        const btn = ev.target.closest("button");
        const row = ev.target.closest("tr[data-effect-id]");
        const id =
            btn?.dataset.load || btn?.dataset.run || btn?.dataset.toggle ||
            btn?.dataset.edit || btn?.dataset.del || row?.dataset.effectId;

        if (!id) return;

        try {
            // Load (also row click falls through here)
            if (!btn || btn.dataset.load || row) {
                selectedEffectId = id;
                renderEffectsTable();
                await loadEffectDetails(id);
                const badge = el("effectLoadedBadge");
                if (badge) badge.textContent = `Loaded: ${selectedEffect?.name || id}`;
                return;
            }

            // Run
            if (btn.dataset.run) {
                selectedEffectId = id;
                await runSelected();
                return;
            }

            // Toggle enabled (requires backend patch endpoint)
            if (btn.dataset.toggle) {
                await toggleEffectEnabled(id);
                await loadEffects();
                if (selectedEffectId === id) await loadEffectDetails(id);
                return;
            }

            // Edit (prefill drawer; reuse create form)
            if (btn.dataset.edit) {
                selectedEffectId = id;
                await loadEffectDetails(id);
                openDrawer();
                fillEditEffectForm(selectedEffect);
                return;
            }

            function openDrawerForCreate() {
                resetEffectForm();
                openDrawer();
            }

            // Delete (requires backend delete endpoint)
            if (btn.dataset.del) {
                const name = effects.find(x => x.id === id)?.name || id;
                if (!confirm(`Delete effect "${name}"? This cannot be undone.`)) return;
                await deleteEffect(id);
                if (selectedEffectId === id) { selectedEffectId = null; selectedEffect = null; renderSelected(); renderTargets(); }
                await loadEffects();
                addActivity("ok", "Effect deleted", name, { effectId: id });
                return;
            }
        } catch (e) {
            addActivity("bad", "Action failed", e.message, e.payload || { error: e.message });
            setOut(e.payload || { error: e.message });
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
