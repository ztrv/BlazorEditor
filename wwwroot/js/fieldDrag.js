// Blazor's DragEventArgs is a serialized snapshot of the browser event, so C# handlers
// cannot touch dataTransfer. Three things therefore have to happen here:
//
//   1. setData() during dragstart. Firefox silently cancels any drag without it.
//   2. effectAllowed / dropEffect, so the pointer shows a move cursor instead of "no drop".
//   3. preventDefault on dragover, which is what makes an element a valid drop target.
//
// Everything else — what may nest where, and where a dropped field lands — stays in C#.
//
// This file deliberately does not add or remove CSS classes. Blazor owns the class
// attribute on these elements and would overwrite them on the next render.

(function () {
    'use strict';

    var TREE = '[data-fhe-tree]';
    var ROW = '[data-fhe-row]';
    var HANDLE = '[data-fhe-handle]';

    function closest(el, sel) {
        return el && el.closest ? el.closest(sel) : null;
    }

    document.addEventListener('dragstart', function (e) {
        var handle = closest(e.target, HANDLE);
        if (!handle) return;

        var dt = e.dataTransfer;
        if (!dt) return;

        var row = closest(handle, ROW);

        // Firefox requires payload data or it will not begin the drag at all.
        try {
            dt.setData('text/plain', row ? (row.getAttribute('data-fhe-row') || '') : '');
        } catch (_) {
            // Some browsers throw on a protected dataTransfer; the drag still proceeds.
        }

        dt.effectAllowed = 'move';

        // Drag the whole row rather than the grip glyph on its own.
        if (row && dt.setDragImage) {
            dt.setDragImage(row, 24, row.offsetHeight / 2);
        }
    }, true);

    // A drop only fires where dragover was prevented. Doing it here rather than relying on
    // Blazor's :preventDefault keeps it client-side — no round trip per pointer move.
    document.addEventListener('dragover', function (e) {
        if (!closest(e.target, TREE)) return;
        e.preventDefault();
        if (e.dataTransfer) e.dataTransfer.dropEffect = 'move';
    }, true);

    document.addEventListener('drop', function (e) {
        // Stop the browser navigating to the dragged text on a missed drop.
        if (closest(e.target, TREE)) e.preventDefault();
    }, true);
})();
