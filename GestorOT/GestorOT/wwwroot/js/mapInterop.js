window.mapInterop = {
    map: null,
    lotLayers: {},

    initMap: function(containerId, centerLat, centerLng, zoom) {
        if (this.map) {
            this.map.remove();
            this.map = null;
        }
        
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

    addLotPolygon: function(lotId, lotName, status, area, coordinatesJson) {
        if (!this.map) return false;
        
        try {
            const coordinates = JSON.parse(coordinatesJson);
            
            const color = status === 'Active' ? '#2ECC71' : '#E74C3C';
            const fillColor = status === 'Active' ? '#2ECC71' : '#E74C3C';
            
            const polygon = L.polygon(coordinates, {
                color: color,
                fillColor: fillColor,
                fillOpacity: 0.4,
                weight: 2
            }).addTo(this.map);
            
            polygon.bindPopup(`
                <div style="min-width: 150px;">
                    <strong style="font-size: 14px;">${lotName}</strong>
                    <hr style="margin: 8px 0; border-color: #eee;">
                    <div style="display: flex; justify-content: space-between;">
                        <span>Estado:</span>
                        <span style="color: ${color}; font-weight: bold;">${status === 'Active' ? 'Activo' : 'Inactivo'}</span>
                    </div>
                    <div style="display: flex; justify-content: space-between; margin-top: 4px;">
                        <span>Área:</span>
                        <span>${area.toFixed(2)} has</span>
                    </div>
                </div>
            `);
            
            this.lotLayers[lotId] = polygon;
            return true;
        } catch (e) {
            console.error('Error adding lot polygon:', e);
            return false;
        }
    },

    centerOnLot: function(lotId) {
        if (!this.map || !this.lotLayers[lotId]) return false;
        
        const layer = this.lotLayers[lotId];
        this.map.fitBounds(layer.getBounds(), { padding: [50, 50] });
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
    },

    invalidateSize: function() {
        if (this.map) {
            setTimeout(() => this.map.invalidateSize(), 100);
        }
    }
};
