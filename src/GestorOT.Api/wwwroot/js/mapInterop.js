window.mapInterop = {
    initMap: function (elementId, center, zoom) {
        const element = document.getElementById(elementId);
        if (!element) return null;
        const map = L.map(elementId).setView(center, zoom);
        L.tileLayer('https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png', {
            attribution: '© OpenStreetMap contributors'
        }).addTo(map);
        return map;
    },
    addPolygon: function (map, coordinates) {
        if (!map) return;
        const polygon = L.polygon(coordinates).addTo(map);
        map.fitBounds(polygon.getBounds());
        return polygon;
    },
    addMarker: function (map, lat, lng, popup) {
        if (!map) return;
        const marker = L.marker([lat, lng]).addTo(map);
        if (popup) marker.bindPopup(popup);
        return marker;
    }
};