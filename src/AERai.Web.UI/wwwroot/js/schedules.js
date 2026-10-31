// Amazon sync pages: progressive enhancements only. Every action still works without JavaScript
// (forms have submit buttons that this script hides via the "js" class set in theme.js).
(function () {
    "use strict";

    var refreshOffKey = "aerai-sync-autorefresh-off";

    // Toggles and filter selects submit on change via site.js ([data-autosubmit], [data-autosubmit-form]).

    // ---------- Auto-refresh while a run is in progress ----------
    function storageGet() {
        try { return sessionStorage.getItem(refreshOffKey) === "1"; } catch (e) { return false; }
    }

    function storageSet(off) {
        try {
            if (off) { sessionStorage.setItem(refreshOffKey, "1"); } else { sessionStorage.removeItem(refreshOffKey); }
        } catch (e) {
            // Storage unavailable: the choice applies to this page view only.
        }
    }

    function busy() {
        // Never pull the page out from under someone who is typing, choosing, or confirming.
        var active = document.activeElement;
        return document.querySelector(".modal.show, .dropdown-menu.show") !== null
            || (active && /^(INPUT|SELECT|TEXTAREA)$/.test(active.tagName));
    }

    // A reload queries the database, so an idle or background tab must not keep reloading: the serverless
    // database only auto-pauses after 15 minutes without a query. Refreshing stops once the tab is hidden or
    // untouched for this long, and resumes (after a short delay) when the person comes back.
    var idleLimitMs = 5 * 60 * 1000;
    var resumeDelayMs = 1000;

    function setupAutoRefresh(chip) {
        var seconds = parseInt(chip.getAttribute("data-auto-refresh"), 10) || 10;
        var label = chip.querySelector("span:not(.status-light)");
        var button = chip.querySelector("[data-auto-refresh-stop]");
        var timer = null;
        var lastActivity = Date.now();
        var idle = false;

        function schedule(delayMs) {
            window.clearTimeout(timer);
            timer = window.setTimeout(function tick() {
                if (document.hidden || Date.now() - lastActivity > idleLimitMs) {
                    // Nobody is looking: make no request and start no timer until they return.
                    idle = true;
                    render(false);
                    return;
                }
                if (busy()) {
                    timer = window.setTimeout(tick, 2000);
                    return;
                }
                window.location.reload();
            }, delayMs === undefined ? seconds * 1000 : delayMs);
        }

        function render(off) {
            chip.classList.toggle("is-off", off || idle);
            label.textContent = off ? "A run is in progress — auto-refresh is off."
                : idle ? "A run is in progress — auto-refresh paused while this page is idle."
                : "A run is in progress — refreshing every " + seconds + " s.";
            button.textContent = off ? "Resume" : "Stop";
        }

        function activity() {
            lastActivity = Date.now();
            if (idle && !document.hidden && !storageGet()) {
                idle = false;
                render(false);
                schedule(resumeDelayMs);
            }
        }

        ["pointerdown", "keydown", "touchstart", "wheel"].forEach(function (name) {
            document.addEventListener(name, activity, { passive: true });
        });
        document.addEventListener("visibilitychange", function () {
            if (!document.hidden) { activity(); }
        });

        button.addEventListener("click", function () {
            var off = !storageGet();
            storageSet(off);
            window.clearTimeout(timer);
            idle = false;
            render(off);
            if (!off) { schedule(); }
        });

        var off = storageGet();
        render(off);
        if (!off) { schedule(); }
    }

    // ---------- Add / edit schedule form ----------
    function setupScheduleForm(form) {
        var summary = form.querySelector("[data-schedule-summary]");
        var enabledFact = form.querySelector("[data-summary-enabled]");
        var promoteFact = form.querySelector("[data-summary-promote]");

        function field(name) { return form.querySelector("[name='Input." + name + "']"); }
        function checked(name) { return form.querySelector("[name='Input." + name + "']:checked"); }

        function zoneShort(id) {
            var map = { "America/New_York": "ET", "America/Toronto": "ET", "Europe/London": "UK", "America/Chicago": "CT", "America/Denver": "MT", "America/Phoenix": "AZ",
                "America/Los_Angeles": "PT", "America/Anchorage": "AKT", "Pacific/Honolulu": "HT" };
            return map[id] || id;
        }

        function intervalText(minutes) {
            if (!minutes || minutes < 1) { return "on an interval"; }
            if (minutes < 60) { return "every " + minutes + " min"; }
            return minutes % 60 === 0 ? "every " + (minutes / 60) + " h" : "every " + Math.floor(minutes / 60) + " h " + (minutes % 60) + " min";
        }

        function timeText(value) {
            if (!value) { return "a set time"; }
            var parts = value.split(":");
            var h = parseInt(parts[0], 10);
            return ((h % 12) || 12) + ":" + parts[1] + " " + (h < 12 ? "AM" : "PM");
        }

        function update() {
            var frequency = checked("Frequency") ? checked("Frequency").value : "Interval";
            var fields = form.querySelectorAll("[data-frequency-field]");
            for (var i = 0; i < fields.length; i++) {
                fields[i].hidden = fields[i].getAttribute("data-frequency-field") !== frequency;
            }

            var report = checked("ReportType");
            var zone = zoneShort(field("TimeZoneId").value);
            var from = field("ActiveFrom").value;
            var until = field("ActiveUntil").value;
            var when = frequency === "Daily"
                ? "daily at " + timeText(field("DailyTime").value) + " " + zone
                : intervalText(parseInt(field("IntervalMinutes").value, 10))
                    + (from && until ? ", " + timeText(from) + "–" + timeText(until) + " " + zone : "");
            summary.textContent = (report ? report.getAttribute("data-report-label") : "Report") + " · " + when;
            enabledFact.textContent = field("IsEnabled").checked ? "Runs automatically" : "Manual only";
            promoteFact.textContent = field("AutoPromote").checked ? "New data goes straight into reports" : "New data waits for review";
        }

        form.addEventListener("input", update);
        form.addEventListener("change", update);

        form.addEventListener("click", function (event) {
            var preset = event.target.closest("[data-preset-frequency]");
            if (!preset) { return; }
            var frequency = preset.getAttribute("data-preset-frequency");
            var radio = form.querySelector("[name='Input.Frequency'][value='" + frequency + "']");
            if (radio) { radio.checked = true; }
            if (preset.getAttribute("data-preset-minutes")) { field("IntervalMinutes").value = preset.getAttribute("data-preset-minutes"); }
            if (preset.getAttribute("data-preset-time")) { field("DailyTime").value = preset.getAttribute("data-preset-time"); }
            update();
        });

        update();
    }

    document.addEventListener("DOMContentLoaded", function () {
        var chip = document.querySelector("[data-auto-refresh]");
        if (chip) { setupAutoRefresh(chip); }

        var form = document.querySelector("[data-schedule-form]");
        if (form) { setupScheduleForm(form); }
    });
})();
