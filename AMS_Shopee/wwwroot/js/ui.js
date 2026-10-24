// This is the light/dark theme switch and the Philippine time clock (UTC+8).
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

    // Clock: always Philippine time, whatever time zone the computer has.
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

// amsPrint.ris() prints only the Requisition and Issue Slip on a clean white A4 page.
// I copy the slip into #ris-print-root (outside the page layout), so the side columns,
// scroll boxes and dark theme can't cut it or tint the paper. ris.css does the rest.
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

// amsWhatsNew remembers (in this browser) the newest version whose "What's new" page was opened.
window.amsWhatsNew = {
    isNew: function (version) {
        try { return localStorage.getItem("ams-seen-version") !== version; } catch (e) { return false; }
    },
    markSeen: function (version) {
        try { localStorage.setItem("ams-seen-version", version); } catch (e) { }
    }
};

// Scrolling: the top bar and any announcements stay at the top (app.css). --topbar-h has their
// height so other sticky boxes (the RIS preview in the cart, the Help topics) go right under it.
// html.show-scroll-top shows the back to top button after about one screen.
(function () {
    var root = document.documentElement, queued = false, lastH = -1;
    function update() {
        queued = false;
        var bar = document.querySelector(".topbar");
        var ann = document.querySelector(".ann-stack");
        var barH = bar ? bar.offsetHeight : 0;
        // Announcements stay under the top bar while scrolling, so sticky boxes go under both.
        var h = barH + (ann ? ann.offsetHeight : 0);
        if (h !== lastH) { lastH = h; root.style.setProperty("--bar-h", barH + "px"); root.style.setProperty("--topbar-h", h + "px"); }
        var y = window.scrollY || root.scrollTop || 0;
        root.classList.toggle("show-scroll-top", y > Math.max(400, window.innerHeight * 0.6));
        // A small gap between on and off, so the page doesn't flicker when the announcement gets shorter.
        if (y > 60) root.classList.add("is-scrolled"); else if (y < 8) root.classList.remove("is-scrolled");
    }
    function queue() { if (!queued) { queued = true; requestAnimationFrame(update); } }
    window.addEventListener("scroll", queue, { passive: true });
    window.addEventListener("resize", queue);
    document.addEventListener("click", function (e) {
        var btn = e.target && e.target.closest ? e.target.closest("[data-scroll-top]") : null;
        if (!btn) return;
        var smooth = !(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
        window.scrollTo({ top: 0, behavior: smooth ? "smooth" : "auto" });
    });
    update();
    setTimeout(update, 300);
    setTimeout(update, 1500);   // after Blazor drew the top bar
    setInterval(update, 1000);  // an announcement can show up or end anytime
})();

// amsDoc.print(frameId) prints the PDF shown in a dialog. If the browser won't print the
// frame directly, I open the PDF in a new tab where it can be printed.
window.amsDoc = {
    print: function (frameId) {
        var f = document.getElementById(frameId);
        if (!f) return;
        try {
            if (f.offsetParent === null) throw new Error("hidden");   // phones: the PDF isn't shown in the page
            f.contentWindow.focus();
            f.contentWindow.print();
        } catch (e) {
            window.open(f.getAttribute("src"), "_blank", "noopener");
        }
    }
};

// amsDatePicker puts the open calendar (DatePicker.razor) next to its field.
// It floats over everything (dialogs too), is as wide as the field, opens above the field
// if there's no room below, never goes off screen, and follows the field when scrolling.
window.amsDatePicker = (function () {
    var current = null;
    var MARGIN = 8, GAP = 4, MIN_WIDTH = 296;

    function place(toggle, panel, minWidth) {
        var r = toggle.getBoundingClientRect();
        var vw = document.documentElement.clientWidth, vh = window.innerHeight;
        panel.style.position = "fixed";
        panel.style.right = "auto";
        panel.style.bottom = "auto";
        var w = Math.min(Math.max(r.width, minWidth || MIN_WIDTH), vw - 2 * MARGIN);
        panel.style.width = w + "px";
        var h = panel.offsetHeight;
        // Wider than the field: grow toward the middle of the screen (for fields on the right, the right edges line up)
        var left = w > r.width && r.left + r.width / 2 > vw / 2 ? r.right - w : r.left;
        left = Math.min(Math.max(left, MARGIN), vw - w - MARGIN);
        var top = r.bottom + GAP;
        if (top + h > vh - MARGIN) {
            var above = r.top - GAP - h;
            top = above >= MARGIN ? above : Math.max(MARGIN, vh - MARGIN - h);
        }
        panel.style.left = left + "px";
        panel.style.top = top + "px";
        // A transformed parent moves "fixed" elements, so I correct for it if that happens.
        var a = panel.getBoundingClientRect();
        if (Math.abs(a.left - left) > 1 || Math.abs(a.top - top) > 1) {
            panel.style.left = (2 * left - a.left) + "px";
            panel.style.top = (2 * top - a.top) + "px";
        }
        panel.classList.add("is-placed");
    }

    function detach() {
        if (!current) return;
        window.removeEventListener("resize", current.handler);
        window.removeEventListener("scroll", current.handler, true);
        current = null;
    }

    function attach(toggle, panel, minWidth) {
        if (!toggle || !panel) return;
        if (!current || current.panel !== panel) {
            detach();
            var handler = function () {
                if (document.body.contains(panel) && document.body.contains(toggle)) place(toggle, panel, minWidth); else detach();
            };
            window.addEventListener("resize", handler);
            window.addEventListener("scroll", handler, true);
            current = { panel: panel, handler: handler };
        }
        place(toggle, panel, minWidth);
    }

    return { attach: attach, detach: detach };
})();
