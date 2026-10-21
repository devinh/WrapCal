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

        // Where the gesture began, in the drawing's own coordinates. Recorded before the zoom
        // resizes anything, so the scroll can be corrected afterwards to put that same point back
        // under the pointer — which is what makes zooming feel like a map.
        //
        // Held as a point in the SVG's user space rather than as a fraction of the element: the
        // element is as big as the frame, and a sideways drawing only fills a corner of it, so a
        // fraction of the element is not a fraction of the drawing. The SVG's own matrices do that
        // conversion exactly, letterboxing included.
        let anchor = null;

        // Measured through the drawing group rather than the <svg>: a translate on the group is
        // invisible to the svg's matrix, so a drift corrected that way would never look corrected
        // and would be rolled back.
        function drawing(svg) {
            return svg.querySelector("g.drawing") || svg;
        }

        function userPointFrom(clientX, clientY, svg) {
            const matrix = drawing(svg).getScreenCTM();

            return matrix ? new DOMPoint(clientX, clientY).matrixTransform(matrix.inverse()) : null;
        }

        function screenPointOf(point, svg) {
            const matrix = drawing(svg).getScreenCTM();

            return matrix ? new DOMPoint(point.x, point.y).matrixTransform(matrix) : null;
        }

        function rememberAnchor(clientX, clientY) {
            const svg = owner.querySelector(".diagram-scroller svg");

            if (!svg) {
                return;
            }

            const point = userPointFrom(clientX, clientY, svg);
            anchor = point ? { point: point, x: clientX, y: clientY } : null;
        }

        // The listeners live on the document, so they see every wheel on the page; this decides
        // which ones belong to the drawing.
        function pointerOverFigure(x, y) {
            const box = owner.getBoundingClientRect();

            return x >= box.left && x <= box.right && y >= box.top && y <= box.bottom;
        }

        owner.wrapcalAnchor = function () {
            const scroller = owner.querySelector(".diagram-scroller");
            const svg = scroller && scroller.querySelector("svg");

            if (!anchor || !svg) {
                return;
            }

            // How far the anchored point has drifted from the pointer, in screen pixels. Positive
            // means the drawing has to move right/down to get back under it.
            function error() {
                const now = screenPointOf(anchor.point, svg);

                return now ? { x: anchor.x - now.x, y: anchor.y - now.y } : { x: 0, y: 0 };
            }

            function scrollBy(axis, amount) {
                if (axis === "x") {
                    scroller.scrollLeft += amount;
                } else {
                    scroller.scrollTop += amount;
                }
            }

            // Scroll only moves content that overflows. A sideways drawing is only as wide as its
            // own length, so a short plan sits inside the frame with nothing to scroll — there the
            // drawing itself has to move. A transform on the drawing group rather than on the
            // <svg>: the svg's box has to keep describing the space the scroll offsets refer to.
            function translate(axis, pixels) {
                const group = svg.querySelector("g.drawing");

                if (!group) {
                    return;
                }

                const matrix = drawing(svg).getScreenCTM();

                if (!matrix) {
                    return;
                }

                // Screen pixels to user units, through the scale the drawing is currently drawn at.
                const scale = axis === "x" ? matrix.a : matrix.d;
                const shift = pixels / scale;

                const current = group.transform.baseVal.consolidate();
                const existing = current ? current.matrix : new DOMMatrix();
                const dx = axis === "x" ? shift : 0;
                const dy = axis === "y" ? shift : 0;

                group.setAttribute(
                    "transform",
                    `translate(${existing.e + dx} ${existing.f + dy})`);
            }

            function restore(scrollerLeft, scrollerTop, transform) {
                scroller.scrollLeft = scrollerLeft;
                scroller.scrollTop = scrollerTop;

                const group = svg.querySelector("g.drawing");

                if (group) {
                    if (transform === null) {
                        group.removeAttribute("transform");
                    } else {
                        group.setAttribute("transform", transform);
                    }
                }
            }

            // Which way a scroll offset moves the content depends on the layout — Up and Left lay
            // the scroller out from the far end — so rather than encode four conventions this tries
            // one direction, measures, and takes the other if the error did not shrink. Restoring
            // exactly between attempts matters: at the end of the scroll range the browser clamps,
            // so a move and its "undo" are not equal and opposite.
            function correct(axis) {
                for (const move of [scrollBy, translate]) {
                    for (const direction of [-1, 1]) {
                        const errorBefore = error();
                        const magnitude = Math.abs(axis === "x" ? errorBefore.x : errorBefore.y);

                        if (magnitude < 0.5) {
                            return;
                        }

                        const left = scroller.scrollLeft;
                        const top = scroller.scrollTop;
                        const group = svg.querySelector("g.drawing");
                        const transform = group ? group.getAttribute("transform") : null;

                        move(axis, direction * magnitude);

                        const after = error();
                        const magnitudeAfter = Math.abs(axis === "x" ? after.x : after.y);

                        if (magnitudeAfter < magnitude - 0.5) {
                            return;
                        }

                        restore(left, top, transform);
                    }
                }
            }

            correct("x");
            correct("y");
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
