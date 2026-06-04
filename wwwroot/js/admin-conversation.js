// Agent conversation workspace: live transcript + sending replies over SignalR.
(function () {
    "use strict";

    const transcript = document.getElementById("transcript");
    const form = document.getElementById("replyForm");
    const input = document.getElementById("replyText");
    const statusLabel = document.getElementById("statusLabel");

    function bubble(side, name, text) {
        const wrap = document.createElement("div");
        wrap.className = "msg msg-" + side;
        const meta = document.createElement("div");
        meta.className = "msg-meta";
        meta.textContent = name;
        const txt = document.createElement("div");
        txt.className = "msg-text";
        txt.textContent = text;
        wrap.appendChild(meta);
        wrap.appendChild(txt);
        transcript.appendChild(wrap);
        transcript.scrollTop = transcript.scrollHeight;
    }

    function render(m) {
        if (m.senderType === "Agent") bubble("out", m.senderName, m.text);
        else if (m.senderType === "Customer") bubble("in customer", m.senderName, m.text);
        else bubble("in bot", m.senderName, m.text);
    }

    transcript.scrollTop = transcript.scrollHeight;

    const connection = new signalR.HubConnectionBuilder()
        .withUrl("/chathub")
        .withAutomaticReconnect()
        .build();

    connection.on("ReceiveMessage", render);

    connection.on("ConversationUpdated", function (u) {
        if (u.systemText) bubble("in bot", "System", u.systemText);
        if (u.status && statusLabel) statusLabel.textContent = u.status;
    });

    connection.start()
        .then(function () { return connection.invoke("JoinConversation", PUBLIC_ID); })
        .catch(function (e) { console.error(e); });

    if (form && CAN_REPLY) {
        form.addEventListener("submit", async function (e) {
            e.preventDefault();
            const text = input.value.trim();
            if (!text) return;
            input.value = "";
            await connection.invoke("SendAgentMessage", PUBLIC_ID, AGENT_NAME, text);
        });
    }
})();
