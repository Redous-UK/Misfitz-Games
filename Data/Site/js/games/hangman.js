import { el, setText, pretty } from "../core/dom.js";

export function bindHangman(ctx) {
    el("hangmanGuessBtn")?.addEventListener("click", ctx.actions.sendHangmanGuess);
    el("hangmanGuessInput")?.addEventListener("keydown", (ev) => {
        if (ev.key === "Enter") ctx.actions.sendHangmanGuess();
    });
}

export function renderHangman(gs, roomStateRaw) {
    // call your existing global function if you kept it, or copy it here
    window.renderHangmanPublic?.(gs);
    setText("stateOutHangman", pretty(roomStateRaw));
}