/* global Chart */
(function () {
  'use strict';

  function tickColor() {
    return document.documentElement.classList.contains('dark') ? '#94a3b8' : '#64748b';
  }

  function legendColor() {
    return document.documentElement.classList.contains('dark') ? '#e2e8f0' : '#334155';
  }

  function gridColor() {
    return document.documentElement.classList.contains('dark')
      ? 'rgba(148, 163, 184, 0.12)'
      : 'rgba(15, 23, 42, 0.08)';
  }

  function borderSlice() {
    return document.documentElement.classList.contains('dark') ? '#0f172a' : '#ffffff';
  }

  const palette = [
    '#0ea5e9', '#8b5cf6', '#10b981', '#f59e0b', '#f43f5e', '#06b6d4', '#a855f7',
    '#84cc16', '#ec4899', '#6366f1', '#14b8a6', '#eab308', '#f97316', '#22c55e'
  ];

  window.animalTrackerStatsChartsDestroy = function (canvasId) {
    try {
      if (typeof Chart === 'undefined') return;
      const existing = Chart.getChart(canvasId);
      if (existing) existing.destroy();
    } catch { /* ignore */ }
  };

  window.animalTrackerStatsChartsDoughnut = function (canvasId, labels, data) {
    window.animalTrackerStatsChartsDestroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === 'undefined') return;

    const bg = labels.map(function (_, i) { return palette[i % palette.length]; });

    new Chart(el, {
      type: 'doughnut',
      data: {
        labels: labels,
        datasets: [{
          data: data,
          backgroundColor: bg,
          borderColor: borderSlice(),
          borderWidth: 2,
          hoverOffset: 10
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        plugins: {
          legend: {
            position: 'bottom',
            labels: {
              color: legendColor(),
              boxWidth: 12,
              padding: 14,
              font: { size: 11, family: "Inter, system-ui, sans-serif" }
            }
          },
          tooltip: {
            callbacks: {
              label: function (ctx) {
                const v = ctx.parsed;
                const sum = ctx.dataset.data.reduce(function (a, b) { return a + b; }, 0);
                const pct = sum ? Math.round((100 * v) / sum) : 0;
                return ctx.label + ': ' + v + ' (' + pct + '%)';
              }
            }
          }
        }
      }
    });
  };

  window.animalTrackerStatsChartsBar = function (canvasId, labels, data, datasetLabel) {
    window.animalTrackerStatsChartsDestroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === 'undefined') return;

    new Chart(el, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          label: datasetLabel || 'Sightings',
          data: data,
          backgroundColor: 'rgba(14, 165, 233, 0.55)',
          borderColor: 'rgb(14, 165, 233)',
          borderWidth: 1,
          borderRadius: 5
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          x: {
            ticks: { color: tickColor(), maxRotation: 50, minRotation: 0, font: { size: 10 } },
            grid: { color: gridColor() }
          },
          y: {
            beginAtZero: true,
            ticks: { color: tickColor(), precision: 0, font: { size: 11 } },
            grid: { color: gridColor() }
          }
        },
        plugins: {
          legend: { display: false }
        }
      }
    });
  };

  window.animalTrackerStatsChartsHorizontalBar = function (canvasId, labels, data) {
    window.animalTrackerStatsChartsDestroy(canvasId);
    const el = document.getElementById(canvasId);
    if (!el || typeof Chart === 'undefined') return;

    new Chart(el, {
      type: 'bar',
      data: {
        labels: labels,
        datasets: [{
          data: data,
          backgroundColor: labels.map(function (_, i) { return palette[(i + 3) % palette.length] + 'cc'; }),
          borderColor: labels.map(function (_, i) { return palette[(i + 3) % palette.length]; }),
          borderWidth: 1,
          borderRadius: 4
        }]
      },
      options: {
        indexAxis: 'y',
        responsive: true,
        maintainAspectRatio: false,
        scales: {
          x: {
            beginAtZero: true,
            ticks: { color: tickColor(), precision: 0 },
            grid: { color: gridColor() }
          },
          y: {
            ticks: { color: tickColor(), font: { size: 11 } },
            grid: { display: false }
          }
        },
        plugins: {
          legend: { display: false }
        }
      }
    });
  };
})();
