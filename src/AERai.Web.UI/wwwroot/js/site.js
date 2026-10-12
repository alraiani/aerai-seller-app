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
    // Categories are <details> groups rendered open. This browser's collapsed ones are kept in
    // localStorage and closed again on load, except a group holding the current page, which always
    // shows. The rail toggle shrinks the sidebar to icons on desktop (theme.js applies the saved choice
    // early); the rail opens every group, because its icons are the only way to reach those pages, and
    // turns each label into a tooltip. Storage failures just mean nothing is remembered.
    var navGroupsKey = "aerai-nav-groups";
    var navRailKey = "aerai-nav";
    var navTips = [];
    // Must match the rail's media query in site.css: phones always get the full drawer.
    var navDesktop = window.matchMedia ? window.matchMedia("(min-width: 769px)") : null;

    function readClosedGroups() {
        try {
            var value = JSON.parse(localStorage.getItem(navGroupsKey) || "[]");
            return Array.isArray(value) ? value : [];
        } catch (e) {
            return [];
        }
    }

    function navGroups() {
        return document.querySelectorAll(".app-sidebar details[data-nav-group]");
    }

    function railShown() {
        return document.documentElement.getAttribute("data-nav") === "rail" && (!navDesktop || navDesktop.matches);
    }

    function setupNavGroups() {
        var groups = navGroups();
        if (groups.length === 0) { return; }

        var closed = readClosedGroups();
        for (var i = 0; i < groups.length; i++) {
            var key = groups[i].getAttribute("data-nav-group");
            if (closed.indexOf(key) >= 0 && !groups[i].querySelector("[aria-current='page']")) {
                groups[i].open = false;
            }
        }

        // Only the toggled group's key changes, so a group held open for the current page keeps its saved
        // "closed" preference (the restore above also fires toggle events, which then change nothing).
        // Opening and closing in the rail is temporary and never saved.
        for (var j = 0; j < groups.length; j++) {
            groups[j].addEventListener("toggle", function (event) {
                if (railShown()) { return; }
                var group = event.currentTarget;
                var keys = readClosedGroups();
                var key = group.getAttribute("data-nav-group");
                var at = keys.indexOf(key);
                if (group.open && at >= 0) { keys.splice(at, 1); }
                if (!group.open && at < 0) { keys.push(key); }
                try { localStorage.setItem(navGroupsKey, JSON.stringify(keys)); } catch (e) { /* not remembered */ }
            });
        }
    }

    // Opens collapsed groups while the rail shows, and closes exactly those again when it goes away.
    function syncRailGroups() {
        var rail = railShown();
        var groups = navGroups();
        for (var i = 0; i < groups.length; i++) {
            if (rail && !groups[i].open) {
                groups[i].setAttribute("data-rail-opened", "");
                groups[i].open = true;
            } else if (!rail && groups[i].hasAttribute("data-rail-opened")) {
                groups[i].removeAttribute("data-rail-opened");
                groups[i].open = false;
            }
        }
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
            navTips.push(new window.bootstrap.Tooltip(targets[j], { placement: "right", container: "body", delay: { show: 200, hide: 0 } }));
        }
    }

    document.addEventListener("click", function (event) {
        var toggle = event.target.closest && event.target.closest("[data-nav-rail-toggle]");
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
