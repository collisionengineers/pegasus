// Send a photo link (FRD-28; R806): "Edited for this one only." shows as soon
// as a message (the e-mail's or the text's) differs from its standard wording, and "Put the standard
// wording back" restores it in place. The form remains fully usable without
// this asset: the button then posts and the page returns with the standard
// wording. "Type" shows, and is required, only while Quick is chosen (R808).
(function () {
    'use strict';

    var type = document.querySelector('[data-photo-link-type]');
    var modes = document.querySelectorAll('input[name="Mode"]');

    function showType() {
        var quick = document.querySelector('input[name="Mode"][value="Quick"]');
        var asked = !!quick && quick.checked;
        type.hidden = !asked;
        type.querySelectorAll('input').forEach(function (input) {
            input.required = asked;
        });
    }

    if (type) {
        modes.forEach(function (mode) {
            mode.addEventListener('change', showType);
        });
        showType();
    }

    function normalized(text) {
        return text.replace(/\r\n/g, '\n').trim();
    }

    // One group per message: the e-mail's and the text's (R917).
    document.querySelectorAll('[data-photo-link-wording]').forEach(function (group) {
        var message = group.querySelector('[data-photo-link-message]');
        var standard = group.querySelector('[data-photo-link-standard]');
        var edited = group.querySelector('[data-photo-link-edited]');
        var reset = group.querySelector('[data-photo-link-reset]');
        if (!message || !standard || !edited || !reset) {
            return;
        }

        function showEdited() {
            edited.hidden = normalized(message.value) === normalized(standard.value);
        }

        message.addEventListener('input', showEdited);
        reset.addEventListener('click', function (event) {
            event.preventDefault();
            message.value = standard.value;
            showEdited();
            message.focus();
        });
    });
})();
