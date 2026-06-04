// Agent inbox: loads conversation rows for the active tab and refreshes them
// live whenever the hub signals an inbox change.
(function () {
    "use strict";

    const rows = document.getElementById("inboxRows");

    function fmtTime(iso) {
        if (!iso) return "";
        const d = new Date(iso);
        return d.toLocaleString();
    }

    function badge(status) {
        const map = { Waiting: "bg-warning text-dark", Assigned: "bg-primary", Closed: "bg-secondary" };
        return `<span class="badge ${map[status] || "bg-light text-dark"}">${status}</span>`;
    }

    async function load() {
        const res = await fetch(`/Admin/InboxData?tab=${encodeURIComponent(CURRENT_TAB)}`);
        if (!res.ok) return;
        const data = await res.json();

        if (!data.length) {
            rows.innerHTML = `<tr><td colspan="7" class="text-center text-muted">No conversations in this tab.</td></tr>`;
            return;
        }

        rows.innerHTML = data.map(function (c) {
            const assigned = c.assignedTo
                ? `${c.assignedTo}${c.assignedToMe ? " <span class='badge bg-success'>me</span>" : ""}`
                : "<span class='text-muted'>Unassigned</span>";
            const preview = c.preview ? escapeHtml(c.preview) : "";
            return `<tr>
                <td><strong>${escapeHtml(c.customer)}</strong></td>
                <td>${c.category}</td>
                <td>${c.subOption}</td>
                <td>${badge(c.status)}</td>
                <td>${assigned}</td>
                <td class="text-truncate" style="max-width:240px">${preview}</td>
                <td><a class="btn btn-sm btn-outline-primary" href="/Admin/Conversation/${c.id}">Open</a></td>
            </tr>`;
        }).join("");
    }

    function escapeHtml(s) {
        return s.replace(/[&<>"']/g, function (ch) {
            return { "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[ch];
        });
    }

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect()
        .build();

    connection.on("InboxUpdated", load);

    connection.start()
        .then(function () { return connection.invoke("JoinAgents"); })
        .then(load)
        .catch(function () { load(); });
})();
