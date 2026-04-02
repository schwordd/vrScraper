let player = null;
let dotNetRef = null;
let currentVideoId = null;
// Speichere die Lautstärke und Mute-Zustand in globalen Variablen
let savedVolume = localStorage.getItem('vrPlayerVolume') ? parseFloat(localStorage.getItem('vrPlayerVolume')) : 0.7;
let savedMuted = localStorage.getItem('vrPlayerMuted') === 'true';

function registerPlayerErrorCallback(dotnetRef) {
    dotNetRef = dotnetRef;
    console.log("Callback-Referenz registriert");
}

function initializeVRPlayer(videoElementId, sourceUrl, sourceType, videoTitle, vrType, videoId) {
    console.log("Initialisiere VR-Player:", videoElementId, sourceUrl, vrType);
    currentVideoId = videoId;
    window._vrPlayerOpen = true;

    try {
        // Stelle sicher, dass kein vorheriger Player existiert
        if (player) {
            console.log("Bestehender Player gefunden, wird entfernt");
            disposeVRPlayer(true); // keepOpen=true to preserve _vrPlayerOpen flag
        }

        // Überprüfe, ob das Video-Element existiert
        let videoElement = document.getElementById(videoElementId);

        if (!videoElement) {
            console.warn("Video-Element nicht gefunden, wird neu erstellt:", videoElementId);

            // Versuche, das Element neu zu erstellen
            const container = document.querySelector('.video-container');
            if (container) {
                console.log("Erstelle Video-Element neu");
                videoElement = document.createElement('video');
                videoElement.id = videoElementId;
                videoElement.className = 'video-js vjs-big-play-centered';
                container.appendChild(videoElement);
            } else {
                throw new Error("Video container not found");
            }
        }

        // Stelle sicher, dass das Video-Element leer ist
        videoElement.innerHTML = '';

        // Player-Optionen einrichten
        const options = {
            controls: true,
            autoplay: true,
            fluid: true,
            preload: 'auto',
            playsinline: true,
            muted: savedMuted,
            volume: savedVolume,
            userActions: {
                hotkeys: false // Wir übernehmen Keyboard-Handling selbst
            },
            html5: {
                vhs: {
                    overrideNative: true
                },
                nativeVideoTracks: false,
                nativeAudioTracks: false,
                nativeTextTracks: false
            },
            controlBar: {
                pictureInPictureToggle: false
            }
        };

        // Player erstellen
        player = videojs(videoElementId, options, function onPlayerReady() {
            console.log('Player bereit:', this);

            // Stelle gespeichertes Volumen und Mute-Zustand ein
            player.volume(savedVolume);
            player.muted(savedMuted);

            // VR-Plugin initialisieren
            this.vr({
                projection: vrType === '360' ? '360' : '180_LR',
                forceCardboard: false,
                debug: false
            });

            // Event-Listener für Lautstärken- und Mute-Änderungen
            this.on('volumechange', function() {
                savedVolume = player.volume();
                savedMuted = player.muted();
                localStorage.setItem('vrPlayerVolume', savedVolume);
                localStorage.setItem('vrPlayerMuted', savedMuted);
            });

            // Event-Listener für Vollbildänderungen
            this.on('fullscreenchange', function() {
                const isFullscreen = player.isFullscreen();
                console.log('Fullscreen-Änderung:', isFullscreen);

                if (!isFullscreen) {
                    // Verzögerung hinzufügen, da die Controls manchmal nicht sofort aktualisiert werden
                    setTimeout(fixPlayerDimensions, 100);

                    try {
                        // Benachrichtige Blazor über das Verlassen des Vollbildmodus
                        if (dotNetRef) {
                            dotNetRef.invokeMethodAsync('OnFullscreenExit');
                        }
                    } catch (error) {
                        console.error('Fehler beim Aufrufen von OnFullscreenExit:', error);
                    }
                }
            });

            // Verwende den Proxy-Endpoint statt der direkten URL
            const proxyUrl = `/api/VideoProxy/${currentVideoId}`;
            console.log("Verwende Proxy-URL:", proxyUrl);

            // Quelle setzen und abspielen
            this.src({
                src: proxyUrl,
                type: sourceType
            });

            this.play()
                .catch(error => {
                    console.error('Fehler beim Starten der Wiedergabe:', error);
                    if (dotNetRef) {
                        dotNetRef.invokeMethodAsync('OnPlayerError', error.message || 'Unknown playback error');
                    }
                });
        });

        // Fehlerbehandlung für Player
        player.on('error', function(error) {
            console.error('Player-Fehler:', error);
            if (dotNetRef) {
                const errorMsg = player.error_ ? player.error_.message : 'Unknown error';
                dotNetRef.invokeMethodAsync('OnPlayerError', errorMsg);
            }
        });

        // Globaler Event-Listener für document fullscreenchange
        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('mozfullscreenchange', handleFullscreenChange);
        document.addEventListener('MSFullscreenChange', handleFullscreenChange);

        // Keyboard-Handler registrieren (capture phase, damit VideoJS nicht schluckt)
        if (window._vrPlayerKeyHandler) {
            document.removeEventListener('keydown', window._vrPlayerKeyHandler, true);
        }
        window._vrPlayerKeyHandler = handlePlayerKeydown;
        document.addEventListener('keydown', window._vrPlayerKeyHandler, true);

    } catch (error) {
        console.error('Fehler bei Player-Initialisierung:', error);
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnPlayerError', error.message || 'Player initialization failed');
        }
    }
}

// Keyboard-Handler für den Player
function handlePlayerKeydown(e) {
    if (!player) return;
    // Nicht verarbeiten wenn User in einem Eingabefeld tippt
    if (document.activeElement && ['INPUT', 'SELECT', 'TEXTAREA'].includes(document.activeElement.tagName)) return;

    const key = e.key;
    const ctrl = e.ctrlKey;

    switch (key) {
        case 'ArrowLeft':
            e.preventDefault();
            player.currentTime(Math.max(0, player.currentTime() - (ctrl ? 30 : 5)));
            player.userActive(true);
            break;
        case 'ArrowRight':
            e.preventDefault();
            player.currentTime(Math.min(player.duration() || Infinity, player.currentTime() + (ctrl ? 30 : 5)));
            player.userActive(true);
            break;
        case 'ArrowUp':
            e.preventDefault();
            player.volume(Math.min(1, player.volume() + 0.05));
            break;
        case 'ArrowDown':
            e.preventDefault();
            player.volume(Math.max(0, player.volume() - 0.05));
            break;
        case 'm':
        case 'M':
            player.muted(!player.muted());
            break;
        case ' ':
            e.preventDefault();
            player.paused() ? player.play() : player.pause();
            break;
        case 'f':
        case 'F':
            player.isFullscreen() ? player.exitFullscreen() : player.requestFullscreen();
            break;
        case 'n':
        case 'N':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardNext');
            break;
        case 'p':
        case 'P':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardPrevious');
            break;
        case 'l':
        case 'L':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardToggleLike');
            break;
        case 'b':
        case 'B':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardToggleWatchlist');
            break;
        case 'Escape':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardClose');
            break;
        case '?':
        case 'h':
        case 'H':
            if (dotNetRef) dotNetRef.invokeMethodAsync('OnKeyboardToggleHelp');
            break;
    }
}

// Funktion zur Behandlung von Fullscreen-Änderungen
function handleFullscreenChange() {
    if (!document.fullscreenElement &&
        !document.webkitFullscreenElement &&
        !document.mozFullScreenElement &&
        !document.msFullscreenElement) {
        console.log('Fullscreen verlassen (dokument-event)');
        setTimeout(fixPlayerDimensions, 100);
    }
}

// Funktion zur Korrektur der Player-Dimensionen nach dem Verlassen des Vollbildmodus
function fixPlayerDimensions() {
    if (player) {
        console.log('Korrigiere Player-Dimensionen nach Fullscreen');

        // Stelle sicher, dass der Player die richtigen Dimensionen hat
        const playerElement = player.el();
        if (playerElement) {
            // Erzwinge Layout-Update
            playerElement.style.width = '100%';
            playerElement.style.height = '100%';

            // Stelle sicher, dass die Control-Bar sichtbar ist
            const controlBar = playerElement.querySelector('.vjs-control-bar');
            if (controlBar) {
                controlBar.style.display = 'flex';
                controlBar.style.visibility = 'visible';
                controlBar.style.opacity = '1';
            }

            // Löse ein Mouse-Event aus, um die Controls anzuzeigen
            const event = new MouseEvent('mousemove', {
                view: window,
                bubbles: true,
                cancelable: true
            });
            playerElement.dispatchEvent(event);
        }

        // Erzwinge Player-Update
        player.handleTechResize_();
    }
}

// VideoJS-Player beenden
// keepOpen: wenn true, wird _vrPlayerOpen nicht zurückgesetzt (für Re-Init bei Navigation)
function disposeVRPlayer(keepOpen) {
    try {
        // Speichere Lautstärke und Mute-Zustand bevor Player beendet wird
        if (player && player.volume) {
            savedVolume = player.volume();
            savedMuted = player.muted();
            localStorage.setItem('vrPlayerVolume', savedVolume);
            localStorage.setItem('vrPlayerMuted', savedMuted);
        }

        // Event-Listener entfernen
        document.removeEventListener('fullscreenchange', handleFullscreenChange);
        document.removeEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.removeEventListener('mozfullscreenchange', handleFullscreenChange);
        document.removeEventListener('MSFullscreenChange', handleFullscreenChange);

        if (player) {
            player.dispose();
            console.log("Player erfolgreich beendet");
        }
    } catch (error) {
        console.error("Fehler beim Beenden des Players:", error);
    } finally {
        player = null;

        if (!keepOpen) {
            currentVideoId = null;
            window._vrPlayerOpen = false;
            // Keyboard-Handler entfernen
            if (window._vrPlayerKeyHandler) {
                document.removeEventListener('keydown', window._vrPlayerKeyHandler, true);
                window._vrPlayerKeyHandler = null;
            }
        }
    }
}

// Exportiere Funktionen
window.initializeVRPlayer = initializeVRPlayer;
window.disposeVRPlayer = disposeVRPlayer;
window.registerPlayerErrorCallback = registerPlayerErrorCallback;
