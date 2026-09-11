window.mapInterop = {
    map: null,
    dashboardMap: null,
    lotLayers: {},
    fieldLayers: {},
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
        this.fieldLayers = {};
        this.selectedLayer = null;
        this.editingLotId = null;
        this.lotFilter = null;
        this.selectedFieldId = null;
        this.symbology = 'status';

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

        this.map.on('zoomend', () => this.updateLabelVisibility());
        this.updateLabelVisibility();

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

    viewMode: 'interactive',
    selectedFieldId: null,
    lotFilter: null,
    symbology: 'status',

    LABEL_MIN_ZOOM: 13,

    // ------------------------------------------------------------------
    // Estilo y visibilidad de las capas, resueltos en un unico lugar.
    //
    // Antes cada operacion decidia por su cuenta y con sus propias constantes:
    // el alta de la capa, highlightLot, selectField y setGisViewMode. La ultima
    // en correr pisaba a las anteriores, asi que pasar el mouse por el lote
    // seleccionado lo apagaba y cambiar de modo de vista revivia lo filtrado.
    // Ahora esas operaciones cambian estado y llaman a estas funciones.
    // ------------------------------------------------------------------

    lotBaseColor: function (layer) {
        // El color por cultivo lo calcula el servidor y viaja con el feature: la leyenda usa
        // el mismo valor, asi que no pueden discrepar.
        if (this.symbology === 'crop') return layer._cropColor || '#7F8C8D';
        return layer._status === 'Active' ? '#2ECC71' : '#E74C3C';
    },

    // OT-34: 'status' (el de siempre) o 'crop'. Solo cambia el color, no que se ve.
    setSymbology: function (mode) {
        this.symbology = mode;
        this.applyLayerVisibility();
    },

    // Cuanto se destaca un lote. El color no se toca aca: sale de lotBaseColor.
    lotEmphasis: function (layer) {
        if (layer === this.selectedLayer) return { weight: 4, fillOpacity: 0.65 };
        if (this.selectedFieldId && layer._fieldId === this.selectedFieldId) return { weight: 2.5, fillOpacity: 0.55 };
        return { weight: 2, fillOpacity: 0.35 };
    },

    applyLotStyle: function (layer) {
        const color = this.lotBaseColor(layer);
        const emphasis = this.lotEmphasis(layer);

        // El hover suma un delta sobre lo que corresponda, nunca reemplaza: por eso
        // al salir no hace falta acordarse del estilo previo, se vuelve a calcular.
        layer.setStyle({
            color: color,
            fillColor: color,
            weight: emphasis.weight + (layer._hovered ? 2 : 0),
            fillOpacity: emphasis.fillOpacity + (layer._hovered ? 0.15 : 0)
        });
    },

    applyFieldStyle: function (fieldId, layer) {
        if (fieldId === this.selectedFieldId) {
            layer.setStyle({ weight: 4, fillOpacity: 0.15, color: '#16A085' });
        } else if (this.selectedFieldId && this.viewMode === 'interactive') {
            layer.setStyle({ weight: 1.5, fillOpacity: 0.1, color: '#7F8C8D' });
        } else if (this.viewMode === 'fields') {
            layer.setStyle({ weight: 2.5, fillOpacity: 0.35, color: '#16A085' });
        } else {
            layer.setStyle({ weight: 2, fillOpacity: 0.25, color: '#16A085' });
        }
    },

    // Un lote se ve si lo pide el modo de vista Y pasa el filtro. Las dos condiciones
    // juntas y en un solo lado: separadas, la ultima en evaluarse ganaba.
    isLotVisible: function (layer) {
        if (this.lotFilter && !this.lotFilter.has(layer._lotId)) return false;
        if (this.viewMode === 'lots') return true;
        return !!(this.selectedFieldId && layer._fieldId === this.selectedFieldId);
    },

    isFieldVisible: function () {
        return this.viewMode !== 'lots';
    },

    setLayerPresence: function (layer, shouldBeVisible) {
        const isOn = this.map.hasLayer(layer);
        if (shouldBeVisible && !isOn) layer.addTo(this.map);
        else if (!shouldBeVisible && isOn) this.map.removeLayer(layer);
    },

    applyLayerVisibility: function () {
        if (!this.map) return;

        Object.keys(this.fieldLayers).forEach(fId => {
            const layer = this.fieldLayers[fId];
            this.setLayerPresence(layer, this.isFieldVisible());
            if (this.map.hasLayer(layer)) this.applyFieldStyle(fId, layer);
        });

        Object.values(this.lotLayers).forEach(layer => {
            this.setLayerPresence(layer, this.isLotVisible(layer));
            if (this.map.hasLayer(layer)) this.applyLotStyle(layer);
        });
    },

    // Las etiquetas se prenden y apagan con una clase en el contenedor: iterar N
    // tooltips en cada zoom es justamente lo que el umbral busca evitar.
    updateLabelVisibility: function () {
        if (!this.map) return;
        const container = this.map.getContainer();
        if (container) {
            container.classList.toggle('lot-labels-hidden', this.map.getZoom() < this.LABEL_MIN_ZOOM);
        }
    },

    setDotNetRef: function (ref) {
        this.dotNetRef = ref;
    },

    addLotPolygon: function (lotId, lotName, status, area, fieldName, coordinatesJson, fieldId, cropName, cropColor) {
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
            });

            polygon._lotId = String(lotId).toLowerCase();
            polygon._fieldId = fieldId;
            polygon._status = status;
            polygon._cropName = cropName || null;
            polygon._cropColor = cropColor || null;

            polygon.bindTooltip(lotName || '', {
                permanent: true,
                direction: 'center',
                className: 'lot-label'
            });

            this.setLayerPresence(polygon, this.isLotVisible(polygon));
            if (this.map.hasLayer(polygon)) this.applyLotStyle(polygon);

            const popupContent = `
                <div style="min-width: 200px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #1a1a2e;">
                    <strong style="font-size: 14px; display: block; margin-bottom: 6px;">${lotName}</strong>
                    <div style="font-size: 12px; color: #555; margin-bottom: 10px;">${fieldName || '—'}</div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px;">
                        <span style="color: #888;">Estado</span>
                        <span style="color: ${color}; font-weight: 600;">${isActive ? 'Activo' : 'Inactivo'}</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 12px;">
                        <span style="color: #888;">Área</span>
                        <span style="font-weight: 600;">${area.toFixed(2)} ha</span>
                    </div>
                    <a href="/lots?editId=${lotId}"
                       style="display: block; text-align: center; background: #E74C3C; color: #fff; border-radius: 6px; padding: 6px 12px; font-size: 12px; font-weight: 600; text-decoration: none; cursor: pointer;"
                       id="gis-edit-btn-${lotId}">
                        Ver / Editar Lote
                    </a>
                </div>
            `;
            polygon.bindPopup(popupContent, { maxWidth: 240 });

            polygon.on('click', () => {
                this.highlightLot(lotId);
                if (this.dotNetRef) {
                    this.dotNetRef.invokeMethodAsync('OnLotSelected', lotId);
                }
            });

            polygon.on('mouseover', () => {
                polygon._hovered = true;
                this.applyLotStyle(polygon);
            });

            polygon.on('mouseout', () => {
                polygon._hovered = false;
                this.applyLotStyle(polygon);
            });

            this.lotLayers[lotId] = polygon;
            return true;
        } catch (e) {
            console.error('Error adding lot polygon:', e);
            return false;
        }
    },

    highlightLot: function (lotId) {
        const target = this.lotLayers[lotId];
        if (!target) return;

        const previous = this.selectedLayer;
        this.selectedLayer = target;

        // Al anterior se le recalcula el estilo en vez de devolverlo a una constante:
        // si pertenece al campo seleccionado tiene que volver a su estilo de campo
        // activo, no al base.
        if (previous && previous !== target) this.applyLotStyle(previous);

        // Seleccionar un lote lo muestra aunque el modo de vista lo tuviera oculto.
        if (!this.map.hasLayer(target)) target.addTo(this.map);
        this.applyLotStyle(target);
    },

    centerOnLot: function (lotId) {
        if (!this.map || !this.lotLayers[lotId]) return false;

        const layer = this.lotLayers[lotId];
        if (!this.map.hasLayer(layer)) {
            layer.addTo(this.map);
        }
        const bounds = layer.getBounds();
        if (bounds && bounds.isValid()) {
            this.map.fitBounds(bounds, { padding: [80, 80], maxZoom: 16 });
        }
        this.highlightLot(lotId);
        layer.openPopup();
        return true;
    },

    fitAllLots: function () {
        if (!this.map) return false;

        const layers = Object.values(this.lotLayers).concat(Object.values(this.fieldLayers));
        if (layers.length === 0) return false;

        const group = L.featureGroup(layers);
        const bounds = group.getBounds();
        if (bounds && bounds.isValid()) {
            this.map.fitBounds(bounds, { padding: [50, 50] });
            return true;
        }
        return false;
    },

    clearLots: function () {
        if (!this.map) return;

        Object.values(this.lotLayers).forEach(layer => {
            this.map.removeLayer(layer);
        });
        this.lotLayers = {};
        this.selectedLayer = null;
    },

    addFieldPolygon: function (fieldId, fieldName, lotsCount, area, coordinatesJson) {
        if (!this.map) return false;

        try {
            const coordinates = JSON.parse(coordinatesJson);
            const color = '#16A085'; // Clean Ocean Teal for unified Field polygon

            const polygon = L.polygon(coordinates, {
                color: color,
                fillColor: color,
                fillOpacity: 0.22,
                weight: 2.5
            });

            this.setLayerPresence(polygon, this.isFieldVisible());
            if (this.map.hasLayer(polygon)) this.applyFieldStyle(fieldId, polygon);

            polygon.bindTooltip(`🏡 <strong>${fieldName}</strong> (${area.toFixed(1)} ha - ${lotsCount} lotes)`, {
                sticky: true,
                direction: 'top'
            });

            const popupContent = `
                <div style="min-width: 200px; font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', sans-serif; color: #1a1a2e;">
                    <strong style="font-size: 14px; display: block; margin-bottom: 6px; color: #16A085;">🏡 Campo: ${fieldName}</strong>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 4px; font-size: 12px;">
                        <span style="color: #888;">Lotes</span>
                        <span style="font-weight: 600;">${lotsCount} lotes</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-bottom: 12px; font-size: 12px;">
                        <span style="color: #888;">Superficie Total</span>
                        <span style="font-weight: 600;">${area.toFixed(2)} ha</span>
                    </div>
                    <button style="width: 100%; border: none; background: #16A085; color: #fff; border-radius: 6px; padding: 7px 12px; font-size: 12px; font-weight: 600; cursor: pointer;"
                            onclick="window.mapInterop.selectField('${fieldId}')">
                        🔍 Ver Lotes del Campo
                    </button>
                </div>
            `;
            polygon.bindPopup(popupContent, { maxWidth: 240 });

            polygon.on('click', () => {
                this.selectField(fieldId);
            });

            polygon._fieldId = fieldId;
            this.fieldLayers[fieldId] = polygon;
            return true;
        } catch (e) {
            console.error('Error adding field polygon:', e);
            return false;
        }
    },

    selectField: function (fieldId) {
        if (!this.map) return;
        this.selectedFieldId = fieldId;

        this.applyLayerVisibility();

        const selected = this.fieldLayers[fieldId];
        if (selected) {
            this.map.fitBounds(selected.getBounds(), { padding: [60, 60], maxZoom: 15 });
        }

        if (this.dotNetRef) {
            this.dotNetRef.invokeMethodAsync('OnFieldSelected', fieldId);
        }
    },

    setGisViewMode: function (mode) {
        if (!this.map) return;
        this.viewMode = mode;
        this.selectedFieldId = null;

        // El filtro sobrevive al cambio de modo: son condiciones independientes y
        // applyLayerVisibility las combina.
        this.applyLayerVisibility();
    },

    // OT-38: visibleLotIds es un array de ids; null limpia el filtro.
    // Devuelve cuantos lotes quedan visibles en el mapa.
    filterLots: function (visibleLotIds) {
        if (!this.map) return 0;

        this.lotFilter = visibleLotIds
            ? new Set(visibleLotIds.map(id => String(id).toLowerCase()))
            : null;
        this.applyLayerVisibility();

        return Object.values(this.lotLayers).filter(l => this.isLotVisible(l)).length;
    },

    // Encuadra solo lo que se esta viendo, no el universo completo.
    fitVisibleLots: function () {
        if (!this.map) return false;

        const visibles = Object.values(this.lotLayers).filter(l => this.isLotVisible(l));
        if (visibles.length === 0) return false;

        const bounds = L.featureGroup(visibles).getBounds();
        if (bounds && bounds.isValid()) {
            this.map.fitBounds(bounds, { padding: [50, 50] });
            return true;
        }
        return false;
    },

    clearFields: function () {
        if (!this.map || !this.fieldLayers) return;

        Object.values(this.fieldLayers).forEach(layer => {
            this.map.removeLayer(layer);
        });
        this.fieldLayers = {};
    },

    centerOnField: function (fieldId) {
        if (!this.map || !this.fieldLayers[fieldId]) return false;

        const layer = this.fieldLayers[fieldId];
        this.map.fitBounds(layer.getBounds(), { padding: [60, 60], maxZoom: 15 });
        return true;
    },

    clearDrawn: function () {
        if (this.drawnItems) {
            this.drawnItems.clearLayers();
        }
    },

    enableDrawing: function () {
        if (this.map && this.drawControl) {
            try {
                if (typeof L !== 'undefined' && L.Draw && L.Draw.Polygon) {
                    new L.Draw.Polygon(this.map, this.drawControl.options.draw.polygon).enable();
                }
            } catch (e) {
                console.error('Error enabling polygon drawing:', e);
            }
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

    wktToLeafletCoords: function (wkt) {
        if (!wkt) return [];
        try {
            var isMulti = wkt.toUpperCase().indexOf('MULTIPOLYGON') >= 0;
            if (isMulti) {
                var polyMatches = wkt.match(/\(\(\s*([^()]+)\s*\)\)/g);
                if (!polyMatches) return [];
                var allPolys = polyMatches.map(function (polyStr) {
                    var clean = polyStr.replace(/^\(\(|\)\)$/g, '').trim();
                    var pairs = clean.split(',');
                    var ring = pairs.map(function (p) {
                        var parts = p.trim().split(/\s+/);
                        return [parseFloat(parts[1]), parseFloat(parts[0])];
                    });
                    if (ring[0][0] !== ring[ring.length - 1][0] || ring[0][1] !== ring[ring.length - 1][1]) {
                        ring.push(ring[0]);
                    }
                    return ring;
                });
                return [allPolys];
            } else {
                var match = wkt.match(/POLYGON\s*\(\(\s*(.+)\s*\)\)/i);
                if (!match) return [];
                var pairs = match[1].split(',');
                var ring = pairs.map(function (p) {
                    var parts = p.trim().split(/\s+/);
                    return [parseFloat(parts[1]), parseFloat(parts[0])];
                });
                if (ring[0][0] !== ring[ring.length - 1][0] || ring[0][1] !== ring[ring.length - 1][1]) {
                    ring.push(ring[0]);
                }
                return [[ring]];
            }
        } catch (e) {
            console.error('Error parsing WKT to coords:', e);
            return [];
        }
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
                if (!feature || !feature.geometry || !feature.geometry.coordinates) return;

                var name = (feature.properties && (feature.properties.name || feature.properties.Name || feature.properties.NOMBRE || feature.properties.nombre || feature.properties.lote || feature.properties.Lote || feature.properties.LOTE)) || '';

                if (feature.geometry.type === 'Polygon') {
                    var coords = feature.geometry.coordinates && feature.geometry.coordinates[0];
                    if (!coords || !Array.isArray(coords) || coords.length < 3) return;
                    var wktCoords = [];
                    coords.forEach(function (c) {
                        if (Array.isArray(c) && c.length >= 2 && !isNaN(c[0]) && !isNaN(c[1])) {
                            wktCoords.push(c[0].toFixed(8) + ' ' + c[1].toFixed(8));
                        }
                    });
                    if (wktCoords.length >= 3) {
                        if (wktCoords[0] !== wktCoords[wktCoords.length - 1]) {
                            wktCoords.push(wktCoords[0]);
                        }
                        var wkt = 'POLYGON ((' + wktCoords.join(', ') + '))';
                        results.push({ wkt: wkt, name: name });
                    }
                } else if (feature.geometry.type === 'MultiPolygon') {
                    var polyStrings = [];
                    var polygons = feature.geometry.coordinates;
                    if (Array.isArray(polygons)) {
                        polygons.forEach(function (poly) {
                            var outerRing = poly && poly[0];
                            if (outerRing && Array.isArray(outerRing) && outerRing.length >= 3) {
                                var wktCoords = [];
                                outerRing.forEach(function (c) {
                                    if (Array.isArray(c) && c.length >= 2 && !isNaN(c[0]) && !isNaN(c[1])) {
                                        wktCoords.push(c[0].toFixed(8) + ' ' + c[1].toFixed(8));
                                    }
                                });
                                if (wktCoords.length >= 3) {
                                    if (wktCoords[0] !== wktCoords[wktCoords.length - 1]) {
                                        wktCoords.push(wktCoords[0]);
                                    }
                                    polyStrings.push('((' + wktCoords.join(', ') + '))');
                                }
                            }
                        });
                    }

                    if (polyStrings.length > 0) {
                        var wkt = 'MULTIPOLYGON (' + polyStrings.join(', ') + ')';
                        results.push({ wkt: wkt, name: name });
                    }
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
                var name = nameEl ? nameEl.textContent.trim() : '';

                var polygonElements = pm.getElementsByTagName('Polygon');
                var polyStrings = [];

                if (polygonElements.length > 0) {
                    for (var p = 0; p < polygonElements.length; p++) {
                        var coordsEl = polygonElements[p].getElementsByTagName('coordinates')[0];
                        if (!coordsEl) continue;

                        var coordsText = coordsEl.textContent.trim();
                        var points = coordsText.split(/\s+/).filter(function (s) { return s.length > 0; });
                        var wktCoords = points.map(function (pt) {
                            var parts = pt.split(',');
                            return parseFloat(parts[0]).toFixed(8) + ' ' + parseFloat(parts[1]).toFixed(8);
                        });

                        if (wktCoords.length >= 3) {
                            if (wktCoords[0] !== wktCoords[wktCoords.length - 1]) {
                                wktCoords.push(wktCoords[0]);
                            }
                            polyStrings.push('((' + wktCoords.join(', ') + '))');
                        }
                    }
                } else {
                    var coordsEls = pm.getElementsByTagName('coordinates');
                    for (var c = 0; c < coordsEls.length; c++) {
                        var coordsText = coordsEls[c].textContent.trim();
                        var points = coordsText.split(/\s+/).filter(function (s) { return s.length > 0; });
                        var wktCoords = points.map(function (pt) {
                            var parts = pt.split(',');
                            return parseFloat(parts[0]).toFixed(8) + ' ' + parseFloat(parts[1]).toFixed(8);
                        });

                        if (wktCoords.length >= 3) {
                            if (wktCoords[0] !== wktCoords[wktCoords.length - 1]) {
                                wktCoords.push(wktCoords[0]);
                            }
                            polyStrings.push('((' + wktCoords.join(', ') + '))');
                        }
                    }
                }

                if (polyStrings.length === 1) {
                    var wkt = 'POLYGON ' + polyStrings[0];
                    results.push({ wkt: wkt, name: name });
                } else if (polyStrings.length > 1) {
                    var wkt = 'MULTIPOLYGON (' + polyStrings.join(', ') + ')';
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
            var isMulti = wkt.toUpperCase().indexOf('MULTIPOLYGON') >= 0;
            var coordsArray = [];

            if (isMulti) {
                var polyMatches = wkt.match(/\(\(\s*([^()]+)\s*\)\)/g);
                if (!polyMatches) return false;

                coordsArray = polyMatches.map(function (polyStr) {
                    var clean = polyStr.replace(/^\(\(|\)\)$/g, '').trim();
                    var pairs = clean.split(',');
                    return pairs.map(function (pair) {
                        var parts = pair.trim().split(/\s+/);
                        return [parseFloat(parts[1]), parseFloat(parts[0])];
                    });
                });
            } else {
                var match = wkt.match(/POLYGON\s*\(\(\s*(.+)\s*\)\)/i);
                if (!match) return false;

                var coords = match[1].split(',').map(function (pair) {
                    var parts = pair.trim().split(/\s+/);
                    return [parseFloat(parts[1]), parseFloat(parts[0])];
                });
                coordsArray = [coords];
            }

            var polygon = L.polygon(coordsArray, {
                color: '#9B59B6',
                fillColor: '#9B59B6',
                fillOpacity: 0.35,
                weight: 3,
                dashArray: '5,5'
            }).addTo(this.map);

            polygon.bindPopup('<strong>' + (name || 'Polígono importado') + '</strong>');
            this.drawnItems.addLayer(polygon);
            var bounds = polygon.getBounds();
            if (bounds && bounds.isValid()) {
                this.map.fitBounds(bounds, { padding: [50, 50], maxZoom: 16 });
            }
            return true;
        } catch (e) {
            console.error('Error adding imported polygon:', e);
            return false;
        }
    },

    fitImportedBounds: function () {
        if (!this.map || !this.drawnItems) return false;
        try {
            var bounds = this.drawnItems.getBounds();
            if (bounds && bounds.isValid()) {
                this.map.fitBounds(bounds, { padding: [50, 50], maxZoom: 16 });
                return true;
            }
        } catch (e) {
            console.error('Error fitting imported bounds:', e);
        }
        return false;
    },

    getCurrentBounds: function () {
        if (!this.map) return null;
        var b = this.map.getBounds();
        return {
            southWestLat: b.getSouthWest().lat,
            southWestLng: b.getSouthWest().lng,
            northEastLat: b.getNorthEast().lat,
            northEastLng: b.getNorthEast().lng
        };
    },

    restoreBounds: function (bounds) {
        if (!this.map || !bounds) return;
        this.map.fitBounds([
            [bounds.southWestLat, bounds.southWestLng],
            [bounds.northEastLat, bounds.northEastLng]
        ]);
    },

    searchCity: async function (query) {
        if (!this.map || !query) return;
        try {
            const response = await fetch(`https://nominatim.openstreetmap.org/search?q=${encodeURIComponent(query)}&format=json&limit=1`);
            const data = await response.json();
            if (data && data.length > 0) {
                const result = data[0];
                this.map.flyTo([parseFloat(result.lat), parseFloat(result.lon)], 13);
                return true;
            }
        } catch (e) {
            console.error('Error searching city:', e);
        }
        return false;
    },

    startEditExistingLot: function (lotId, lotName, status, area, fieldName, coordinatesJson) {
        if (!this.map) return false;

        if (this.drawnItems) {
            this.drawnItems.clearLayers();
        }

        if (this.lotLayers && this.lotLayers[lotId]) {
            var layer = this.lotLayers[lotId];

            this.map.removeLayer(layer);
            delete this.lotLayers[lotId];
            if (this.selectedLayer === layer) {
                this.selectedLayer = null;
            }

            layer.setStyle({
                color: '#E74C3C',
                fillColor: '#E74C3C',
                fillOpacity: 0.3,
                weight: 3
            });

            this.drawnItems.addLayer(layer);
            this.editingLotId = lotId;

            this.map.fitBounds(layer.getBounds(), { padding: [80, 80], maxZoom: 16 });
            return true;
        } else if (coordinatesJson) {
            try {
                var coords = JSON.parse(coordinatesJson);
                if (coords && coords.length > 0) {
                    var polygon = L.polygon(coords, {
                        color: '#E74C3C',
                        fillColor: '#E74C3C',
                        fillOpacity: 0.3,
                        weight: 3
                    });
                    this.drawnItems.addLayer(polygon);
                    this.editingLotId = lotId;
                    this.map.fitBounds(polygon.getBounds(), { padding: [80, 80], maxZoom: 16 });
                    return true;
                }
            } catch (e) {
                console.error('Error parsing coordinates for edit:', e);
            }
        }
        return false;
    },

    cancelEditExistingLot: function (lotId, lotName, status, area, fieldName, coordinatesJson) {
        if (!this.map) return;

        if (this.drawnItems) {
            this.drawnItems.clearLayers();
        }
        this.editingLotId = null;

        if (lotId) {
            this.addLotPolygon(lotId, lotName, status, area, fieldName, coordinatesJson);
        }
    },

    getEditingLotWkt: function () {
        if (!this.drawnItems) return null;
        var layers = this.drawnItems.getLayers();
        if (layers.length > 0) {
            return this.layerToWkt(layers[0]);
        }
        return null;
    }
};