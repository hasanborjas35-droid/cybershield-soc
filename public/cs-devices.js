/* cs-devices.js — Live data binding for devices.html
 * GET /api/devices          → table rows
 * GET /api/devices/summary  → stat cards + OS distribution + agent telemetry
 * POST /api/devices/:id/isolate  → quarantine button
 * POST /api/devices/:id/release → release button
 */
(function () {
  'use strict';

  var page = 1, pageSize = 20, totalPages = 1;
  var currentRisk = '', currentSort = 'lastSeen';

  CS.ready.then(function (live) {
    if (!live) return;

    loadSummary();
    loadDevices();

    // Filter / sort controls
    var riskSel = document.getElementById('cs-risk-filter');
    var sortSel = document.getElementById('cs-sort-select');
    if (riskSel) riskSel.addEventListener('change', function () { currentRisk = this.value; page = 1; loadDevices(); });
    if (sortSel) sortSel.addEventListener('change', function () { currentSort = this.value; page = 1; loadDevices(); });
  });

  /* ── Summary cards ── */
  function loadSummary() {
    CS.api('/api/devices/summary').then(function (s) {
      setText('cs-total-endpoints', CS.fmt(s.total));
      setText('cs-at-risk-count', CS.fmt(s.atRisk));
      setText('cs-quarantined-count', CS.fmt(s.quarantined));
      setText('cs-health-index', s.avgHealthIndex + '%');
      setText('cs-pending-update', CS.fmt(s.pendingUpdate));

      // OS distribution bars
      var container = document.getElementById('cs-os-dist');
      if (container && s.osDist && s.osDist.length) {
        container.innerHTML = s.osDist.map(function (o) {
          return '<div>' +
            '<div class="flex justify-between text-body-sm mb-1">' +
            '<span class="text-on-surface">' + CS.esc(o.name) + '</span>' +
            '<span class="font-code-mono text-secondary">' + o.percent + '%</span></div>' +
            '<div class="w-full h-2 rounded-full bg-surface-container-highest overflow-hidden">' +
            '<div class="h-full bg-primary rounded-full" style="width:' + o.percent + '%"></div></div></div>';
        }).join('');
      }
    }).catch(function (e) { CS.toast('Devices summary: ' + e.message, 'error'); });
  }

  /* ── Devices table ── */
  function loadDevices() {
    var params = '?page=' + page + '&pageSize=' + pageSize + '&sort=' + currentSort;
    if (currentRisk) params += '&risk=' + currentRisk;

    CS.api('/api/devices' + params).then(function (res) {
      totalPages = Math.max(1, Math.ceil(res.total / res.pageSize));
      setText('cs-devices-count', CS.fmt(res.total) + ' Active');
      setText('cs-showing-range', 'Showing ' + ((page - 1) * pageSize + 1) + '–' + Math.min(page * pageSize, res.total) + ' of ' + CS.fmt(res.total) + ' endpoints');

      var tbody = document.getElementById('cs-devices-tbody');
      if (!tbody) return;

      if (!res.items || res.items.length === 0) {
        tbody.innerHTML = '<tr><td colspan="7" class="py-10 text-center text-on-surface-variant">No devices found.</td></tr>';
        return;
      }

      tbody.innerHTML = res.items.map(function (d) {
        var riskBadge = riskBadgeHtml(d.riskLevel);
        var statusBadge = statusBadgeHtml(d.status);
        var icon = deviceIcon(d.operatingSystem);
        var iconColor = d.riskLevel === 'Critical' ? 'text-error' : d.riskLevel === 'High' ? 'text-amber-400' : 'text-primary';
        var isolateBtn = d.status === 'Quarantined'
          ? '<button onclick="csReleaseDevice(' + d.id + ',this)" class="px-3 py-1.5 rounded-lg bg-secondary/20 text-secondary hover:bg-secondary hover:text-on-secondary font-label-md transition-all border border-secondary/40 flex items-center gap-1"><span class="material-symbols-outlined text-sm">lock_open</span>Release</button>'
          : '<button onclick="csIsolateDevice(' + d.id + ',this)" class="px-3 py-1.5 rounded-lg bg-error/20 text-error hover:bg-error hover:text-on-error font-label-md transition-all border border-error/40 flex items-center gap-1"><span class="material-symbols-outlined text-sm">lock</span>Isolate</button>';

        return '<tr class="hover:bg-surface-container-high/40 transition-colors" data-device-id="' + d.id + '">' +
          '<td class="py-4 px-6"><div class="flex items-center gap-3">' +
          '<span class="material-symbols-outlined ' + iconColor + '">' + icon + '</span>' +
          '<div><span class="font-semibold text-on-surface block">' + CS.esc(d.name) + '</span>' +
          '<span class="text-body-sm text-on-surface-variant">' + CS.esc(d.segment || '') + '</span></div></div></td>' +
          '<td class="py-4 px-6 font-code-mono text-secondary">' + CS.esc(d.ipAddress) + '</td>' +
          '<td class="py-4 px-6 text-on-surface-variant">' + CS.esc(d.operatingSystem) + '</td>' +
          '<td class="py-4 px-6">' + riskBadge + '</td>' +
          '<td class="py-4 px-6">' + statusBadge + '</td>' +
          '<td class="py-4 px-6 font-code-mono text-on-surface-variant text-body-sm">' + CS.ago(d.lastSeenAt) + '</td>' +
          '<td class="py-4 px-6 text-right"><div class="flex items-center justify-end gap-2">' + isolateBtn +
          '<button class="p-1.5 rounded-lg bg-surface-container-high text-on-surface-variant hover:text-on-surface transition-colors"><span class="material-symbols-outlined text-sm">more_vert</span></button>' +
          '</div></td></tr>';
      }).join('');

      renderPagination();
    }).catch(function (e) { CS.toast('Devices: ' + e.message, 'error'); });
  }

  /* ── Quarantine actions (global so onclick= works) ── */
  window.csIsolateDevice = function (id, btn) {
    btn.disabled = true;
    CS.api('/api/devices/' + id + '/isolate', { method: 'POST' })
      .then(function () { CS.toast('Device isolated.'); loadDevices(); loadSummary(); })
      .catch(function (e) { CS.toast(e.message, 'error'); btn.disabled = false; });
  };

  window.csReleaseDevice = function (id, btn) {
    btn.disabled = true;
    CS.api('/api/devices/' + id + '/release', { method: 'POST' })
      .then(function () { CS.toast('Device released.'); loadDevices(); loadSummary(); })
      .catch(function (e) { CS.toast(e.message, 'error'); btn.disabled = false; });
  };

  /* ── Pagination ── */
  function renderPagination() {
    var el = document.getElementById('cs-devices-pagination');
    if (!el) return;
    var btns = '';
    var start = Math.max(1, page - 1), end = Math.min(totalPages, page + 1);
    for (var p = start; p <= end; p++) {
      btns += '<button onclick="csDevicesPage(' + p + ')" class="px-3 py-1.5 rounded-lg ' +
        (p === page ? 'bg-primary text-on-primary font-semibold' : 'bg-surface-container-high hover:bg-surface-variant text-on-surface') +
        ' transition-colors">' + p + '</button>';
    }
    el.innerHTML =
      '<button onclick="csDevicesPage(' + (page - 1) + ')" ' + (page <= 1 ? 'disabled' : '') +
      ' class="px-3 py-1.5 rounded-lg bg-surface-container-high hover:bg-surface-variant text-on-surface transition-colors disabled:opacity-50">Previous</button>' +
      btns +
      (totalPages > end ? '<span class="px-2">...</span><button onclick="csDevicesPage(' + totalPages + ')" class="px-3 py-1.5 rounded-lg bg-surface-container-high text-on-surface">' + totalPages + '</button>' : '') +
      '<button onclick="csDevicesPage(' + (page + 1) + ')" ' + (page >= totalPages ? 'disabled' : '') +
      ' class="px-3 py-1.5 rounded-lg bg-surface-container-high hover:bg-surface-variant text-on-surface transition-colors disabled:opacity-50">Next</button>';
  }

  window.csDevicesPage = function (p) {
    p = Math.max(1, Math.min(totalPages, p));
    if (p === page) return;
    page = p; loadDevices();
  };

  /* ── Helpers ── */
  function riskBadgeHtml(r) {
    var map = {
      Critical: 'bg-error/20 text-error border-error/30 shadow-[0_0_12px_rgba(255,180,171,0.3)]',
      High: 'bg-amber-500/20 text-amber-300 border-amber-500/30',
      Medium: 'bg-surface-container-high text-on-surface-variant border-outline-variant/30',
      Low: 'bg-secondary/10 text-secondary border-secondary/20',
    };
    var cls = map[r] || map.Medium;
    var dot = (r === 'Critical' || r === 'High') ? '<span class="w-1.5 h-1.5 rounded-full bg-current mr-1.5 animate-pulse"></span>' : '';
    return '<span class="inline-flex items-center px-2.5 py-1 rounded-full text-xs font-semibold border ' + cls + '">' + dot + r.toUpperCase() + '</span>';
  }

  function statusBadgeHtml(s) {
    if (s === 'Online') return '<span class="inline-flex items-center gap-1.5 text-secondary text-body-sm font-semibold"><span class="w-2 h-2 rounded-full bg-secondary shadow-[0_0_8px_rgba(0,217,255,0.8)] animate-pulse"></span>ONLINE</span>';
    if (s === 'Offline') return '<span class="inline-flex items-center gap-1.5 text-on-surface-variant text-body-sm font-semibold"><span class="w-2 h-2 rounded-full bg-outline-variant"></span>OFFLINE</span>';
    if (s === 'Quarantined') return '<span class="inline-flex items-center gap-1.5 text-error text-body-sm font-semibold"><span class="w-2 h-2 rounded-full bg-error animate-pulse"></span>QUARANTINED</span>';
    return '<span class="text-on-surface-variant text-body-sm">' + CS.esc(s) + '</span>';
  }

  function deviceIcon(os) {
    if (!os) return 'dns';
    var o = os.toLowerCase();
    if (o.includes('mac')) return 'laptop_mac';
    if (o.includes('windows')) return 'desktop_windows';
    if (o.includes('ubuntu') || o.includes('debian') || o.includes('rhel') || o.includes('linux')) return 'dns';
    return 'laptop';
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
})();
