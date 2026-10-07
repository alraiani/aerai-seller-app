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

    document.addEventListener("DOMContentLoaded", setupTooltips);
})();
