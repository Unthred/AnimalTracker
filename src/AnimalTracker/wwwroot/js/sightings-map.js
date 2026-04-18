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
            const sightingMarkerEntries = [];

            /**
             * Zoomed out → small green dot; zoomed in → photo thumbnails grow (capped ~ Explorer large / extra-large icons).
             * Leaflet zoom: low = world view, high = street level.
             */
            const ZOOM_DOT_MAX = 11;
            const ZOOM_THUMB_START = 12;
            const ZOOM_THUMB_MAX = 18;
            const THUMB_MIN_PX = 28;
            /** ~ Explorer "Large" / lower "Extra large" tiles; cap so pins never dominate the map */
            const THUMB_MAX_PX = 96;

            function primaryPhotoIdOf(p) {
                if (p == null) {
                    return null;
                }
                const id = p.primaryPhotoId ?? p.PrimaryPhotoId;
                return typeof id === "number" && id > 0 ? id : null;
            }

            /**
             * @returns {{ dotOnly: boolean, px: number }}
             */
            function sightingVisualForZoom(zoom) {
                const z = typeof zoom === "number" ? zoom : 12;
                if (z <= ZOOM_DOT_MAX) {
                    return { dotOnly: true, px: 32 };
                }
                const t = Math.min(1, Math.max(0, (z - ZOOM_THUMB_START) / (ZOOM_THUMB_MAX - ZOOM_THUMB_START)));
                const px = Math.round(THUMB_MIN_PX + t * (THUMB_MAX_PX - THUMB_MIN_PX));
                return { dotOnly: false, px: Math.min(THUMB_MAX_PX, Math.max(THUMB_MIN_PX, px)) };
            }

            function greenDotHtml(s) {
                const dot = Math.max(8, Math.min(14, Math.round(s * 0.31)));
                return `<div class="at-map-thumb-fallback" aria-hidden="true" style="width:${s}px;height:${s}px;display:flex;align-items:center;justify-content:center;line-height:0;"><span style="width:${dot}px;height:${dot}px;border-radius:9999px;background:#22c55e;box-shadow:0 0 0 2px #fff,0 1px 2px rgba(15,23,42,.4);"></span></div>`;
            }

            function sightingMarkerIcon(p, zoom) {
                const pid = primaryPhotoIdOf(p);
                const hasPhoto = !!pid;
                const vis = sightingVisualForZoom(zoom);
                const s = vis.px;
                const half = s / 2;
                const r = Math.max(4, Math.round(s * 0.19));

                let inner;
                if (!hasPhoto || vis.dotOnly) {
                    inner = greenDotHtml(s);
                } else {
                    inner = `<div class="at-map-thumb-wrap" style="width:${s}px;height:${s}px;max-width:${s}px;max-height:${s}px;overflow:hidden;border-radius:${r}px;box-shadow:0 1px 3px rgba(15,23,42,.45);border:2px solid #fff;line-height:0;box-sizing:border-box;background:#0f172a;"><img src="/photos/${pid}" alt="" width="${s}" height="${s}" loading="lazy" decoding="async" style="width:${s}px;height:${s}px;max-width:${s}px;max-height:${s}px;object-fit:cover;display:block;" /></div>`;
                }

                return L.divIcon({
                    className: "at-map-thumb-marker",
                    html: inner,
                    iconSize: [s, s],
                    iconAnchor: [half, half],
                    popupAnchor: [0, -Math.max(10, Math.round(half * 0.55))]
                });
            }

            function refreshSightingMarkerIcons() {
                const z = map.getZoom();
                for (const { marker, point } of sightingMarkerEntries) {
                    marker.setIcon(sightingMarkerIcon(point, z));
                }
            }

            for (const point of points || []) {
                if (typeof point.latitude !== "number" || typeof point.longitude !== "number") {
                    continue;
                }

                const marker = L.marker([point.latitude, point.longitude], {
                    icon: sightingMarkerIcon(point, map.getZoom())
                });
                sightingMarkerEntries.push({ marker, point });

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

            map.on("zoomend", refreshSightingMarkerIcons);

            if (latLngs.length > 0) {
                map.fitBounds(L.latLngBounds(latLngs), { padding: [20, 20], maxZoom: 16 });
            } else {
                map.setView([54.5, -2.0], 5);
            }

            map.whenReady(function () {
                refreshSightingMarkerIcons();
            });
        }
    };
})();
