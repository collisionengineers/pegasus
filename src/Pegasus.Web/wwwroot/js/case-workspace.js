// Case workspace behaviour. Image preparation and damage are staged in the
// one Case form; no control in this file creates a separate mutation path.
(function () {
    'use strict';

    function number(value, fallback) {
        var parsed = Number(value);
        return Number.isFinite(parsed) ? parsed : fallback;
    }
    function clamp(value) { return Math.max(0, Math.min(1, value)); }
    function round(value) { return Math.round(value * 10000000) / 10000000; }

    function bindPreparations(root) {
        var form = document.getElementById('case-edit-form');
        if (!form) { return; }
        var cards = Array.prototype.slice.call((root || document).querySelectorAll('[data-preparation-card]'));
        if (!cards.length) { return; }
        var staged = form.__pegasusPreparationStaged || (form.__pegasusPreparationStaged = {});
        function cardState(card) {
            var id = card.getAttribute('data-preparation-occurrence');
            if (!staged[id]) {
                staged[id] = {
                    id: id,
                    version: number(card.getAttribute('data-preparation-version'), 0),
                    role: card.getAttribute('data-preparation-role'),
                    order: number(card.getAttribute('data-preparation-order'), null),
                    rotation: number(card.getAttribute('data-preparation-rotation'), 0),
                    left: number(card.getAttribute('data-preparation-crop-left'), 0),
                    top: number(card.getAttribute('data-preparation-crop-top'), 0),
                    width: number(card.getAttribute('data-preparation-crop-width'), 1),
                    height: number(card.getAttribute('data-preparation-crop-height'), 1),
                    changed: false
                };
            }
            return staged[id];
        }
        function updateCard(card) {
            var value = cardState(card);
            var role = card.querySelector('[data-preparation-role-select]');
            var order = card.querySelector('[data-preparation-order]');
            var image = card.querySelector('[data-preparation-preview-image]');
            var roleLabel = card.querySelector('[data-preparation-role-label]');
            var rotationLabel = card.querySelector('[data-preparation-rotation-label]');
            var cropLabel = card.querySelector('[data-preparation-crop-label]');
            if (role) { role.value = value.role; }
            if (order) { order.value = value.order === null ? '' : value.order; order.disabled = value.role !== 'Supporting'; }
            if (roleLabel) { roleLabel.textContent = value.role.replace(/([A-Z])/g, ' $1').trim(); }
            if (rotationLabel) { rotationLabel.textContent = value.rotation === 0 ? 'None' : value.rotation + '°'; }
            if (cropLabel) { cropLabel.textContent = value.width === 1 && value.height === 1 && value.left === 0 && value.top === 0 ? 'Full frame' : 'Custom crop'; }
            if (image) { image.style.transform = 'rotate(' + value.rotation + 'deg)'; }
        }
        function sync(id) {
            Array.prototype.slice.call(document.querySelectorAll('[data-preparation-card]'))
                .filter(function (card) { return card.getAttribute('data-preparation-occurrence') === id; })
                .forEach(updateCard);
        }
        function write() {
            Array.prototype.slice.call(form.querySelectorAll('[data-preparation-hidden]')).forEach(function (element) { element.remove(); });
            var index = 0;
            Object.keys(staged).forEach(function (id) {
                var value = staged[id];
                if (!value.changed) { return; }
                [
                    ['OccurrenceId', value.id], ['ExpectedPreparationVersion', value.version], ['Role', value.role],
                    ['Order', value.role === 'Supporting' && value.order !== null ? value.order : ''], ['Rotation', value.rotation],
                    ['CropLeft', round(value.left)], ['CropTop', round(value.top)], ['CropWidth', round(value.width)], ['CropHeight', round(value.height)]
                ].forEach(function (field) {
                    var input = document.createElement('input');
                    input.type = 'hidden'; input.name = 'preparationEdits[' + index + '].' + field[0]; input.value = field[1];
                    input.setAttribute('data-preparation-hidden', ''); form.appendChild(input);
                });
                index += 1;
            });
        }
        cards.forEach(function (card) {
            if (card.dataset.preparationBound) { updateCard(card); return; }
            card.dataset.preparationBound = 'true';
            var value = cardState(card); updateCard(card);
            var role = card.querySelector('[data-preparation-role-select]');
            var order = card.querySelector('[data-preparation-order]');
            if (role) { role.addEventListener('change', function () { value.role = role.value; if (value.role !== 'Supporting') { value.order = null; } value.changed = true; sync(value.id); write(); }); }
            if (order) { order.addEventListener('change', function () { value.order = Math.max(1, Math.floor(number(order.value, 1))); value.changed = true; sync(value.id); write(); }); }
            card.querySelectorAll('[data-preparation-rotate]').forEach(function (button) { button.addEventListener('click', function () { value.rotation = (value.rotation + number(button.getAttribute('data-preparation-rotate'), 0) + 360) % 360; value.changed = true; sync(value.id); write(); }); });
            var reset = card.querySelector('[data-preparation-reset]');
            if (reset) { reset.addEventListener('click', function () { value.role = 'NotUsed'; value.order = null; value.rotation = 0; value.left = 0; value.top = 0; value.width = 1; value.height = 1; value.changed = true; sync(value.id); write(); }); }
            var crop = card.querySelector('[data-preparation-crop]');
            if (crop) { crop.addEventListener('click', function () { window.pegasusOpenCaseCrop(value.id); }); }
        });

        var dialog = document.querySelector('[data-case-crop-dialog]');
        if (!dialog || dialog.dataset.cropBound) { return; }
        dialog.dataset.cropBound = 'true';
        var stage = dialog.querySelector('[data-case-crop-stage]');
        var stageCanvas = dialog.querySelector('[data-case-crop-stage-canvas]');
        var previewCanvas = dialog.querySelector('[data-case-crop-preview]');
        var selection = dialog.querySelector('[data-case-crop-selection]');
        var aspect = dialog.querySelector('[data-case-crop-aspect]');
        var source = new Image();
        var rotatedSource = document.createElement('canvas');
        var current, entry, drag, imageBox;

        function rotation(value) { return ((value % 360) + 360) % 360; }
        function sourceSize(value) {
            var quarterTurn = rotation(value.rotation) % 180 !== 0;
            return {
                width: quarterTurn ? source.naturalHeight : source.naturalWidth,
                height: quarterTurn ? source.naturalWidth : source.naturalHeight
            };
        }
        function drawRotatedSource() {
            if (!source.naturalWidth || !current) { return; }
            var size = sourceSize(current);
            rotatedSource.width = size.width;
            rotatedSource.height = size.height;
            var context = rotatedSource.getContext('2d');
            context.save();
            context.translate(size.width / 2, size.height / 2);
            context.rotate(rotation(current.rotation) * Math.PI / 180);
            context.drawImage(source, -source.naturalWidth / 2, -source.naturalHeight / 2);
            context.restore();
        }
        function resizeCanvas(canvas, rect) {
            var scale = window.devicePixelRatio || 1;
            canvas.width = Math.max(1, Math.round(rect.width * scale));
            canvas.height = Math.max(1, Math.round(rect.height * scale));
            return { context: canvas.getContext('2d'), scale: scale };
        }
        function render() {
            if (!current || !source.naturalWidth) { return; }
            drawRotatedSource();
            var stageRect = stage.getBoundingClientRect();
            var stageDrawing = resizeCanvas(stageCanvas, stageRect);
            var sourceAspect = rotatedSource.width / rotatedSource.height;
            var stageAspect = stageRect.width / stageRect.height;
            var width = sourceAspect > stageAspect ? stageRect.width : stageRect.height * sourceAspect;
            var height = sourceAspect > stageAspect ? stageRect.width / sourceAspect : stageRect.height;
            imageBox = { left: (stageRect.width - width) / 2, top: (stageRect.height - height) / 2, width: width, height: height };
            stageDrawing.context.clearRect(0, 0, stageCanvas.width, stageCanvas.height);
            stageDrawing.context.drawImage(rotatedSource, imageBox.left * stageDrawing.scale, imageBox.top * stageDrawing.scale, imageBox.width * stageDrawing.scale, imageBox.height * stageDrawing.scale);
            selection.style.left = imageBox.left + current.left * imageBox.width + 'px';
            selection.style.top = imageBox.top + current.top * imageBox.height + 'px';
            selection.style.width = current.width * imageBox.width + 'px';
            selection.style.height = current.height * imageBox.height + 'px';

            var previewRect = previewCanvas.getBoundingClientRect();
            var previewDrawing = resizeCanvas(previewCanvas, previewRect);
            previewDrawing.context.clearRect(0, 0, previewCanvas.width, previewCanvas.height);
            var cropWidth = current.width * rotatedSource.width;
            var cropHeight = current.height * rotatedSource.height;
            var displayScale = Math.min(previewCanvas.width / cropWidth, previewCanvas.height / cropHeight);
            var displayWidth = cropWidth * displayScale;
            var displayHeight = cropHeight * displayScale;
            previewDrawing.context.drawImage(rotatedSource,
                current.left * rotatedSource.width, current.top * rotatedSource.height, cropWidth, cropHeight,
                (previewCanvas.width - displayWidth) / 2, (previewCanvas.height - displayHeight) / 2, displayWidth, displayHeight);
        }
        function selectedAspect() { return aspect.value === 'free' ? null : number(aspect.value, 1); }
        function lockAspect(value, anchor) {
            var desired = selectedAspect();
            if (!desired || !source.naturalWidth) { return; }
            var size = sourceSize(value);
            var pixelAspect = (value.width * size.width) / (value.height * size.height);
            if (Math.abs(pixelAspect - desired) < .000001) { return; }
            if (anchor === 'height') {
                value.width = value.height * desired * size.height / size.width;
                if (value.width > 1 - value.left) {
                    value.width = 1 - value.left;
                    value.height = value.width * size.width / (desired * size.height);
                }
            } else {
                value.height = value.width * size.width / (desired * size.height);
                if (value.height > 1 - value.top) {
                    value.height = 1 - value.top;
                    value.width = value.height * desired * size.height / size.width;
                }
            }
            value.width = Math.max(.02, value.width);
            value.height = Math.max(.02, value.height);
        }
        function close(save) {
            if (save && current) { current.changed = true; sync(current.id); write(); }
            else if (entry && current) { Object.assign(current, entry); sync(current.id); }
            dialog.hidden = true; current = null; entry = null; drag = null;
        }
        function openCrop(card) {
            current = cardState(card); entry = Object.assign({}, current);
            source.onload = render;
            source.src = card.getAttribute('data-preparation-preview');
            dialog.hidden = false;
            if (source.complete) { render(); }
        }
        window.pegasusOpenCaseCrop = function (occurrenceId) {
            var card = document.querySelector('[data-preparation-card][data-preparation-occurrence="' + occurrenceId + '"]');
            if (card) { openCrop(card); }
        };
        dialog.querySelectorAll('[data-case-crop-cancel]').forEach(function (button) { button.addEventListener('click', function () { close(false); }); });
        dialog.querySelector('[data-case-crop-save]').addEventListener('click', function () { close(true); });
        dialog.querySelector('[data-case-crop-full]').addEventListener('click', function () { if (current) { current.left = 0; current.top = 0; current.width = 1; current.height = 1; render(); } });
        dialog.querySelector('[data-case-crop-reset]').addEventListener('click', function () { if (current) { Object.assign(current, entry); render(); } });
        dialog.querySelectorAll('[data-case-crop-rotate]').forEach(function (button) { button.addEventListener('click', function () { if (current) { current.rotation = rotation(current.rotation + number(button.getAttribute('data-case-crop-rotate'), 0)); lockAspect(current, 'width'); render(); } }); });
        aspect.addEventListener('change', function () { if (current) { lockAspect(current, 'width'); current.left = Math.min(current.left, 1 - current.width); current.top = Math.min(current.top, 1 - current.height); render(); } });
        selection.addEventListener('pointerdown', function (event) { if (!current || !imageBox) { return; } drag = { x: event.clientX, y: event.clientY, value: Object.assign({}, current), handle: event.target.getAttribute('data-crop-handle') }; selection.setPointerCapture(event.pointerId); event.preventDefault(); });
        selection.addEventListener('pointermove', function (event) {
            if (!drag || !current || !imageBox) { return; }
            var dx = (event.clientX - drag.x) / imageBox.width;
            var dy = (event.clientY - drag.y) / imageBox.height;
            var value = drag.value, handle = drag.handle;
            if (!handle) { current.left = Math.max(0, Math.min(1 - value.width, value.left + dx)); current.top = Math.max(0, Math.min(1 - value.height, value.top + dy)); }
            else {
                current.left = value.left; current.top = value.top; current.width = value.width; current.height = value.height;
                if (handle.indexOf('n') >= 0) { current.top = Math.max(0, Math.min(value.top + value.height - .02, value.top + dy)); current.height = value.top + value.height - current.top; }
                if (handle.indexOf('e') >= 0) { current.width = Math.max(.02, Math.min(1 - current.left, value.width + dx)); }
                if (handle.indexOf('w') >= 0) { current.left = Math.max(0, Math.min(value.left + value.width - .02, value.left + dx)); current.width = value.left + value.width - current.left; }
                if (handle.indexOf('s') >= 0) { current.height = Math.max(.02, Math.min(1 - current.top, value.height + dy)); }
                lockAspect(current, handle.indexOf('n') >= 0 || handle.indexOf('s') >= 0 ? 'height' : 'width');
                current.left = Math.min(current.left, 1 - current.width); current.top = Math.min(current.top, 1 - current.height);
            }
            render();
        });
        selection.addEventListener('pointerup', function () { drag = null; });
        new ResizeObserver(function () { if (current && !dialog.hidden) { render(); } }).observe(stage);
    }

    function bindDamage(root) {
        Array.prototype.slice.call((root || document).querySelectorAll('[data-damage-editor]')).forEach(function (editor) {
            if (editor.dataset.damageBound) { return; } editor.dataset.damageBound = 'true';
            var input = editor.querySelector('[data-damage-input]');
            var editable = editor.getAttribute('data-damage-editable') === 'true';
            var impacts; try { impacts = JSON.parse(input ? input.value : editor.getAttribute('data-damage-impacts') || '[]'); } catch (_) { impacts = []; }
            var selected, fields = editor.querySelector('[data-damage-fields]'), empty = editor.querySelector('[data-damage-empty]'), list = editor.querySelector('[data-damage-impact-list]');
            var labels = {
                bonnet: 'Bonnet', windscreen: 'Windscreen', roof: 'Roof', rear_screen: 'Rear screen', tailgate: 'Boot / tailgate',
                front_left_corner: 'Front N/S corner', front_centre: 'Front centre', front_right_corner: 'Front O/S corner',
                left_front_wing: 'N/S front wing', left_front_door: 'N/S front door', left_rear_door: 'N/S rear door', left_quarter: 'N/S rear quarter',
                right_front_wing: 'O/S front wing', right_front_door: 'O/S front door', right_rear_door: 'O/S rear door', right_quarter: 'O/S rear quarter',
                rear_left_corner: 'Rear N/S corner', rear_centre: 'Rear centre', rear_right_corner: 'Rear O/S corner',
                wheel_left_front: 'Left front wheel', wheel_right_front: 'Right front wheel', wheel_left_rear: 'Left rear wheel', wheel_right_rear: 'Right rear wheel',
                underside: 'Underside', interior: 'Interior', mechanical: 'Mechanical'
            };
            function impact(zone) { return impacts.filter(function (item) { return item.zone === zone; })[0]; }
            function persist() { if (input) { input.value = JSON.stringify(impacts); } }
            function render() { editor.querySelectorAll('[data-damage-zone]').forEach(function (element) { element.classList.toggle('is-damaged', !!impact(element.getAttribute('data-damage-zone'))); element.classList.toggle('is-selected', element.getAttribute('data-damage-zone') === selected); }); list.innerHTML = ''; impacts.forEach(function (item) { var li = document.createElement('li'); var button = document.createElement('button'); button.type = 'button'; button.className = 'link-button'; button.textContent = (labels[item.zone] || item.zone.replace(/_/g, ' ')) + ' · ' + item.severity; button.addEventListener('click', function () { select(item.zone); }); li.appendChild(button); list.appendChild(li); }); if (!selected) { fields.hidden = true; empty.hidden = false; return; } var current = impact(selected); empty.hidden = true; fields.hidden = false; editor.querySelector('[data-damage-zone-label]').textContent = labels[selected] || selected.replace(/_/g, ' '); if (editable) { editor.querySelector('[data-damage-severity]').value = current ? current.severity : 'light'; editor.querySelector('[data-damage-note]').value = current ? current.note : ''; } else { editor.querySelector('[data-damage-read-severity]').textContent = current ? current.severity : 'Not recorded'; editor.querySelector('[data-damage-read-note]').textContent = current ? current.note : ''; } }
            function select(zone) { selected = zone; if (editable && !impact(zone)) { impacts.push({ zone: zone, severity: 'light', note: '' }); persist(); } render(); }
            editor.querySelectorAll('[data-damage-zone]').forEach(function (element) { element.addEventListener('click', function () { if (editable || impact(element.getAttribute('data-damage-zone'))) { select(element.getAttribute('data-damage-zone')); } }); });
            if (editable) { editor.querySelector('[data-damage-severity]').addEventListener('change', function (event) { var current = impact(selected); if (current) { current.severity = event.target.value; persist(); render(); } }); editor.querySelector('[data-damage-note]').addEventListener('input', function (event) { var current = impact(selected); if (current) { current.note = event.target.value; persist(); } }); editor.querySelector('[data-damage-remove]').addEventListener('click', function () { impacts = impacts.filter(function (item) { return item.zone !== selected; }); selected = null; persist(); render(); }); }
            render();
        });
    }

    function bindReportRecipients(root) {
        root.querySelectorAll('[data-add-report-recipient]').forEach(function (button) {
            if (button.dataset.recipientBound) { return; }
            button.dataset.recipientBound = 'true';
            button.addEventListener('click', function () {
                var target = document.getElementById(button.dataset.addReportRecipient);
                if (!target) { return; }
                var input = document.createElement('input');
                input.name = button.dataset.reportRecipientName;
                input.type = 'email';
                input.autocomplete = 'email';
                input.setAttribute('aria-label', input.name === 'ccRecipients' ? 'Report copy recipient' : 'Report recipient');
                target.appendChild(input);
                input.focus();
            });
        });
    }

    bindPreparations(document); bindDamage(document); bindReportRecipients(document);
    (window.pegasusMountBinders = window.pegasusMountBinders || []).push(function (root) { bindPreparations(root); bindDamage(root); bindReportRecipients(root); });
})();
