import { el, setText, pretty, escapeHtml } from "../core/dom.js";

export function bindHigherLower(ctx) {
    el("btnHlStart")?.addEventListener("click", ctx.actions.hlStart);
    el("btnHigher")?.addEventListener("click", () => ctx.actions.hlGuess("higher"));
    el("btnLower")?.addEventListener("click", () => ctx.actions.hlGuess("lower"));
    el("btnHlContinue")?.addEventListener("click", ctx.actions.hlContinue);
    el("btnHlStop")?.addEventListener("click", ctx.actions.hlStop);
}

export function renderHigherLower(gs, roomStateRaw) {
    const current = el("hlCurrent");
    if (!current) return;

    const left =
        gs?.current ??
        gs?.Current ??
        gs?.left ??
        gs?.Left ??
        "?";

    const right =
        gs?.next ??
        gs?.Next ??
        gs?.right ??
        gs?.Right ??
        "?";

    const score =
        gs?.score ??
        gs?.Score ??
        0;

    const message =
        gs?.message ??
        gs?.Message ??
        "";

    current.innerHTML = `
        <div class="hl-current">${escapeHtml(String(left))}</div>
        <div class="small">Next: ${escapeHtml(String(right))}</div>
        <div class="small">Score: ${escapeHtml(String(score))}</div>
        ${message ? `<div class="small">${escapeHtml(String(message))}</div>` : ""}
    `;

    setText("hlOut", pretty(gs ?? {}));
    setText("stateOutHigherLower", pretty(roomStateRaw));
}