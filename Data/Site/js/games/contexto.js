import { el, setText, escapeHtml, pretty } from "../core/dom.js";

export function bindContexto(ctx) {
    el("btnGuess") && (el("btnGuess").onclick = ctx.actions.sendContextoGuess);
    el("guess")?.addEventListener("keydown", (ev) => {
        if (ev.key === "Enter") ctx.actions.sendContextoGuess();
    });
}

export function renderContexto(gs, roomStateRaw) {
    const ul = el("recentGuesses");
    if (!ul) return;
    ul.innerHTML = "";

    const guesses = gs?.recentGuesses ?? gs?.RecentGuesses ?? [];
    const arr = Array.isArray(guesses) ? guesses : [];
    const list = arr.slice(0, 10);

    if (list.length === 0) {
        ul.innerHTML = `<li class="small">No guesses yet…</li>`;
    } else {
        for (const g of list) {
            const user = (g.username ?? g.Username ?? "Unknown").toString();
            const guess = (g.guess ?? g.Guess ?? g.word ?? g.Word ?? "").toString();

            const rank = g.rank ?? g.Rank ?? g.rankOrScore ?? g.RankOrScore;
            const pct = g.percentage ?? g.Percentage;

            const right =
                (pct != null)
                    ? `<div class="right"><div class="small">%</div><div class="who">${escapeHtml(String(pct))}</div></div>`
                    : (rank != null)
                        ? `<div class="right"><div class="small">rank</div><div class="who">${escapeHtml(String(rank))}</div></div>`
                        : `<div class="right"><div class="small">&nbsp;</div><div class="who">&nbsp;</div></div>`;

            const li = document.createElement("li");
            li.className = "item";
            li.innerHTML = `
        <div>
          <div class="who">${escapeHtml(user)}</div>
          <div class="small">${escapeHtml(guess)}</div>
        </div>
        ${right}
      `;
            ul.appendChild(li);
        }
    }

    setText("stateOutContexto", pretty(roomStateRaw));
}