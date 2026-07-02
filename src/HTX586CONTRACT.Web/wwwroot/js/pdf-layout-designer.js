(function () {
    const states = new WeakMap();

    function loadScript(src) {
        return new Promise((resolve, reject) => {
            const existing = document.querySelector(`script[src="${src}"]`);
            if (existing) {
                existing.addEventListener('load', resolve, { once: true });
                if (window.pdfjsLib) resolve();
                return;
            }

            const script = document.createElement('script');
            script.src = src;
            script.async = true;
            script.onload = resolve;
            script.onerror = () => reject(new Error(`Không tải được ${src}`));
            document.head.appendChild(script);
        });
    }

    async function ensurePdfJs() {
        if (window.pdfjsLib) return;
        await loadScript('https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.min.js');
        window.pdfjsLib.GlobalWorkerOptions.workerSrc = 'https://cdnjs.cloudflare.com/ajax/libs/pdf.js/3.11.174/pdf.worker.min.js';
    }

    function base64ToBytes(base64) {
        const raw = atob(base64);
        const bytes = new Uint8Array(raw.length);
        for (let i = 0; i < raw.length; i++) bytes[i] = raw.charCodeAt(i);
        return bytes;
    }

    function normalizeField(field) {
        return {
            id: field.id ?? field.Id,
            kind: field.kind ?? field.Kind,
            key: field.key ?? field.Key,
            page: field.page ?? field.Page,
            x: field.x ?? field.X,
            y: field.y ?? field.Y,
            width: field.width ?? field.Width,
            height: field.height ?? field.Height,
            fontSize: field.fontSize ?? field.FontSize,
            minFontSize: field.minFontSize ?? field.MinFontSize,
            maxLines: field.maxLines ?? field.MaxLines,
            alignment: field.alignment ?? field.Alignment,
            verticalAlignment: field.verticalAlignment ?? field.VerticalAlignment,
            bold: field.bold ?? field.Bold,
            italic: field.italic ?? field.Italic,
            uppercase: field.uppercase ?? field.Uppercase,
            clearBackground: field.clearBackground ?? field.ClearBackground,
            fit: field.fit ?? field.Fit
        };
    }

    function kindCss(field) {
        if (typeof field.kind === 'string') return field.kind.toLowerCase();
        return field.id && field.id.startsWith('image:') ? 'image' : 'text';
    }

    function updateElementPosition(element, field, scale) {
        element.style.left = `${field.x * scale}px`;
        element.style.top = `${field.y * scale}px`;
        element.style.width = `${field.width * scale}px`;
        element.style.height = `${field.height * scale}px`;
    }

    async function renderPages(host, state) {
        host.innerHTML = '';
        state.pageLayers = new Map();
        state.fieldElements = new Map();

        for (let pageNumber = 1; pageNumber <= state.pdf.numPages; pageNumber++) {
            const page = await state.pdf.getPage(pageNumber);
            const viewport = page.getViewport({ scale: state.scale });

            const pageShell = document.createElement('div');
            pageShell.className = 'pdf-designer-page-shell';

            const pageLabel = document.createElement('div');
            pageLabel.className = 'pdf-designer-page-label';
            pageLabel.textContent = `Trang ${pageNumber}`;
            pageShell.appendChild(pageLabel);

            const pageWrap = document.createElement('div');
            pageWrap.className = 'pdf-designer-page';
            pageWrap.style.width = `${viewport.width}px`;
            pageWrap.style.height = `${viewport.height}px`;
            pageShell.appendChild(pageWrap);

            const canvas = document.createElement('canvas');
            canvas.width = Math.ceil(viewport.width);
            canvas.height = Math.ceil(viewport.height);
            canvas.style.width = `${viewport.width}px`;
            canvas.style.height = `${viewport.height}px`;
            pageWrap.appendChild(canvas);

            const overlay = document.createElement('div');
            overlay.className = 'pdf-designer-overlay';
            overlay.dataset.page = pageNumber.toString();
            pageWrap.appendChild(overlay);
            state.pageLayers.set(pageNumber, overlay);

            await page.render({
                canvasContext: canvas.getContext('2d'),
                viewport
            }).promise;

            host.appendChild(pageShell);
        }

        renderFieldOverlays(state);
    }

    function renderFieldOverlays(state) {
        for (const layer of state.pageLayers.values()) layer.innerHTML = '';
        state.fieldElements.clear();

        for (const field of state.fields) {
            const layer = state.pageLayers.get(Number(field.page));
            if (!layer) continue;

            const element = document.createElement('div');
            element.className = `pdf-designer-field pdf-designer-field-${kindCss(field)}`;
            element.dataset.fieldId = field.id;
            element.tabIndex = 0;
            updateElementPosition(element, field, state.scale);

            const label = document.createElement('span');
            label.className = 'pdf-designer-field-label';
            label.textContent = field.key;
            element.appendChild(label);

            const handle = document.createElement('span');
            handle.className = 'pdf-designer-field-resize';
            handle.title = 'Kéo để resize';
            element.appendChild(handle);

            attachFieldEvents(element, handle, field, state);
            layer.appendChild(element);
            state.fieldElements.set(field.id, element);
        }

        if (state.selectedId) selectFieldElement(state, state.selectedId, false);
    }

    function attachFieldEvents(element, handle, field, state) {
        element.addEventListener('mousedown', (event) => {
            event.preventDefault();
            event.stopPropagation();

            const resize = event.target === handle;
            const startClientX = event.clientX;
            const startClientY = event.clientY;
            const start = { x: field.x, y: field.y, width: field.width, height: field.height };

            selectFieldElement(state, field.id, true);

            function onMove(moveEvent) {
                const dx = (moveEvent.clientX - startClientX) / state.scale;
                const dy = (moveEvent.clientY - startClientY) / state.scale;

                if (resize) {
                    field.width = Math.max(2, round(start.width + dx));
                    field.height = Math.max(2, round(start.height + dy));
                } else {
                    field.x = Math.max(0, round(start.x + dx));
                    field.y = Math.max(0, round(start.y + dy));
                }

                updateElementPosition(element, field, state.scale);
            }

            function onUp() {
                document.removeEventListener('mousemove', onMove);
                document.removeEventListener('mouseup', onUp);
                notifyChanged(state, field);
            }

            document.addEventListener('mousemove', onMove);
            document.addEventListener('mouseup', onUp);
        });

        element.addEventListener('keydown', (event) => {
            if (!event.ctrlKey) return;

            let handled = true;
            const step = event.shiftKey ? 2 : 0.5;
            switch (event.key) {
                case 'ArrowLeft': field.x = Math.max(0, round(field.x - step)); break;
                case 'ArrowRight': field.x = round(field.x + step); break;
                case 'ArrowUp': field.y = Math.max(0, round(field.y - step)); break;
                case 'ArrowDown': field.y = round(field.y + step); break;
                default: handled = false; break;
            }

            if (handled) {
                event.preventDefault();
                updateElementPosition(element, field, state.scale);
                notifyChanged(state, field);
            }
        });
    }

    function selectFieldElement(state, fieldId, notify) {
        state.selectedId = fieldId;
        for (const [id, element] of state.fieldElements) {
            element.classList.toggle('is-selected', id === fieldId);
            if (id === fieldId) element.focus({ preventScroll: true });
        }

        if (notify && state.dotNetRef) {
            state.dotNetRef.invokeMethodAsync('OnDesignerFieldSelected', fieldId);
        }
    }

    function notifyChanged(state, field) {
        if (!state.dotNetRef) return;
        state.dotNetRef.invokeMethodAsync('OnDesignerFieldChanged', {
            id: field.id,
            kind: kindCss(field),
            key: field.key,
            page: Number(field.page),
            x: field.x,
            y: field.y,
            width: field.width,
            height: field.height
        });
    }

    function round(value) {
        return Math.round(value * 100) / 100;
    }

    window.htxPdfLayoutDesigner = {
        init: async function (host, pdfBase64, fields, dotNetRef, zoom) {
            await ensurePdfJs();
            this.destroy(host);

            const pdf = await window.pdfjsLib.getDocument({ data: base64ToBytes(pdfBase64) }).promise;
            const state = {
                pdf,
                fields: (fields || []).map(normalizeField),
                dotNetRef,
                scale: zoom || 1.25,
                selectedId: null,
                pageLayers: new Map(),
                fieldElements: new Map()
            };
            states.set(host, state);
            await renderPages(host, state);
        },

        destroy: function (host) {
            const state = states.get(host);
            if (state && state.pdf) {
                try { state.pdf.destroy(); } catch { }
            }
            states.delete(host);
            if (host) host.innerHTML = '';
        },

        selectField: function (host, fieldId) {
            const state = states.get(host);
            if (!state) return;
            selectFieldElement(state, fieldId, false);
            const element = state.fieldElements.get(fieldId);
            if (element) element.scrollIntoView({ block: 'center', inline: 'center', behavior: 'smooth' });
        },

        updateField: function (host, field) {
            const state = states.get(host);
            if (!state) return;

            const updated = normalizeField(field);
            const index = state.fields.findIndex(x => x.id === updated.id);
            if (index >= 0) {
                state.fields[index] = { ...state.fields[index], ...updated };
                const element = state.fieldElements.get(updated.id);
                if (element) updateElementPosition(element, state.fields[index], state.scale);
            }
        },

        setZoom: async function (host, zoom) {
            const state = states.get(host);
            if (!state) return;
            state.scale = zoom;
            await renderPages(host, state);
        }
    };
})();
