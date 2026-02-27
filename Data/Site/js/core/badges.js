import { el } from "./dom.js";

/**
 * Maps gameId -> badge element id in the DOM.
 * Keep these aligned with your HTML.
 */
const GAME_BADGE_IDS = {
    contexto: "gameBadgeContexto",
    hangman: "gameBadgeHangman",
    trivia: "gameBadgeTrivia",
    deal: null,              // deal panel currently has no badge (add one if you want)
    higher_lower: "gameBadgeHL",
};

function applyBadge(node, text, kind) {
    if (!node) return;
    node.textContent = text;
    node.className =
        "badge " + (kind === "ok" ? "ok" : kind === "bad" ? "bad" : "warn");
}

/**
 * Sets connection badge (top right).
 */
export function setStatus(text, kind = "warn") {
    applyBadge(el("statusBadge"), text, kind);
}

/**
 * Sets the badge for a specific game panel.
 * Example: setGameBadge("higher_lower", "Active", "ok")
 */
export function setGameBadge(gameId, text, kind = "warn") {
    const key = String(gameId ?? "none").toLowerCase();
    const id = GAME_BADGE_IDS[key] ?? null;
    if (!id) return; // no badge for that game
    applyBadge(el(id), text, kind);
}

/**
 * Convenience: mark one game as active and mark the others as "No game" (optional).
 */
export function setActiveGameBadges(activeGameId, { noGameText = "No game" } = {}) {
    const activeKey = String(activeGameId ?? "none").toLowerCase();

    for (const [gameId, badgeId] of Object.entries(GAME_BADGE_IDS)) {
        if (!badgeId) continue;
        const node = el(badgeId);
        if (!node) continue;

        if (gameId === activeKey) {
            // don't overwrite here—caller should set exact text (Active / Revealed / etc.)
            continue;
        }

        applyBadge(node, noGameText, "warn");
    }
}