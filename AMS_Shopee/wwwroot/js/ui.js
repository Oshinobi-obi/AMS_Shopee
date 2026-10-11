// ui.js — light/dark theme switch and the Philippine-time clock (UTC+8).
window.amsUi = (function () {
    var KEY = "ams-theme";

    function current() { return document.documentElement.getAttribute("data-theme") === "dark" ? "dark" : "light"; }

    function apply(theme) {
        document.documentElement.setAttribute("data-theme", theme);
        document.documentElement.setAttribute("data-bs-theme", theme);
    }

    function toggle() {
        var next = current() === "dark" ? "light" : "dark";
        apply(next);
        try { localStorage.setItem(KEY, next); } catch (e) { }
        return next;
    }

    // Clock: always Philippine time, whatever time zone the computer is set to.
    var dateFmt = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", weekday: "short", month: "short", day: "numeric", year: "numeric" });
    var timeFmt = new Intl.DateTimeFormat("en-PH", { timeZone: "Asia/Manila", hour: "numeric", minute: "2-digit", second: "2-digit", hour12: true });

    function tick() {
        var now = new Date();
        var d = dateFmt.format(now), t = timeFmt.format(now);
        document.querySelectorAll("[data-ph-clock]").forEach(function (el) {
            var de = el.querySelector("[data-date]"), te = el.querySelector("[data-time]");
            if (de && de.textContent !== d) de.textContent = d;
            if (te && te.textContent !== t) te.textContent = t;
            el.setAttribute("title", d + ", " + t + " (Philippine time, UTC+8)");
        });
    }
    tick();
    setInterval(tick, 1000);

    // Announcements an office closed on this computer (kept for 60 days)
    var ANN_KEY = "ams-closed-announcements";
    function readClosed() {
        try {
            var list = JSON.parse(localStorage.getItem(ANN_KEY) || "[]");
            var cutoff = Date.now() - 60 * 24 * 3600 * 1000;
            return list.filter(function (x) { return x && x.at > cutoff; });
        } catch (e) { return []; }
    }
    function dismissedAnnouncements() { return readClosed().map(function (x) { return x.key; }); }
    function dismissAnnouncement(key) {
        var list = readClosed().filter(function (x) { return x.key !== key; });
        list.push({ key: key, at: Date.now() });
        try { localStorage.setItem(ANN_KEY, JSON.stringify(list.slice(-50))); } catch (e) { }
    }

    return { current: current, toggle: toggle, tick: tick, dismissedAnnouncements: dismissedAnnouncements, dismissAnnouncement: dismissAnnouncement };
})();

// amsPrint.ris() — prints only the Requisition and Issue Slip on a clean white A4 page.
// The slip is copied into #ris-print-root (outside the page layout), so side columns,
// scroll boxes and the dark theme can't crop it or tint the paper. ris.css does the rest.
window.amsPrint = (function () {
    function cleanup() {
        document.documentElement.classList.remove("ris-printing");
        var old = document.getElementById("ris-print-root");
        if (old) old.remove();
    }
    function ris() {
        var sheet = document.querySelector(".ris-sheet");
        if (!sheet) { window.print(); return; }
        cleanup();
        var root = document.createElement("div");
        root.id = "ris-print-root";
        root.appendChild(sheet.cloneNode(true));
        document.body.appendChild(root);
        document.documentElement.classList.add("ris-printing");
        window.addEventListener("afterprint", cleanup, { once: true });
        var ready = document.fonts && document.fonts.ready ? document.fonts.ready : Promise.resolve();
        ready.then(function () { setTimeout(function () { window.print(); }, 50); });
    }
    return { ris: ris };
})();

// amsWhatsNew — remembers (in this browser) the newest version whose "What's new" page was opened.
window.amsWhatsNew = {
    isNew: function (version) {
        try { return localStorage.getItem("ams-seen-version") !== version; } catch (e) { return false; }
    },
    markSeen: function (version) {
        try { localStorage.setItem("ams-seen-version", version); } catch (e) { }
    }
};
