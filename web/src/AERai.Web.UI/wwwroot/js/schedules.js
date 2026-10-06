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

    function setupAutoRefresh(chip) {
        var seconds = parseInt(chip.getAttribute("data-auto-refresh"), 10) || 10;
        var label = chip.querySelector("span:not(.status-light)");
        var button = chip.querySelector("[data-auto-refresh-stop]");
        var timer = null;

        function schedule() {
            timer = window.setTimeout(function tick() {
                if (busy()) {
                    timer = window.setTimeout(tick, 2000);
                    return;
                }
                window.location.reload();
            }, seconds * 1000);
        }

        function render(off) {
            chip.classList.toggle("is-off", off);
            label.textContent = off ? "A run is in progress — auto-refresh is off." : "A run is in progress — refreshing every " + seconds + " s.";
            button.textContent = off ? "Resume" : "Stop";
        }

        button.addEventListener("click", function () {
            var off = !storageGet();
            storageSet(off);
            window.clearTimeout(timer);
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
            var map = { "America/New_York": "ET", "America/Chicago": "CT", "America/Denver": "MT", "America/Phoenix": "AZ",
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
            var when = frequency === "Daily"
                ? "daily at " + timeText(field("DailyTime").value) + " " + zoneShort(field("TimeZoneId").value)
                : intervalText(parseInt(field("IntervalMinutes").value, 10));
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
