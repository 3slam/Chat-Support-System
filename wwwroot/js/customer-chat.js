// Customer chat widget: pre-chat bot flow (free text -> category -> sub-option)
// then real-time chat with the assigned agent over SignalR.
(function () {
    "use strict";

    const body = document.getElementById("chatBody");
    const options = document.getElementById("chatOptions");
    const form = document.getElementById("chatForm");
    const input = document.getElementById("chatText");
    const statusEl = document.getElementById("chatStatus");
    const agentBadge = document.getElementById("agentBadge");
    const newChatBtn = document.getElementById("newChatBtn");

    const STORE_KEY = "trips_chat_public_id";
    let publicId = localStorage.getItem(STORE_KEY) || null;
    let step = "greeting";   // greeting -> category -> suboption -> live -> closed
    let connection = null;

    const CATEGORIES = [
        { key: "Flight", label: "✈️ Flight" },
        { key: "Hotel", label: "🏨 Hotel" },
        { key: "Visa", label: "🛂 Visa" },
        { key: "Activity", label: "🎟️ Activity" }
    ];
    const SUBOPTIONS = [
        { key: "GeneralInquiry", label: "استفسار عام" },
        { key: "BookingProblem", label: "لدي مشكلة مع الحجز" }
    ];

    // ---- rendering helpers -------------------------------------------------

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
        body.appendChild(wrap);
        body.scrollTop = body.scrollHeight;
    }

    function render(senderType, name, text) {
        if (senderType === "Customer") bubble("out", "You", text);
        else if (senderType === "Agent") bubble("in agent", name, text);
        else bubble("in bot", name || "TripsBot", text);
    }

    function clearOptions() { options.innerHTML = ""; }

    function showButtons(items, handler) {
        clearOptions();
        items.forEach(function (it) {
            const b = document.createElement("button");
            b.type = "button";
            b.className = "opt-btn";
            b.textContent = it.label;
            b.addEventListener("click", function () { handler(it); });
            options.appendChild(b);
        });
    }

    function setInputEnabled(enabled, placeholder) {
        input.disabled = !enabled;
        document.getElementById("chatSend").disabled = !enabled;
        if (placeholder) input.placeholder = placeholder;
    }

    // ---- bot flow ----------------------------------------------------------

    function startGreeting() {
        step = "greeting";
        render("Bot", "TripsBot", "👋 أهلاً بك في دعم Trips! كيف يمكننا مساعدتك اليوم؟");
        render("Bot", "TripsBot", "Welcome to Trips Support! Tell us how we can help.");
        setInputEnabled(true, "Type your message…");
        input.focus();
    }

    function showCategories() {
        step = "category";
        setInputEnabled(false, "Please pick an option above…");
        showButtons(CATEGORIES, function (cat) {
            render("Customer", "You", cat.label);
            postJson(`/api/chat/${publicId}/category`, { category: cat.key })
                .then(function () { showSubOptions(); });
        });
    }

    function showSubOptions() {
        step = "suboption";
        render("Bot", "TripsBot", "كيف يمكننا مساعدتك؟ / How can we help?");
        setInputEnabled(false, "Please pick an option above…");
        showButtons(SUBOPTIONS, function (sub) {
            render("Customer", "You", sub.label);
            postJson(`/api/chat/${publicId}/suboption`, { subOption: sub.key })
                .then(function () { goLive(); });
        });
    }

    function goLive() {
        step = "live";
        clearOptions();
        render("Bot", "TripsBot", "جارٍ تحويلك إلى أحد موظفينا… / Connecting you to an agent…");
        statusEl.textContent = "Waiting for an agent…";
        setInputEnabled(true, "Type your message…");
        input.focus();
    }

    function markClosed() {
        step = "closed";
        clearOptions();
        statusEl.textContent = "Conversation closed";
        setInputEnabled(false, "This conversation is closed.");
    }

    // ---- networking --------------------------------------------------------

    async function postJson(url, payload) {
        const res = await fetch(url, {
            method: "POST",
            headers: { "Content-Type": "application/json" },
            body: JSON.stringify(payload)
        });
        if (!res.ok) throw new Error("Request failed: " + res.status);
        return res.json().catch(function () { return {}; });
    }

    async function connectHub() {
        connection = new signalR.HubConnectionBuilder()
            .withUrl("/chathub")
            .withAutomaticReconnect()
            .build();

        connection.on("ReceiveMessage", function (m) {
            render(m.senderType, m.senderName, m.text);
        });

        connection.on("ConversationUpdated", function (u) {
            if (u.systemText) render("Bot", "System", u.systemText);
            if (u.agentName) {
                agentBadge.style.display = "inline-block";
                agentBadge.textContent = "Agent: " + u.agentName;
                statusEl.textContent = "Chatting with " + u.agentName;
            }
            if (u.status === "Closed") markClosed();
        });

        await connection.start();
        await connection.invoke("JoinConversation", publicId);
    }

    // ---- start a fresh conversation ----------------------------------------

    async function resetChat() {
        if (connection) {
            try { await connection.stop(); } catch (e) { /* ignore */ }
            connection = null;
        }
        localStorage.removeItem(STORE_KEY);
        publicId = null;
        body.innerHTML = "";
        clearOptions();
        agentBadge.style.display = "none";
        agentBadge.textContent = "";
        statusEl.textContent = "We're here to help";
        startGreeting();
    }

    if (newChatBtn) newChatBtn.addEventListener("click", resetChat);

    // ---- form submit -------------------------------------------------------

    form.addEventListener("submit", async function (e) {
        e.preventDefault();
        const text = input.value.trim();
        if (!text) return;

        if (step === "greeting") {
            input.value = "";
            const data = await postJson("/api/chat/start", { message: text });
            publicId = data.publicId;
            localStorage.setItem(STORE_KEY, publicId);
            render("Customer", "You", text);
            render("Bot", "TripsBot", "Please choose a service:");
            await connectHub();
            showCategories();
        } else if (step === "live") {
            input.value = "";
            await connection.invoke("SendCustomerMessage", publicId, text);
        }
    });

    // ---- resume an existing conversation -----------------------------------

    async function resume() {
        try {
            const res = await fetch(`/api/chat/${publicId}`);
            if (!res.ok) { localStorage.removeItem(STORE_KEY); publicId = null; startGreeting(); return; }
            const state = await res.json();

            state.messages.forEach(function (m) { render(m.senderType, m.senderName, m.text); });
            if (state.agentName) {
                agentBadge.style.display = "inline-block";
                agentBadge.textContent = "Agent: " + state.agentName;
            }

            if (state.status === "Closed") { markClosed(); return; }
            await connectHub();

            if (state.status === "Waiting" || state.status === "Assigned") {
                step = "live";
                statusEl.textContent = state.agentName ? "Chatting with " + state.agentName : "Waiting for an agent…";
                setInputEnabled(true, "Type your message…");
            } else if (!state.category) {
                showCategories();
            } else {
                showSubOptions();
            }
        } catch (err) {
            localStorage.removeItem(STORE_KEY);
            publicId = null;
            startGreeting();
        }
    }

    // ---- boot --------------------------------------------------------------

    if (publicId) resume();
    else startGreeting();
})();
