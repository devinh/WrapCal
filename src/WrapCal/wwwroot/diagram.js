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

        // Where the gesture began and what part of the drawing was under it at the time. Recorded
        // before the zoom resizes anything, so the scroll can be corrected afterwards to put that
        // same part back under the pointer — which is what makes zooming feel like a map.
        let anchor = null;

        // The listeners live on the document, so they see every wheel on the page; this decides
        // which ones belong to the drawing.
        function pointerOverFigure(x, y) {
            const box = owner.getBoundingClientRect();

            return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
        }

        function rememberAnchor(clientX, clientY) {
            const svg = owner.querySelector(".diagram-scroller svg");

            if (!svg) {
                return;
            }

            const box = svg.getBoundingClientRect();
            const fractionX = (clientX - box.left) / box.width;
            const fractionY = (clientY - box.top) / box.height;

            // A gesture that began off the drawing has nothing to stay anchored to.
            anchor = fractionX < 0 || fractionX > 1 || fractionY < 0 || fractionY > 1
                ? null
                : { fractionX: fractionX, fractionY: fractionY, x: clientX, y: clientY };
        }

        owner.wrapcalAnchor = function () {
            const scroller = owner.querySelector(".diagram-scroller");
            const svg = scroller && scroller.querySelector("svg");

            if (!anchor || !svg) {
                return;
            }

            // The drawing has been resized by now. How far is the anchored part from the pointer?
            function errorX() {
                const box = svg.getBoundingClientRect();
                return (box.left + (anchor.fractionX * box.width)) - anchor.x;
            }

            function errorY() {
                const box = svg.getBoundingClientRect();
                return (box.top + (anchor.fractionY * box.height)) - anchor.y;
            }

            function shift(axis, amount) {
                if (axis === "x") {
                    scroller.scrollLeft += amount;
                } else {
                    scroller.scrollTop += amount;
                }
            }

            // Which way a scroll offset moves the content depends on the layout: Up and Left lay
            // the scroller out from the far end, so the same correction runs the other way. Rather
            // than encode four conventions, try one direction and keep it only if the error
            // actually shrank; otherwise undo and take the other. An offset clamped at its end
            // cannot move at all, and is left alone.
            function correct(axis, measure) {
                const before = measure();

                if (Math.abs(before) < 0.5) {
                    return;
                }

                for (const direction of [-1, 1]) {
                    const left = scroller.scrollLeft;
                    const top = scroller.scrollTop;

                    shift(axis, direction * before);

                    if (Math.abs(measure()) < Math.abs(before) - 0.5) {
                        return;
                    }

                    // Restore the exact offsets rather than subtracting the shift again: at the end
                    // of the scroll range the browser clamps, so the shift and its "undo" are not
                    // equal and opposite — negating moved the drawing the wrong way.
                    scroller.scrollLeft = left;
                    scroller.scrollTop = top;
                }
            }

            correct("x", errorX);
            correct("y", errorY);
        };

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
            rememberAnchor(event.clientX, event.clientY);
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
            rememberAnchor(event.clientX, event.clientY);
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

            rememberAnchor(
                (event.touches[0].clientX + event.touches[1].clientX) / 2,
                (event.touches[0].clientY + event.touches[1].clientY) / 2);

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

    /// Puts the point a gesture started on back under the pointer. Called after the zoom has been
    /// rendered, since it has to measure the drawing at its new size.
    anchor: function (element) {
        if (element && element.wrapcalAnchor) {
            element.wrapcalAnchor();
        }
    },

    detach: function (element) {
        if (element && element.wrapcalDetach) {
            element.wrapcalDetach();
        }
    }
};
