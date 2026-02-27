import { el, setText, pretty } from "../core/dom.js";
  );
}

export function bindHigherLower(ctx) {
    el("hlStartBtn") && (el("hlStartBtn").onclick = () => ctx.actions.hlStart());
    el("hlHigherBtn") && (el("hlHigherBtn").onclick = () => ctx.actions.hlGuess("higher"));
    el("hlLowerBtn") && (el("hlLowerBtn").onclick = () => ctx.actions.hlGuess("lower"));
    el("hlContinueBtn") && (el("hlContinueBtn").onclick = () => ctx.actions.hlContinue());
    el("hlStopBtn") && (el("hlStopBtn").onclick = () => ctx.actions.hlStop());
}

export function renderHigherLower(gs, roomStateRaw, joinedRef) {
    // unwrap common wrappers
    gs = gs?.publicState ?? gs?.PublicState ?? gs?.state ?? gs?.State ?? gs;

    const status = hlStatus(gs);

    const currentCard = hlFindCurrent(gs);
    const revealedCard = hlFindRevealed(gs);

    const currentLabel =
        hlGet(gs, "current.label") ??
        hlGet(gs, "Current.Label") ??
        hlGet(gs, "currentLabel") ??
        hlGet(gs, "CurrentLabel") ??
        hlCardLabel(currentCard) ??
        "—";

    const revealedLabel =
        hlGet(gs, "revealedNext.label") ??
        hlGet(gs, "RevealedNext.Label") ??
        hlGet(gs, "revealedNextLabel") ??
        hlGet(gs, "RevealedNextLabel") ??
        hlCardLabel(revealedCard) ??
        null;

    const streak = hlGet(gs, "streak", 0) ?? hlGet(gs, "Streak", 0) ?? 0;
    const best = hlGet(gs, "bestStreak", 0) ?? hlGet(gs, "BestStreak", 0) ?? 0;
    const lastChoice = hlGet(gs, "lastChoice") ?? hlGet(gs, "LastChoice") ?? null;
    const lastWasCorrect = hlGet(gs, "lastWasCorrect") ?? hlGet(gs, "LastWasCorrect") ?? null;

    // Badge (HL has its own badge)
    const badge = el("gameBadgeHL");
    if (badge) {
        const active = status !== "idle";
        badge.textContent = active ? "Active" : "No game";
        badge.className = "badge " + (active ? "ok" : "warn");
    }

    setText("hlCurrent", String(currentLabel));
    setText("hlStreak", String(streak));
    setText("hlBest", String(best));

    const lastBits =
        (lastChoice ? ` • last: ${lastChoice}` : "") +
        (lastWasCorrect === true ? " • ✅" : lastWasCorrect === false ? " • ❌" : "");

    setText("hlStatus", `${status}${lastBits}`);

    const revealRow = el("hlRevealRow");
    const showReveal = (status === "revealed" && !!revealedLabel);
    if (revealRow) revealRow.classList.toggle("hidden", !showReveal);
    setText("hlRevealed", revealedLabel ? String(revealedLabel) : "—");

    const resultBadge = el("hlResultBadge");
    if (resultBadge) {
        const ok = lastWasCorrect === true;
        const bad = lastWasCorrect === false;
        resultBadge.textContent = ok ? "Correct" : bad ? "Wrong" : "—";
        resultBadge.className = "badge " + (ok ? "ok" : bad ? "bad" : "warn");
    }

    // Buttons
    const hasRoom = !!joinedRef;
    const isInRound = status === "inround";
    const isRevealed = status === "revealed";

    el("hlStartBtn") && (el("hlStartBtn").disabled = !hasRoom);
    el("hlHigherBtn") && (el("hlHigherBtn").disabled = !(hasRoom && isInRound));
    el("hlLowerBtn") && (el("hlLowerBtn").disabled = !(hasRoom && isInRound));
    el("hlContinueBtn") && (el("hlContinueBtn").disabled = !(hasRoom && isRevealed));
    el("hlStopBtn") && (el("hlStopBtn").disabled = !hasRoom);

    setText("stateOutHL", pretty(roomStateRaw));
}