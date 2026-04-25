const panelFiles = {
    contexto: "/js/games/contexto.html",
    trivia: "/js/games/trivia.html",
    hangman: "/js/games/hangman.html",
    higher_lower: "/js/games/higherlower.html",
    riddle_me_this: "/js/games/riddle.html",
    deal: "/js/games/deal.html"
};

export async function loadGamePanels(containerId = "gamePanels") {
    const container = document.getElementById(containerId);
    if (!container) throw new Error(`Missing panel container: ${containerId}`);

    container.innerHTML = "";

    for (const [gameId, url] of Object.entries(panelFiles)) {
        const res = await fetch(url, { credentials: "include" });

        if (!res.ok) {
            console.warn(`Failed to load panel ${gameId}: ${res.status} ${url}`);
            continue;
        }

        const wrapper = document.createElement("section");
        wrapper.id = `panel-${gameId}`;
        wrapper.className = "game-panel";
        wrapper.hidden = true;
        wrapper.innerHTML = await res.text();

        container.appendChild(wrapper);
    }
}