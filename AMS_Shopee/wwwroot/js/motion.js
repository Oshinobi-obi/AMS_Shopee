// These are the animations of e-SRS: fly to cart, cart badge pop, card tilt, cards coming in
// one by one, and the "Submitted" check. Nothing moves if the person turned animations off.
window.amsMotion = (function () {
    var reduce = window.matchMedia("(prefers-reduced-motion: reduce)");
    var finePointer = window.matchMedia("(hover: hover) and (pointer: fine)");
    function calm() { return reduce.matches; }

    // Remember where an "Add to cart" click came from
    var lastAdd = null;   // { rect, html, at }
    document.addEventListener("click", function (e) {
        var btn = e.target.closest && e.target.closest("button");
        if (!btn) return;
        var card = btn.closest(".item-card");
        var dialog = btn.closest("[role=dialog]");
        var media = null;
        if (card && btn.closest(".item-actions")) media = card.querySelector(".item-media");
        else if (dialog && btn.classList.contains("btn-accent") && /add to cart/i.test(btn.textContent))
            media = dialog.querySelector(".detail-photo, .item-media, .detail-media") || btn;
        if (!media) return;
        var img = media.querySelector("img"), icon = media.querySelector(".bi");
        lastAdd = {
            rect: media.getBoundingClientRect(),
            html: img ? '<img src="' + img.getAttribute("src") + '" alt="">' : icon ? '<i class="bi ' + icon.className.replace(/^bi\s*/, "") + '"></i>' : '<i class="bi bi-box-seam"></i>',
            at: Date.now()
        };
    }, true);

    function cartLink() {
        var links = document.querySelectorAll('a.topbar-link[href="cart"], a[href="cart"].topbar-link, a[href="/cart"]');
        for (var i = 0; i < links.length; i++) if (links[i].offsetParent !== null) return links[i];
        return null;
    }

    function fly(from, html, target) {
        var t = target.getBoundingClientRect();
        var size = 72;
        var sx = from.left + from.width / 2 - size / 2, sy = from.top + from.height / 2 - size / 2;
        var tx = t.left + t.width / 2 - size / 2, ty = t.top + t.height / 2 - size / 2;
        var el = document.createElement("div");
        el.className = "fly-item"; el.innerHTML = html;
        el.style.left = sx + "px"; el.style.top = sy + "px";
        document.body.appendChild(el);
        var dx = tx - sx, dy = ty - sy, lift = Math.min(160, Math.abs(dx) * .25 + 60);
        var anim = el.animate([
            { transform: "translate(0,0) scale(1)", opacity: 1 },
            { transform: "translate(" + dx * .45 + "px," + (dy * .45 - lift) + "px) scale(.85)", opacity: 1, offset: .45 },
            { transform: "translate(" + dx + "px," + dy + "px) scale(.2)", opacity: .35 }
        ], { duration: 700, easing: "cubic-bezier(.45,.05,.55,.95)" });
        anim.onfinish = function () { el.remove(); catchIt(target); };
    }

    function catchIt(link) {
        if (!link) return;
        link.classList.remove("is-catch"); void link.offsetWidth; link.classList.add("is-catch");
        setTimeout(function () { link.classList.remove("is-catch"); }, 500);
    }

    function bump(badge) {
        badge.classList.remove("is-bump"); void badge.offsetWidth; badge.classList.add("is-bump");
        setTimeout(function () { badge.classList.remove("is-bump"); }, 600);
    }

    // Watch the cart count, and when it goes up, play the animation
    var lastCount = null;
    function readCount() {
        var b = document.querySelector(".cart-count");
        return { badge: b, n: b ? parseInt(b.textContent, 10) || 0 : 0 };
    }
    function onCartChange() {
        var c = readCount();
        if (lastCount !== null && c.n > lastCount && !calm()) {
            var link = cartLink();
            if (lastAdd && Date.now() - lastAdd.at < 8000 && link) {
                fly(lastAdd.rect, lastAdd.html, link);
                if (c.badge) setTimeout(function () { bump(c.badge); }, 650);
            } else if (c.badge) bump(c.badge);
            lastAdd = null;
        }
        lastCount = c.n;
    }

    // Card tilt (mouse only)
    var tilted = null;
    document.addEventListener("mousemove", function (e) {
        if (calm() || !finePointer.matches) return;
        var card = e.target.closest && e.target.closest(".item-card:not(.is-skeleton)");
        if (tilted && tilted !== card) { tilted.style.removeProperty("--rx"); tilted.style.removeProperty("--ry"); tilted = null; }
        if (!card) return;
        var r = card.getBoundingClientRect();
        var px = (e.clientX - r.left) / r.width - .5, py = (e.clientY - r.top) / r.height - .5;
        card.style.setProperty("--ry", (px * 5).toFixed(2) + "deg");
        card.style.setProperty("--rx", (-py * 4).toFixed(2) + "deg");
        tilted = card;
    }, { passive: true });
    document.addEventListener("mouseleave", function () { if (tilted) { tilted.style.removeProperty("--rx"); tilted.style.removeProperty("--ry"); tilted = null; } });

    // Show new catalog cards one after another
    function stagger(nodes) {
        var i = 0;
        nodes.forEach(function (n) {
            if (n.nodeType === 1 && n.classList.contains("item-card") && !n.classList.contains("is-skeleton")) {
                n.style.setProperty("--i", Math.min(i++, 12));
            }
        });
    }

    var queued = false;
    new MutationObserver(function (records) {
        var added = [];
        records.forEach(function (r) { r.addedNodes.forEach(function (n) { added.push(n); }); });
        if (added.length) stagger(added);
        if (!queued) { queued = true; requestAnimationFrame(function () { queued = false; onCartChange(); }); }
    }).observe(document.body, { childList: true, subtree: true, characterData: true });
    document.addEventListener("DOMContentLoaded", function () { lastCount = readCount().n; });

    // "Submitted" check
    function success(text) {
        var el = document.createElement("div");
        el.className = "done-burst"; el.setAttribute("role", "status");
        el.innerHTML = '<div class="done-card"><svg viewBox="0 0 84 84" aria-hidden="true"><circle cx="42" cy="42" r="38"/><path d="M26 43 L37 54 L59 31"/></svg><span></span></div>';
        el.querySelector("span").textContent = text || "Done";
        document.body.appendChild(el);
        setTimeout(function () { el.remove(); }, 1700);
    }

    return { success: success };
})();
