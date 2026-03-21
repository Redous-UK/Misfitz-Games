import { el, setText, escapeHtml, pretty } from "../core/dom.js";

export function bindRiddleMeThis(ctx) {
    el("btnRmtStart") && (el("btnRmtStart").onclick = ctx.actions.rmtStart);
    el("btnRmtNext") && (el("btnRmtNext").onclick = ctx.actions.rmtNext);
    el("btnRmtReveal") && (el("btnRmtReveal").onclick = ctx.actions.rmtReveal);
    el("btnRmtRefresh") && (el("btnRmtRefresh").onclick = ctx.actions.rmtRefresh);

    el("rmtCategory")?.addEventListener("change", () => {
        const v = el("rmtCategory")?.value ?? "";
        localStorage.setItem("rmtCategory", v);
    });
}

export function renderRiddleMeThis(gs, roomStateRaw) {
    const round = gs?.round ?? gs?.Round ?? "-";
    const category = gs?.category ?? gs?.Category ?? "-";
    const riddle = gs?.riddle ?? gs?.Riddle ?? "-";
    const isSolved = gs?.isSolved ?? gs?.IsSolved ?? false;
    const solvedBy = gs?.solvedByUserId ?? gs?.SolvedByUserId ?? "-";
    const startedAt = gs?.startedAtUtc ?? gs?.StartedAtUtc ?? "-";

    setText("rmtRound", round);
    setText("rmtCategoryText", category);
    setText("rmtRiddle", riddle);
    setText("rmtSolved", isSolved ? "Yes" : "No");
    setText("rmtSolvedBy", solvedBy || "-");
    setText("rmtStartedAt", startedAt || "-");

    const ul = el("rmtRecentGuesses");
    if (ul) {
        ul.innerHTML = "";

        const guesses = gs?.recentGuesses ?? gs?.RecentGuesses ?? [];
        const arr = Array.isArray(guesses) ? guesses : [];
        const list = arr.slice(-10).reverse();

        if (list.length === 0) {
            ul.innerHTML = `<li class="small">No guesses yet…</li>`;
        } else {
            for (const g of list) {
                const user = (g.userId ?? g.UserId ?? "Unknown").toString();
                const guess = (g.guess ?? g.Guess ?? "").toString();
                const correct = g.isCorrect ?? g.IsCorrect ?? false;

                const li = document.createElement("li");
                li.className = "item";
                li.innerHTML = `
          <div>
            <div class="who">${escapeHtml(user)}</div>
            <div class="small">${escapeHtml(guess)}</div>
          </div>
          <div class="right">
            <div class="small">result</div>
            <div class="who">${correct ? "✅" : "❌"}</div>
          </div>
        `;
                ul.appendChild(li);
            }
        }
    }

    setText("stateOutRiddleMeThis", pretty(roomStateRaw));
}