// Light / dark / system theme for the whole app.
// Loaded synchronously in <head> so the theme is applied before first paint (no white flash).
// The choice is a per-browser convenience kept in localStorage; if storage is unavailable
// (private mode, blocked site data) the app simply follows the operating system setting.
(function () {
    "use strict";

    var storageKey = "aerai-theme";
    var media = window.matchMedia ? window.matchMedia("(prefers-color-scheme: dark)") : null;

    function stored() {
        try {
            var value = localStorage.getItem(storageKey);
            return value === "light" || value === "dark" ? value : "auto";
        } catch (e) {
            return "auto";
        }
    }

    function store(choice) {
        try {
            if (choice === "auto") {
                localStorage.removeItem(storageKey);
            } else {
                localStorage.setItem(storageKey, choice);
            }
        } catch (e) {
            // Storage unavailable: the choice still applies for this page view.
        }
    }

    function resolve(choice) {
        if (choice === "light" || choice === "dark") {
            return choice;
        }
        return media && media.matches ? "dark" : "light";
    }

    function apply(choice) {
        document.documentElement.setAttribute("data-bs-theme", resolve(choice));
        document.documentElement.setAttribute("data-theme-choice", choice);
        var buttons = document.querySelectorAll("[data-theme-option]");
        for (var i = 0; i < buttons.length; i++) {
            var active = buttons[i].getAttribute("data-theme-option") === choice;
            buttons[i].classList.toggle("active", active);
            buttons[i].setAttribute("aria-pressed", active ? "true" : "false");
        }
    }

    apply(stored());

    // Lets CSS hide no-script fallbacks (e.g. "Apply" buttons) before first paint, so they never flash.
    document.documentElement.classList.add("js");

    // Follow OS changes live while on "System".
    if (media && media.addEventListener) {
        media.addEventListener("change", function () {
            if (stored() === "auto") {
                apply("auto");
            }
        });
    }

    document.addEventListener("DOMContentLoaded", function () {
        apply(stored());
        document.addEventListener("click", function (event) {
            var button = event.target.closest ? event.target.closest("[data-theme-option]") : null;
            if (!button) {
                return;
            }
            var choice = button.getAttribute("data-theme-option");
            store(choice);
            apply(choice);
        });
    });
})();
