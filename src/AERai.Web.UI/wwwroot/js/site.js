// Site-wide progressive enhancements loaded on every page after Bootstrap.
(function () {
    "use strict";

    // ---------- Hover tips ----------
    // Explicit "?" tips plus every plain title="" on the page become styled tooltips that also open
    // on keyboard focus. Without JavaScript the native title tooltip still shows.
    var tipSelector = "[data-bs-toggle='tooltip'], [title]";

    function setupTooltips(root) {
        if (!window.bootstrap || !window.bootstrap.Tooltip) { return; }
        var elements = root ? root.querySelectorAll(tipSelector) : document.querySelectorAll(".app-content [data-bs-toggle='tooltip'], .app-content [title]");
        for (var i = 0; i < elements.length; i++) {
            window.bootstrap.Tooltip.getOrCreateInstance(elements[i], { container: "body", delay: { show: 150, hide: 50 } });
        }
    }

    // Tooltips live in <body>, so ones belonging to a section about to be replaced are removed first.
    function disposeTooltips(root) {
        if (!window.bootstrap || !window.bootstrap.Tooltip) { return; }
        var elements = root.querySelectorAll("*");
        for (var i = 0; i < elements.length; i++) {
            var tip = window.bootstrap.Tooltip.getInstance(elements[i]);
            if (tip) { tip.dispose(); }
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

    // ---------- Sticky table headers ----------
    // A sticky header only sticks inside its nearest scroll container, and every .table-responsive is one
    // (for sideways scrolling). So a table that fits its card gets .is-sticky, which drops the wrapper's
    // overflow so the header (and the card's .card-toolbar above it, via .has-sticky-head) sticks to the
    // window as the page scrolls. A table too wide to fit gets .table-scroll instead: a box about a screen
    // tall that scrolls both ways with the header stuck inside it. Width changes (window, sidebar rail,
    // expanded rows) re-decide. Without script the tables simply scroll sideways as before.
    function updateStickyTable(wrapper) {
        var table = wrapper.querySelector(":scope > table");
        if (!table) { return; }
        var fits = table.scrollWidth <= wrapper.clientWidth + 1;
        wrapper.classList.toggle("is-sticky", fits);
        wrapper.classList.toggle("table-scroll", !fits);

        // The toolbar sticks only when its card holds just this table, and the header sits right below it.
        var card = wrapper.parentElement;
        var toolbar = card && card.classList.contains("card") ? card.querySelector(":scope > .card-toolbar") : null;
        var withToolbar = fits && toolbar !== null && card.querySelectorAll(":scope > .table-responsive").length === 1;
        if (card && card.classList.contains("card")) {
            card.classList.toggle("has-sticky-head", withToolbar);
            if (withToolbar) {
                // Rounded down so the header tucks under the toolbar instead of leaving a sliver rows show through.
                card.style.setProperty("--sticky-head", Math.floor(toolbar.getBoundingClientRect().height) + "px");
            } else {
                card.style.removeProperty("--sticky-head");
            }
        }
    }

    var stickyWrappers = [];
    var stickyObserver = null;
    var stickyPending = false;

    function updateAllStickyTables() {
        for (var i = 0; i < stickyWrappers.length; i++) { updateStickyTable(stickyWrappers[i]); }
    }

    // Called for the page at load and again for each section a partial reload swaps in; wrappers that
    // were replaced are dropped so the observer never holds detached tables.
    function setupStickyTables(root) {
        var found = (root || document).querySelectorAll(root ? ".table-responsive" : ".app-content .table-responsive");
        stickyWrappers = stickyWrappers.filter(function (wrapper) {
            if (wrapper.isConnected) { return true; }
            if (stickyObserver) {
                stickyObserver.unobserve(wrapper);
                var table = wrapper.querySelector(":scope > table");
                if (table) { stickyObserver.unobserve(table); }
            }
            return false;
        });
        if (found.length === 0) { return; }
        for (var i = 0; i < found.length; i++) {
            stickyWrappers.push(found[i]);
            updateStickyTable(found[i]);
        }
        if (!window.ResizeObserver) { return; }

        // Deferred a frame so the class changes don't re-trigger the observer inside its own callback.
        if (!stickyObserver) {
            stickyObserver = new ResizeObserver(function () {
                if (stickyPending) { return; }
                stickyPending = true;
                window.requestAnimationFrame(function () { stickyPending = false; updateAllStickyTables(); });
            });
        }
        for (var j = 0; j < found.length; j++) {
            stickyObserver.observe(found[j]);
            var table = found[j].querySelector(":scope > table");
            if (table) { stickyObserver.observe(table); }
            var toolbar = found[j].parentElement && found[j].parentElement.querySelector(":scope > .card-toolbar");
            if (toolbar) { stickyObserver.observe(toolbar); }
        }
    }

    // ---------- Print buttons ----------
    document.addEventListener("click", function (event) {
        var button = event.target.closest && event.target.closest("[data-print]");
        if (button) { window.print(); }
    });

    document.addEventListener("DOMContentLoaded", function () {
        setupTooltips();
        updateNavRail();
        setupStickyTables();

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
                window.history.replaceState(window.history.state, "", url.pathname + url.search + url.hash);
            }
            var focus = dialog.querySelector("input[type=text]");
            if (focus) { dialog.addEventListener("shown.bs.modal", function () { focus.focus(); }, { once: true }); }
        }
    });

    // ---------- Show / hide password ----------
    // A [data-password-toggle] button (Shared/_PasswordToggle) flips the password input in its .input-group
    // between hidden and plain text; aria-pressed drives which eye icon shows (site.css).
    document.addEventListener("click", function (event) {
        var button = event.target.closest && event.target.closest("[data-password-toggle]");
        if (!button) { return; }
        var input = button.parentElement.querySelector("input");
        if (!input) { return; }
        var show = input.type === "password";
        input.type = show ? "text" : "password";
        var label = show ? "Hide password" : "Show password";
        button.setAttribute("aria-pressed", show ? "true" : "false");
        button.setAttribute("aria-label", label);
        button.setAttribute("title", label);
        input.focus();
    });

    // ---------- Confirm before destructive submits ----------
    // A form with data-confirm="message" asks first; without JavaScript it simply submits.
    document.addEventListener("submit", function (event) {
        var message = event.target.getAttribute && event.target.getAttribute("data-confirm");
        if (message && !window.confirm(message)) { event.preventDefault(); }
    });

    // ---------- Expandable row groups (Settlements → Lines) ----------
    // A [data-expand-row] button opens or closes its group (the <tbody> named by aria-controls) by
    // toggling .is-expanded; [data-expand-all="true|false"] does every group on the page. Without
    // script the lines simply show (CSS hides them only under html.js).
    function setExpanded(button, open) {
        var group = document.getElementById(button.getAttribute("aria-controls"));
        if (!group) { return; }
        group.classList.toggle("is-expanded", open);
        button.setAttribute("aria-expanded", open ? "true" : "false");
    }
    document.addEventListener("click", function (event) {
        var row = event.target.closest && event.target.closest("[data-expand-row]");
        // A click anywhere on a group's parent row (other than a link) toggles it too.
        var parent = !row && event.target.closest && !event.target.closest("a, button") && event.target.closest(".group-row");
        if (parent) { row = parent.querySelector("[data-expand-row]"); }
        if (row) {
            setExpanded(row, row.getAttribute("aria-expanded") !== "true");
            return;
        }
        var all = event.target.closest && event.target.closest("[data-expand-all]");
        if (all) {
            var open = all.getAttribute("data-expand-all") === "true";
            document.querySelectorAll("[data-expand-row]").forEach(function (button) { setExpanded(button, open); });
        }
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

    // ---------- Partial reloads ----------
    // Inside a [data-fragment] section, links to the same page (pager, sorts, view switches) and GET forms
    // (filters, search) refresh only that section: the same URL is fetched with an X-Fragment header, the
    // page answers with just the section's partial (PageFragments.PageOrFragment), and the address is
    // updated so Back, refresh, and bookmarks behave as before. [data-fragment-skip] opts a link or form
    // out (downloads, other pages). Anything unexpected falls back to loading the page normally.
    var fragmentRequest = 0;

    // Only this page's own GET: a ?handler= link (CSV export, download) answers with something else.
    function fragmentTarget(url) {
        return url.origin === window.location.origin && url.pathname === window.location.pathname && !url.searchParams.has("handler");
    }

    function restoreFocus(region, focused) {
        if (!focused) { return; }
        var match = focused.id ? document.getElementById(focused.id)
            : focused.name && window.CSS && CSS.escape ? region.querySelector("[name='" + CSS.escape(focused.name) + "']") : null;
        if (match && region.contains(match)) { match.focus({ preventScroll: true }); }
    }

    function loadFragment(region, url, push) {
        var request = ++fragmentRequest;
        var active = document.activeElement;
        var focused = active && region.contains(active) && (active.id || active.name) ? { id: active.id, name: active.name } : null;
        region.setAttribute("aria-busy", "true");

        window.fetch(url.href, { headers: { "X-Fragment": "1" }, credentials: "same-origin" })
            .then(function (response) {
                // A sign-in redirect or an error page is a whole document, not this section.
                if (!response.ok || response.redirected) { throw new Error("fragment " + response.status); }
                return response.text();
            })
            .then(function (html) {
                if (request !== fragmentRequest) { return; }
                if (push) {
                    window.history.pushState({ fragment: region.getAttribute("data-fragment") }, "", url.pathname + url.search);
                }
                disposeTooltips(region);
                region.innerHTML = html;
                region.removeAttribute("aria-busy");
                setupTooltips(region);
                setupStickyTables(region);
                var bulk = region.querySelector("[data-bulk-bar]");
                if (bulk) { setupBulk(bulk); }
                restoreFocus(region, focused);
                // A pager at the bottom of a long list brings the list back into view.
                if (region.getBoundingClientRect().top < 0) { region.scrollIntoView({ block: "start" }); }
            })
            .catch(function () {
                if (request === fragmentRequest) { window.location.assign(url.href); }
            });
    }

    function fragmentRegion(element) {
        if (!window.fetch || !window.history || !window.history.pushState || !element.closest) { return null; }
        if (element.closest("[data-fragment-skip]")) { return null; }
        return element.closest("[data-fragment]");
    }

    document.addEventListener("click", function (event) {
        if (event.defaultPrevented || event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) { return; }
        var link = event.target.closest && event.target.closest("a[href]");
        if (!link || link.hasAttribute("download") || (link.target && link.target !== "_self")) { return; }
        var region = fragmentRegion(link);
        if (!region) { return; }
        var url = new URL(link.href, window.location.href);
        if (!fragmentTarget(url) || url.hash) { return; }
        event.preventDefault();
        loadFragment(region, url, true);
    });

    // Registered after the data-confirm handler, so a cancelled confirm is already defaultPrevented here.
    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (event.defaultPrevented || !form.matches || (form.getAttribute("method") || "get").toLowerCase() !== "get") { return; }
        if (form.target && form.target !== "_self") { return; }
        var region = fragmentRegion(form);
        if (!region) { return; }
        var submitter = event.submitter && event.submitter.getAttribute("formaction") ? event.submitter.formAction : form.action;
        var url = new URL(submitter, window.location.href);
        if (!fragmentTarget(url)) { return; }
        var data = new FormData(form, event.submitter || undefined);
        url.search = new URLSearchParams(data).toString();
        event.preventDefault();
        loadFragment(region, url, true);
    });

    // Back and Forward between partial reloads refresh the same section for the address now shown.
    window.addEventListener("popstate", function (event) {
        var name = event.state && event.state.fragment;
        var region = name ? document.querySelector("[data-fragment='" + name + "']") : null;
        if (!region) { return; }
        loadFragment(region, new URL(window.location.href), false);
    });

    // The page as first loaded gets a history entry too, so Back from the first partial reload returns to it.
    if (window.history && window.history.replaceState && document.querySelector("[data-fragment]")) {
        var first = document.querySelector("[data-fragment]");
        window.history.replaceState({ fragment: first.getAttribute("data-fragment") }, "", window.location.href);
    }
})();
