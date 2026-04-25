const panelFiles = {
    contexto: "/partials/games/contexto.html",
    trivia: "/partials/games/trivia.html",
    hangman: "/partials/games/hangman.html",
    higherlower: "/partials/games/higherlower.html",
    riddle_me_this: "/partials/games/riddle-me-this.html",
    deal: "/partials/games/deal.html"
};

export async function loadGamePanels(containerId = "gamePanels") {
    const container = document.getElementById(containerId);
    if (!container) throw new Error(`Missing panel container: ${containerId}`);

    container.innerHTML = "";

    for (const [gameId, url] of Object.entries(panelFiles)) {
        const res = await fetch(url, { credentials: "include" });

        if (!res.ok) {
            console.warn(`Failed to load panel ${gameId}: ${res.status}`);
            continue;
        }

        const html = await res.text();

        const wrapper = document.createElement("section");
        wrapper.className = "game-panel";
        wrapper.dataset.panel = gameId;
        wrapper.id = `panel-${gameId}`;
        wrapper.innerHTML = html;

        container.appendChild(wrapper);
    }
}

export function showOnlyPanel(gameId) {
    document.querySelectorAll(".game-panel").forEach(panel => {
        panel.classList.toggle("active", panel.dataset.panel === gameId);
    });
}

export function panelExists(gameId) {
    return !!document.querySelector(`.game-panel[data-panel="${gameId}"]`);
}