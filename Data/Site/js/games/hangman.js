import { el, setText, pretty, escapeHtml } from "../core/dom.js";

export function bindHangman(ctx) {
    el("hangmanGuessBtn")?.addEventListener("click", ctx.actions.sendHangmanGuess);

    el("hangmanGuessInput")?.addEventListener("keydown", (ev) => {
        if (ev.key === "Enter") ctx.actions.sendHangmanGuess();
    });
}

export function renderHangman(gs, roomStateRaw) {
    const board = el("hangmanBoard");
    if (!board) return;

    const display =
        gs?.display ??
        gs?.Display ??
        gs?.maskedWord ??
        gs?.MaskedWord ??
        "_ _ _ _";

    const lives =
        gs?.lives ??
        gs?.Lives ??
        gs?.remainingLives ??
        gs?.RemainingLives ??
        "?";

    const guessed =
        gs?.guessedLetters ??
        gs?.GuessedLetters ??
        gs?.wrongLetters ??
        gs?.WrongLetters ??
        [];

    const status =
        gs?.status ??
        gs?.Status ??
        gs?.message ??
        gs?.Message ??
        "";

    board.innerHTML = `
        <div class="hangman-word">${escapeHtml(display)}</div>
        <div class="small">Lives: ${escapeHtml(String(lives))}</div>
        <div class="small">Guessed: ${escapeHtml(Array.isArray(guessed) ? guessed.join(", ") : String(guessed))}</div>
        ${status ? `<div class="small">${escapeHtml(status)}</div>` : ""}
    `;

    setText("stateOutHangman", pretty(roomStateRaw));
}