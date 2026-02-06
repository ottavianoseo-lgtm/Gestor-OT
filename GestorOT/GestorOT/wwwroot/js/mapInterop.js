window.mapInterop = {
    map: null,
    lotLayers: {},
    selectedLayer: null,
    dotNetRef: null,

    initMap: function(containerId, centerLat, centerLng, zoom) {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }

        this.lotLayers = {};
        this.selectedLayer = null;

        this.map = L.map(containerId, {
            preferCanvas: true,
            zoomControl: true
        }).setView([centerLat, centerLng], zoom);

        L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/World_Imagery/MapServer/tile/{z}/{y}/{x}', {
            attribution: 'Tiles &copy; Esri',
            maxZoom: 19
        }).addTo(this.map);

        L.tileLayer('https://server.arcgisonline.com/ArcGIS/rest/services/Reference/World_Boundaries_and_Places/MapServer/tile/{z}/{y}/{x}', {
            attribution: '',
            maxZoom: 19
        }).addTo(this.map);

        return true;
    },

    setDotNetRef: function(ref) {
        this.dotNetRef = ref;
    },

    addLotPolygon: function(lotId, lotName, status, area, fieldName, coordinatesJson) {
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

    highlightLot: function(lotId) {
        if (this.selectedLayer) {
            var prevId = Object.keys(this.lotLayers).find(k => this.lotLayers[k] === this.selectedLayer);
            if (prevId && this.lotLayers[prevId]) {
                var prevStatus = this.lotLayers[prevId].options._status;
                var prevColor = prevStatus === 'Active' ? '#2ECC71' : '#E74C3C';
                this.selectedLayer.setStyle({ weight: 2, fillOpacity: 0.35 });
            }
        }

        if (this.lotLayers[lotId]) {
            this.selectedLayer = this.lotLayers[lotId];
            this.selectedLayer.setStyle({ weight: 4, fillOpacity: 0.55 });
        }
    },

    centerOnLot: function(lotId) {
        if (!this.map || !this.lotLayers[lotId]) return false;

        const layer = this.lotLayers[lotId];
        this.map.fitBounds(layer.getBounds(), { padding: [80, 80], maxZoom: 16 });
        this.highlightLot(lotId);
        layer.openPopup();
        return true;
    },

    fitAllLots: function() {
        if (!this.map) return false;

        const layerIds = Object.keys(this.lotLayers);
        if (layerIds.length === 0) return false;

        const group = L.featureGroup(Object.values(this.lotLayers));
        this.map.fitBounds(group.getBounds(), { padding: [50, 50] });
        return true;
    },

    clearLots: function() {
        if (!this.map) return;

        Object.values(this.lotLayers).forEach(layer => {
            this.map.removeLayer(layer);
        });
        this.lotLayers = {};
        this.selectedLayer = null;
    },

    invalidateSize: function() {
        if (this.map) {
            setTimeout(() => this.map.invalidateSize(), 100);
        }
    }
};
