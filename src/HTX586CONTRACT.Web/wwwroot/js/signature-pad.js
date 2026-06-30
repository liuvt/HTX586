window.signaturePad = {
    pads: {},

    initialize: function (canvasId) {
        const canvas = document.getElementById(canvasId);

        if (!canvas) {
            return;
        }

        const context = canvas.getContext("2d");

        let drawing = false;
        let hasSignature = false;

        function resizeCanvas() {
            const ratio = Math.max(
                window.devicePixelRatio || 1,
                1
            );

            const rect = canvas.getBoundingClientRect();

            canvas.width = rect.width * ratio;
            canvas.height = 220 * ratio;

            context.setTransform(
                ratio,
                0,
                0,
                ratio,
                0,
                0
            );

            context.lineWidth = 2;
            context.lineCap = "round";
            context.lineJoin = "round";
            context.strokeStyle = "#172B28";
        }

        function getPoint(event) {
            const rect = canvas.getBoundingClientRect();

            const source =
                event.touches?.[0] ??
                event.changedTouches?.[0] ??
                event;

            return {
                x: source.clientX - rect.left,
                y: source.clientY - rect.top
            };
        }

        function start(event) {
            event.preventDefault();

            drawing = true;
            hasSignature = true;

            const point = getPoint(event);

            context.beginPath();
            context.moveTo(point.x, point.y);
        }

        function move(event) {
            if (!drawing) {
                return;
            }

            event.preventDefault();

            const point = getPoint(event);

            context.lineTo(point.x, point.y);
            context.stroke();
        }

        function end(event) {
            if (!drawing) {
                return;
            }

            event.preventDefault();
            drawing = false;
            context.closePath();
        }

        resizeCanvas();

        canvas.addEventListener("pointerdown", start);
        canvas.addEventListener("pointermove", move);
        canvas.addEventListener("pointerup", end);
        canvas.addEventListener("pointerleave", end);

        this.pads[canvasId] = {
            canvas,
            context,
            hasSignature: () => hasSignature,
            resetSignature: () => {
                hasSignature = false;
            }
        };
    },

    clear: function (canvasId) {
        const pad = this.pads[canvasId];

        if (!pad) {
            return;
        }

        pad.context.clearRect(
            0,
            0,
            pad.canvas.width,
            pad.canvas.height
        );

        pad.resetSignature();
    },

    getData: function (canvasId) {
        const pad = this.pads[canvasId];

        if (!pad || !pad.hasSignature()) {
            return null;
        }

        return pad.canvas.toDataURL("image/png");
    }
};