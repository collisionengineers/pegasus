// --- Find a Case picker -------------------------------------------------------
// A [data-case-picker] wraps one search input and an empty
// [data-case-picker-options] popover. Typing fetches the page's option list
// (data-case-picker-url, a GET fragment) and shows it under the input;
// ArrowUp/Down move the selection, Enter chooses it, Escape closes the list.
// Each option is the page's own select submit button, so choosing one runs
// the form's ordinary submit path: the composer's fetch-swap on the Inbox,
// a plain post elsewhere. Enter never submits the form itself.
(function () {
    "use strict";

    var DEBOUNCE_MS = 200;
    var MIN_LENGTH = 2;

    function bind(picker) {
        if (picker.pegasusCasePicker) {
            return;
        }
        var input = picker.querySelector("input");
        var options = picker.querySelector("[data-case-picker-options]");
        var url = picker.getAttribute("data-case-picker-url");
        if (!input || !options || !url || typeof window.fetch !== "function") {
            return;
        }
        picker.pegasusCasePicker = true;

        var timer = null;
        var sequence = 0;
        var index = -1;

        function items() {
            return Array.prototype.slice.call(options.querySelectorAll("[role='option']"));
        }

        function close() {
            window.clearTimeout(timer);
            sequence++;
            options.hidden = true;
            options.replaceChildren();
            input.setAttribute("aria-expanded", "false");
            input.removeAttribute("aria-activedescendant");
            index = -1;
        }

        function select(at) {
            var list = items();
            if (list.length === 0) {
                return;
            }
            index = (at + list.length) % list.length;
            list.forEach(function (item, position) {
                item.setAttribute("aria-selected", position === index ? "true" : "false");
            });
            input.setAttribute("aria-activedescendant", list[index].id);
            list[index].scrollIntoView({ block: "nearest" });
        }

        function show(markup) {
            var fragment = new DOMParser().parseFromString(markup, "text/html");
            var list = fragment.querySelector("[data-case-picker-list]");
            if (!list || !list.querySelector("[role='option']")) {
                close();
                return;
            }
            options.replaceChildren(document.importNode(list, true));
            options.hidden = false;
            input.setAttribute("aria-expanded", "true");
            input.removeAttribute("aria-activedescendant");
            index = -1;
        }

        function search() {
            var term = input.value.trim();
            if (term.length < MIN_LENGTH) {
                close();
                return;
            }
            var current = ++sequence;
            window.fetch(url + (url.indexOf("?") >= 0 ? "&" : "?") + "q=" + encodeURIComponent(term), {
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            })
                .then(function (response) { return response.ok ? response.text() : ""; })
                .then(function (markup) {
                    if (current !== sequence) {
                        return;
                    }
                    if (markup) {
                        show(markup);
                    } else {
                        close();
                    }
                })
                .catch(function () {
                    if (current === sequence) {
                        close();
                    }
                });
        }

        input.addEventListener("input", function () {
            window.clearTimeout(timer);
            timer = window.setTimeout(search, DEBOUNCE_MS);
        });

        input.addEventListener("keydown", function (event) {
            if (event.key === "ArrowDown") {
                event.preventDefault();
                if (options.hidden) {
                    search();
                } else {
                    select(index + 1);
                }
            } else if (event.key === "ArrowUp") {
                event.preventDefault();
                if (!options.hidden) {
                    select(index - 1);
                }
            } else if (event.key === "Enter") {
                // Enter chooses the highlighted Case; it never submits the
                // form the input sits in.
                event.preventDefault();
                var list = items();
                if (!options.hidden && list[index]) {
                    list[index].click();
                }
            } else if (event.key === "Escape" && !options.hidden) {
                event.preventDefault();
                event.stopPropagation();
                close();
            }
        });

        picker.addEventListener("focusout", function (event) {
            if (!picker.contains(event.relatedTarget)) {
                close();
            }
        });

        document.addEventListener("click", function outside(event) {
            if (!document.contains(picker)) {
                // The composer re-rendered this picker away; let it go.
                document.removeEventListener("click", outside);
                return;
            }
            if (!options.hidden && !picker.contains(event.target)) {
                close();
            }
        });
    }

    function bindAll(root) {
        (root || document).querySelectorAll("[data-case-picker]").forEach(bind);
    }

    window.pegasusBindCasePickers = bindAll;

    if (document.readyState === "loading") {
        document.addEventListener("DOMContentLoaded", function () { bindAll(document); });
    } else {
        bindAll(document);
    }
})();
