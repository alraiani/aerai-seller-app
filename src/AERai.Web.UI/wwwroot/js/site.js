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
            makeDetailFocusable(elements[i]);
            window.bootstrap.Tooltip.getOrCreateInstance(elements[i], { container: "body", delay: { show: 150, hide: 50 } });
        }
    }

    // A cued value (.has-detail) that isn't a link or button becomes focusable, so a click or tap
    // focuses it and its tip stays open until focus moves on (Bootstrap keeps a tip open while any
    // trigger is active). That is what makes the detail reachable on touch screens and by keyboard.
    function makeDetailFocusable(el) {
        if (!el.classList.contains("has-detail") || el.matches("a[href], button, input, select, textarea")) { return; }
        if (!el.hasAttribute("tabindex")) { el.setAttribute("tabindex", "0"); }
    }

    // Escape closes a tip opened by a click: focus leaves the value, which hides it.
    document.addEventListener("keydown", function (e) {
        if (e.key === "Escape" && document.activeElement && document.activeElement.classList.contains("has-detail")) {
            document.activeElement.blur();
        }
    });

    // Tooltips live in <body>, so ones belonging to a section about to be replaced are removed first.
    function disposeTooltips(root) {
        if (!window.bootstrap || !window.bootstrap.Tooltip) { return; }
        var elements = root.querySelectorAll("*");
        for (var i = 0; i < elements.length; i++) {
            var tip = window.bootstrap.Tooltip.getInstance(elements[i]);
            if (tip) { tip.dispose(); }
        }
    }

    // ---------- Announcements ----------
    // The shared live region in _Layout ([data-announcer]) tells screen readers about changes that happen
    // without a page load. Cleared first, so the same message twice is read twice.
    function announce(message) {
        var region = document.querySelector("[data-announcer]");
        if (!region) { return; }
        region.textContent = "";
        window.setTimeout(function () { region.textContent = message; }, 50);
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
        var named = document.querySelectorAll(".app-sidebar summary.nav-section[aria-label]");
        for (var n = 0; n < named.length; n++) { named[n].removeAttribute("aria-label"); }
        // Labels are visible in the full sidebar, so only the rail needs tooltips (the toggle always has one).
        var targets = document.querySelectorAll(rail ? ".app-sidebar [data-bs-title]" : "[data-nav-rail-toggle]");
        for (var j = 0; j < targets.length; j++) {
            var options = { placement: "right", container: "body", delay: { show: 200, hide: 0 } };
            var group = targets[j].matches("summary.nav-section") ? targets[j].parentElement : null;
            var link = group && !holdsCurrentPage(group) ? firstLink(group) : null;
            // A category icon in the rail navigates, so its tooltip (and its name for screen readers) says where to.
            if (link) {
                options.title = targets[j].getAttribute("data-bs-title") + " · opens " + link.querySelector(".nav-label").textContent;
                targets[j].setAttribute("aria-label", options.title);
            }
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

    // A [data-select-group] box ticks or clears every [data-select-row] in its <tbody> (e.g. all the sends of one
    // shipment) and shows whether all, some, or none of them are ticked. The rows' own change events keep the
    // bulk bar's count right.
    function syncSelectGroup(group) {
        var box = group.querySelector("[data-select-group]");
        if (!box) { return; }
        var rows = group.querySelectorAll("[data-select-row]");
        var ticked = 0;
        rows.forEach(function (row) { if (row.checked) { ticked++; } });
        box.checked = rows.length > 0 && ticked === rows.length;
        box.indeterminate = ticked > 0 && ticked < rows.length;
    }
    document.addEventListener("change", function (event) {
        var target = event.target;
        if (!target.matches) { return; }
        var group = target.closest("tbody");
        if (target.matches("[data-select-group]") && group) {
            // Read once: each row's change re-syncs this box, which would flip it partway through.
            var on = target.checked;
            group.querySelectorAll("[data-select-row]").forEach(function (row) {
                row.checked = on;
                row.dispatchEvent(new Event("change", { bubbles: true }));
            });
        } else if (target.matches("[data-select-row]") && group) {
            syncSelectGroup(group);
        } else if (target.matches("[data-select-all]")) {
            document.querySelectorAll("[data-select-group]").forEach(function (box) { syncSelectGroup(box.closest("tbody")); });
        }
    });

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
    // The total row's [data-send-total] shows the sum of every box, so it says what will actually go out;
    // the suggestion moves underneath ([data-send-suggested]). Without script the total is the suggestion.
    function updateSendTotal() {
        var total = document.querySelector("[data-send-total]");
        var boxes = document.querySelectorAll("[data-send-input]");
        // Viewers have no boxes, so their total stays the suggestion.
        if (!total || boxes.length === 0) { return; }
        var sum = 0;
        boxes.forEach(function (box) {
            var units = parseInt(box.value, 10);
            if (units > 0) { sum += units; }
        });
        total.textContent = sum > 0 ? "Sending " + sum.toLocaleString("en-US") : "Sending 0";
        var suggested = document.querySelector("[data-send-suggested]");
        if (suggested) { suggested.hidden = false; }
    }

    document.addEventListener("input", function (event) {
        var input = event.target;
        if (!input.matches || !input.matches("[data-send-input]")) { return; }
        updateSendTotal();
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
    // Every table's header sticks to the window as the page scrolls, under the card's .card-toolbar when
    // that sticks too (.has-sticky-head), and so does a totals row placed as the header's last row. A
    // sticky header only sticks inside its nearest scroll container, and every .table-responsive is one
    // (for sideways scrolling). So a table that fits its card gets .is-sticky, which drops the wrapper's
    // overflow and lets the header cells stick. A table too wide to fit keeps scrolling sideways and gets
    // a floating copy of its header instead (.sticky-float, see buildFloatHead). Width changes (window,
    // sidebar rail, expanded rows) re-decide. Without script the tables simply scroll sideways.
    function updateStickyTable(wrapper) {
        var table = wrapper.querySelector(":scope > table");
        if (!table) { return; }
        var fits = table.scrollWidth <= wrapper.clientWidth + 1;
        wrapper.classList.toggle("is-sticky", fits);
        wrapper.classList.toggle("has-float-head", !fits);

        // A second header row (a totals row) sticks right under the first.
        var first = table.tHead && table.tHead.rows.length > 1 ? table.tHead.rows[0] : null;
        if (first) {
            table.style.setProperty("--sticky-row1", Math.floor(first.getBoundingClientRect().height) + "px");
        }

        // The toolbar sticks only when its card holds just this table, and the header sits right below it.
        var card = wrapper.parentElement;
        var toolbar = card && card.classList.contains("card") ? card.querySelector(":scope > .card-toolbar") : null;
        var withToolbar = toolbar !== null && card.querySelectorAll(":scope > .table-responsive").length === 1;
        if (card && card.classList.contains("card")) {
            card.classList.toggle("has-sticky-head", withToolbar);
            if (withToolbar) {
                // Rounded down so the header tucks under the toolbar instead of leaving a sliver rows show through.
                card.style.setProperty("--sticky-head", Math.floor(toolbar.getBoundingClientRect().height) + "px");
            } else {
                card.style.removeProperty("--sticky-head");
            }
        }

        if (fits) {
            removeFloatHead(wrapper);
        } else {
            buildFloatHead(wrapper, table);
        }
    }

    // A wide table's header can't stick to the window from inside its sideways-scrolling wrapper, so a
    // copy of the whole header (totals row included) is laid over it in a sticky box just before the
    // wrapper, cell widths copied, scrolled sideways with the table (both ways, so a swipe on the header
    // scrolls the table). The real header stays for screen readers and is what the copy acts on: the copy
    // loses ids, names, and behavior attributes so page scripts never find it, and a click on one of its
    // links, buttons, or boxes is passed to the same element in the real header.
    var floatKeep = { "data-bs-toggle": true, "data-bs-title": true };

    function floatOf(wrapper) {
        var previous = wrapper.previousElementSibling;
        return previous && previous.classList.contains("sticky-float") ? previous : null;
    }

    function removeFloatHead(wrapper) {
        var float = floatOf(wrapper);
        if (wrapper.floatObserver) { wrapper.floatObserver.disconnect(); wrapper.floatObserver = null; }
        if (!float) { return; }
        disposeTooltips(float);
        float.remove();
    }

    function buildFloatHead(wrapper, table) {
        var head = table.tHead;
        if (!head) { removeFloatHead(wrapper); return; }
        var float = floatOf(wrapper);
        if (!float) {
            float = document.createElement("div");
            float.className = "sticky-float";
            float.setAttribute("aria-hidden", "true");
            wrapper.parentNode.insertBefore(float, wrapper);
            float.addEventListener("click", function (event) { forwardFloatClick(event, float, table); });
            float.addEventListener("scroll", function () { if (wrapper.scrollLeft !== float.scrollLeft) { wrapper.scrollLeft = float.scrollLeft; } });
            wrapper.addEventListener("scroll", function () { if (float.scrollLeft !== wrapper.scrollLeft) { float.scrollLeft = wrapper.scrollLeft; } });
        }
        if (!wrapper.floatObserver && window.MutationObserver) {
            // Header text can change in place (e.g. the Worksheet's live "Sending N" total).
            var pending = false;
            wrapper.floatObserver = new MutationObserver(function () {
                if (pending) { return; }
                pending = true;
                window.setTimeout(function () { pending = false; if (floatOf(wrapper)) { buildFloatHead(wrapper, table); } }, 0);
            });
            wrapper.floatObserver.observe(head, { childList: true, subtree: true, characterData: true });
        }

        disposeTooltips(float);
        var copy = document.createElement("table");
        copy.className = table.className;
        copy.style.width = table.getBoundingClientRect().width + "px";
        var rowOne = table.style.getPropertyValue("--sticky-row1");
        if (rowOne) { copy.style.setProperty("--sticky-row1", rowOne); }
        var headCopy = head.cloneNode(true);
        copy.appendChild(headCopy);

        // Same widths cell by cell, so the copy lines up with the columns below it.
        var cells = head.querySelectorAll("th, td");
        var copies = headCopy.querySelectorAll("th, td");
        for (var i = 0; i < cells.length && i < copies.length; i++) {
            var width = cells[i].getBoundingClientRect().width + "px";
            copies[i].style.width = width;
            copies[i].style.minWidth = width;
            copies[i].style.maxWidth = width;
        }

        // Boxes keep their current state (cloning copies attributes, not what the user ticked).
        var boxes = head.querySelectorAll("input");
        var boxCopies = headCopy.querySelectorAll("input");
        for (var b = 0; b < boxes.length && b < boxCopies.length; b++) {
            boxCopies[b].checked = boxes[b].checked;
            boxCopies[b].indeterminate = boxes[b].indeterminate;
        }

        var all = headCopy.querySelectorAll("*");
        for (var j = 0; j < all.length; j++) { quietFloatElement(all[j]); }

        float.replaceChildren(copy);
        float.style.marginBottom = -head.getBoundingClientRect().height + "px";
        float.scrollLeft = wrapper.scrollLeft;
        setupTooltips(float);
    }

    function quietFloatElement(element) {
        // A tip already set up moved its text to data-bs-original-title; the copy gets it back as a title.
        var tip = element.getAttribute("data-bs-original-title");
        if (tip && !element.getAttribute("title")) { element.setAttribute("title", tip); }
        var names = [];
        for (var a = 0; a < element.attributes.length; a++) { names.push(element.attributes[a].name); }
        for (var n = 0; n < names.length; n++) {
            var name = names[n];
            if ((name.indexOf("data-") === 0 && !floatKeep[name]) || name === "id" || name === "name" || name === "for"
                || name === "aria-describedby" || name === "aria-controls") {
                element.removeAttribute(name);
            }
        }
        if (element.matches("a, button, input, select, textarea, [tabindex]")) { element.setAttribute("tabindex", "-1"); }
    }

    function forwardFloatClick(event, float, table) {
        var target = event.target.closest && event.target.closest("a, button, input, select, label");
        var headCopy = float.querySelector("thead");
        if (!target || !headCopy || !headCopy.contains(target) || !table.tHead) { return; }
        var path = [];
        for (var node = target; node && node !== headCopy; node = node.parentElement) {
            path.unshift(Array.prototype.indexOf.call(node.parentElement.children, node));
        }
        var original = table.tHead;
        for (var i = 0; i < path.length && original; i++) { original = original.children[path[i]]; }
        if (!original) { return; }
        event.preventDefault();
        original.click();
        var wrapper = float.nextElementSibling;
        if (wrapper && wrapper.isConnected) { window.setTimeout(function () { buildFloatHead(wrapper, table); }, 0); }
    }

    // Ticking rows changes a header's select-all box (ticked, partly ticked), which the copy then shows too.
    document.addEventListener("change", function (event) {
        var wrapper = event.target.closest && event.target.closest(".table-responsive.has-float-head");
        var table = wrapper && wrapper.querySelector(":scope > table");
        if (table && table.tHead && table.tHead.querySelector("input")) {
            window.setTimeout(function () { buildFloatHead(wrapper, table); }, 0);
        }
    });

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
            if (wrapper.floatObserver) { wrapper.floatObserver.disconnect(); wrapper.floatObserver = null; }
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

    // A [data-print-group="id"] button prints just that row group (e.g. one kind of ledger movement), opened:
    // html.printing-group hides every other group in print (site.css); the page is put back afterwards.
    document.addEventListener("click", function (event) {
        var button = event.target.closest && event.target.closest("[data-print-group]");
        var group = button && document.getElementById(button.getAttribute("data-print-group"));
        if (!group) { return; }
        var root = document.documentElement;
        var wasOpen = group.classList.contains("is-expanded");
        root.classList.add("printing-group");
        group.classList.add("is-printing", "is-expanded");
        window.addEventListener("afterprint", function () {
            root.classList.remove("printing-group");
            group.classList.remove("is-printing");
            group.classList.toggle("is-expanded", wasOpen);
        }, { once: true });
        window.print();
    });

    document.addEventListener("DOMContentLoaded", function () {
        setupTooltips();
        updateNavRail();
        setupStickyTables();
        updateSendTotal();

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
    // A form with data-confirm="message" asks first; without JavaScript it simply submits. A submit button with its
    // own data-confirm (a second action in the same form, e.g. Undo ticked) asks its own question instead.
    document.addEventListener("submit", function (event) {
        var submitter = event.submitter;
        var message = (submitter && submitter.getAttribute("data-confirm")) || (event.target.getAttribute && event.target.getAttribute("data-confirm"));
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
        // A click anywhere on a group's parent row (other than a link or a form control) toggles it too.
        var parent = !row && event.target.closest && !event.target.closest("a, button, input, select, textarea, label") && event.target.closest(".group-row");
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

    // Focus goes back to the same control in the new content: by id, by name, by link address, or by the
    // link's text (a sort link's address changes with its direction). Failing that, to the section itself,
    // so keyboard users never drop back to the top of the page (WCAG 2.4.3).
    function restoreFocus(region, focused) {
        if (!focused) { return; }
        var match = null;
        if (focused.id) { match = document.getElementById(focused.id); }
        if (!match && focused.name && window.CSS && CSS.escape) { match = region.querySelector("[name='" + CSS.escape(focused.name) + "']"); }
        if (!match && focused.href && window.CSS && CSS.escape) { match = region.querySelector("a[href='" + CSS.escape(focused.href) + "']"); }
        if (!match && focused.text) {
            var candidates = region.querySelectorAll("a[href], button");
            for (var i = 0; i < candidates.length && !match; i++) {
                if (candidates[i].textContent.trim() === focused.text) { match = candidates[i]; }
            }
        }
        if (!match || !region.contains(match)) {
            if (!region.hasAttribute("tabindex")) { region.setAttribute("tabindex", "-1"); }
            match = region;
        }
        match.focus({ preventScroll: true });
    }

    // Says what happened in a partial reload for screen readers: the list's own count line when it has one.
    function fragmentSummary(region) {
        var count = region.querySelector(".pager-summary, .list-count, .card-toolbar-title");
        var text = count ? count.textContent.replace(/\s+/g, " ").trim() : "";
        return text ? "Updated: " + text : "Updated";
    }

    function loadFragment(region, url, push) {
        var request = ++fragmentRequest;
        var active = document.activeElement;
        var focused = active && active !== region && region.contains(active)
            ? { id: active.id, name: active.name, href: active.getAttribute("href"), text: active.textContent.trim() }
            : null;
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
                announce(fragmentSummary(region));
                // A pager at the bottom of a long list brings the list back into view.
                if (region.getBoundingClientRect().top < 0) { region.scrollIntoView({ block: "start" }); }
            })
            .catch(function () {
                if (request !== fragmentRequest) { return; }
                announce("Couldn't update the list; reloading the page.");
                window.location.assign(url.href);
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

    // ---------- Busy submits ----------
    // Every POST shows it is working and can't be sent twice: the pressed button gets a spinner and is
    // disabled, the form is aria-busy, and screen readers hear "Working…". The button is disabled a tick
    // later, because a disabled submitter would drop its own name/value from the post. Downloads are GETs,
    // so a POST always leaves the page. Opt out with data-busy="off" on the form. Registered last, so a
    // cancelled confirm (data-confirm) is already defaultPrevented here.
    document.addEventListener("submit", function (event) {
        var form = event.target;
        if (!form.matches || (form.getAttribute("method") || "get").toLowerCase() !== "post") { return; }
        if (form.getAttribute("data-busy") === "off" || (form.target && form.target !== "_self")) { return; }
        if (form.getAttribute("aria-busy") === "true") { event.preventDefault(); return; }
        if (event.defaultPrevented) { return; }

        var button = event.submitter || form.querySelector("[type=submit]");
        form.setAttribute("aria-busy", "true");
        announce("Working…");
        window.setTimeout(function () {
            if (!button) { return; }
            button.disabled = true;
            button.classList.add("is-busy");
            if (!button.querySelector(".busy-spinner")) {
                var spinner = document.createElement("span");
                spinner.className = "busy-spinner";
                spinner.setAttribute("aria-hidden", "true");
                button.insertBefore(spinner, button.firstChild);
            }
        }, 0);
    });

    // Back to a page from the browser cache: undo any busy state, or its buttons would stay disabled.
    window.addEventListener("pageshow", function (event) {
        if (!event.persisted) { return; }
        document.querySelectorAll("form[aria-busy='true']").forEach(function (form) { form.removeAttribute("aria-busy"); });
        document.querySelectorAll(".is-busy").forEach(function (button) {
            button.disabled = false;
            button.classList.remove("is-busy");
            var spinner = button.querySelector(".busy-spinner");
            if (spinner) { spinner.remove(); }
        });
    });

    // ---------- Printing ----------
    // Paper is white: a page printed in the dark theme switches to the light tokens for the print only.
    var themeBeforePrint = null;
    window.addEventListener("beforeprint", function () {
        themeBeforePrint = document.documentElement.getAttribute("data-bs-theme");
        document.documentElement.setAttribute("data-bs-theme", "light");
    });
    window.addEventListener("afterprint", function () {
        if (themeBeforePrint) { document.documentElement.setAttribute("data-bs-theme", themeBeforePrint); }
        themeBeforePrint = null;
    });
})();
