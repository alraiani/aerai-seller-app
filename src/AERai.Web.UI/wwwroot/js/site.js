// Site-wide progressive enhancements loaded on every page after Bootstrap.
(function () {
    "use strict";

    // ---------- Hover tips ----------
    // Explicit "?" tips plus every plain title="" on the page become styled tooltips that also open
    // on keyboard focus. Without JavaScript the native title tooltip still shows.
    function setupTooltips() {
        if (!window.bootstrap || !window.bootstrap.Tooltip) { return; }
        var elements = document.querySelectorAll(".app-content [data-bs-toggle='tooltip'], .app-content [title]");
        for (var i = 0; i < elements.length; i++) {
            window.bootstrap.Tooltip.getOrCreateInstance(elements[i], { container: "body", delay: { show: 150, hide: 50 } });
        }
    }

    // ---------- Sidebar ----------
    // Categories are <details> groups. The server opens Overview and the current page's category; what
    // this browser opened or collapsed since sign-in is kept in localStorage (the login page forgets it,
    // so every sign-in starts from that default), but the current page's category always shows.
    // The rail toggle shrinks the sidebar to icons on desktop (theme.js applies the saved choice early).
    // The rail shows only the current page's category; another category's icon opens that category's
    // first page, and each label becomes a tooltip. Storage failures just mean nothing is remembered.
    var navStateKey = "aerai-nav-state";
    var navRailKey = "aerai-nav";
    var navTips = [];
    // Must match the rail's media query in site.css: phones always get the full drawer.
    var navDesktop = window.matchMedia ? window.matchMedia("(min-width: 769px)") : null;

    function readNavState() {
        try {
            var value = JSON.parse(localStorage.getItem(navStateKey) || "{}");
            return value && typeof value === "object" && !Array.isArray(value) ? value : {};
        } catch (e) {
            return {};
        }
    }

    function navGroups() {
        return document.querySelectorAll(".app-sidebar details[data-nav-group]");
    }

    function holdsCurrentPage(group) {
        return !!group.querySelector("[aria-current='page']");
    }

    function railShown() {
        return document.documentElement.getAttribute("data-nav") === "rail" && (!navDesktop || navDesktop.matches);
    }

    function setupNavGroups() {
        try {
            // Replaced by aerai-nav-state; drop the old list so it doesn't linger.
            localStorage.removeItem("aerai-nav-groups");
            if (document.querySelector("[data-nav-reset]")) { localStorage.removeItem(navStateKey); }
        } catch (e) { /* nothing stored */ }

        var groups = navGroups();
        if (groups.length === 0) { return; }

        var state = readNavState();
        for (var i = 0; i < groups.length; i++) {
            var key = groups[i].getAttribute("data-nav-group");
            if (typeof state[key] === "boolean" && !holdsCurrentPage(groups[i])) {
                groups[i].open = state[key];
            }
        }

        // Only the toggled group's key is written, so a group held open for the current page keeps its
        // saved choice (the restore above fires toggle events too, which write what was just read).
        // Opening and closing in the rail is temporary and never saved.
        for (var j = 0; j < groups.length; j++) {
            groups[j].addEventListener("toggle", function (event) {
                var group = event.currentTarget;
                if (group.hasAttribute("data-rail-restoring")) { group.removeAttribute("data-rail-restoring"); return; }
                if (railShown()) { return; }
                if (holdsCurrentPage(group) && group.open) { return; }
                var saved = readNavState();
                saved[group.getAttribute("data-nav-group")] = group.open;
                try { localStorage.setItem(navStateKey, JSON.stringify(saved)); } catch (e) { /* not remembered */ }
            });
        }
    }

    // While the rail shows, only the current page's groups are open; the others are put back exactly as
    // they were when the full sidebar returns.
    function syncRailGroups() {
        var rail = railShown();
        var groups = navGroups();
        for (var i = 0; i < groups.length; i++) {
            var group = groups[i];
            if (rail) {
                // A sub-page group (Inventory → Worksheet) also counts as current on its parent's own page.
                var scope = group.parentElement.classList.contains("nav-subgroup") ? group.parentElement : group;
                var want = holdsCurrentPage(scope);
                if (group.open !== want) {
                    if (!group.hasAttribute("data-rail-was")) { group.setAttribute("data-rail-was", group.open ? "open" : "closed"); }
                    group.open = want;
                }
            } else if (group.hasAttribute("data-rail-was")) {
                var was = group.getAttribute("data-rail-was") === "open";
                group.removeAttribute("data-rail-was");
                if (group.open !== was) {
                    // Putting a group back is not a choice of the user's; the toggle handler skips saving it.
                    group.setAttribute("data-rail-restoring", "");
                    group.open = was;
                }
            }
        }
    }

    function firstLink(group) {
        return group.querySelector(".nav-group-items a.nav-link");
    }

    function updateNavRail() {
        var rail = railShown();
        syncRailGroups();
        var toggle = document.querySelector("[data-nav-rail-toggle]");
        if (toggle) {
            var text = rail ? "Expand menu" : "Collapse menu";
            toggle.setAttribute("aria-expanded", rail ? "false" : "true");
            toggle.setAttribute("data-bs-title", text);
            var label = toggle.querySelector("[data-nav-rail-label]");
            if (label) { label.textContent = text; }
        }

        if (!window.bootstrap || !window.bootstrap.Tooltip) { return; }
        for (var i = 0; i < navTips.length; i++) { navTips[i].dispose(); }
        navTips = [];
        // Labels are visible in the full sidebar, so only the rail needs tooltips (the toggle always has one).
        var targets = document.querySelectorAll(rail ? ".app-sidebar [data-bs-title]" : "[data-nav-rail-toggle]");
        for (var j = 0; j < targets.length; j++) {
            var options = { placement: "right", container: "body", delay: { show: 200, hide: 0 } };
            var group = targets[j].matches("summary.nav-section") ? targets[j].parentElement : null;
            var link = group && !holdsCurrentPage(group) ? firstLink(group) : null;
            // A category icon in the rail navigates, so its tooltip says where to.
            if (link) { options.title = targets[j].getAttribute("data-bs-title") + " · opens " + link.querySelector(".nav-label").textContent; }
            navTips.push(new window.bootstrap.Tooltip(targets[j], options));
        }
    }

    document.addEventListener("click", function (event) {
        var target = event.target.closest ? event.target : null;
        if (!target) { return; }

        // In the rail a category icon opens that category's first page (its group then opens as the current
        // one). The current category's icon does nothing. Enter/Space on a focused summary also land here.
        var summary = target.closest(".app-sidebar summary.nav-section");
        if (summary && railShown()) {
            event.preventDefault();
            var group = summary.parentElement;
            var link = holdsCurrentPage(group) ? null : firstLink(group);
            if (link) {
                group.open = true;
                window.location.assign(link.href);
            }
            return;
        }

        var toggle = target.closest("[data-nav-rail-toggle]");
        if (!toggle) { return; }
        var rail = document.documentElement.getAttribute("data-nav") !== "rail";
        if (rail) {
            document.documentElement.setAttribute("data-nav", "rail");
        } else {
            document.documentElement.removeAttribute("data-nav");
        }
        try {
            if (rail) { localStorage.setItem(navRailKey, "rail"); } else { localStorage.removeItem(navRailKey); }
        } catch (e) { /* not remembered */ }
        updateNavRail();
    });

    // Crossing the phone/desktop width switches between the drawer and the rail.
    if (navDesktop && navDesktop.addEventListener) { navDesktop.addEventListener("change", updateNavRail); }

    // site.js loads at the end of <body>, so the sidebar already exists: restore groups now, before the
    // first paint where possible, rather than waiting for DOMContentLoaded.
    setupNavGroups();
    syncRailGroups();

    // ---------- Bulk selection ----------
    // Row checkboxes ([data-select-row]) feed a bulk bar ([data-bulk-bar]): its count reads
    // "N <noun>s selected" (noun from data-bulk-noun) and its [data-bulk-button] controls are disabled
    // until something is ticked. [data-select-all] ticks every row shown.
    function setupBulk(bar) {
        var all = document.querySelector("[data-select-all]");
        var rows = document.querySelectorAll("[data-select-row]");
        var count = bar.querySelector("[data-bulk-count]");
        var idle = count ? count.textContent : "";
        var noun = bar.getAttribute("data-bulk-noun") || "item";
        var modalCount = document.querySelector("[data-bulk-count-modal]");
        var buttons = bar.querySelectorAll("[data-bulk-button]");
        if (rows.length === 0) {
            bar.hidden = true;
            if (all) { all.disabled = true; }
            return;
        }

        function update() {
            var selected = 0;
            for (var i = 0; i < rows.length; i++) {
                if (rows[i].checked) { selected++; }
                rows[i].closest("tr").classList.toggle("row-selected", rows[i].checked);
            }

            var plural = selected === 1 ? noun : noun + "s";
            if (count) { count.textContent = selected === 0 ? idle : selected + " " + plural + " selected"; }
            if (modalCount) { modalCount.textContent = "The " + selected + " selected " + plural; }
            bar.classList.toggle("has-selection", selected > 0);
            for (var j = 0; j < buttons.length; j++) { buttons[j].disabled = selected === 0; }
            if (all) {
                all.checked = selected === rows.length;
                all.indeterminate = selected > 0 && selected < rows.length;
            }
        }

        if (all) {
            all.addEventListener("change", function () {
                for (var i = 0; i < rows.length; i++) { rows[i].checked = all.checked; }
                update();
            });
        }

        for (var i = 0; i < rows.length; i++) { rows[i].addEventListener("change", update); }
        update();
    }

    // ---------- Unsaved-changes bar ----------
    // In a [data-dirty-form], inputs marked [data-original] are compared with their starting value:
    // changed ones get .is-changed, the [data-save-bar] shows how many, and leaving the page asks first.
    // Without JavaScript the bar's Save button is always visible (it is only hidden by script).
    function setupDirtyForm(form) {
        var inputs = form.querySelectorAll("[data-original]");
        var bar = form.querySelector("[data-save-bar]");
        var count = form.querySelector("[data-save-count]");
        var submitting = false;

        function changed() {
            var n = 0;
            for (var i = 0; i < inputs.length; i++) {
                var dirty = inputs[i].value !== inputs[i].getAttribute("data-original");
                inputs[i].classList.toggle("is-changed", dirty);
                if (dirty) { n++; }
            }
            return n;
        }

        function update() {
            var n = changed();
            if (bar) { bar.hidden = n === 0; }
            if (count) { count.textContent = n + " unsaved change" + (n === 1 ? "" : "s"); }
        }

        form.addEventListener("input", update);
        form.addEventListener("submit", function () { submitting = true; });
        window.addEventListener("beforeunload", function (event) {
            if (!submitting && changed() > 0) { event.preventDefault(); event.returnValue = ""; }
        });
        update();
    }

    // ---------- Worksheet send amounts ----------
    // Typing units to send into a [data-send-input] shows what home stock will be after the save
    // ("→ 70 at home after sending") under the same row's home count. The server does the real subtraction.
    document.addEventListener("input", function (event) {
        var input = event.target;
        if (!input.matches || !input.matches("[data-send-input]")) { return; }
        var row = input.closest("tr");
        var hint = row && row.querySelector("[data-after-send]");
        var home = row && row.querySelector(".home-input");
        if (!hint || !home) { return; }

        var send = parseInt(input.value, 10);
        var original = parseInt(home.getAttribute("data-original"), 10);
        if (!(send > 0) || isNaN(original)) { hint.hidden = true; return; }

        var after = original - send;
        hint.textContent = after < 0 ? "only " + original.toLocaleString("en-US") + " at home" : "→ " + after.toLocaleString("en-US") + " at home after sending";
        hint.classList.toggle("text-warn", after < 0);
        hint.hidden = false;
    });

    // ---------- Print buttons ----------
    document.addEventListener("click", function (event) {
        var button = event.target.closest && event.target.closest("[data-print]");
        if (button) { window.print(); }
    });

    document.addEventListener("DOMContentLoaded", function () {
        setupTooltips();
        updateNavRail();

        var dirty = document.querySelectorAll("[data-dirty-form]");
        for (var d = 0; d < dirty.length; d++) { setupDirtyForm(dirty[d]); }

        var bulk = document.querySelector("[data-bulk-bar]");
        if (bulk) { setupBulk(bulk); }

        // A dialog marked data-open-on-load opens right away (e.g. to keep managing families after a save).
        var dialog = document.querySelector("[data-open-on-load='true']");
        if (dialog && window.bootstrap && window.bootstrap.Modal) {
            window.bootstrap.Modal.getOrCreateInstance(dialog).show();

            // Drop the one-off "open" flag from the address so a refresh doesn't reopen the dialog.
            if (window.history && window.history.replaceState && window.URL) {
                var url = new URL(window.location.href);
                url.searchParams.delete(dialog.getAttribute("data-open-param") || "");
                window.history.replaceState(null, "", url.pathname + url.search + url.hash);
            }
            var focus = dialog.querySelector("input[type=text]");
            if (focus) { dialog.addEventListener("shown.bs.modal", function () { focus.focus(); }, { once: true }); }
        }
    });

    // ---------- Confirm before destructive submits ----------
    // A form with data-confirm="message" asks first; without JavaScript it simply submits.
    document.addEventListener("submit", function (event) {
        var message = event.target.getAttribute && event.target.getAttribute("data-confirm");
        if (message && !window.confirm(message)) { event.preventDefault(); }
    });

    // ---------- Toggles and filters submit as soon as they change ----------
    // A [data-autosubmit] control, or any select inside a [data-autosubmit-form], submits its form on
    // change. Every such form also has a submit button (hidden via .js-hide), so it works without script.
    document.addEventListener("change", function (event) {
        var target = event.target;
        var form = target.form;
        if (!form) { return; }
        if (target.matches("[data-autosubmit]") || (form.hasAttribute("data-autosubmit-form") && target.tagName === "SELECT")) {
            if (form.requestSubmit) { form.requestSubmit(); } else { form.submit(); }
        }
    });
})();
