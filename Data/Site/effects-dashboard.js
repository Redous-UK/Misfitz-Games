// ============================
// Misfitz Effects Dashboard JS
// Production-grade baseline
// ============================

const el = (id) => document.getElementById(id);

function on(id, evt, handler) {
    const node = el(id);
    if (!node) return;
    node.addEventListener(evt, handler);
}

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

function setOut(obj) {
    const o = el("out");
    if (!o) return;
    o.textContent = typeof obj === "string" ? obj : pretty(obj);
}

// ---------- action helpers ----------
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

// ---------- DOM helpers (no innerHTML) ----------
function mk(tag, cls, text) {
    const n = document.createElement(tag);
    if (cls) n.className = cls;
    if (text !== undefined) n.textContent = text;
    return n;
}

// ---------- state ----------
let devices = [];
let groups = [];
let effects = [];

let selectedEffectId = null;
let selectedEffect = null;

let activity = [];

let editMode = false; // drawer is editing an effect when true

// ---------- activity ----------
function addActivity(type, title, meta, raw) {
    activity.unshift({ type, title, meta, at: new Date(), raw });
    if (activity.length > 50) activity = activity.slice(0, 50);
    renderActivity();
}

function renderActivity() {
    const box = el("activity");
    if (!box) return;

    box.replaceChildren();

    if (!activity.length) {
        box.appendChild(mk("div", "muted", "No activity yet."));
        return;
    }

    for (const a of activity) {
        const t = a.at.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });

        const div = mk("div", "fxActItem " + (a.type === "ok" ? "ok" : a.type === "bad" ? "bad" : ""));
        const ttl = mk("div", "fxActTitle", a.title);
        const meta = mk("div", "fxActMeta", `${t} • ${a.meta || ""}`);

        div.appendChild(ttl);
        div.appendChild(meta);

        div.addEventListener("click", () => {
            if (a.raw) setOut(a.raw);
        });

        box.appendChild(div);
    }
}

// ---------- drawer ----------
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

function setEditMode(on) {
    editMode = on;
    const btn = el("btnCreateEffect");
    if (btn) btn.textContent = editMode ? "Save" : "Create";

    // Optional: if you add <div id="effectFormTitle">CREATE EFFECT</div>
    const title = el("effectFormTitle");
    if (title) title.textContent = editMode ? "EDIT EFFECT" : "CREATE EFFECT";
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

// ---------- health badge ----------
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

// ---------- loads ----------
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
    renderEffectsTable();
}

async function loadEffectDetails(effectId) {
    const r = await api(`/api/effects/${effectId}`);
    selectedEffect = r.effect;
    renderSelected();
    renderTargets();
    renderTargetPicker();

    const badge = el("effectLoadedBadge");
    if (badge) badge.textContent = `Loaded: ${selectedEffect?.name || effectId}`;
}

// ---------- render: devices/groups ----------
function renderDevices() {
    const box = el("devicesList");
    if (!box) return;

    const q = (el("deviceSearch")?.value || "").toLowerCase().trim();
    const list = q ? devices.filter(d => (d.name || "").toLowerCase().includes(q)) : devices;

    box.replaceChildren();

    if (!list.length) {
        box.appendChild(mk("div", "muted", "No devices."));
        return;
    }

    for (const d of list) {
        const row = mk("div", "listRow");

        const left = mk("div", "grow");
        left.appendChild(mk("div", "strong", d.name || "(unnamed)"));

        const chips = mk("div", "fxChips");
        const c1 = mk("span", "chip" + (d.isEnabled ? "" : " warn"), d.isEnabled ? "Enabled" : "Disabled");
        const c2 = mk("span", "chip subtle", `CD ${d.cooldownSeconds}s`);
        const c3 = mk("span", "chip subtle", `Max ${d.maxPulseSeconds}s`);
        chips.appendChild(c1); chips.appendChild(c2); chips.appendChild(c3);
        left.appendChild(chips);

        const btn = mk("button", "btn", "+ Target");
        btn.dataset.addDevice = d.id;

        row.appendChild(left);
        row.appendChild(btn);

        box.appendChild(row);
    }
}

function renderGroups() {
    const box = el("groupsList");
    if (!box) return;

    const q = (el("groupSearch")?.value || "").toLowerCase().trim();
    const list = q ? groups.filter(g => (g.name || "").toLowerCase().includes(q)) : groups;

    box.replaceChildren();

    if (!list.length) {
        box.appendChild(mk("div", "muted", "No groups."));
        return;
    }

    for (const g of list) {
        const row = mk("div", "listRow");

        const left = mk("div", "grow");
        left.appendChild(mk("div", "strong", g.name || "(unnamed)"));

        const btn = mk("button", "btn", "+ Target");
        btn.dataset.addGroup = g.id;

        row.appendChild(left);
        row.appendChild(btn);

        box.appendChild(row);
    }
}

// ---------- render: effects table (DOM-only) ----------
function renderEffectsTable() {
    const tbody = el("effectsTbody");
    if (!tbody) return;

    const q = (el("effectSearch")?.value || "").toLowerCase().trim();
    const list = q
        ? effects.filter(e =>
            (e.name || "").toLowerCase().includes(q) ||
            String(e.id || "").toLowerCase().includes(q) ||
            actionLabel(e.action).toLowerCase().includes(q))
        : effects;

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

        // Name + ID
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

        // Actions (widen in HTML/CSS if clipped)
        const tdAct = mk("td");
        tdAct.style.width = "240px";
        tdAct.style.textAlign = "right";
        tdAct.style.whiteSpace = "nowrap";

        const btnLoad = mk("button", "btn btnTiny" + (isSel ? " primary" : ""), isSel ? "Loaded" : "Load");
        btnLoad.dataset.load = e.id;

        const btnRun = mk("button", "btn btnTiny primary", "Run");
        btnRun.dataset.run = e.id;

        const btnToggle = mk("button", "btn btnTiny", e.isEnabled ? "Disable" : "Enable");
        btnToggle.dataset.toggle = e.id;

        const btnEdit = mk("button", "btn btnTiny", "Edit");
        btnEdit.dataset.edit = e.id;

        const btnDel = mk("button", "btn btnTiny warn", "Delete");
        btnDel.dataset.del = e.id;

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

// ---------- render selected + targets ----------
function renderSelected() {
    const name = el("selName");
    const meta = el("selMeta");
    const chips = el("selChips");

    if (!name || !meta || !chips) return;

    if (!selectedEffect) {
        name.textContent = "Select an effect";
        meta.textContent = "—";
        chips.replaceChildren();
        el("btnRun") && (el("btnRun").disabled = true);
        el("btnRunSelectedMini") && (el("btnRunSelectedMini").disabled = true);
        return;
    }

    name.textContent = selectedEffect.name;
    meta.textContent = `${actionLabel(selectedEffect.action)} • ${selectedEffect.durationSeconds}s • CD ${selectedEffect.cooldownSeconds}s`;

    chips.replaceChildren();

    const c1 = mk("span", "chip" + (selectedEffect.isEnabled ? "" : " warn"), selectedEffect.isEnabled ? "Enabled" : "Disabled");
    const c2 = mk("span", "chip subtle", `Targets ${selectedEffect.targets?.length ?? 0}`);
    chips.appendChild(c1);
    chips.appendChild(c2);

    el("btnRun") && (el("btnRun").disabled = false);
    el("btnRunSelectedMini") && (el("btnRunSelectedMini").disabled = false);
}

function renderTargets() {
    const box = el("targetsList");
    if (!box) return;

    box.replaceChildren();

    const targets = selectedEffect?.targets || [];
    if (!targets.length) {
        box.appendChild(mk("div", "muted", "No targets yet. Click “+ Add”."));
        return;
    }

    targets
        .slice()
        .sort((a, b) => (a.sortOrder ?? 0) - (b.sortOrder ?? 0))
        .forEach(t => {
            const name = t.targetType === 1 ? (t.deviceName || t.deviceId) : (t.groupName || t.groupId);
            const row = mk("div", "listRow");

            const left = mk("div", "grow");
            left.appendChild(mk("div", "strong", name || "(unknown)"));

            const chips = mk("div", "fxChips");
            chips.appendChild(mk("span", "chip", t.targetType === 1 ? "Device" : "Group"));
            chips.appendChild(mk("span", "chip subtle", `Sort ${t.sortOrder ?? 0}`));
            if (t.durationSecondsOverride) chips.appendChild(mk("span", "chip warn", `Override ${t.durationSecondsOverride}s`));
            else chips.appendChild(mk("span", "chip subtle", "No override"));
            left.appendChild(chips);

            const btn = mk("button", "btn", "Remove");
            btn.dataset.delTarget = t.id;

            row.appendChild(left);
            row.appendChild(btn);
            box.appendChild(row);
        });
}

function renderTargetPicker() {
    const typeEl = el("targetType");
    const picker = el("targetPicker");
    if (!typeEl || !picker) return;

    const type = Number(typeEl.value);
    const items = type === 1 ? devices : groups;

    const opts = [];
    for (const it of items) {
        const opt = document.createElement("option");
        opt.value = it.id;
        opt.textContent = it.name || it.id;
        opts.push(opt);
    }

    picker.replaceChildren(...opts);
}

// ---------- actions ----------
async function syncDevices() {
    try {
        setOut("Syncing from Tuya…");
        const r = await api("/api/effects/devices/sync", { method: "POST" });
        setOut(r);
        addActivity("ok", "Synced from Tuya", `added=${r.added} updated=${r.updated}`, r);
        await loadDevices();
    } catch (e) {
        addActivity("bad", "Sync failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

async function addTarget() {
    if (!selectedEffectId) return setOut("Select an effect first.");

    const targetType = Number(el("targetType")?.value || 1);
    const pickId = el("targetPicker")?.value;
    if (!pickId) return setOut("Pick a target first.");

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
        addActivity("ok", "Target added", selectedEffect?.name || selectedEffectId, r);
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

async function patchEffect(effectId, patch) {
    return await api(`${EFFECTS_BASE}/${effectId}`, {
        method: "PATCH",
        body: JSON.stringify(patch),
    });
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

async function createOrSaveEffect() {
    const name = el("newEffectName").value.trim();
    const action = Number(el("newEffectAction").value);
    const durationSeconds = Number(el("newEffectDuration").value || 2);
    const cooldownSeconds = Number(el("newEffectCooldown").value || 0);

    if (!name) return setOut("Name required");

    const payload = { name, action, durationSeconds, cooldownSeconds };

    try {
        if (editMode && selectedEffectId) {
            setOut("Saving…");
            const r = await patchEffect(selectedEffectId, payload);
            addActivity("ok", "Effect saved", name, r);

            await loadEffects();
            await loadEffectDetails(selectedEffectId);

            setOut(r);
            closeDrawer();
            return;
        }

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
        }

        resetEffectForm();
        setOut(r);
        closeDrawer();
    } catch (e) {
        addActivity("bad", editMode ? "Save failed" : "Create failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
}

// ============================
// Boot
// ============================
document.addEventListener("DOMContentLoaded", async () => {

    // Basic required nodes
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

    // Drawer controls
    on("btnOpenDrawer", "click", () => { resetEffectForm(); openDrawer(); });
    on("btnOpenDrawer2", "click", openDrawer);
    on("btnCloseDrawer", "click", closeDrawer);
    on("drawerBackdrop", "click", closeDrawer);

    // Refresh buttons
    on("btnRefreshDevices", "click", () => loadDevices().catch(e => setOut(e.payload || e.message)));
    on("btnRefreshGroups", "click", () => loadGroups().catch(e => setOut(e.payload || e.message)));
    on("btnRefreshEffects", "click", () => loadEffects().catch(e => setOut(e.payload || e.message)));
    on("btnSyncTuya", "click", () => syncDevices().catch(e => setOut(e.payload || e.message)));

    // Searches
    on("deviceSearch", "input", renderDevices);
    on("groupSearch", "input", renderGroups);
    on("effectSearch", "input", renderEffectsTable);

    // Drawer tools
    on("btnCreateEffect", "click", () => createOrSaveEffect().catch(() => { }));
    on("targetType", "change", renderTargetPicker);
    on("btnAddTarget", "click", () => addTarget().catch(() => { }));

    // Run
    on("btnRun", "click", () => runSelected().catch(() => { }));
    on("btnRunSelectedMini", "click", () => runSelected().catch(() => { }));

    // Activity
    on("btnClearActivity", "click", () => { activity = []; renderActivity(); });

    // Delegated: devices/groups add target
    on("devicesList", "click", (ev) => {
        const btn = ev.target.closest("button[data-add-device]");
        if (!btn) return;
        if (!selectedEffectId) return setOut("Select an effect first.");
        el("targetType").value = "1";
        renderTargetPicker();
        el("targetPicker").value = btn.dataset.addDevice;
        openDrawer();
    });

    on("groupsList", "click", (ev) => {
        const btn = ev.target.closest("button[data-add-group]");
        if (!btn) return;
        if (!selectedEffectId) return setOut("Select an effect first.");
        el("targetType").value = "2";
        renderTargetPicker();
        el("targetPicker").value = btn.dataset.addGroup;
        openDrawer();
    });

    // Delegated: targets remove
    on("targetsList", "click", async (ev) => {
        const btn = ev.target.closest("button[data-del-target]");
        if (!btn) return;
        const targetId = btn.dataset.delTarget;
        if (!targetId) return;

        try {
            await api(`/api/effects/targets/${targetId}`, { method: "DELETE" });
            addActivity("ok", "Target removed", targetId, { ok: true, targetId });
            if (selectedEffectId) await loadEffectDetails(selectedEffectId);
        } catch (e) {
            addActivity("bad", "Remove failed", e.message, e.payload || { error: e.message });
            setOut(e.payload || { error: e.message });
        }
    });

    // Delegated: effects actions
    on("effectsTbody", "click", async (ev) => {
        const btn = ev.target.closest("button");
        const row = ev.target.closest("tr[data-effect-id]");
        const rowId = row?.dataset.effectId;

        if (!rowId) return;

        try {
            // 1) If a button was clicked, handle that specific action FIRST
            if (btn) {
                if (btn.dataset.load) {
                    selectedEffectId = btn.dataset.load;
                    renderEffectsTable();
                    await loadEffectDetails(selectedEffectId);
                    return;
                }

                if (btn.dataset.run) {
                    selectedEffectId = btn.dataset.run;
                    renderEffectsTable();
                    await loadEffectDetails(selectedEffectId);
                    await runSelected();
                    return;
                }

                if (btn.dataset.toggle) {
                    const id = btn.dataset.toggle;
                    await toggleEffectEnabled(id);
                    await loadEffects();
                    // keep right panel in sync if currently selected
                    if (selectedEffectId === id) await loadEffectDetails(id);
                    return;
                }

                if (btn.dataset.edit) {
                    selectedEffectId = btn.dataset.edit;
                    renderEffectsTable();
                    await loadEffectDetails(selectedEffectId);
                    openDrawer();
                    fillEditEffectForm(selectedEffect);
                    return;
                }

                if (btn.dataset.del) {
                    const id = btn.dataset.del;
                    const name = effects.find(x => x.id === id)?.name || id;
                    if (!confirm(`Delete effect "${name}"? This cannot be undone.`)) return;

                    await deleteEffect(id);

                    // clear selection if we deleted the selected one
                    if (selectedEffectId === id) {
                        selectedEffectId = null;
                        selectedEffect = null;
                        renderSelected();
                        renderTargets();
                        const badge = el("effectLoadedBadge");
                        if (badge) badge.textContent = "";
                    }

                    await loadEffects();
                    addActivity("ok", "Effect deleted", name, { effectId: id });
                    return;
                }

                // If it's some other button, do nothing
                return;
            }

            // 2) Otherwise, it was a plain row click: treat as "Load"
            selectedEffectId = rowId;
            renderEffectsTable();
            await loadEffectDetails(rowId);

        } catch (e) {
            addActivity("bad", "Action failed", e.message, e.payload || { error: e.message });
            setOut(e.payload || { error: e.message });
        }
    });

    // Initial load sequence
    setOut("Loading…");
    try {
        await loadTuyaBadge();
        await loadDevices();
        await loadGroups();
        await loadEffects();
        renderSelected();
        renderActivity();
        setOut(`Ready. devices=${devices.length} groups=${groups.length} effects=${effects.length}`);
    } catch (e) {
        addActivity("bad", "Boot failed", e.message, e.payload || { error: e.message });
        setOut(e.payload || { error: e.message });
    }
});