// Spielt beim Hovern ueber ein Thumbnail eine stumme Vorschau an einer interessanten
// Stelle (Mitte des Videos) ab. Die Quelle wird bewusst erst nach einer Verzoegerung
// angefordert: /api/VideoProxy loest die Quell-URL live beim Original-Anbieter auf,
// ein Preview pro ueberstrichener Kachel wuerde dort ins Rate-Limit laufen.
(function () {
    const HOVER_DELAY_MS = 400;
    const CONTAINER_SELECTOR = '.thumbnail-container[data-preview-id]';
    const MIN_SECONDS_FOR_SEEK = 20;
    // Nach dieser Spielzeit springt die Vorschau von der Mitte auf 2/3 - eine zweite
    // Stelle zeigt mehr vom Video, ohne dass man laenger warten muss.
    const SECOND_SEEK_AFTER_MS = 5000;
    const SECOND_SEEK_RATIO = 2 / 3;
    // Maximaler vertikaler Schwenk, als Vielfaches der Kachelhoehe.
    const MAX_VERTICAL_PAN = 0.9;
    // Anteil der Reststrecke pro Frame - kleiner = traeger und weicher.
    const PAN_SMOOTHING = 0.08;
    // > 1: voller Ausschlag wird schon vor dem Kachelrand erreicht.
    const EDGE_GAIN = 1.25;
    // > 1: Mitte ruhig, Zugewinn konzentriert sich nach aussen.
    const RESPONSE_EXPONENT = 1.5;

    // Auf Touch-Geraeten gibt es kein echtes Hover - dort waere die Vorschau nur Ballast.
    if (window.matchMedia && !window.matchMedia('(hover: hover)').matches) return;

    let hoverTimer = null;
    let secondSeekTimer = null;
    let pendingContainer = null;
    let active = null; // { container, video, id }
    let lastMouse = null; // { x, y } - letzte Cursorposition, fuer den Kameraschwenk
    let panFrame = null;
    let panTarget = { x: 0, y: 0 };  // wohin die Kamera soll (px)
    let panCurrent = { x: 0, y: 0 }; // wo sie gerade ist (px)

    // Bildet die Cursorposition (-1 .. 1 ab Kachelmitte) auf den Schwenk ab.
    // EDGE_GAIN laesst den vollen Ausschlag schon vor dem aeussersten Rand erreichen,
    // der Exponent macht die Mitte ruhig und schiebt den Zugewinn nach aussen.
    function response(c) {
        const t = Math.max(-1, Math.min(1, c * 2 * EDGE_GAIN));
        return Math.sign(t) * Math.pow(Math.abs(t), RESPONSE_EXPONENT);
    }

    // Verschiebt den sichtbaren Ausschnitt innerhalb des linken Auges anhand der
    // Cursorposition. So laesst sich in der Vorschau umsehen, ohne zu klicken.
    function updatePanTarget() {
        if (active === null || lastMouse === null) return;

        const { container, video } = active;
        const rect = container.getBoundingClientRect();
        if (rect.width === 0 || video.offsetWidth === 0) return;

        // Nur das linke Auge darf ins Bild wandern - deshalb die halbe Elementbreite.
        const slackX = Math.max(0, (video.offsetWidth / 2 - rect.width) / 2);
        // Vertikal ist der Ueberstand riesig; ungebremst schwenkt man bis in die
        // stark verzerrten Pole der Fischaugen-Projektion. Deshalb begrenzen.
        const slackY = Math.min(
            Math.max(0, (video.offsetHeight - rect.height) / 2),
            rect.height * MAX_VERTICAL_PAN
        );

        const clamp = (v) => Math.min(1, Math.max(0, v));
        const cx = clamp((lastMouse.x - rect.left) / rect.width) - 0.5;
        const cy = clamp((lastMouse.y - rect.top) / rect.height) - 0.5;

        // Cursor nach rechts -> Bild nach links schieben -> Blick wandert nach rechts.
        panTarget = { x: -response(cx) * slackX, y: -response(cy) * slackY };
    }

    function renderPan() {
        if (active === null) return;
        active.video.style.transform =
            `translate(${panCurrent.x.toFixed(1)}px, calc(-50% + ${panCurrent.y.toFixed(1)}px))`;
    }

    // Exponentielle Daempfung: die Kamera zieht dem Ziel weich hinterher, statt der Maus
    // hart zu folgen. Laeuft aus, sobald das Ziel praktisch erreicht ist.
    function panLoop() {
        panFrame = null;
        if (active === null) return;

        updatePanTarget();
        panCurrent.x += (panTarget.x - panCurrent.x) * PAN_SMOOTHING;
        panCurrent.y += (panTarget.y - panCurrent.y) * PAN_SMOOTHING;
        renderPan();

        const settled = Math.abs(panTarget.x - panCurrent.x) < 0.3
            && Math.abs(panTarget.y - panCurrent.y) < 0.3;
        if (!settled) panFrame = requestAnimationFrame(panLoop);
    }

    function schedulePan() {
        if (panFrame !== null || active === null) return;
        panFrame = requestAnimationFrame(panLoop);
    }

    function stopPreview() {
        if (hoverTimer !== null) {
            clearTimeout(hoverTimer);
            hoverTimer = null;
        }
        if (secondSeekTimer !== null) {
            clearTimeout(secondSeekTimer);
            secondSeekTimer = null;
        }
        pendingContainer = null;

        if (panFrame !== null) {
            cancelAnimationFrame(panFrame);
            panFrame = null;
        }

        panTarget = { x: 0, y: 0 };
        panCurrent = { x: 0, y: 0 };

        if (active === null) return;

        const { container, video } = active;
        active = null;

        container.classList.remove('preview-loading', 'preview-active');
        video.classList.remove('active');
        video.style.transform = '';
        try {
            video.pause();
            video.removeAttribute('src');
            video.load(); // bricht einen noch laufenden Upstream-Request ab
        } catch (e) {
            /* Element bereits aus dem DOM entfernt */
        }
    }

    function startPreview(container) {
        const video = container.querySelector('video.hover-preview');
        const id = container.dataset.previewId;
        if (!video || !id) return;

        const seconds = parseInt(container.dataset.previewSeconds || '0', 10);
        const seekTo = seconds > MIN_SECONDS_FOR_SEEK ? Math.floor(seconds / 2) : 0;

        active = { container, video, id };
        container.classList.add('preview-loading');

        video.muted = true;
        video.defaultMuted = true;
        video.loop = true;
        video.preload = 'auto';
        // Media-Fragment: die meisten Browser starten damit direkt am Zielpunkt.
        video.src = seekTo > 0 ? `/api/VideoProxy/${id}#t=${seekTo}` : `/api/VideoProxy/${id}`;

        const isStale = () => active === null
            || active.video !== video
            || container.dataset.previewId !== id;

        video.addEventListener('loadedmetadata', () => {
            if (isStale()) return;
            // Fallback fuer Browser, die das Media-Fragment ignorieren.
            if (seekTo > 0 && video.currentTime < 1 && isFinite(video.duration)) {
                try {
                    video.currentTime = Math.min(seekTo, Math.max(0, video.duration - 2));
                } catch (e) {
                    /* Seek nicht moeglich - Vorschau laeuft dann ab Anfang */
                }
            }
        }, { once: true });

        video.addEventListener('canplay', () => {
            if (isStale()) return;
            container.classList.remove('preview-loading');
            container.classList.add('preview-active');
            // Noch unsichtbar auf die Cursorposition setzen (ohne Daempfung), damit die
            // Vorschau nicht sichtbar aus der Mitte heranfaehrt.
            updatePanTarget();
            panCurrent = { x: panTarget.x, y: panTarget.y };
            renderPan();
            video.classList.add('active');
            video.play().catch(() => stopPreview());

            // Timer erst ab tatsaechlichem Playback, sonst frisst das Puffern die 5s auf.
            if (seekTo > 0 && secondSeekTimer === null) {
                secondSeekTimer = setTimeout(() => {
                    secondSeekTimer = null;
                    if (isStale()) return;

                    const total = isFinite(video.duration) && video.duration > 0
                        ? video.duration
                        : seconds;
                    if (total <= 0) return;

                    try {
                        video.currentTime = Math.min(total * SECOND_SEEK_RATIO, Math.max(0, total - 2));
                    } catch (e) {
                        /* Seek nicht moeglich - Vorschau laeuft einfach weiter */
                    }
                }, SECOND_SEEK_AFTER_MS);
            }
        }, { once: true });

        video.addEventListener('error', () => {
            // z.B. HLS-Quellen, die ein nacktes <video> nicht abspielen kann.
            if (!isStale()) stopPreview();
        }, { once: true });

        video.load();
    }

    function containerFrom(target) {
        return target && target.closest ? target.closest(CONTAINER_SELECTOR) : null;
    }

    document.addEventListener('mousemove', (e) => {
        lastMouse = { x: e.clientX, y: e.clientY };
        if (active !== null) schedulePan();
    }, { passive: true });

    document.addEventListener('mouseover', (e) => {
        const container = containerFrom(e.target);
        if (!container) return;
        if (active !== null && active.container === container) return;
        if (pendingContainer === container) return;

        stopPreview();
        pendingContainer = container;
        hoverTimer = setTimeout(() => {
            hoverTimer = null;
            pendingContainer = null;
            startPreview(container);
        }, HOVER_DELAY_MS);
    });

    document.addEventListener('mouseout', (e) => {
        const container = containerFrom(e.target);
        if (!container) return;
        // Bewegungen innerhalb derselben Kachel sind kein Verlassen.
        if (e.relatedTarget && container.contains(e.relatedTarget)) return;

        if (pendingContainer === container || (active !== null && active.container === container)) {
            stopPreview();
        }
    });

    // Klick oeffnet den richtigen Player - Vorschau hat dann ausgedient.
    document.addEventListener('click', () => stopPreview(), true);

    document.addEventListener('visibilitychange', () => {
        if (document.hidden) stopPreview();
    });
})();
