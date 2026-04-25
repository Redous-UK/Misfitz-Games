const panelFiles = {
    contexto: "../games/contexto.html",
    trivia: "../games/trivia.html",
    hangman: "../games/hangman.html",
    higherlower: "../games/higherlower.html",
    riddle_me_this: "../games/riddle-me-this.html",
    deal: "../games/deal.html"
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

        const wrapper = document.createElement("section");
        wrapper.id = `panel-${gameId}`;
        wrapper.className = "game-panel";
        wrapper.hidden = true;
        wrapper.innerHTML = await res.text();

        container.appendChild(wrapper);
    }
}