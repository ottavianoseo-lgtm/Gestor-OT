window.mapInterop = {
    map: null,
    dashboardMap: null,
    lotLayers: {},
    selectedLayer: null,
    dotNetRef: null,
    drawControl: null,
    drawnItems: null,
    editingLotId: null,

    initDashboardMap: function (containerId, centerLat, centerLng, zoom) {
        if (this.dashboardMap) {
            this.dashboardMap.remove();
            this.dashboardMap = null;
        }

        this.dashboardMap = L.map(containerId, {
            preferCanvas: true,
            zoomControl: true,
            attributionControl: false,
            dragging: true,
            scrollWheelZoom: true,
            doubleClickZoom: true,
            boxZoom: true,
            keyboard: true,
            touchZoom: true
        }).setView([centerLat, centerLng], zoom);

        var satellite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19
        });
        var streets = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            maxZoom: 19
        });
        var topo = L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
            maxZoom: 17
        });
        var hybridLabels = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
            maxZoom: 19
        });

        satellite.addTo(this.dashboardMap);
        hybridLabels.addTo(this.dashboardMap);

        var baseMaps = {
            "Satélite": satellite,
            "Calles y Rutas": streets,
            "Topográfico": topo
        };
        var overlays = {
            "Localidades y Límites": hybridLabels
        };
        L.control.layers(baseMaps, overlays, { position: 'topright', collapsed: true }).addTo(this.dashboardMap);

        this.dashboardMap.on('baselayerchange', function (e) {
            if (e.name === 'Satélite') {
                if (!this.hasLayer(hybridLabels)) hybridLabels.addTo(this);
            } else {
                if (this.hasLayer(hybridLabels)) this.removeLayer(hybridLabels);
            }
        });

        var self = this;
        setTimeout(function () {
            if (self.dashboardMap) {
                self.dashboardMap.invalidateSize();
            }
        }, 300);

        return true;
    },

    addDashboardLotPolygon: function (coordinatesJson, status) {
        if (!this.dashboardMap) return false;
        try {
            var coordinates = JSON.parse(coordinatesJson);
            var isActive = status === 'Active';
            var color = isActive ? '#2ECC71' : '#E74C3C';
            L.polygon(coordinates, {
                color: color,
                fillColor: color,
                fillOpacity: 0.25,
                weight: 1.5
            }).addTo(this.dashboardMap);
            return true;
        } catch (e) {
            return false;
        }
    },

    fitDashboardLots: function () {
        if (!this.dashboardMap) return false;
        var bounds = [];
        this.dashboardMap.eachLayer(function (layer) {
            if (layer.getBounds) {
                bounds.push(layer.getBounds());
            }
        });
        if (bounds.length > 0) {
            var group = new L.LatLngBounds(bounds[0].getSouthWest(), bounds[0].getNorthEast());
            bounds.forEach(function (b) { group.extend(b); });
            this.dashboardMap.fitBounds(group, { padding: [100, 100], maxZoom: 14 });
        }
        return true;
    },

    destroyDashboardMap: function () {
        if (this.dashboardMap) {
            this.dashboardMap.remove();
            this.dashboardMap = null;
        }
    },

    initMap: function (containerId, centerLat, centerLng, zoom) {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }

        this.lotLayers = {};
        this.selectedLayer = null;
        this.editingLotId = null;

        this.map = L.map(containerId, {
            preferCanvas: true,
            zoomControl: true
        }).setView([centerLat, centerLng], zoom);

        var satellite = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            attribution: 'Tiles &copy; Esri',
            maxZoom: 19
        });
        var streets = L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenStreetMap',
            maxZoom: 19
        });
        var topo = L.tileLayer('https://{s}.tile.opentopomap.org/{z}/{x}/{y}.png', {
            attribution: '&copy; OpenTopoMap',
            maxZoom: 17
        });
        var hybridLabels = L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
            attribution: '',
            maxZoom: 19
        });

        satellite.addTo(this.map);
        hybridLabels.addTo(this.map);

        var baseMaps = {
            "Satélite": satellite,
            "Calles y Rutas": streets,
            "Topográfico": topo
        };
        var overlays = {
            "Localidades y Límites": hybridLabels
        };
        L.control.layers(baseMaps, overlays, { position: 'topright', collapsed: true }).addTo(this.map);

        this.map.on('baselayerchange', function (e) {
            if (e.name === 'Satélite') {
                if (!this.hasLayer(hybridLabels)) hybridLabels.addTo(this);
            } else {
                if (this.hasLayer(hybridLabels)) this.removeLayer(hybridLabels);
            }
        });

        this.drawnItems = new L.FeatureGroup();
        this.map.addLayer(this.drawnItems);

        if (typeof L.Control.Draw !== 'undefined') {
            this.drawControl = new L.Control.Draw({
                position: 'topleft',
                draw: {
                    polygon: {
                        allowIntersection: false,
                        showArea: true,
                        shapeOptions: {
                            color: '#E74C3C',
                            fillColor: '#E74C3C',
                            fillOpacity: 0.3,
                            weight: 3
                        }
                    },
                    polyline: false,
                    circle: false,
                    rectangle: false,
                    marker: false,
                    circlemarker: false
                },
                edit: {
                    featureGroup: this.drawnItems,
                    edit: true,
                    remove: true
                }
            });
            this.map.addControl(this.drawControl);

            this.map.on(L.Draw.Event.CREATED, (e) => {
                var layer = e.layer;
                this.drawnItems.addLayer(layer);
                var wkt = this.layerToWkt(layer);
                var area = this.calculateArea(layer);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnPolygonDrawn', wkt, area);
                }
            });

            this.map.on(L.Draw.Event.EDITED, (e) => {
                var layers = e.layers;
                layers.eachLayer((layer) => {
                    var wkt = this.layerToWkt(layer);
                    var area = this.calculateArea(layer);
                    if (this.dotNetRef) {
                        this.dotNetRef.invokeMethodAsync('OnPolygonEdited', wkt, area);
                    }
                });
            });

            this.map.on(L.Draw.Event.DELETED, (e) => {
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnPolygonDeleted');
                }
            });
        }

        return true;
    },

    setDotNetRef: function (ref) {
        this.dotNetRef = ref;
    },

    addLotPolygon: function (lotId, lotName, status, area, fieldName, coordinatesJson) {
        if (!this.map) return false;

        try {
            const coordinates = JSON.parse(coordinatesJson);
            const isActive = status === 'Active';
            const color = isActive ? '#2ECC71' : '#E74C3C';

            const polygon = L.polygon(coordinates, {
                color: color,
                fillColor: color,
                fillOpacity: 0.35,
                weight: 2
            }).addTo(this.map);

            polygon.bindPopup(`
                <div style="min-width: 180px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif;">
                    <strong style="font-size: 14px; display: block; margin-bottom: 8px;">${lotName}</strong>
                    <div style="font-size: 12px; color: #666; margin-bottom: 8px;">${fieldName}</div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 4px;">
                        <span style="color: #888;">Estado</span>
                        <span style="color: ${color}; font-weight: 600;">${isActive ? 'Activo' : 'Inactivo'}</span>
                    </div>
                    <div style="display: flex; justify-content: space-between;">
                        <span style="color: #888;">Área</span>
                        <span style="font-weight: 600;">${area.toFixed(2)} ha</span>
                    </div>
                </div>
            `);

            polygon.on('click', () => {
                this.highlightLot(lotId);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnLotSelected', lotId);
                }
            });

            this.lotLayers[lotId] = polygon;
            return true;
        } catch (e) {
            console.error('Error adding lot polygon:', e);
            return false;
        }
    },

    highlightLot: function (lotId) {
        if (this.selectedLayer) {
            this.selectedLayer.setStyle({ weight: 2, fillOpacity: 0.35 });
        }

        if (this.lotLayers[lotId]) {
            this.selectedLayer = this.lotLayers[lotId];
            this.selectedLayer.setStyle({ weight: 4, fillOpacity: 0.55 });
        }
    },

    centerOnLot: function (lotId) {
        if (!this.map || !this.lotLayers[lotId]) return false;

        const layer = this.lotLayers[lotId];
        this.map.fitBounds(layer.getBounds(), { padding: [80, 80], maxZoom: 16 });
        this.highlightLot(lotId);
        layer.openPopup();
        return true;
    },

    fitAllLots: function () {
        if (!this.map) return false;

        const layerIds = Object.keys(this.lotLayers);
        if (layerIds.length === 0) return false;

        const group = L.featureGroup(Object.values(this.lotLayers));
        this.map.fitBounds(group.getBounds(), { padding: [50, 50] });
        return true;
    },

    clearLots: function () {
        if (!this.map) return;

        Object.values(this.lotLayers).forEach(layer => {
            this.map.removeLayer(layer);
        });
        this.lotLayers = {};
        this.selectedLayer = null;
    },

    clearDrawn: function () {
        if (this.drawnItems) {
            this.drawnItems.clearLayers();
        }
    },

    invalidateSize: function () {
        if (this.map) {
            setTimeout(() => this.map.invalidateSize(), 100);
        }
    },

    layerToWkt: function (layer) {
        var latlngs = layer.getLatLngs()[0];
        var coords = latlngs.map(function (ll) {
            return ll.lng.toFixed(8) + ' ' + ll.lat.toFixed(8);
        });
        coords.push(coords[0]);
        return 'POLYGON ((' + coords.join(', ') + '))';
    },

    calculateArea: function (layer) {
        if (!layer || !layer.getLatLngs) return 0;
        var latlngs = layer.getLatLngs()[0];
        var area = 0;
        for (var i = 0; i < latlngs.length; i++) {
            var j = (i + 1) % latlngs.length;
            var xi = latlngs[i].lng * Math.PI / 180;
            var yi = latlngs[i].lat * Math.PI / 180;
            var xj = latlngs[j].lng * Math.PI / 180;
            var yj = latlngs[j].lat * Math.PI / 180;
            area += (xj - xi) * (2 + Math.sin(yi) + Math.sin(yj));
        }
        area = Math.abs(area * 6371000 * 6371000 / 2);
        return area / 10000;
    },

    parseGeoJsonFile: function (geoJsonString) {
        try {
            var geojson = JSON.parse(geoJsonString);
            var results = [];

            var features = [];
            if (geojson.type === 'FeatureCollection') {
                features = geojson.features || [];
            } else if (geojson.type === 'Feature') {
                features = [geojson];
            } else if (geojson.type === 'Polygon' || geojson.type === 'MultiPolygon') {
                features = [{ type: 'Feature', geometry: geojson, properties: {} }];
            }

            features.forEach(function (feature) {
                if (feature.geometry && feature.geometry.type === 'Polygon') {
                    var coords = feature.geometry.coordinates[0];
                    var wktCoords = coords.map(function (c) {
                        return c[0].toFixed(8) + ' ' + c[1].toFixed(8);
                    });
                    var wkt = 'POLYGON ((' + wktCoords.join(', ') + '))';
                    var name = (feature.properties && feature.properties.name) ||
                        (feature.properties && feature.properties.Name) || '';
                    results.push({ wkt: wkt, name: name });
                }
            });

            return JSON.stringify(results);
        } catch (e) {
            console.error('Error parsing GeoJSON:', e);
            return '[]';
        }
    },

    parseKmlFile: function (kmlString) {
        try {
            var parser = new DOMParser();
            var kml = parser.parseFromString(kmlString, 'text/xml');
            var results = [];

            var placemarks = kml.getElementsByTagName('Placemark');
            for (var i = 0; i < placemarks.length; i++) {
                var pm = placemarks[i];
                var nameEl = pm.getElementsByTagName('name')[0];
                var name = nameEl ? nameEl.textContent : '';

                var coordsEl = pm.getElementsByTagName('coordinates')[0];
                if (!coordsEl) continue;

                var coordsText = coordsEl.textContent.trim();
                var points = coordsText.split(/\s+/).filter(function (s) { return s.length > 0; });
                var wktCoords = points.map(function (p) {
                    var parts = p.split(',');
                    return parseFloat(parts[0]).toFixed(8) + ' ' + parseFloat(parts[1]).toFixed(8);
                });

                if (wktCoords.length >= 3) {
                    if (wktCoords[0] !== wktCoords[wktCoords.length - 1]) {
                        wktCoords.push(wktCoords[0]);
                    }
                    var wkt = 'POLYGON ((' + wktCoords.join(', ') + '))';
                    results.push({ wkt: wkt, name: name });
                }
            }

            return JSON.stringify(results);
        } catch (e) {
            console.error('Error parsing KML:', e);
            return '[]';
        }
    },

    addImportedPolygon: function (wkt, name) {
        if (!this.map || !wkt) return false;
        try {
            var match = wkt.match(/POLYGON\s*\(\((.+)\)\)/i);
            if (!match) return false;

            var coords = match[1].split(',').map(function (pair) {
                var parts = pair.trim().split(/\s+/);
                return [parseFloat(parts[1]), parseFloat(parts[0])];
            });

            var polygon = L.polygon(coords, {
                color: '#9B59B6',
                fillColor: '#9B59B6',
                fillOpacity: 0.3,
                weight: 3,
                dashArray: '5,5'
            }).addTo(this.map);

            polygon.bindPopup('<strong>' + (name || 'Polígono importado') + '</strong>');
            this.drawnItems.addLayer(polygon);
            this.map.fitBounds(polygon.getBounds(), { padding: [50, 50] });
            return true;
        } catch (e) {
            console.error('Error adding imported polygon:', e);
            return false;
        }
    }
};