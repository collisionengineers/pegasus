// Progressive enhancement only: every behaviour here is a convenience on top of
// markup that already works without it.
//
// This file exists as a file rather than as inline <script> blocks because the
// deployed Content-Security-Policy is `default-src 'self'` with no nonce or
// hash allowance, so an inline script is silently discarded in Production. The
// enhancements below therefore ran only in Development until they moved here.
(function () {
    'use strict';

    // Bounded status pages ask to be reloaded while their state is still
    // moving; the delay is data on the element, so nothing here is inline.
    var autoRefresh = document.querySelector('[data-auto-refresh]');
    if (autoRefresh) {
        var delay = Number(autoRefresh.getAttribute('data-auto-refresh'));
        if (Number.isFinite(delay) && delay > 0) {
            // Exactly one pending timer at a time: a page can be sent to the
            // background and brought back any number of times, and every one
            // of those returns re-arms the same timer rather than adding
            // another reload to the pile.
            var timer = 0;
            var schedule = function () {
                window.clearTimeout(timer);
                timer = window.setTimeout(reload, delay);
            };
            // A page can still be moving while an operator has a form open
            // that a reload would wipe; any element opting in with
            // data-refresh-hold pauses the reload while it is open.
            var reload = function () {
                timer = 0;
                if (document.querySelector('[data-refresh-hold][open]')) {
                    schedule();
                    return;
                }
                window.location.reload();
            };
            // A hidden tab does not poll. Returning to it reloads immediately
            // instead of showing content that can be a full delay out of date.
            var trackVisibility = function () {
                if (document.hidden) {
                    window.clearTimeout(timer);
                    timer = 0;
                } else {
                    reload();
                }
            };
            document.addEventListener('visibilitychange', trackVisibility);
            if (!document.hidden) {
                schedule();
            }
        }
    }

    // Manual refresh feedback. The label change is the signal; the spin is
    // decoration on top of it, so the feedback still reads correctly under
    // reduced motion or with no CSS at all.
    document.querySelectorAll('[data-refresh-form]').forEach(function (form) {
        form.addEventListener('submit', function () {
            var region = form.closest('[data-refresh-region]') || form.parentElement;
            if (region) {
                region.classList.add('is-refreshing');
                region.setAttribute('aria-busy', 'true');
            }
            var label = form.querySelector('[data-refresh-label]');
            if (label) {
                label.textContent = 'Refreshing';
            }
            form.querySelectorAll('button').forEach(function (button) {
                button.disabled = true;
            });
        });
    });

    // Copy a support reference. Without script the value is still selectable
    // text, which is why the button is rendered hidden and revealed here rather
    // than shipped as a control that might do nothing.
    document.querySelectorAll('[data-copy-target]').forEach(function (button) {
        var source = document.getElementById(button.getAttribute('data-copy-target'));
        if (!source || !navigator.clipboard) {
            return;
        }

        button.hidden = false;
        button.addEventListener('click', function () {
            navigator.clipboard.writeText(source.textContent.trim()).then(function () {
                var original = button.textContent;
                button.textContent = 'Copied';
                window.setTimeout(function () { button.textContent = original; }, 2000);
            });
        });
    });

    // Reason dialogs: a focus trap so a modal that asks for a required reason
    // cannot be tabbed out of while it is open.
    document.querySelectorAll('dialog[data-focus-trap]').forEach(function (dialog) {
        dialog.addEventListener('keydown', function (event) {
            if (event.key !== 'Tab') {
                return;
            }

            var focusable = dialog.querySelectorAll(
                'a[href], button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), [tabindex]:not([tabindex="-1"])');
            if (focusable.length === 0) {
                return;
            }

            var first = focusable[0];
            var last = focusable[focusable.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        });
    });

    // Buttons that open their own dialog, so an action can carry its fields
    // without the page shipping a permanently open form for every action.
    document.querySelectorAll('[data-dialog-open]').forEach(function (trigger) {
        var dialog = document.getElementById(trigger.getAttribute('data-dialog-open'));
        if (!dialog || typeof dialog.showModal !== 'function') {
            return;
        }

        trigger.addEventListener('click', function (event) {
            event.preventDefault();
            dialog.showModal();
            var initial = dialog.querySelector('[data-dialog-initial-focus]')
                || dialog.querySelector('input, select, textarea, button');
            if (initial) {
                initial.focus();
            }
        });
    });

    document.querySelectorAll('[data-dialog-close]').forEach(function (button) {
        button.addEventListener('click', function (event) {
            event.preventDefault();
            var dialog = button.closest('dialog');
            if (dialog) {
                dialog.close();
            }
        });
    });

    // Global drop safety net. Without this, a file dropped anywhere off a
    // dropzone's own listeners below — the heading, a panel border, released
    // a beat early while still moving — is unhandled, and the browser's
    // default action navigates the whole tab to the dropped file, losing the
    // page. A dropzone's own listener runs first (event bubbling) and calls
    // preventDefault() itself, so this only ever catches a drop nothing more
    // specific already handled.
    document.addEventListener('dragover', function (event) {
        if (!event.defaultPrevented) {
            event.preventDefault();
        }
    });
    document.addEventListener('drop', function (event) {
        if (!event.defaultPrevented) {
            event.preventDefault();
        }
    });

    // Upload dropzones. The native file input is the control and keeps working
    // on its own; with script the effective drop target is the whole panel
    // the dashed area sits in, not the dashed area itself — a small rectangle
    // is too easy to miss on a real drag — and a real button opens the same
    // input. Nothing here is required: without script the input is simply
    // visible. A genuine OS drag's dataTransfer.files is empty until the drop
    // itself; only .types is readable during dragenter/dragover, so both the
    // affordance and the drop check key off "Files" in .types rather than
    // .files.
    document.querySelectorAll('[data-dropzone]').forEach(function (zone) {
        var input = zone.querySelector('input[type="file"]');
        var browse = zone.querySelector('[data-dropzone-browse]');
        // §1.10 draws the file list under the dashed area rather than inside
        // it, so the readout is looked up in the enclosing form when the zone
        // does not carry it itself.
        var form = zone.closest('form');
        var readout = zone.querySelector('[data-dropzone-file]')
            || (form && form.querySelector('[data-dropzone-file]'));
        if (!input || !browse || !readout) {
            return;
        }

        var formatSize = function (bytes) {
            return bytes >= 1048576
                ? (bytes / 1048576).toFixed(1) + ' MB'
                : Math.max(1, Math.round(bytes / 1024)) + ' KB';
        };

        var glyph = function (iconId) {
            var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
            svg.setAttribute('class', 'icon');
            svg.setAttribute('aria-hidden', 'true');
            var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
            use.setAttribute('href', iconId);
            svg.appendChild(use);
            return svg;
        };

        // One .file-row per file (§1.10): glyph, name and size, the drawn
        // progress bar, and the row's own state placeholder. The progress
        // element is indeterminate on purpose — one POST stores the whole
        // batch, so there is no per-file fraction to report and inventing one
        // would be a state this page cannot know. It stays hidden, and the
        // state placeholder stays empty, until a submission is actually under
        // way (see the upload-progress block below), so a page whose form has
        // no progress enhancement (Uploads/Request) renders as it always did.
        var describe = function () {
            var files = input.files ? Array.from(input.files) : [];
            zone.classList.toggle('has-file', files.length > 0);
            if (files.length === 0) {
                readout.hidden = true;
                readout.replaceChildren();
                browse.textContent = 'Choose files';
                return;
            }

            var rows = files.map(function (file) {
                var row = document.createElement('div');
                row.className = 'file-row';

                var mark = document.createElement('span');
                mark.append(glyph('#icon-file'));

                var detail = document.createElement('span');
                var name = document.createElement('strong');
                name.textContent = file.name;
                var size = document.createElement('small');
                size.textContent = formatSize(file.size);
                // The adjacent chip already names the state in words, so the
                // bar is the visual echo and is not announced twice.
                var progress = document.createElement('progress');
                progress.className = 'progress';
                progress.setAttribute('aria-hidden', 'true');
                progress.hidden = true;
                detail.append(name, size, progress);

                var status = document.createElement('span');
                status.setAttribute('data-file-row-status', '');

                row.append(mark, detail, status);
                return row;
            });
            readout.replaceChildren.apply(readout, rows);
            readout.hidden = false;
            browse.textContent = 'Choose different files';
        };

        zone.classList.add('is-enhanced');
        input.classList.add('sr-only');
        browse.hidden = false;
        browse.addEventListener('click', function () { input.click(); });
        input.addEventListener('change', describe);

        // dragenter/dragleave fire once per element the pointer crosses
        // inside the target, so a depth counter (not a toggle) decides when
        // the drag has genuinely left it, rather than flickering as it moves
        // across the panel's own children.
        var target = zone.closest('.panel') || zone;
        var depth = 0;
        var isFileDrag = function (event) {
            return Boolean(event.dataTransfer)
                && Array.from(event.dataTransfer.types || []).includes('Files');
        };

        target.addEventListener('dragenter', function (event) {
            if (!isFileDrag(event)) {
                return;
            }
            depth += 1;
            zone.classList.add('is-dragover');
        });
        target.addEventListener('dragover', function (event) {
            if (isFileDrag(event)) {
                event.preventDefault();
            }
        });
        target.addEventListener('dragleave', function () {
            depth = Math.max(0, depth - 1);
            if (depth === 0) {
                zone.classList.remove('is-dragover');
            }
        });
        target.addEventListener('dragend', function () {
            depth = 0;
            zone.classList.remove('is-dragover');
        });
        target.addEventListener('drop', function (event) {
            depth = 0;
            zone.classList.remove('is-dragover');
            if (!isFileDrag(event)) {
                return;
            }
            event.preventDefault();
            var dropped = event.dataTransfer ? event.dataTransfer.files : null;
            if (!dropped || dropped.length === 0) {
                return;
            }
            input.files = dropped;
            input.dispatchEvent(new Event('change', { bubbles: true }));
        });

        describe();

        // Clear is a native <button type="reset">: the browser empties the
        // input on its own, and the rows are re-rendered from the emptied
        // input once the reset has actually been applied.
        if (form) {
            form.addEventListener('reset', function () {
                window.setTimeout(describe, 0);
            });
        }

        // Per-file upload progress: opt-in via data-upload-progress on the
        // form, so this only changes behaviour on the one form that owns the
        // contract below (Upload.cshtml) and never touches the document
        // request form, which keeps its plain native submit.
        if (form
            && form.hasAttribute('data-upload-progress')
            && typeof fetch === 'function'
            && typeof FormData === 'function') {
            var setRowStatus = function (state, text) {
                readout.classList.toggle('is-refreshing', state === 'uploading');
                readout.querySelectorAll('.file-row').forEach(function (row) {
                    var progress = row.querySelector('progress.progress');
                    if (progress) {
                        progress.hidden = state !== 'uploading';
                    }
                    var mark = row.querySelector('svg.icon use');
                    if (mark) {
                        mark.setAttribute(
                            'href', state === 'stored' ? '#icon-check-circle' : '#icon-file');
                    }
                    var status = row.querySelector('[data-file-row-status]');
                    if (!status) {
                        return;
                    }
                    // The chip carries its own word and its own dot, so no
                    // state is conveyed by colour alone.
                    var chip = document.createElement('span');
                    chip.className = state === 'stored'
                        ? 'status status--green'
                        : 'status status--navy';
                    chip.textContent = text;
                    status.setAttribute('data-state', state);
                    status.replaceChildren(chip);
                });
            };

            form.addEventListener('submit', function (event) {
                var files = input.files ? Array.from(input.files) : [];
                if (files.length === 0) {
                    // Nothing chosen: let native "choose a file" validation
                    // say so, exactly as it always has.
                    return;
                }

                event.preventDefault();
                // Every row enters the same state together here: a single
                // POST stores the whole batch, so there is no per-file signal
                // to show yet, and showing one anyway would be a state this
                // page cannot actually know (see research.md).
                setRowStatus('uploading', 'Uploading');

                fetch(form.getAttribute('action') || window.location.href, {
                    method: 'POST',
                    body: new FormData(form)
                }).then(function (response) {
                    if (response.redirected) {
                        // The response proves the whole batch is durably
                        // stored — a real fact from the actual response, not
                        // a guess — so every row ticks together.
                        setRowStatus('stored', 'Stored');
                        window.location.assign(response.url);
                        return;
                    }

                    // Validation failed. Upload.cshtml.cs stores nothing
                    // until every file passes validation, so nothing was
                    // written on this path and a native re-submit is safe —
                    // it shows the exact, already-correct error page rather
                    // than this script guessing which row to blame.
                    form.submit();
                }).catch(function () {
                    form.submit();
                });
            });
        }
    });

    // Case search on the upload confirmation surface. The input is a plain
    // required text field that the server resolves as a typed case reference,
    // so the form keeps working without script; with script it becomes a
    // combobox — a debounced fetch of the page's own suggestion handler, a
    // listbox of matching cases, and a selection that fills the hidden case
    // value. The ARIA combobox wiring is added here rather than shipped in
    // markup, because without script there is no popup for it to describe.
    document.querySelectorAll('[data-case-search]').forEach(function (form) {
        var input = form.querySelector('[data-case-search-input]');
        var list = form.querySelector('[data-case-search-list]');
        var hidden = form.querySelector('[data-case-search-value]');
        var url = form.getAttribute('data-case-search-url');
        if (!input || !list || !hidden || !url || typeof fetch !== 'function') {
            return;
        }

        input.setAttribute('role', 'combobox');
        input.setAttribute('aria-expanded', 'false');
        input.setAttribute('aria-controls', list.id);
        input.setAttribute('aria-autocomplete', 'list');
        input.setAttribute('aria-haspopup', 'listbox');
        list.setAttribute('role', 'listbox');

        var options = [];
        var active = -1;
        var timer = null;
        var requestSequence = 0;
        var inFlight = null;

        var close = function () {
            list.hidden = true;
            list.replaceChildren();
            options = [];
            active = -1;
            input.setAttribute('aria-expanded', 'false');
            input.removeAttribute('aria-activedescendant');
        };

        var setActive = function (index) {
            active = index;
            list.querySelectorAll('[role="option"]').forEach(function (option, position) {
                option.classList.toggle('is-active', position === index);
                option.setAttribute('aria-selected', position === index ? 'true' : 'false');
            });
            if (index >= 0) {
                input.setAttribute('aria-activedescendant', list.id + '-option-' + index);
            } else {
                input.removeAttribute('aria-activedescendant');
            }
        };

        var choose = function (index) {
            var chosen = options[index];
            if (!chosen) {
                return;
            }
            hidden.value = chosen.caseId;
            input.value = chosen.reference;
            close();
        };

        var render = function (items) {
            options = items;
            var rows = items.map(function (item, index) {
                var row = document.createElement('li');
                row.id = list.id + '-option-' + index;
                row.setAttribute('role', 'option');
                row.setAttribute('aria-selected', 'false');
                row.textContent = [item.reference, item.registration, item.claimant, item.stage]
                    .filter(function (part) { return Boolean(part); })
                    .join(' · ');
                // mousedown, not click: click lands after the input's blur
                // would have closed the list.
                row.addEventListener('mousedown', function (event) {
                    event.preventDefault();
                    choose(index);
                });
                return row;
            });
            if (rows.length === 0) {
                var empty = document.createElement('li');
                empty.className = 'case-search-list__empty';
                empty.textContent = 'No matching cases found';
                rows = [empty];
            }
            list.replaceChildren.apply(list, rows);
            list.hidden = false;
            input.setAttribute('aria-expanded', 'true');
            setActive(-1);
        };

        input.addEventListener('input', function () {
            // Typing again always invalidates any earlier selection: the
            // submitted case is either the one just chosen or the typed
            // reference the server resolves — never a stale hidden value.
            hidden.value = '';
            var term = input.value.trim();
            if (timer) {
                window.clearTimeout(timer);
            }
            if (term.length < 2) {
                close();
                return;
            }
            timer = window.setTimeout(function () {
                var sequence = ++requestSequence;
                // Abort the superseded request rather than merely ignoring
                // its result: the server honours the cancellation, so the
                // abandoned search stops running instead of completing.
                if (inFlight) {
                    inFlight.abort();
                }
                inFlight = typeof AbortController === 'function' ? new AbortController() : null;
                fetch(url + (url.indexOf('?') >= 0 ? '&' : '?') + 'term=' + encodeURIComponent(term), {
                    headers: { Accept: 'application/json' },
                    signal: inFlight ? inFlight.signal : undefined
                }).then(function (response) {
                    return response.ok ? response.json() : [];
                }).then(function (items) {
                    if (sequence === requestSequence) {
                        render(items);
                    }
                }).catch(function () {
                    if (sequence === requestSequence) {
                        close();
                    }
                });
            }, 250);
        });

        input.addEventListener('keydown', function (event) {
            if (list.hidden) {
                return;
            }
            if (event.key === 'ArrowDown') {
                event.preventDefault();
                setActive(Math.min(active + 1, options.length - 1));
            } else if (event.key === 'ArrowUp') {
                event.preventDefault();
                setActive(Math.max(active - 1, 0));
            } else if (event.key === 'Enter' && active >= 0) {
                event.preventDefault();
                choose(active);
            } else if (event.key === 'Escape') {
                event.preventDefault();
                close();
            }
        });

        input.addEventListener('blur', function () {
            close();
        });
    });

    // Live character counters for reason fields whose limit is policy.
    document.querySelectorAll('[data-counter-for]').forEach(function (counter) {
        var field = document.getElementById(counter.getAttribute('data-counter-for'));
        if (!field) {
            return;
        }

        var limit = field.getAttribute('maxlength');
        var render = function () {
            counter.textContent = field.value.length + '/' + limit + ' characters';
        };

        field.addEventListener('input', render);
        render();
    });

}());


// CASE-007: finishing edit mode with unsaved changes asks first. Dirty means
// any input owned by a lease-carrying form changed since load; Save submits the
// form that changed, Discard releases the lease as posted.
(function () {
    var toggle = document.querySelector('[data-edit-toggle-off]');
    var dialog = document.getElementById('edit-finish-confirm');
    if (!toggle || !dialog) {
        return;
    }
    var dirtyForm = null;
    // Resolve the owning form from the control at event time. Native input
    // events follow the DOM tree, not a control's `form=` association, so a
    // listener on the form cannot see associated controls rendered elsewhere.
    document.addEventListener('input', function (event) {
        var control = event.target;
        var form = control.form || (control.closest ? control.closest('form') : null);
        if (!form
            || form === toggle
            || !form.querySelector('input[name="editLeaseToken"]')) {
            return;
        }
        dirtyForm = form;
    });
    // Root-scoped and idempotent so a lazily mounted Case section's
    // lease-carrying forms join the guard instead of escaping it.
    function bind(root) {
        root.querySelectorAll('form').forEach(function (form) {
            if (form === toggle
                || form.dataset.dirtyGuardBound === 'true'
                || !form.querySelector('input[name="editLeaseToken"]')) {
                return;
            }
            form.dataset.dirtyGuardBound = 'true';
            form.addEventListener('submit', function () { dirtyForm = null; });
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);

    // Ctrl+S submits the Case form that changed, not the document's first
    // [data-edit-save] form.
    window.pegasusDirtyEditForm = function () { return dirtyForm; };
    var allowed = false;
    toggle.addEventListener('submit', function (event) {
        if (allowed || !dirtyForm) {
            return;
        }
        event.preventDefault();
        dialog.hidden = false;
    });
    dialog.querySelector('[data-edit-finish-keep]').addEventListener('click', function () {
        dialog.hidden = true;
    });
    dialog.querySelector('[data-edit-finish-discard]').addEventListener('click', function () {
        dialog.hidden = true;
        allowed = true;
        toggle.requestSubmit();
    });
    dialog.querySelector('[data-edit-finish-save]').addEventListener('click', function () {
        dialog.hidden = true;
        if (dirtyForm) {
            dirtyForm.requestSubmit();
        }
    });
})();

// CASE-041: Inspect-at choices fill the ordinary form-associated address
// input. The input remains the no-script editing path.
(function () {
    function bind(root) {
        root.querySelectorAll('[data-inspection-address-choice]').forEach(function (select) {
            if (select.dataset.inspectionAddressBound === 'true') return;
            var input = document.querySelector('[data-inspection-address-input]');
            var mode = document.querySelector('input[name="inspectionMode"]');
            var field = document.querySelector('[data-inspection-address-field]');
            var providerDefault = document.querySelector('[data-inspection-provider-default]');
            if (!input || !mode || !field || !providerDefault) return;

            select.dataset.inspectionAddressBound = 'true';
            function showImageBasedAssessment(show) {
                field.hidden = show;
                providerDefault.hidden = !show;
            }
            function choose() {
                var option = select.options[select.selectedIndex];
                if (!option) return;
                if (option.value === 'ManualEntry') {
                    if (input.value.toLowerCase() === 'image based assessment') input.value = '';
                    mode.value = input.value.trim() ? 'PhysicalAddress' : '';
                    showImageBasedAssessment(false);
                    return;
                }
                input.value = option.dataset.address || '';
                var imageBased = option.value === 'ImageBasedAssessment';
                mode.value = imageBased ? 'ImageBasedAssessment' : 'PhysicalAddress';
                showImageBasedAssessment(imageBased);
            }
            select.addEventListener('change', choose);
            input.addEventListener('input', function () {
                mode.value = input.value.trim() ? 'PhysicalAddress' : '';
            });
            showImageBasedAssessment(select.value === 'ImageBasedAssessment');
        });
    }
    bind(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bind);
})();

// CASE-024: an open editor keeps its own lease alive, so a real editing session
// is never timed out mid-edit. The beat posts the rendered form, whose
// antiforgery token rides in the FormData exactly as the upload enhancement
// above does. With script the manual "Renew editing" button is redundant, so it
// is hidden here; without script it stays and is the only way to keep editing.
(function () {
    var form = document.querySelector('[data-edit-heartbeat]');
    if (!form) {
        return;
    }

    var renew = document.querySelector('[data-edit-renew]');
    if (renew) {
        renew.hidden = true;
    }

    var seconds = parseInt(form.getAttribute('data-heartbeat-seconds'), 10);
    if (!(seconds > 0)) {
        // The interval is a server value; without it, leave the Renew button
        // showing rather than beat on a guessed one.
        if (renew) {
            renew.hidden = false;
        }
        return;
    }

    // A live timer is what "still beating" means; visibilitychange checks it too,
    // because it calls beat() directly rather than through the interval.
    var timer = null;
    var stop = function () {
        window.clearInterval(timer);
        timer = null;
    };

    var beat = function () {
        if (timer === null) {
            return;
        }

        fetch(form.getAttribute('action') || window.location.href, {
            method: 'POST',
            body: new FormData(form)
        }).then(function (response) {
            // 204 is the only answer that means the lease is still ours. A
            // refusal is final - the lease was released, expired, or is now
            // someone else's - and the page the operator lands on next already
            // shows the case's real edit state, so nothing is said here.
            if (response.status !== 204) {
                stop();
            }
        }).catch(function () {
            // A single failed beat is not a lost lease: there are several more
            // before the lease could lapse, so keep beating.
        });
    };

    timer = window.setInterval(beat, seconds * 1000);
    // A hidden tab has its timers throttled, so the phase on return is
    // unknowable; one beat on becoming visible again settles it.
    document.addEventListener('visibilitychange', function () {
        if (!document.hidden) {
            beat();
        }
    });
})();

// INTK-022: a filter form marked data-auto-submit submits itself when any of
// its selects change; the noscript Apply button covers the rest.
(function () {
    document.querySelectorAll('form[data-auto-submit]').forEach(function (form) {
        form.addEventListener('change', function (event) {
            if (event.target instanceof HTMLSelectElement) {
                form.submit();
            }
        });
    });
})();

// UI-10: evidence-only mail preview. A subject remains an ordinary full-detail
// link; this enhancement selects its row on pointer/keyboard intent and reads
// the same authorized exact-message projection without moving focus or state.
// When that intent moves on, the pane restores the server-selected message
// instead of hiding: the pane is a fixture of the page, not a tooltip.
(function () {
    document.querySelectorAll('[data-mail-preview-workspace]').forEach(function (workspace) {
        var panel = workspace.querySelector('[data-mail-preview]');
        var status = workspace.querySelector('[data-mail-preview-status]');
        var facts = workspace.querySelector('[data-mail-preview-facts]');
        var rows = Array.from(workspace.querySelectorAll('[data-mail-preview-row]'));
        if (!panel || !status || !facts || rows.length === 0) {
            return;
        }

        var activeRow = null;
        var request = null;
        var cache = new Map();

        // The pane renders only beside a list that has a server-selected row
        // (the page model resolves one whenever it renders the pane at all),
        // and that row's trigger is the pane's fallback wherever intent goes.
        var selectedRow = rows.filter(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            return trigger && trigger.getAttribute('aria-current') === 'true';
        })[0] || null;
        if (!selectedRow) {
            return;
        }
        var actions = facts.querySelector('[data-mail-preview-actions]');
        activeRow = selectedRow;

        var field = function (name) {
            return facts.querySelector('[data-mail-preview-' + name + ']');
        };

        // The pane already shows the selected message; seeding the cache from
        // its rendered fields means restoring that message never waits on the
        // network and never leaves its actions hidden behind a failed fetch.
        cache.set(
            selectedRow
                .querySelector('[data-mail-preview-trigger]')
                .getAttribute('data-mail-preview-url'),
            {
                sender: field('sender').textContent,
                subject: field('subject').textContent,
                received: field('received').textContent,
                receivedAtUtc: field('received').getAttribute('datetime'),
                excerpt: field('excerpt').textContent,
                classification: field('classification').textContent,
                association: field('association').textContent,
                attachments: Array.prototype.map.call(
                    field('attachments').querySelectorAll('li'),
                    function (item) { return item.textContent; })
            });

        var render = function (data) {
            field('sender').textContent = data.sender;
            field('subject').textContent = data.subject;
            field('received').textContent = data.received;
            field('received').setAttribute('datetime', data.receivedAtUtc);
            field('excerpt').textContent = data.excerpt;
            field('classification').textContent = data.classification;
            field('association').textContent = data.association;

            var attachments = field('attachments');
            attachments.replaceChildren();
            (data.attachments.length === 0 ? ['No attachments'] : data.attachments)
                .forEach(function (name) {
                    var item = document.createElement('li');
                    item.textContent = name;
                    attachments.appendChild(item);
                });

            status.hidden = true;
            facts.hidden = false;
            panel.removeAttribute('aria-busy');
        };

        var select = function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            var url = trigger && trigger.getAttribute('data-mail-preview-url');
            if (!trigger || !url || activeRow === row) {
                return;
            }

            if (request) {
                request.abort();
            }
            rows.forEach(function (candidate) {
                var candidateTrigger = candidate.querySelector('[data-mail-preview-trigger]');
                if (candidateTrigger) {
                    candidateTrigger.setAttribute(
                        'aria-expanded',
                        candidate === row ? 'true' : 'false');
                }
            });
            activeRow = row;
            if (actions) {
                // The pane's actions belong to the selected message; while a
                // transient preview shows a different row, they are not its.
                actions.hidden = row !== selectedRow;
            }
            panel.hidden = false;
            status.hidden = false;
            status.textContent = 'Loading quick preview…';
            facts.hidden = true;
            panel.setAttribute('aria-busy', 'true');

            if (cache.has(url)) {
                render(cache.get(url));
                return;
            }

            request = new AbortController();
            var currentRequest = request;
            fetch(url, {
                headers: { 'Accept': 'application/json' },
                signal: currentRequest.signal
            }).then(function (response) {
                if (!response.ok) {
                    throw new Error('Preview unavailable');
                }
                return response.json();
            }).then(function (data) {
                cache.set(url, data);
                if (activeRow === row) {
                    render(data);
                }
            }).catch(function (error) {
                if (error.name === 'AbortError' || activeRow !== row) {
                    return;
                }
                facts.hidden = true;
                status.hidden = false;
                status.textContent = 'Quick preview unavailable. Open the message for full detail.';
                panel.removeAttribute('aria-busy');
            }).finally(function () {
                if (request === currentRequest) {
                    request = null;
                }
            });
        };

        // Leaving the rows ends the transient preview, not the pane: it falls
        // back to the server-selected message, whose actions must stay
        // reachable. select() no-ops when that row is already active, so
        // leaving the selected row itself leaves the pane untouched.
        var restoreSelection = function () {
            select(selectedRow);
        };

        rows.forEach(function (row) {
            var trigger = row.querySelector('[data-mail-preview-trigger]');
            row.addEventListener('pointerenter', function () { select(row); });
            row.addEventListener('pointerleave', function (event) {
                if (activeRow !== row || row.contains(document.activeElement)) {
                    return;
                }
                // Moving between rows is not leaving them: the next row's
                // pointerenter supersedes this event, and restoring between
                // every pair of rows would repaint the pane down the list.
                var entered = event.relatedTarget;
                if (!entered || !entered.closest('[data-mail-preview-row]')) {
                    restoreSelection();
                }
            });
            if (!trigger) {
                return;
            }
            trigger.addEventListener('focus', function () { select(row); });
            trigger.addEventListener('blur', function () {
                setTimeout(function () {
                    if (activeRow === row && !row.contains(document.activeElement)) {
                        restoreSelection();
                    }
                }, 0);
            });
        });
    });

    // Dialogs built as div backdrops ([data-dialog="<id>"]; [data-reason-dialog]
    // is the older alias and still works): open from any
    // [data-dialog-open="<id>"] control, close on [data-dialog-close] (or the
    // older [data-dialog-dismiss]), Escape, or a backdrop click, contain focus
    // while open, set `inert` on the application shell so nothing behind the
    // dialog is reachable, and return focus to the invoking control. This
    // lives here rather than beside the markup because the deployed
    // Content-Security-Policy discards inline scripts.
    // While a dialog is open everything outside it is inert. A dialog may be
    // rendered anywhere in the page (a Case page's reason dialogs live inside
    // the shell), so inert is set on the siblings of each of its ancestors up
    // to body - never on an ancestor - and exactly those elements are
    // released on close.
    function inertOutside(dialog) {
        var made = [];
        for (var node = dialog; node && node !== document.body; node = node.parentElement) {
            Array.prototype.forEach.call(node.parentElement.children, function (sibling) {
                if (sibling !== node && !sibling.hasAttribute('inert') && sibling.tagName !== 'SCRIPT') {
                    sibling.setAttribute('inert', '');
                    made.push(sibling);
                }
            });
        }
        return function release() {
            made.forEach(function (element) { element.removeAttribute('inert'); });
        };
    }

    var dialogOpeners = {};

    document.querySelectorAll('[data-dialog], [data-reason-dialog]').forEach(function (dialog) {
        if (dialog.dataset.dialogBound === 'true') {
            return;
        }
        dialog.dataset.dialogBound = 'true';

        var dialogId = dialog.getAttribute('data-dialog') || dialog.id;
        var release = null;
        var invoker = null;

        // A hidden input (the antiforgery token) matches the selector but
        // cannot take focus; focusing it leaves focus on the invoking control,
        // which is about to become inert and lose it to body.
        function focusable() {
            return Array.prototype.filter.call(
                dialog.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
                function (element) {
                    return !element.disabled && !element.hidden && element.type !== 'hidden' && element.getClientRects().length > 0;
                });
        }

        function open(source) {
            invoker = source;
            dialog.hidden = false;
            release = inertOutside(dialog);
            document.addEventListener('keydown', onKeydown, true);
            var items = focusable();
            var initial = dialog.querySelector('[data-dialog-initial-focus]')
                || items.find(function (element) { return element.matches('input, select, textarea'); })
                || items[0];
            if (initial) {
                initial.focus();
            }
            dialog.dispatchEvent(new CustomEvent('pegasus:dialog-open', { bubbles: true }));
        }

        function close() {
            dialog.hidden = true;
            if (release) {
                release();
                release = null;
            }
            document.removeEventListener('keydown', onKeydown, true);
            if (invoker) {
                invoker.focus();
            }
        }

        dialog.pegasusClose = close;
        dialog.pegasusOpen = open;

        function onKeydown(event) {
            if (event.key === 'Escape') {
                // Safe: closing abandons an unsent reason and changes nothing.
                event.preventDefault();
                close();
                return;
            }
            if (event.key !== 'Tab') {
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

        dialog.querySelectorAll('[data-dialog-dismiss], [data-dialog-close]').forEach(function (control) {
            control.addEventListener('click', close);
        });

        dialog.addEventListener('click', function (event) {
            if (event.target === dialog) {
                close();
            }
        });

        dialogOpeners[dialogId] = open;
    });

    // The openers are bound by root rather than once over the document, so a
    // lazily mounted Case section's controls open their dialog too.
    function bindDialogOpeners(root) {
        root.querySelectorAll('[data-dialog-open]').forEach(function (control) {
            if (control.dataset.dialogOpenBound === 'true') {
                return;
            }
            var open = dialogOpeners[control.getAttribute('data-dialog-open')];
            if (!open) {
                return;
            }
            control.dataset.dialogOpenBound = 'true';
            control.addEventListener('click', function () { open(control); });
        });
    }
    bindDialogOpeners(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindDialogOpeners);

    // Evidence viewer ([data-evidence-viewer], DOCS-011): preview an evidence
    // image or PDF over the page instead of navigating away from the case.
    // Modelled on the reason-dialog block above and sharing its contract --
    // initial focus, focus containment, Escape, focus return -- with paging
    // added. Every trigger is a real link, so with no script a click still
    // opens the file exactly as it did before.
    (function () {
        var viewer = document.querySelector('[data-evidence-viewer]');
        if (!viewer) {
            return;
        }

        var stage = viewer.querySelector('[data-evidence-stage]');
        var image = viewer.querySelector('[data-evidence-image]');
        var frame = viewer.querySelector('[data-evidence-document]');
        var caption = viewer.querySelector('[data-evidence-name]');
        var position = viewer.querySelector('[data-evidence-position]');
        var download = viewer.querySelector('[data-evidence-download]');
        var previous = viewer.querySelector('[data-evidence-previous]');
        var following = viewer.querySelector('[data-evidence-next]');

        var items = [];
        var index = 0;
        var invoker = null;

        // Only what a browser renders without executing it. Anything else is
        // left to the link, which saves it -- the server refuses to disposition
        // it inline either way, so the two agree.
        function previewKind(value) {
            var type = String(value || '').split(';')[0].trim().toLowerCase();
            // SVG is excluded to stay in step with the server's inline rule:
            // it is an image that executes script when navigated to, and these
            // triggers are real links. Anything not listed here is left to the
            // link, which saves it.
            if (type.indexOf('image/') === 0 && type !== 'image/svg+xml') {
                return 'image';
            }
            return type === 'application/pdf' ? 'document' : '';
        }

        function focusable() {
            return Array.prototype.filter.call(
                viewer.querySelectorAll('button, [href], input, select, textarea, [tabindex]:not([tabindex="-1"])'),
                function (element) { return !element.disabled && !element.hidden; });
        }

        function settle() {
            stage.removeAttribute('aria-busy');
            stage.classList.remove('is-loading');
        }

        function show(at) {
            var item = items[at];
            if (!item) {
                return;
            }
            index = at;
            var kind = previewKind(item.getAttribute('data-media-type'));
            var href = item.getAttribute('href');
            var fileName = item.getAttribute('data-file-name') || '';

            // The design contract calls for an explicit loading state. It is
            // shown, not said: aria-busy plus the same class-driven treatment
            // the manual refresh feedback above uses.
            stage.setAttribute('aria-busy', 'true');
            stage.classList.add('is-loading');

            image.hidden = kind !== 'image';
            frame.hidden = kind !== 'document';
            if (kind === 'image') {
                frame.removeAttribute('src');
                image.alt = fileName;
                image.src = href;
            } else {
                image.removeAttribute('src');
                frame.title = fileName;
                frame.src = href;
            }

            caption.textContent = fileName;
            position.textContent = (index + 1) + ' / ' + items.length;
            download.href = item.getAttribute('data-download-href') || href;
            download.setAttribute('download', fileName);
            previous.disabled = index === 0;
            following.disabled = index === items.length - 1;
        }

        var release = null;

        function open(trigger) {
            var set = trigger.closest('[data-evidence-set]');
            // Only previewable siblings join the paging set. A document table
            // carries every version, previewable or not; without this filter
            // Next could land on one and set a hidden iframe's src, which
            // downloads it unasked and leaves the loading state stuck on.
            items = (set
                ? Array.prototype.slice.call(set.querySelectorAll('[data-evidence-item]'))
                : [trigger]).filter(function (item) {
                    return previewKind(item.getAttribute('data-media-type')) !== '';
                });
            var start = items.indexOf(trigger);
            invoker = trigger;
            viewer.hidden = false;
            release = inertOutside(viewer);
            document.addEventListener('keydown', onKeydown, true);
            show(start < 0 ? 0 : start);
            var controls = focusable();
            if (controls.length > 0) {
                controls[0].focus();
            }
        }

        function close() {
            viewer.hidden = true;
            if (release) {
                release();
                release = null;
            }
            document.removeEventListener('keydown', onKeydown, true);
            // Drop the source so a large preview stops loading once it is off
            // screen; the next open sets it again.
            image.removeAttribute('src');
            frame.removeAttribute('src');
            stage.classList.remove('rot-90', 'rot-180', 'rot-270');
            settle();
            if (invoker) {
                invoker.focus();
            }
        }

        function step(offset) {
            var target = index + offset;
            if (target >= 0 && target < items.length) {
                show(target);
            }
        }

        function onKeydown(event) {
            if (event.key === 'Escape') {
                // Safe: closing a preview changes nothing.
                event.preventDefault();
                close();
                return;
            }
            if (event.key === 'ArrowLeft') {
                event.preventDefault();
                step(-1);
                return;
            }
            if (event.key === 'ArrowRight') {
                event.preventDefault();
                step(1);
                return;
            }
            if (event.key !== 'Tab') {
                return;
            }
            var controls = focusable();
            if (controls.length === 0) {
                return;
            }
            var first = controls[0];
            var last = controls[controls.length - 1];
            if (event.shiftKey && document.activeElement === first) {
                event.preventDefault();
                last.focus();
            } else if (!event.shiftKey && document.activeElement === last) {
                event.preventDefault();
                first.focus();
            }
        }

        image.addEventListener('load', settle);
        image.addEventListener('error', settle);
        frame.addEventListener('load', settle);
        frame.addEventListener('error', settle);
        previous.addEventListener('click', function () { step(-1); });
        following.addEventListener('click', function () { step(1); });
        viewer.querySelectorAll('[data-evidence-close]').forEach(function (control) {
            control.addEventListener('click', close);
        });
        viewer.addEventListener('click', function (event) {
            if (event.target === viewer) {
                close();
            }
        });

        // Root-scoped for the same reason as the dialog openers: a Files body
        // mounted as the reader reaches it must open its own viewer.
        function bindEvidenceItems(root) {
            root.querySelectorAll('[data-evidence-item]').forEach(function (trigger) {
                if (trigger.dataset.evidenceItemBound === 'true') {
                    return;
                }
                trigger.dataset.evidenceItemBound = 'true';
                trigger.addEventListener('click', function (event) {
                    if (!previewKind(trigger.getAttribute('data-media-type'))) {
                        return;
                    }
                    event.preventDefault();
                    open(trigger);
                });
            });
        }
        bindEvidenceItems(document);
        (window.pegasusMountBinders = window.pegasusMountBinders || []).push(bindEvidenceItems);
    })();

    // The Other classification name and reasoning fields exist only while an
    // Other option is selected; the select drives their visibility.
    document.querySelectorAll('[data-other-toggle]').forEach(function (select) {
        var scope = select.closest('[data-reason-dialog]') || document;
        function sync() {
            var isOther = select.value === 'other-received' || select.value === 'other-sent';
            scope.querySelectorAll('[data-other-field]').forEach(function (field) {
                field.hidden = !isOther;
            });
        }
        select.addEventListener('change', sync);
        sync();
    });
})();

// ===========================================================================
// PLAT-029 — Integrated Operations Workspace shell modules. Each section is
// self-contained and progressive: without script the markup it enhances is a
// working link, form or list. New sections go below; nothing above is
// reordered.
// ===========================================================================

// --- Toasts ----------------------------------------------------------------
// A transient status line in the fixed [data-toast-region]. A page-rendered
// confirmation ([data-confirmation]) is also announced this way so an action
// taken elsewhere is noticed without hunting for the notice.
(function () {
    'use strict';
    var region = document.querySelector('[data-toast-region]');
    if (!region) {
        return;
    }

    function toast(title, tone) {
        var element = document.createElement('div');
        element.className = 'toast' + (tone ? ' toast--' + tone : '');
        element.setAttribute('role', 'status');
        var strong = document.createElement('strong');
        strong.textContent = title;
        element.appendChild(strong);
        region.appendChild(element);
        window.setTimeout(function () { element.remove(); }, 4200);
    }

    window.pegasusToast = toast;

    var confirmation = document.querySelector('[data-confirmation]');
    if (confirmation) {
        toast(confirmation.textContent.trim());
    }
})();

// --- Command palette --------------------------------------------------------
// The command dialog is pre-rendered with one [data-route] result per route
// the operator may reach plus a "Search Cases for ..." fallback. Typing
// filters by text; ArrowUp/Down move the selection; Enter follows it. Ctrl K
// anywhere, or Enter in the utility bar's [data-command-input], opens it.
(function () {
    'use strict';
    var dialog = document.querySelector('[data-dialog="command-dialog"]');
    if (!dialog) {
        return;
    }
    var input = dialog.querySelector('[data-command-palette-input]');
    var results = Array.prototype.slice.call(dialog.querySelectorAll('.command-result'));
    var fallback = dialog.querySelector('[data-command-fallback]');
    var fallbackTerm = dialog.querySelector('[data-command-fallback-term]');
    var globalInput = document.querySelector('[data-command-input]');
    if (!input || results.length === 0) {
        return;
    }

    var index = 0;

    function visible() {
        return results.filter(function (result) { return !result.hidden; });
    }

    function select(at) {
        var items = visible();
        if (items.length === 0) {
            return;
        }
        index = (at + items.length) % items.length;
        items.forEach(function (item, position) {
            item.setAttribute('aria-selected', position === index ? 'true' : 'false');
        });
        items[index].scrollIntoView({ block: 'nearest' });
    }

    function filter() {
        var term = input.value.trim().toLowerCase();
        results.forEach(function (result) {
            if (result === fallback) {
                return;
            }
            result.hidden = term !== '' && result.textContent.toLowerCase().indexOf(term) < 0;
        });
        if (fallback) {
            fallback.hidden = term === '';
            if (fallbackTerm) {
                fallbackTerm.textContent = input.value.trim();
            }
        }
        select(0);
    }

    function searchUrl() {
        return '/Search?query=' + encodeURIComponent(input.value.trim());
    }

    function go(result) {
        window.location.assign(result === fallback ? searchUrl() : (result.getAttribute('data-route') || '/'));
    }

    function open(seed, opener) {
        // Open through the shell's own dialog binding so focus, inert and
        // Escape behave exactly as for every other dialog. The invoker the
        // dialog records for focus-return is the element that actually asked
        // for the palette (the search box on Enter, whatever had focus on
        // Ctrl+K) -- not the generic workspace "open another record" trigger,
        // which merely provides the dialog's open/close plumbing.
        var trigger = document.querySelector('[data-dialog-open="command-dialog"]');
        var source = opener || document.activeElement || trigger;
        if (dialog.pegasusOpen) {
            dialog.pegasusOpen(source);
        } else if (trigger) {
            trigger.click();
        }
        input.value = seed || '';
        filter();
        input.focus();
    }

    window.pegasusOpenCommandPalette = open;

    input.addEventListener('input', filter);
    input.addEventListener('keydown', function (event) {
        if (event.key === 'ArrowDown') {
            event.preventDefault();
            select(index + 1);
        } else if (event.key === 'ArrowUp') {
            event.preventDefault();
            select(index - 1);
        } else if (event.key === 'Enter') {
            event.preventDefault();
            var items = visible();
            if (items[index]) {
                go(items[index]);
            } else {
                window.location.assign(searchUrl());
            }
        }
    });
    results.forEach(function (result) {
        result.addEventListener('click', function () { go(result); });
    });
    if (globalInput) {
        globalInput.addEventListener('keydown', function (event) {
            if (event.key === 'Enter') {
                event.preventDefault();
                open(globalInput.value, globalInput);
            }
        });
    }
    filter();
})();

// --- Workspace tabs -----------------------------------------------------------
// One closable tab per record opened, remembered per browser in
// localStorage "pegasus.workspaceTabs" as [{href,label,at}], at most four,
// least recently used first out. A page announces itself as a record with
// main[data-workspace-record][data-workspace-href][data-workspace-label].
(function () {
    'use strict';
    var strip = document.querySelector('[data-workspace-tabs]');
    if (!strip) {
        return;
    }
    var KEY = 'pegasus.workspaceTabs';
    var MAX = 4;
    var opener = strip.querySelector('[data-workspace-open]');

    function read() {
        try {
            var stored = JSON.parse(window.localStorage.getItem(KEY) || '[]');
            return Array.isArray(stored) ? stored.filter(function (tab) {
                return tab && typeof tab.href === 'string' && typeof tab.label === 'string';
            }) : [];
        } catch (error) {
            return [];
        }
    }

    function write(tabs) {
        try {
            window.localStorage.setItem(KEY, JSON.stringify(tabs));
        } catch (error) {
            // Storage refused (private mode, quota): the strip still renders
            // this page's own record for the session.
        }
    }

    function icon(name) {
        var svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
        svg.setAttribute('class', 'icon');
        svg.setAttribute('aria-hidden', 'true');
        var use = document.createElementNS('http://www.w3.org/2000/svg', 'use');
        use.setAttribute('href', '#icon-' + name);
        svg.appendChild(use);
        return svg;
    }

    function render(tabs) {
        strip.querySelectorAll('.workspace-tab--record').forEach(function (tab) { tab.remove(); });
        var current = window.location.pathname.toLowerCase();
        tabs.forEach(function (tab) {
            var isActive = current === tab.href.split('?')[0].toLowerCase();
            var wrapper = document.createElement('div');
            wrapper.className = 'workspace-tab workspace-tab--record' + (isActive ? ' is-active' : '');
            var link = document.createElement('a');
            link.className = 'workspace-tab-link';
            link.href = tab.href;
            if (isActive) {
                link.setAttribute('aria-current', 'page');
            }
            link.appendChild(icon('folder-open'));
            var label = document.createElement('span');
            label.textContent = tab.label;
            link.appendChild(label);
            var close = document.createElement('button');
            close.type = 'button';
            close.className = 'workspace-tab-close';
            close.setAttribute('aria-label', 'Close ' + tab.label);
            close.appendChild(icon('x'));
            close.addEventListener('click', function () {
                var remaining = read().filter(function (candidate) { return candidate.href !== tab.href; });
                write(remaining);
                if (isActive) {
                    window.location.assign('/');
                    return;
                }
                render(remaining);
            });
            wrapper.append(link, close);
            strip.insertBefore(wrapper, opener);
        });
    }

    var tabs = read();
    var main = document.querySelector('main[data-workspace-record]');
    if (main) {
        var href = main.getAttribute('data-workspace-href');
        var label = main.getAttribute('data-workspace-label');
        if (href && label) {
            tabs = tabs.filter(function (tab) { return tab.href !== href; });
            tabs.push({ href: href, label: label, at: Date.now() });
            while (tabs.length > MAX) {
                tabs.shift();
            }
            write(tabs);
        }
    }
    render(tabs);
})();

// --- Case record: sticky geometry, section jump, lazy bodies, scroll-spy ----
// CASE-038 (D29): /Cases/{id} is one scrolling record. Without script every
// section that has a body is already on the page or is one ordinary fragment
// link away; script measures the sticky block, fetches the bodies below the
// fold as the reader approaches them, jumps to `?section=` and moves
// `aria-current` along the jump-nav as the page scrolls.
(function () {
    'use strict';
    var sticky = document.querySelector('[data-case-sticky]');
    var main = document.getElementById('case-main');
    if (!sticky || !main) {
        return;
    }

    var links = document.querySelectorAll('[data-section-link]');
    // The fragment is its own path, `/Cases/{id}/Section`, so a section body
    // is never mistaken for the record's own response.
    var fragmentPath = window.location.pathname.replace(/\/+$/, '') + '/Section';

    // The measured height is written on the record itself, not the document
    // element: every consumer (`.case-section`, `.case-context`) is inside it,
    // and one element carrying one custom property is the whole inline-style
    // footprint the record adds.
    var record = sticky.parentElement;

    function measure() {
        record.style.setProperty('--case-sticky-h', sticky.offsetHeight + 'px');
    }

    // The sticky block's own bottom edge is the reading line, so the offset is
    // never written down twice.
    function readingLine() {
        return sticky.getBoundingClientRect().bottom;
    }

    function bindMounted(root) {
        (window.pegasusMountBinders || []).forEach(function (bind) { bind(root); });
    }

    function mount(placeholder, then) {
        var key = placeholder.getAttribute('data-lazy');
        if (!key) {
            return;
        }
        // Every caller waiting on this section is answered by the one fetch,
        // so a jump made while its body is already in flight still scrolls to
        // it instead of being dropped.
        var waiting = placeholder.pegasusLazyWaiting || (placeholder.pegasusLazyWaiting = []);
        if (then) {
            waiting.push(then);
        }
        var attempted = Number(placeholder.dataset.lazyAttemptedAt || 0);
        if (placeholder.dataset.lazyState === 'loading') {
            return;
        }
        // A failed fetch leaves the placeholder in place saying so, and is
        // tried again rather than being dropped: no response is discarded
        // silently and no failure retries in a tight loop.
        if (placeholder.dataset.lazyState === 'failed' && Date.now() - attempted < 5000) {
            return;
        }
        placeholder.dataset.lazyState = 'loading';
        placeholder.dataset.lazyAttemptedAt = String(Date.now());
        fetch(fragmentPath + '?section=' + encodeURIComponent(key), {
            credentials: 'same-origin',
            headers: { 'Accept': 'text/html' }
        }).then(function (response) {
            if (!response.ok) {
                throw new Error('section ' + key + ': ' + response.status);
            }
            return response.text();
        }).then(function (html) {
            if (!placeholder.isConnected) {
                return;
            }
            var host = document.createElement('section');
            host.className = 'case-section';
            host.id = 'section-' + key;
            host.setAttribute('data-section', key);
            host.innerHTML = html;
            placeholder.replaceWith(host);
            bindMounted(host);
            measure();
            spy();
            var answered = waiting.splice(0, waiting.length);
            answered.forEach(function (callback) { callback(host); });
        }).catch(function (error) {
            placeholder.dataset.lazyState = 'failed';
            // The failure is not swallowed: the reader sees the section did
            // not arrive, and the record of why goes to the console.
            placeholder.textContent = 'This section could not be loaded.';
            waiting.length = 0;
            if (window.console && window.console.error) {
                window.console.error('Case section failed to load: ' + key, error);
            }
        });
    }

    function jumpTo(key, focus) {
        var target = document.getElementById('section-' + key);
        if (!target) {
            return;
        }
        if (target.hasAttribute('data-lazy')) {
            mount(target, function (host) { host.scrollIntoView({ block: 'start' }); });
            return;
        }
        target.scrollIntoView({ block: 'start' });
        if (focus) {
            var heading = target.querySelector('h2');
            if (heading) {
                heading.setAttribute('tabindex', '-1');
                heading.focus();
            }
        }
    }

    function spy() {
        var hosts = main.querySelectorAll('.case-section');
        if (!hosts.length) {
            return;
        }
        var line = readingLine() + 8;
        var current = hosts[0].getAttribute('data-section');
        for (var index = 0; index < hosts.length; index += 1) {
            if (hosts[index].getBoundingClientRect().top <= line) {
                current = hosts[index].getAttribute('data-section');
            }
        }
        if (window.innerHeight + window.scrollY >= document.documentElement.scrollHeight - 40) {
            current = hosts[hosts.length - 1].getAttribute('data-section');
        }
        links.forEach(function (link) {
            link.setAttribute(
                'aria-current',
                link.getAttribute('data-section-link') === current ? 'true' : 'false');
        });
    }

    function mountApproaching() {
        var limit = window.innerHeight * 2.5;
        main.querySelectorAll('[data-lazy]').forEach(function (placeholder) {
            if (placeholder.getBoundingClientRect().top < limit) {
                mount(placeholder);
            }
        });
    }

    links.forEach(function (link) {
        link.addEventListener('click', function (event) {
            event.preventDefault();
            jumpTo(link.getAttribute('data-section-link'), true);
        });
    });

    var ticking = false;
    function onScroll() {
        if (ticking) {
            return;
        }
        ticking = true;
        window.setTimeout(function () {
            ticking = false;
            mountApproaching();
            spy();
        }, 80);
    }
    window.addEventListener('scroll', onScroll, { passive: true });
    window.addEventListener('resize', function () { measure(); spy(); });

    measure();
    mountApproaching();
    spy();

    var addressed = new URLSearchParams(window.location.search).get('section');
    if (addressed) {
        jumpTo(addressed.trim().toLowerCase(), false);
    }
})();

// --- Keyboard shortcuts ----------------------------------------------------
// Ctrl K palette, Ctrl U upload, Ctrl N new case, Ctrl S submits the page's
// [data-edit-save] form when one exists, F5 submits [data-refresh-form] so a
// refresh re-queries rather than reloads. Inside an input only Ctrl K acts.
(function () {
    'use strict';
    document.addEventListener('keydown', function (event) {
        var target = event.target;
        var inField = target && target.closest && target.closest('input, select, textarea, [contenteditable="true"]');
        var control = event.ctrlKey || event.metaKey;
        var key = typeof event.key === 'string' ? event.key.toLowerCase() : '';

        if (control && key === 'k') {
            event.preventDefault();
            if (window.pegasusOpenCommandPalette) {
                window.pegasusOpenCommandPalette('', target);
            }
            return;
        }
        if (inField) {
            return;
        }
        if (control && key === 'u' && document.querySelector('[data-route="/Upload"]')) {
            event.preventDefault();
            window.location.assign('/Upload');
        } else if (control && key === 'n') {
            event.preventDefault();
            window.location.assign('/Cases/Create');
        } else if (control && key === 's') {
            var dirty = window.pegasusDirtyEditForm && window.pegasusDirtyEditForm();
            var save = dirty || document.querySelector('[data-edit-save]');
            if (save) {
                event.preventDefault();
                (save.tagName === 'FORM' ? save : save.closest('form')).requestSubmit();
            }
        } else if (event.key === 'F5' && !control) {
            var refresh = document.querySelector('[data-refresh-form]');
            if (refresh) {
                event.preventDefault();
                refresh.requestSubmit();
            }
        }
    });
})();

// --- Row lists: ArrowUp/Down roving focus -------------------------------------
(function () {
    'use strict';
    var ROW = '.row-button, .work-item, .scope-button, tr[data-action], tr[data-select-href]';
    document.querySelectorAll('[data-row-list]').forEach(function (list) {
        list.addEventListener('keydown', function (event) {
            if (event.key !== 'ArrowDown' && event.key !== 'ArrowUp') {
                return;
            }
            var rows = Array.prototype.filter.call(
                list.querySelectorAll(ROW),
                function (row) { return !row.hidden; });
            if (rows.length === 0) {
                return;
            }
            var at = rows.indexOf(document.activeElement.closest(ROW));
            event.preventDefault();
            var next = event.key === 'ArrowDown' ? Math.min(at + 1, rows.length - 1) : Math.max(at - 1, 0);
            var row = rows[next];
            if (row.tagName === 'TR' && !row.hasAttribute('tabindex')) {
                row.setAttribute('tabindex', '-1');
            }
            row.focus();
        });
    });
})();

// --- Sort toggles ------------------------------------------------------------
// The server sorts; the toggle is a link or a form button whose arrow glyph
// swaps on activation so the direction reads before the page returns.
(function () {
    'use strict';
    document.querySelectorAll('[data-sort-toggle]').forEach(function (toggle) {
        toggle.addEventListener('click', function () {
            var label = toggle.querySelector('[data-sort-arrow]') || toggle;
            label.textContent = label.textContent.indexOf('↓') >= 0
                ? label.textContent.replace('↓', '↑')
                : label.textContent.replace('↑', '↓');
        });
    });
})();

// --- Row-selection preview -----------------------------------------------------
// A row carrying [data-select-href] is a link to its full record; with script
// a click, Enter or focus swaps the sibling <template> content into the
// page's [data-preview-target] and rewrites the address, so the operator
// reads the record beside the list. Without script the link navigates.
(function () {
    'use strict';
    var target = document.querySelector('[data-preview-target]');
    if (!target) {
        return;
    }
    var rows = Array.prototype.slice.call(document.querySelectorAll('[data-select-href]'));
    if (rows.length === 0) {
        return;
    }

    function select(row, moveFocus) {
        var template = row.querySelector('template')
            || document.getElementById(row.getAttribute('data-preview-template') || '');
        if (!template || !('content' in template)) {
            return;
        }
        rows.forEach(function (candidate) {
            candidate.setAttribute('aria-selected', candidate === row ? 'true' : 'false');
        });
        target.replaceChildren(template.content.cloneNode(true));
        var url = new URL(window.location.href);
        url.searchParams.set('selected', row.getAttribute('data-select-id') || row.getAttribute('data-select-href'));
        window.history.replaceState(null, '', url.toString());
        if (moveFocus) {
            row.focus();
        }
    }

    rows.forEach(function (row) {
        if (!row.hasAttribute('tabindex')) {
            row.setAttribute('tabindex', '0');
        }
        row.addEventListener('click', function (event) {
            if (event.target.closest('a, button')) {
                return;
            }
            event.preventDefault();
            select(row, false);
        });
        row.addEventListener('keydown', function (event) {
            if (event.key === 'Enter' && !event.target.closest('a, button')) {
                event.preventDefault();
                select(row, true);
            }
        });
        row.addEventListener('focus', function () { select(row, false); });
    });

    var initial = rows.find(function (row) { return row.getAttribute('aria-selected') === 'true'; });
    if (initial) {
        select(initial, false);
    }
})();

// --- Estimate tabs: roving tabindex ---------------------------------------------
(function () {
    'use strict';
    document.querySelectorAll('[role="tablist"]').forEach(function (list) {
        var tabs = Array.prototype.slice.call(list.querySelectorAll('[role="tab"]'));
        if (tabs.length === 0) {
            return;
        }
        function sync(active) {
            tabs.forEach(function (tab) {
                tab.setAttribute('tabindex', tab === active ? '0' : '-1');
            });
        }
        sync(tabs.find(function (tab) { return tab.getAttribute('aria-selected') === 'true'; }) || tabs[0]);
        list.addEventListener('keydown', function (event) {
            var at = tabs.indexOf(document.activeElement);
            if (at < 0) {
                return;
            }
            var next = null;
            if (event.key === 'ArrowRight') { next = tabs[(at + 1) % tabs.length]; }
            else if (event.key === 'ArrowLeft') { next = tabs[(at - 1 + tabs.length) % tabs.length]; }
            else if (event.key === 'Home') { next = tabs[0]; }
            else if (event.key === 'End') { next = tabs[tabs.length - 1]; }
            if (next) {
                event.preventDefault();
                sync(next);
                next.focus();
            }
        });
    });
})();

// --- Range output ----------------------------------------------------------------
// input[type=range][data-range-output="<output id>"] writes its percentage
// and, when [data-range-base] carries an amount and
// [data-range-amount-output] names a second output, that amount x percentage.
(function () {
    'use strict';
    document.querySelectorAll('input[type="range"][data-range-output]').forEach(function (range) {
        var output = document.getElementById(range.getAttribute('data-range-output'));
        if (!output) {
            return;
        }
        var amountOutput = range.hasAttribute('data-range-amount-output')
            ? document.getElementById(range.getAttribute('data-range-amount-output'))
            : null;
        function render() {
            var percent = Number(range.value);
            output.textContent = percent + '%';
            var base = Number(range.getAttribute('data-range-base'));
            if (amountOutput && Number.isFinite(base)) {
                amountOutput.textContent = (base * percent / 100).toLocaleString('en-GB', {
                    style: 'currency', currency: 'GBP', maximumFractionDigits: 0
                });
            }
        }
        range.addEventListener('input', render);
        render();
    });
})();

// --- Assessment evidence rail collapse ----------------------------------------------
(function () {
    'use strict';
    document.querySelectorAll('[data-rail-toggle]').forEach(function (toggle) {
        var layout = toggle.closest('.assessment-v3');
        if (!layout) {
            return;
        }
        toggle.addEventListener('click', function () {
            var collapsed = layout.classList.toggle('assessment-v3-evidence-collapsed');
            toggle.setAttribute('aria-expanded', collapsed ? 'false' : 'true');
        });
    });
})();

// --- Image rotate ----------------------------------------------------------------
// [data-rotate] cycles the .rot-* classes on the nearest [data-rotate-target]
// (the viewer stage, a gallery item), a pure view transform the CSP-safe
// classes carry.
(function () {
    'use strict';
    var steps = ['', 'rot-90', 'rot-180', 'rot-270'];
    document.querySelectorAll('[data-rotate]').forEach(function (button) {
        button.addEventListener('click', function () {
            var scope = button.closest('.dialog, .panel, .gallery-item') || document;
            var target = scope.querySelector('[data-rotate-target]');
            if (!target) {
                return;
            }
            var current = steps.findIndex(function (step) { return step && target.classList.contains(step); });
            steps.forEach(function (step) { if (step) { target.classList.remove(step); } });
            var next = steps[(current + 1) % steps.length];
            if (next) {
                target.classList.add(next);
            }
        });
    });
})();
