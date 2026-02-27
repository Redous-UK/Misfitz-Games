export const el = (id) => document.getElementById(id);
export const pretty = (o) => JSON.stringify(o, null, 2);

export function setText(id, text) {
    const node = el(id);
    if (node) node.textContent = text;
}

export function escapeHtml(s) {
    return (s ?? "").toString()
        .replaceAll("&", "&amp;")
        .replaceAll("<", "&lt;")
        .replaceAll(">", "&gt;")
        .replaceAll('"', "&quot;")
        .replaceAll("'", "&#039;");
}