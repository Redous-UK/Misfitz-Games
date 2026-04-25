import { el, setText, pretty, escapeHtml } from "../core/dom.js";

export function bindRiddleMeThis(ctx) {
    el("btnRmtStart")?.addEventListener("click", ctx.actions.rmtStart);
    el("btnRmtNext")?.addEventListener("click", ctx.actions.rmtNext);
    el("btnRmtReveal")?.addEventListener("click", ctx.actions.rmtReveal);
    el("btnRmtRefresh")?.addEventListener("click", ctx.actions.rmtRefresh);

    el("rmtCategory")?.addEventListener("change", (ev) => {
        localStorage.setItem("rmtCategory", ev.target.value || "");
    });
}

export function renderRiddleMeThis(gs, roomStateRaw) {
    const q = el("rmtQuestion");
    if (!q) return;

    const question =
        gs?.riddle ??
        gs?.Riddle ??
        gs?.question ??
        gs?.Question ??
        "No riddle loaded";

    const answer =
        gs?.answer ??
        gs?.Answer ??
        null;

    const solved =
        gs?.isSolved ??
        gs?.IsSolved ??
        false;

    q.innerHTML = `
        <div>${escapeHtml(String(question))}</div>
        ${answer ? `<div class="small">Answer: ${escapeHtml(String(answer))}</div>` : ""}
        <div class="small">Solved: ${solved ? "Yes" : "No"}</div>
    `;

    setText("rmtOut", pretty(gs ?? {}));
    setText("stateOutRiddle", pretty(roomStateRaw));
}