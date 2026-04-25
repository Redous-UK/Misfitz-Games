import { el, setText, pretty, escapeHtml } from "../core/dom.js";

export function bindDeal(ctx) {
    el("btnDealStart")?.addEventListener("click", ctx.actions.dealStart);
}

export function renderDeal(gs, roomStateRaw) {
    const board = el("dealBoard");
    if (!board) return;

    const status =
        gs?.status ??
        gs?.Status ??
        gs?.message ??
        gs?.Message ??
        "Game not started";

    const offer =
        gs?.offer ??
        gs?.Offer ??
        null;

    board.innerHTML = `
        <div>${escapeHtml(String(status))}</div>
        ${offer != null ? `<div class="small">Banker offer: ${escapeHtml(String(offer))}</div>` : ""}
    `;

    setText("dealOut", pretty(gs ?? {}));
    setText("stateOutDeal", pretty(roomStateRaw));
}