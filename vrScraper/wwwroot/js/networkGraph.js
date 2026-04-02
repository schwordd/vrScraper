window.networkGraph = {
    _canvas: null, _ctx: null, _sim: null, _nodes: null,
    _simLinks: null, _hoverLinks: null,
    _neighbors: null, _neighborLinks: null,
    _dotNetRef: null, _transform: null, _zoom: null,
    _hoverNode: null, _dragNode: null,
    _raf: null, _needsRedraw: true,

    init: function (containerId, data, dotNetRef) {
        this.dispose();
        this._dotNetRef = dotNetRef;

        var container = document.getElementById(containerId);
        if (!container) return;

        var canvas = document.createElement('canvas');
        canvas.style.width = '100%';
        canvas.style.height = '100%';
        container.innerHTML = '';
        container.appendChild(canvas);
        this._canvas = canvas;
        this._ctx = canvas.getContext('2d');
        this._transform = d3.zoomIdentity;

        this._resize(container);
        this._setupData(data);
        this._logStats();
        this._setupSimulation(true);
        this._logPostSimulation();
        this._setupInteraction(container);
        this._startRenderLoop();

        var self = this;
        this._resizeObserver = new ResizeObserver(function () {
            self._resize(container);
            self._needsRedraw = true;
        });
        this._resizeObserver.observe(container);
    },

    _logStats: function () {
        var stars = this._nodes.filter(function(n) { return n.type === 'star'; });
        var tags = this._nodes.filter(function(n) { return n.type === 'tag'; });

        console.group('[Graph] Data Summary');
        console.log('Canvas:', this._width, 'x', this._height);
        console.log('Stars:', stars.length, '| Tags:', tags.length, '| Total Nodes:', this._nodes.length);
        console.log('SimLinks:', this._simLinks.length, '| HoverLinks:', this._hoverLinks.length);

        // Links pro Tag
        var tagLinkCounts = {};
        var tagWeightSums = {};
        this._simLinks.forEach(function(l) {
            var tid = l.target.id;
            tagLinkCounts[tid] = (tagLinkCounts[tid] || 0) + 1;
            tagWeightSums[tid] = (tagWeightSums[tid] || 0) + (l.weight || 1);
        });

        console.group('Tag Clusters');
        tags.sort(function(a,b) { return (tagLinkCounts[b.id]||0) - (tagLinkCounts[a.id]||0); });
        tags.forEach(function(t) {
            var linked = tagLinkCounts[t.id] || 0;
            var totalWeight = tagWeightSums[t.id] || 0;
            var primaryStars = stars.filter(function(s) { return s.primaryTag === t.id; }).length;
            console.log(
                '  ' + t.label + ' (' + t.count + ' videos)',
                '→ ' + linked + ' linked stars',
                '| ' + primaryStars + ' primary',
                '| total weight: ' + totalWeight,
                '| color: ' + t.color
            );
        });
        console.groupEnd();

        // Stars per link count
        var starLinkCounts = {};
        this._simLinks.forEach(function(l) {
            var sid = l.source.id;
            starLinkCounts[sid] = (starLinkCounts[sid] || 0) + 1;
        });
        var linkDist = {};
        Object.values(starLinkCounts).forEach(function(c) {
            linkDist[c] = (linkDist[c] || 0) + 1;
        });
        console.log('Star link distribution:', linkDist);

        // Top stars by video count
        var topStars = stars.slice().sort(function(a,b) { return b.count - a.count; }).slice(0, 10);
        console.group('Top 10 Stars');
        topStars.forEach(function(s) {
            console.log('  ' + s.label, '(' + s.count + ' videos, ' + s.liked + ' liked)', '→ primary: ' + s.primaryTag, '| color: ' + s.color);
        });
        console.groupEnd();

        console.groupEnd();
    },

    _logPostSimulation: function () {
        var tags = this._nodes.filter(function(n) { return n.type === 'tag'; });
        console.group('[Graph] Post-Simulation Layout');

        // Tag-Positionen
        tags.forEach(function(t) {
            console.log('  ' + t.label, '→ pos(' + t.x.toFixed(0) + ', ' + t.y.toFixed(0) + ')');
        });

        // Spread-Metriken
        var xs = this._nodes.map(function(n) { return n.x; });
        var ys = this._nodes.map(function(n) { return n.y; });
        console.log('Bounds: X[' + Math.min.apply(null,xs).toFixed(0) + '..' + Math.max.apply(null,xs).toFixed(0) + ']',
            'Y[' + Math.min.apply(null,ys).toFixed(0) + '..' + Math.max.apply(null,ys).toFixed(0) + ']');

        // Cluster-Dichte: avg distance of stars to their primary tag
        var tagPositions = {};
        tags.forEach(function(t) { tagPositions[t.id] = { x: t.x, y: t.y }; });
        var stars = this._nodes.filter(function(n) { return n.type === 'star' && n.primaryTag; });
        var totalDist = 0, count = 0;
        stars.forEach(function(s) {
            var tp = tagPositions[s.primaryTag];
            if (tp) {
                var dx = s.x - tp.x, dy = s.y - tp.y;
                totalDist += Math.sqrt(dx*dx + dy*dy);
                count++;
            }
        });
        console.log('Avg star-to-primary-tag distance:', count ? (totalDist/count).toFixed(1) : 'N/A');

        // Tag-Tag Abstände
        console.group('Tag-Tag Distances');
        for (var i = 0; i < tags.length; i++) {
            for (var j = i+1; j < tags.length; j++) {
                var dx = tags[i].x - tags[j].x, dy = tags[i].y - tags[j].y;
                var dist = Math.sqrt(dx*dx + dy*dy);
                if (dist < 200) {
                    console.warn('  CLOSE: ' + tags[i].label + ' ↔ ' + tags[j].label + ': ' + dist.toFixed(0) + 'px');
                }
            }
        }
        console.groupEnd();

        console.groupEnd();
    },

    _resize: function (container) {
        var dpr = window.devicePixelRatio || 1;
        this._canvas.width = container.clientWidth * dpr;
        this._canvas.height = container.clientHeight * dpr;
        this._width = container.clientWidth;
        this._height = container.clientHeight;
        this._dpr = dpr;
    },

    _setupData: function (data) {
        this._nodes = data.nodes;
        this._simLinks = data.simLinks || [];
        this._hoverLinks = data.hoverLinks || [];

        var nodeMap = {};
        this._nodes.forEach(function (n) { nodeMap[n.id] = n; });

        function resolveLinks(links) {
            return links.filter(function (l) {
                if (typeof l.source === 'string') l.source = nodeMap[l.source];
                if (typeof l.target === 'string') l.target = nodeMap[l.target];
                return l.source && l.target;
            });
        }
        this._simLinks = resolveLinks(this._simLinks);
        this._hoverLinks = resolveLinks(this._hoverLinks);

        this._neighbors = {};
        this._neighborLinks = {};
        this._nodes.forEach(function (n) {
            this._neighbors[n.id] = new Set();
            this._neighborLinks[n.id] = [];
        }.bind(this));
        this._hoverLinks.forEach(function (l) {
            if (l.source && l.target) {
                this._neighbors[l.source.id].add(l.target.id);
                this._neighbors[l.target.id].add(l.source.id);
                this._neighborLinks[l.source.id].push(l);
                this._neighborLinks[l.target.id].push(l);
            }
        }.bind(this));
    },

    _setupSimulation: function (warmup) {
        var self = this;
        var cx = this._width / 2;
        var cy = this._height / 2;

        // Tags im Kreis vorpositionieren — stabile Startkonfiguration
        var tags = this._nodes.filter(function(n) { return n.type === 'tag'; });
        var radius = Math.min(cx, cy) * 0.6;
        tags.forEach(function(tag, i) {
            var angle = (2 * Math.PI * i) / tags.length - Math.PI / 2;
            tag.x = cx + radius * Math.cos(angle);
            tag.y = cy + radius * Math.sin(angle);
        });
        // Stars zufällig nahe ihrem Primary Tag positionieren
        var tagPos = {};
        tags.forEach(function(t) { tagPos[t.id] = { x: t.x, y: t.y }; });
        this._nodes.forEach(function(n) {
            if (n.type === 'star' && n.primaryTag && tagPos[n.primaryTag]) {
                var tp = tagPos[n.primaryTag];
                n.x = tp.x + (Math.random() - 0.5) * 100;
                n.y = tp.y + (Math.random() - 0.5) * 100;
            } else if (n.type === 'star') {
                n.x = cx + (Math.random() - 0.5) * 200;
                n.y = cy + (Math.random() - 0.5) * 200;
            }
        });

        this._sim = d3.forceSimulation(this._nodes)
            // Tags stoßen sich gegenseitig ab
            .force('charge', d3.forceManyBody()
                .strength(function (n) { return n.type === 'tag' ? -4000 : -5; })
                .distanceMax(2000)
            )
            // Custom Force: Stars mit unterschiedlichem Primary Tag stoßen sich ab
            .force('clusterRepel', function (alpha) {
                for (var i = 0; i < self._nodes.length; i++) {
                    var a = self._nodes[i];
                    if (a.type !== 'star' || !a.primaryTag) continue;
                    for (var j = i + 1; j < self._nodes.length; j++) {
                        var b = self._nodes[j];
                        if (b.type !== 'star' || !b.primaryTag) continue;
                        if (a.primaryTag === b.primaryTag) continue; // gleicher Cluster → ignorieren
                        var dx = a.x - b.x, dy = a.y - b.y;
                        var dist = Math.sqrt(dx * dx + dy * dy) || 1;
                        if (dist > 150) continue; // nur nahestehende abstoßen
                        var force = alpha * 30 / (dist * dist);
                        var fx = dx / dist * force;
                        var fy = dy / dist * force;
                        a.vx += fx; a.vy += fy;
                        b.vx -= fx; b.vy -= fy;
                    }
                }
            })
            // Star↔Tag Links ziehen Stars zu ihren Tags
            .force('link', d3.forceLink(this._simLinks)
                .distance(function (l) { return 20 / Math.sqrt(l.weight || 1); })
                .strength(function (l) { return Math.min(1.0, 0.15 + l.weight * 0.08); })
                .iterations(3)
            )
            // Starke zentrale Anziehung für alle — hält alles kompakt
            .force('x', d3.forceX(cx).strength(function (n) {
                return n.type === 'tag' ? 0.02 : 0.12;
            }))
            .force('y', d3.forceY(cy).strength(function (n) {
                return n.type === 'tag' ? 0.02 : 0.12;
            }))
            .force('collide', d3.forceCollide()
                .radius(function (n) { return self._nodeRadius(n) + 1; })
                .strength(0.4)
            )
            .alphaDecay(0.02)
            .velocityDecay(0.5);

        if (warmup) {
            this._sim.stop();
            for (var i = 0; i < 500; i++) this._sim.tick();
            this._sim.alpha(0).stop();
            this._fitToView();
            this._needsRedraw = true;
        } else {
            this._sim.on('tick', function () { self._needsRedraw = true; });
        }
    },

    _fitToView: function () {
        // Bounding Box berechnen
        var minX = Infinity, maxX = -Infinity, minY = Infinity, maxY = -Infinity;
        this._nodes.forEach(function (n) {
            if (n.x != null) {
                if (n.x < minX) minX = n.x;
                if (n.x > maxX) maxX = n.x;
                if (n.y < minY) minY = n.y;
                if (n.y > maxY) maxY = n.y;
            }
        });
        if (minX >= Infinity) return;

        var gw = maxX - minX || 1;
        var gh = maxY - minY || 1;
        var pad = 80;
        var scaleX = (this._width - pad * 2) / gw;
        var scaleY = (this._height - pad * 2) / gh;
        var scale = Math.min(scaleX, scaleY, 2.0);

        // Setze den Transform statt die Positionen zu verschieben
        var graphCx = (minX + maxX) / 2;
        var graphCy = (minY + maxY) / 2;
        this._transform = d3.zoomIdentity
            .translate(this._width / 2, this._height / 2)
            .scale(scale)
            .translate(-graphCx, -graphCy);

        console.log('[Graph] FitToView: graph center(' + graphCx.toFixed(0) + ',' + graphCy.toFixed(0) +
            ') scale=' + scale.toFixed(3) + ' bounds=' + gw.toFixed(0) + 'x' + gh.toFixed(0));
    },

    _nodeRadius: function (node) {
        var r = Math.sqrt(node.val || 1) * 1.4;
        return node.type === 'tag' ? Math.max(6, Math.min(r, 25)) : Math.max(2, Math.min(r, 14));
    },

    _startRenderLoop: function () {
        var self = this;
        function loop() {
            if (!self._canvas) return;
            if (self._needsRedraw) {
                self._render();
                self._needsRedraw = false;
            }
            self._raf = requestAnimationFrame(loop);
        }
        loop();
    },

    _render: function () {
        var ctx = this._ctx;
        var t = this._transform;
        var dpr = this._dpr;
        var w = this._width;
        var h = this._height;
        var nodes = this._nodes;
        var hover = this._hoverNode;
        var neighborIds = hover ? this._neighbors[hover.id] : null;
        var self = this;

        ctx.save();
        ctx.setTransform(dpr, 0, 0, dpr, 0, 0);
        ctx.clearRect(0, 0, w, h);

        // Hintergrund
        ctx.fillStyle = '#fafbfc';
        ctx.fillRect(0, 0, w, h);

        ctx.translate(t.x, t.y);
        ctx.scale(t.k, t.k);
        var gs = t.k; // globalScale

        // Hover-Links
        if (hover && this._neighborLinks[hover.id]) {
            ctx.globalAlpha = 0.6;
            this._neighborLinks[hover.id].forEach(function (l) {
                if (!l.source || !l.target) return;
                ctx.beginPath();
                ctx.moveTo(l.source.x, l.source.y);
                ctx.lineTo(l.target.x, l.target.y);
                var isCross = l.source.type !== l.target.type;
                ctx.strokeStyle = isCross ? (l.target.color || l.source.color || '#94a3b8') : 'rgba(100,116,139,0.5)';
                ctx.lineWidth = (Math.sqrt(l.weight || 1) * 0.5 + 0.3) / gs;
                ctx.stroke();
            });
            ctx.globalAlpha = 1.0;
        }

        // Nodes zeichnen — erst Stars, dann Tags (Tags on top)
        var stars = [], tags = [];
        for (var i = 0; i < nodes.length; i++) {
            if (nodes[i].x == null) continue;
            if (nodes[i].type === 'tag') tags.push(nodes[i]);
            else stars.push(nodes[i]);
        }

        // Stars
        for (i = 0; i < stars.length; i++) {
            var node = stars[i];
            var r = self._nodeRadius(node);
            var isHovered = node === hover;
            var isNeighbor = neighborIds && neighborIds.has(node.id);
            var dimmed = hover && !isHovered && !isNeighbor;

            ctx.globalAlpha = dimmed ? 0.08 : 1.0;
            ctx.beginPath();
            ctx.arc(node.x, node.y, r, 0, 2 * Math.PI);
            ctx.fillStyle = node.color || '#d97706';
            if (isHovered) {
                ctx.shadowColor = ctx.fillStyle;
                ctx.shadowBlur = 12 / gs;
            }
            ctx.fill();
            ctx.shadowBlur = 0;

            if (isHovered || isNeighbor) {
                ctx.strokeStyle = 'rgba(0,0,0,0.5)';
                ctx.lineWidth = (isHovered ? 2 : 1) / gs;
                ctx.stroke();

                // Label
                var fontSize = (isHovered ? 13 : 10) / gs;
                ctx.font = 'bold ' + fontSize + 'px system-ui, sans-serif';
                ctx.textAlign = 'center';
                ctx.textBaseline = 'top';
                var text = node.label + ' (' + node.count + ')';
                var tw = ctx.measureText(text).width;
                var ty = node.y + r + 2 / gs;
                var pad = 2 / gs;
                ctx.fillStyle = 'rgba(0,0,0,0.8)';
                ctx.fillRect(node.x - tw/2 - pad, ty - pad/2, tw + pad*2, fontSize + pad*2);
                ctx.fillStyle = '#fff';
                ctx.fillText(text, node.x, ty + pad/2);
            }
            ctx.globalAlpha = 1.0;
        }

        // Tags — immer mit Label, größer, prominent
        for (i = 0; i < tags.length; i++) {
            var tag = tags[i];
            var tr = self._nodeRadius(tag);
            var tagHovered = tag === hover;
            var tagNeighbor = neighborIds && neighborIds.has(tag.id);
            var tagDimmed = hover && !tagHovered && !tagNeighbor;

            ctx.globalAlpha = tagDimmed ? 0.15 : 1.0;

            // Halo
            ctx.beginPath();
            ctx.arc(tag.x, tag.y, tr + 4 / gs, 0, 2 * Math.PI);
            ctx.fillStyle = tag.color + '20'; // 12% alpha
            ctx.fill();

            // Kreis
            ctx.beginPath();
            ctx.arc(tag.x, tag.y, tr, 0, 2 * Math.PI);
            ctx.fillStyle = tag.color || '#0284c7';
            if (tagHovered) {
                ctx.shadowColor = ctx.fillStyle;
                ctx.shadowBlur = 15 / gs;
            }
            ctx.fill();
            ctx.shadowBlur = 0;

            // Weißer Rand
            ctx.strokeStyle = '#ffffff';
            ctx.lineWidth = 2 / gs;
            ctx.stroke();

            // Label — immer sichtbar
            var tagFontSize = Math.max(10, 14) / gs;
            ctx.font = 'bold ' + tagFontSize + 'px system-ui, sans-serif';
            ctx.textAlign = 'center';
            ctx.textBaseline = 'top';
            var tagText = tag.label;
            var tagTw = ctx.measureText(tagText).width;
            var tagTy = tag.y + tr + 3 / gs;
            var tagPad = 3 / gs;
            ctx.fillStyle = tag.color || '#0284c7';
            ctx.fillRect(tag.x - tagTw/2 - tagPad, tagTy - tagPad/2, tagTw + tagPad*2, tagFontSize + tagPad*2);
            ctx.fillStyle = '#ffffff';
            ctx.fillText(tagText, tag.x, tagTy + tagPad/2);

            ctx.globalAlpha = 1.0;
        }

        ctx.restore();
    },

    _setupInteraction: function (container) {
        var self = this;
        var canvas = this._canvas;

        this._zoom = d3.zoom()
            .scaleExtent([0.02, 15])
            .on('zoom', function (event) {
                self._transform = event.transform;
                self._needsRedraw = true;
            });

        var sel = d3.select(canvas);
        sel.call(this._zoom);

        // Initialen transform setzen (nach fitToView)
        if (this._transform !== d3.zoomIdentity) {
            sel.call(this._zoom.transform, this._transform);
        }

        // Hover — store references for cleanup in dispose()
        this._onMouseMove = function (e) {
            if (self._dragNode) return;
            var rect = canvas.getBoundingClientRect();
            var mx = e.clientX - rect.left;
            var my = e.clientY - rect.top;
            var t = self._transform;
            var wx = (mx - t.x) / t.k;
            var wy = (my - t.y) / t.k;

            var found = null;
            var minDist = Infinity;
            for (var i = 0; i < self._nodes.length; i++) {
                var n = self._nodes[i];
                if (n.x == null) continue;
                var dx = n.x - wx, dy = n.y - wy;
                var dist = dx * dx + dy * dy;
                var r = self._nodeRadius(n) + 2;
                if (dist < r * r && dist < minDist) {
                    minDist = dist;
                    found = n;
                }
            }
            if (found !== self._hoverNode) {
                self._hoverNode = found;
                canvas.style.cursor = found ? 'pointer' : 'default';
                self._needsRedraw = true;
            }
        };
        canvas.addEventListener('mousemove', this._onMouseMove);

        this._onMouseLeave = function () {
            if (self._hoverNode) {
                self._hoverNode = null;
                canvas.style.cursor = 'default';
                self._needsRedraw = true;
            }
        };
        canvas.addEventListener('mouseleave', this._onMouseLeave);

        this._onClick = function () {
            if (self._hoverNode && self._dotNetRef) {
                self._dotNetRef.invokeMethodAsync('OnNodeClicked', self._hoverNode.id);
            }
        };
        canvas.addEventListener('click', this._onClick);

        // Drag
        sel.call(d3.drag()
            .subject(function (event) {
                var t = self._transform;
                var wx = (event.x - t.x) / t.k;
                var wy = (event.y - t.y) / t.k;
                for (var i = 0; i < self._nodes.length; i++) {
                    var n = self._nodes[i];
                    if (n.x == null) continue;
                    var dx = n.x - wx, dy = n.y - wy;
                    if (dx*dx + dy*dy < self._nodeRadius(n) * self._nodeRadius(n)) {
                        n.x = t.x + n.x * t.k;
                        n.y = t.y + n.y * t.k;
                        return n;
                    }
                }
                return null;
            })
            .on('start', function (event) {
                if (!event.subject) return;
                self._dragNode = event.subject;
                if (!event.active) self._sim.alphaTarget(0.3).restart();
                var t = self._transform;
                event.subject.fx = (event.x - t.x) / t.k;
                event.subject.fy = (event.y - t.y) / t.k;
            })
            .on('drag', function (event) {
                if (!self._dragNode) return;
                var t = self._transform;
                self._dragNode.fx = (event.x - t.x) / t.k;
                self._dragNode.fy = (event.y - t.y) / t.k;
                self._needsRedraw = true;
            })
            .on('end', function (event) {
                if (!self._dragNode) return;
                if (!event.active) self._sim.alphaTarget(0);
                self._dragNode.fx = null;
                self._dragNode.fy = null;
                self._dragNode = null;
            })
        );
    },

    update: function (data) {
        if (!this._canvas) return;
        this._hoverNode = null;
        this._setupData(data);
        this._logStats();
        this._sim.stop();
        this._setupSimulation(true);
        this._logPostSimulation();
        // Sync transform to d3-zoom
        if (this._zoom && this._transform !== d3.zoomIdentity) {
            d3.select(this._canvas).call(this._zoom.transform, this._transform);
        }
    },

    dispose: function () {
        if (this._raf) { cancelAnimationFrame(this._raf); this._raf = null; }
        if (this._resizeObserver) { this._resizeObserver.disconnect(); this._resizeObserver = null; }
        if (this._sim) { this._sim.stop(); this._sim = null; }
        if (this._canvas) {
            if (this._onMouseMove) this._canvas.removeEventListener('mousemove', this._onMouseMove);
            if (this._onMouseLeave) this._canvas.removeEventListener('mouseleave', this._onMouseLeave);
            if (this._onClick) this._canvas.removeEventListener('click', this._onClick);
            d3.select(this._canvas).on('.zoom', null);
            d3.select(this._canvas).on('.drag', null);
            if (this._canvas.parentNode) this._canvas.parentNode.removeChild(this._canvas);
        }
        this._onMouseMove = null; this._onMouseLeave = null; this._onClick = null;
        this._canvas = null; this._ctx = null; this._nodes = null;
        this._simLinks = null; this._hoverLinks = null;
        this._neighbors = null; this._neighborLinks = null;
        this._dotNetRef = null; this._hoverNode = null;
        this._dragNode = null; this._transform = null; this._zoom = null;
    }
};
