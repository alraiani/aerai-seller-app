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

    document.addEventListener("DOMContentLoaded", function () {
        setupTooltips();

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
