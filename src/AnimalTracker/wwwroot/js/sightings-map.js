(function () {
    const maps = new Map();

    function toLatLng(points) {
        return points
            .filter(p => typeof p.latitude === "number" && typeof p.longitude === "number")
            .map(p => [p.latitude, p.longitude]);
    }

    function mapSummaryPopup(summary) {
        const core = (typeof summary.coreLatitude === "number" && typeof summary.coreLongitude === "number")
            ? `${summary.coreLatitude.toFixed(5)}, ${summary.coreLongitude.toFixed(5)}`
            : "Unknown";

        return [
            `<strong>${summary.animalLabel}</strong>`,
            `${summary.speciesName}`,
            `Core: ${core}`,
            `Radius: ~${Math.round(summary.radiusMeters)} m`,
            `Window: ${summary.bestActivityWindowLocal}`,
            `Hunting sightings: ${summary.huntingSightings}`
        ].join("<br/>");
    }

    window.animalTrackerMap = {
        render: function (containerId, points, summaries) {
            if (!window.L) {
                return;
            }

            const existing = maps.get(containerId);
            if (existing) {
                existing.remove();
                maps.delete(containerId);
            }

            const el = document.getElementById(containerId);
            if (!el) {
                return;
            }

            const map = L.map(containerId, {
                zoomControl: true
            });
            maps.set(containerId, map);

            L.tileLayer("https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png", {
                maxZoom: 19,
                attribution: "&copy; OpenStreetMap contributors"
            }).addTo(map);

            const latLngs = [];
            const pointLayer = L.layerGroup().addTo(map);
            for (const point of points || []) {
                if (typeof point.latitude !== "number" || typeof point.longitude !== "number") {
                    continue;
                }

                const marker = L.circleMarker([point.latitude, point.longitude], {
                    radius: 6,
                    weight: 1.5,
                    color: "#1e293b",
                    fillColor: "#22c55e",
                    fillOpacity: 0.85
                });

                marker.bindPopup([
                    `<strong>${point.speciesName}</strong>`,
                    point.animalDisplayName ? `Animal: ${point.animalDisplayName}` : "Animal: unknown",
                    `When: ${new Date(point.occurredAtUtc).toLocaleString()}`,
                    `Location: ${point.locationName}`,
                    point.behavior ? `Behavior: ${point.behavior}` : null,
                    point.locationAccuracyMeters ? `Accuracy: ~${Math.round(point.locationAccuracyMeters)} m` : null
                ].filter(Boolean).join("<br/>"));

                marker.addTo(pointLayer);
                latLngs.push([point.latitude, point.longitude]);

                if (typeof point.locationAccuracyMeters === "number" && point.locationAccuracyMeters > 0) {
                    L.circle([point.latitude, point.longitude], {
                        radius: point.locationAccuracyMeters,
                        weight: 1,
                        color: "#22c55e",
                        fillColor: "#22c55e",
                        fillOpacity: 0.08
                    }).addTo(pointLayer);
                }
            }

            const territoryLayer = L.layerGroup().addTo(map);
            for (const summary of summaries || []) {
                if (typeof summary.coreLatitude !== "number" || typeof summary.coreLongitude !== "number") {
                    continue;
                }

                const center = [summary.coreLatitude, summary.coreLongitude];
                latLngs.push(center);

                L.circle(center, {
                    radius: Math.max(summary.radiusMeters || 0, 25),
                    color: "#dc2626",
                    weight: 1.5,
                    fillColor: "#f97316",
                    fillOpacity: 0.08
                })
                    .bindPopup(mapSummaryPopup(summary))
                    .addTo(territoryLayer);
            }

            if (latLngs.length > 0) {
                map.fitBounds(L.latLngBounds(latLngs), { padding: [20, 20], maxZoom: 16 });
            } else {
                map.setView([54.5, -2.0], 5);
            }
        }
    };
})();
