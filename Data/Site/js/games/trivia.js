import { setText, pretty } from "../core/dom.js";

export function bindTrivia(ctx) {
    // no extra wiring needed; your click-to-answer is inside renderer
}

export function renderTrivia(gs, roomStateRaw) {
    window.renderTriviaPublic?.(gs);
    setText("stateOutTrivia", pretty(roomStateRaw));
}