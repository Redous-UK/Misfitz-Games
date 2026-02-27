export function normalizeGameId(id) {
    id = String(id ?? "none").trim().toLowerCase();
    if (id === "dailytrivia" || id === "daily_trivia") return "trivia";
    if (id === "higherlower") return "higher_lower";
    return id;
}

export function mapGameType(n) {
    switch (Number(n)) {
        case 1: return "contexto";
        case 2: return "hangman";
        case 3: return "trivia";
        case 4: return "deal";
        case 5: return "higher_lower";
        default: return null;
    }
}

export function panelExists(gameId) {
    return !!document.getElementById(`panel-${gameId}`);
}

export function showOnlyPanel(gameId) {
    const target = String(gameId ?? "").toLowerCase();
    document.querySelectorAll('[id^="panel-"]').forEach(node => {
        const id = node.id.replace("panel-", "").toLowerCase();
        node.hidden = (id !== target);
    });
}