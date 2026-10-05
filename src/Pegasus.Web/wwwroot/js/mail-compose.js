(function () {
    "use strict";

    var host = document.querySelector("[data-mail-compose-host]");
    var triggers = Array.prototype.slice.call(document.querySelectorAll("[data-mail-compose-open]"));
    if (!host || triggers.length === 0 || typeof window.fetch !== "function") {
        return;
    }

    var opener = null;
    var releaseBackground = null;
    var loading = false;
    var pendingSend = null;

    function setExpanded(expanded) {
        triggers.forEach(function (trigger) {
            trigger.setAttribute("aria-expanded", expanded ? "true" : "false");
        });
    }

    function inertOutside(element) {
        var madeInert = [];
        for (var node = element; node && node !== document.body; node = node.parentElement) {
            Array.prototype.forEach.call(node.parentElement.children, function (sibling) {
                if (sibling !== node && sibling.tagName !== "SCRIPT" && !sibling.hasAttribute("inert")) {
                    sibling.setAttribute("inert", "");
                    madeInert.push(sibling);
                }
            });
        }
        return function () {
            madeInert.forEach(function (element) { element.removeAttribute("inert"); });
        };
    }

    function focusable() {
        return Array.prototype.filter.call(
            host.querySelectorAll("button, [href], input, select, textarea, [tabindex]:not([tabindex='-1'])"),
            function (element) {
                return !element.disabled && !element.hidden && element.type !== "hidden" && element.getClientRects().length > 0;
            });
    }

    function focusComposer() {
        var content = host.querySelector("#mail-compose-content");
        var initial = host.querySelector("[data-dialog-initial-focus]")
            || host.querySelector("input, select, textarea, button");
        if (initial || content) {
            (initial || content).focus();
        }
    }

    function closeComposer() {
        if (host.hidden) {
            return;
        }
        host.hidden = true;
        document.body.classList.remove("mail-compose-open");
        document.removeEventListener("keydown", trapFocus, true);
        if (releaseBackground) {
            releaseBackground();
            releaseBackground = null;
        }
        setExpanded(false);
        if (opener) {
            opener.focus();
        }
    }

    function openComposer(trigger) {
        opener = trigger || opener;
        if (!host.hidden) {
            focusComposer();
            return;
        }
        host.hidden = false;
        document.body.classList.add("mail-compose-open");
        releaseBackground = inertOutside(host);
        document.addEventListener("keydown", trapFocus, true);
        setExpanded(true);
        focusComposer();
    }

    function trapFocus(event) {
        if (event.key === "Escape") {
            // An open Case / PO option list takes the first Escape and closes
            // itself; the composer closes on the next one.
            if (host.querySelector("[data-case-picker-options]:not([hidden])")) {
                return;
            }
            event.preventDefault();
            closeComposer();
            return;
        }
        if (event.key !== "Tab") {
            return;
        }
        var items = focusable();
        if (items.length === 0) {
            return;
        }
        var first = items[0];
        var last = items[items.length - 1];
        if (event.shiftKey && document.activeElement === first) {
            event.preventDefault();
            last.focus();
        } else if (!event.shiftKey && document.activeElement === last) {
            event.preventDefault();
            first.focus();
        }
    }

    function contentFrom(markup) {
        var documentResponse = new DOMParser().parseFromString(markup, "text/html");
        return documentResponse.getElementById("mail-compose-content");
    }

    function render(markup) {
        var content = contentFrom(markup);
        if (!content) {
            throw new Error("The composer response did not contain its form.");
        }
        host.replaceChildren(content);
        bindContent();
        focusComposer();
    }

    function showFailure(sendMayBeUncertain) {
        var content = host.querySelector("#mail-compose-content");
        if (!content) {
            return;
        }
        var status = content.querySelector("[data-mail-compose-request-status]");
        if (!status) {
            status = document.createElement("p");
            status.setAttribute("data-mail-compose-request-status", "");
            status.setAttribute("role", "alert");
            status.tabIndex = -1;
            content.querySelector(".mail-compose-body").prepend(status);
        }
        status.className = "notice notice--warning";
        status.textContent = sendMayBeUncertain
            ? "No confirmation received. Recover this send before starting another."
            : "The composer could not be refreshed. Your current draft is still open.";

        if (sendMayBeUncertain && pendingSend) {
            var form = content.querySelector("[data-mail-compose-form]");
            if (form) {
                form.querySelectorAll("input:not([type='hidden']), select, textarea, button")
                    .forEach(function (control) { control.disabled = true; });
            }
            if (!content.querySelector("[data-mail-compose-recover-send]")) {
                var recover = document.createElement("button");
                recover.type = "button";
                recover.className = "btn";
                recover.setAttribute("data-mail-compose-recover-send", "");
                recover.textContent = "Recover send";
                recover.setAttribute("data-busy-label", form ? form.getAttribute("data-mail-compose-recovering") || "" : "");
                recover.addEventListener("click", function () {
                    request(pendingSend.action, pendingSend.options, true, recover);
                });
                status.after(recover);
            }
        }
        status.focus();
    }

    // The pressed button shows its busy word until the composer answers
    // (site.js pegasusBusy); a rendered answer replaces the button anyway.
    function request(url, options, sendMayBeUncertain, control, form) {
        loading = true;
        if (window.pegasusBusy && control) {
            window.pegasusBusy.start(control, form || null);
        }
        return window.fetch(url, options)
            .then(function (response) { return response.text(); })
            .then(function (markup) {
                render(markup);
                pendingSend = null;
            })
            .catch(function () { showFailure(sendMayBeUncertain); })
            .finally(function () {
                loading = false;
                if (window.pegasusBusy) {
                    window.pegasusBusy.end(form || control);
                }
            });
    }

    function bindContent() {
        if (typeof window.pegasusBindCasePickers === "function") {
            window.pegasusBindCasePickers(host);
        }
        host.querySelectorAll("[data-mail-compose-close]").forEach(function (close) {
            close.addEventListener("click", function (event) {
                event.preventDefault();
                closeComposer();
            });
        });

        host.querySelectorAll("[data-mail-compose-form]").forEach(function (form) {
            form.addEventListener("submit", function (event) {
                if (loading) {
                    event.preventDefault();
                    return;
                }
                event.preventDefault();
                var submitter = event.submitter;
                // Only a button that names its own action overrides the form's:
                // the formAction property of a button without the attribute is
                // the document URL, which would post the send to the host page.
                var action = submitter && submitter.hasAttribute("formaction")
                    ? submitter.formAction
                    : form.action;
                var formData = new FormData(form);
                // FormData(form) deliberately excludes the successful submit
                // button. Case selection carries its selected reference on
                // that button, so retain the ordinary browser form contract.
                if (submitter && submitter.name) {
                    formData.append(submitter.name, submitter.value);
                }
                var options = {
                    method: form.method || "POST",
                    body: formData,
                    credentials: "same-origin",
                    headers: { "X-Requested-With": "XMLHttpRequest" }
                };
                var isSend = /handler=Send/i.test(action);
                if (isSend) {
                    // A recovery uses this exact key and exact payload. It can
                    // retrieve an already-created operation, but cannot turn
                    // uncertainty into a separately addressed send.
                    pendingSend = { action: action, options: options };
                }
                request(action, options, isSend, submitter || null, form);
            });
        });
    }

    host.addEventListener("click", function (event) {
        if (event.target === host) {
            closeComposer();
        }
    });

    triggers.forEach(function (trigger) {
        trigger.addEventListener("click", function (event) {
            if (event.button !== 0 || event.metaKey || event.ctrlKey || event.shiftKey || event.altKey) {
                return;
            }
            event.preventDefault();
            opener = trigger;
            if (host.firstElementChild) {
                openComposer(trigger);
                return;
            }
            openComposer(trigger);
            request(trigger.getAttribute("data-mail-compose-url") || trigger.href, {
                credentials: "same-origin",
                headers: { "X-Requested-With": "XMLHttpRequest" }
            }, false, null);
        });
    });
}());
