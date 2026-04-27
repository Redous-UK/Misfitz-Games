import { el, setText, pretty, escapeHtml } from "../core/dom.js";

export function bindTrivia(ctx) {
    el("btnTriviaStart")?.addEventListener("click", ctx.actions.triviaStart);
    el("btnTriviaNext")?.addEventListener("click", ctx.actions.triviaNext);

    document.addEventListener("click", async (ev) => {
        const btn = ev.target.closest("[data-trivia-answer]");
        if (!btn) return;

        const answer = btn.getAttribute("data-trivia-answer");
        if (answer && ctx.actions.triviaAnswer) {
            await ctx.actions.triviaAnswer(answer);
        }
    });
}

export function renderTrivia(gs, roomStateRaw) {
    const q = el("triviaQuestion");
    const answers = el("triviaAnswers");

    if (!q || !answers) return;

    const question =
        gs?.question ??
        gs?.Question ??
        "Waiting for question...";

    const opts =
        gs?.answers ??
        gs?.Answers ??
        gs?.options ??
        gs?.Options ??
        [];

    q.innerHTML = escapeHtml(question);
    answers.innerHTML = "";

    if (Array.isArray(opts) && opts.length) {
        opts.forEach((answer, index) => {
            const value = typeof answer === "string"
                ? answer
                : answer?.text ?? answer?.Text ?? String(answer);

            const btn = document.createElement("button");
            btn.type = "button";
            btn.className = "trivia-answer";
            btn.dataset.triviaAnswer = value;
            btn.textContent = `${String.fromCharCode(65 + index)}. ${value}`;

            answers.appendChild(btn);
        });
    } else {
        answers.innerHTML = `<p class="small">No answers loaded.</p>`;
    }

    setText("stateOutTrivia", pretty(roomStateRaw));
}