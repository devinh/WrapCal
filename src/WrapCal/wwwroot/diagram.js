// Zoom on the cut layout. This is the second piece of JS the app owns, and it exists for one
// reason: Blazor registers `wheel` listeners as passive, so preventDefault() cannot come from C#.
// Without it, a Ctrl+wheel over the diagram would zoom the whole page while the drawing zoomed
// inside it.
//
// The listeners go on the document and are scoped by where the pointer is, not by which element
// the event targets. Two reasons: the scroller carries a Blazor @key on (plan, direction) so a new
// plan opens at the origin, which throws that element away and takes any listener on it with it;
// and a wheel event does not always arrive with the element under the cursor as its target — WebKit
// dispatches one aimed at the page root. The figure is only used for its geometry, and it outlives
// every re-key.
window.wrapcalDiagram = {
    attach: function (element, dotNetRef) {
        const owner = element;

        function pointerOverFigure(x, y) {
            const box = owner.getBoundingClientRect();
            return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
        }

        // Spread between two fingers on a touchscreen, captured at the start of a pinch so the
        // ratio sent to C# is relative to the gesture rather than absolute.
        let pinchFrom = null;

        function onWheel(event) {
            // Ctrl (or Cmd) marks a zoom; a plain wheel has to keep scrolling the drawing, which is
            // how the far end of a long plan is reached. Chromium reports a trackpad pinch as a
            // stream of small ctrl+wheel deltas, which is why C# accumulates them instead of
            // stepping once per event.
            if (!event.ctrlKey && !event.metaKey) {
                return;
            }

            // Outside the drawing the gesture belongs to the page, so the browser may zoom.
            if (!pointerOverFigure(event.clientX, event.clientY)) {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("ZoomByWheelAsync", event.deltaY);
        }

        // Safari reports a trackpad pinch as gesture events carrying `scale`, not as ctrl+wheel.
        function onGestureStart(event) {
            if (!pointerOverFigure(event.clientX, event.clientY)) {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("BeginZoomGestureAsync");
        }

        function onGestureChange(event) {
            if (!pointerOverFigure(event.clientX, event.clientY)) {
                return;
            }

            event.preventDefault();
            dotNetRef.invokeMethodAsync("ZoomByRatioAsync", event.scale);
        }

        function onGestureEnd(event) {
            event.preventDefault();
            dotNetRef.invokeMethodAsync("EndZoomGestureAsync");
        }

        function spread(touches) {
            const dx = touches[0].clientX - touches[1].clientX;
            const dy = touches[0].clientY - touches[1].clientY;
            return Math.hypot(dx, dy);
        }

        function onTouchMove(event) {
            if (event.touches.length !== 2) {
                return;
            }

            // Two fingers is ours; one finger is left to scroll the drawing.
            event.preventDefault();

            const current = spread(event.touches);

            if (pinchFrom === null) {
                pinchFrom = current;
                dotNetRef.invokeMethodAsync("BeginZoomGestureAsync");
                return;
            }

            if (pinchFrom > 0) {
                dotNetRef.invokeMethodAsync("ZoomByRatioAsync", current / pinchFrom);
            }
        }

        function onTouchEnd(event) {
            if (pinchFrom === null || event.touches.length >= 2) {
                return;
            }

            pinchFrom = null;
            dotNetRef.invokeMethodAsync("EndZoomGestureAsync");
        }

        // Non-passive for wheel and touchmove: those are the two the browser makes passive by
        // default, and preventDefault is the whole point.
        document.addEventListener("wheel", onWheel, { passive: false });
        document.addEventListener("gesturestart", onGestureStart);
        document.addEventListener("gesturechange", onGestureChange);
        document.addEventListener("gestureend", onGestureEnd);
        document.addEventListener("touchmove", onTouchMove, { passive: false });
        document.addEventListener("touchend", onTouchEnd);
        document.addEventListener("touchcancel", onTouchEnd);

        owner.wrapcalDetach = function () {
            document.removeEventListener("wheel", onWheel);
            document.removeEventListener("gesturestart", onGestureStart);
            document.removeEventListener("gesturechange", onGestureChange);
            document.removeEventListener("gestureend", onGestureEnd);
            document.removeEventListener("touchmove", onTouchMove);
            document.removeEventListener("touchend", onTouchEnd);
            document.removeEventListener("touchcancel", onTouchEnd);
            delete owner.wrapcalDetach;
        };
    },

    detach: function (element) {
        if (element && element.wrapcalDetach) {
            element.wrapcalDetach();
        }
    }
};
