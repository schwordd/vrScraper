let player = null;
let dotNetRef = null;
let currentVideoId = null;
// Speichere die Lautstärke in einer globalen Variable, Standard: 70%
let savedVolume = localStorage.getItem('vrPlayerVolume') ? parseFloat(localStorage.getItem('vrPlayerVolume')) : 0.7;

function registerPlayerErrorCallback(dotnetRef) {
    dotNetRef = dotnetRef;
    console.log("Callback-Referenz registriert");
}

function initializeVRPlayer(videoElementId, sourceUrl, sourceType, videoTitle, vrType, videoId) {
    console.log("Initialisiere VR-Player:", videoElementId, sourceUrl, vrType);
    currentVideoId = videoId;

    try {
        // Stelle sicher, dass kein vorheriger Player existiert
        if (player) {
            console.log("Bestehender Player gefunden, wird entfernt");
            disposeVRPlayer();
        }

        // Überprüfe, ob das Video-Element existiert
        let videoElement = document.getElementById(videoElementId);

        if (!videoElement) {
            console.error("Video-Element nicht gefunden:", videoElementId);

            // Versuche, das Element neu zu erstellen
            const container = document.querySelector('.video-container');
            if (container) {
                console.log("Erstelle Video-Element neu");
                videoElement = document.createElement('video');
                videoElement.id = videoElementId;
                videoElement.className = 'video-js vjs-big-play-centered';
                container.appendChild(videoElement);
            } else {
                throw new Error("Video-Container nicht gefunden");
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
            muted: false,
            volume: savedVolume,
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

            // Stelle das gespeicherte Volumen ein
            player.volume(savedVolume);

            // VR-Plugin initialisieren
            this.vr({
                projection: vrType === '360' ? '360' : '180_LR',
                forceCardboard: false,
                debug: false
            });

            // Event-Listener für Lautstärkenänderungen
            this.on('volumechange', function() {
                savedVolume = player.volume();
                localStorage.setItem('vrPlayerVolume', savedVolume);
                console.log('Lautstärke gespeichert:', savedVolume);
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
                        dotNetRef.invokeMethodAsync('OnPlayerError', error.message || 'Unbekannter Fehler beim Starten der Wiedergabe');
                    }
                });
        });

        // Fehlerbehandlung für Player
        player.on('error', function(error) {
            console.error('Player-Fehler:', error);
            if (dotNetRef) {
                const errorMsg = player.error_ ? player.error_.message : 'Unbekannter Fehler';
                dotNetRef.invokeMethodAsync('OnPlayerError', errorMsg);
            }
        });

        // Globaler Event-Listener für document fullscreenchange
        document.addEventListener('fullscreenchange', handleFullscreenChange);
        document.addEventListener('webkitfullscreenchange', handleFullscreenChange);
        document.addEventListener('mozfullscreenchange', handleFullscreenChange);
        document.addEventListener('MSFullscreenChange', handleFullscreenChange);

    } catch (error) {
        console.error('Fehler bei Player-Initialisierung:', error);
        if (dotNetRef) {
            dotNetRef.invokeMethodAsync('OnPlayerError', error.message || 'Fehler bei der Player-Initialisierung');
        }
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
function disposeVRPlayer() {
    try {
        // Speichere aktuelle Lautstärke bevor Player beendet wird
        if (player && player.volume) {
            savedVolume = player.volume();
            localStorage.setItem('vrPlayerVolume', savedVolume);
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
        currentVideoId = null;
    }
}

// Exportiere Funktionen
window.initializeVRPlayer = initializeVRPlayer;
window.disposeVRPlayer = disposeVRPlayer;
window.registerPlayerErrorCallback = registerPlayerErrorCallback;
