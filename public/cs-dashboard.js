/* cs-dashboard.js — Live data binding for index.html (Dashboard)
 * Loads standard SOC summary + real agent telemetry from the Windows monitoring agent.
 */
(function () {
  'use strict';

  CS.ready.then(function (live) {
    if (!live) return; // demo mode — static HTML stays as-is

    loadSocSummary();
    loadAgentStatus();
  });

  /* ══════════════════════════════════════════════════════════════
     SOC Summary (existing endpoint)
  ══════════════════════════════════════════════════════════════ */
  function loadSocSummary() {
    CS.api('/api/dashboard/summary')
      .then(function (d) {
        setText('cs-score-value', d.postureScore + '%');
        var circle = document.querySelector('#cs-score-circle');
        if (circle) circle.setAttribute('stroke-dashoffset',
          (314 * (1 - d.postureScore / 100)).toFixed(2));

        setText('cs-active-threats',    CS.fmt(d.activeThreats));
        setText('cs-critical-alerts',   CS.fmt(d.criticalAlerts));
        setText('cs-protected-devices', CS.fmt(d.totalDevices));
        setText('cs-network-events',    CS.fmt(d.networkEvents));
        setText('cs-at-risk-devices',   CS.fmt(d.atRiskDevices));

        // Sub-texts
        var healthy = d.totalDevices > 0 ? Math.round(100 * (d.totalDevices - d.atRiskDevices) / d.totalDevices) : 0;
        setText('cs-device-health-pct', healthy + '% healthy');
        setText('cs-vuln-count',   (d.atRiskDevices || 0) + ' At Risk');
        setText('cs-compliance',   d.postureScore + '%');

        // Severity distribution — donut legend + total + SVG arcs
        if (d.severityDistribution && d.severityDistribution.length) {
          var total = d.severityDistribution.reduce(function(s, x) { return s + x.count; }, 0);
          setText('cs-donut-total', CS.fmt(total));

          // SVG circumference = 2 * PI * 40 = 251.2
          var circ = 251.2;
          var offset = 0;
          var order = ['Critical', 'High', 'Medium', 'Low'];
          var idMap  = { Critical: 'donut-critical', High: 'donut-high', Medium: 'donut-medium', Low: 'donut-low' };

          order.forEach(function(sev) {
            var item = d.severityDistribution.find(function(x) { return x.name === sev; });
            var count = item ? item.count : 0;
            var pct   = total > 0 ? count / total : 0;
            var arc   = pct * circ;
            var el    = document.getElementById(idMap[sev]);
            if (el) {
              el.setAttribute('stroke-dasharray', arc.toFixed(1) + ' ' + (circ - arc).toFixed(1));
              el.setAttribute('stroke-dashoffset', (-offset).toFixed(1));
            }
            offset += arc;
            var key = sev.toLowerCase();
            setText('cs-sev-' + key + '-pct', Math.round(pct * 100) + '% (' + CS.fmt(count) + ')');
          });
        }

        var tbody = document.getElementById('cs-recent-alerts-body');
        if (tbody && d.recentAlerts && d.recentAlerts.length) {
          tbody.innerHTML = d.recentAlerts.map(function (a) {
            return '<tr class="hover:bg-surface-container-low/50 transition-colors">' +
              '<td class="py-4 px-4">' + CS.sevBadge(a.severity) + '</td>' +
              '<td class="py-4 px-4 font-code-mono text-secondary">' + CS.esc(a.sourceIp || '—') + '</td>' +
              '<td class="py-4 px-4">' + CS.esc(a.destinationTarget || '—') + '</td>' +
              '<td class="py-4 px-4">' + CS.alertStatusBadge(a.status) + '</td>' +
              '<td class="py-4 px-4 text-on-surface-variant font-code-mono text-body-sm">' + CS.ago(a.createdAt) + '</td>' +
              '<td class="py-4 px-4 text-right">' +
              '<a href="alerts.html" class="px-3 py-1 rounded-lg bg-surface-container-high hover:bg-primary-container hover:text-on-primary-container text-on-surface text-body-sm transition-all">Inspect</a>' +
              '</td></tr>';
          }).join('');
        }
      })
      .catch(function (e) { CS.toast('Dashboard: ' + e.message, 'error'); });
  }

  /* ══════════════════════════════════════════════════════════════
     Agent Telemetry Panel (real Windows endpoint data)
  ══════════════════════════════════════════════════════════════ */
  function loadAgentStatus() {
    CS.api('/api/dashboard/agent-status')
      .then(function (res) {
        var container = document.getElementById('cs-agent-panel');
        if (!container) return;

        if (!res.liveMonitoring || !res.agents || res.agents.length === 0) {
          container.innerHTML =
            '<div class="flex flex-col items-center justify-center py-10 text-on-surface-variant gap-3">' +
            '<span class="material-symbols-outlined text-4xl opacity-40">sensors_off</span>' +
            '<p class="text-body-md">No agent connected.</p>' +
            '<p class="text-body-sm opacity-60">Run <code class="font-code-mono">dotnet run</code> inside <code class="font-code-mono">backend/CyberShield.Agent</code> to start monitoring.</p>' +
            '</div>';
          return;
        }

        container.innerHTML = res.agents.map(function (a) {
          return buildAgentCard(a);
        }).join('');
      })
      .catch(function () {
        // Silently fail — agent panel is non-critical if endpoint doesn't exist yet
      });
  }

  function buildAgentCard(a) {
    var onlineDot = a.isOnline
      ? '<span class="w-2.5 h-2.5 rounded-full bg-secondary shadow-[0_0_8px_rgba(0,217,255,0.8)] animate-pulse inline-block"></span>'
      : '<span class="w-2.5 h-2.5 rounded-full bg-outline-variant inline-block"></span>';
    var onlineLabel = a.isOnline
      ? '<span class="text-secondary font-semibold text-label-md">ONLINE</span>'
      : '<span class="text-on-surface-variant text-label-md">OFFLINE</span>';

    var defBadge   = secBadge(a.defenderEnabled && a.realTimeProtectionEnabled, 'Defender', 'shield', 'shield_off');
    var fwBadge    = secBadge(a.firewallEnabled, 'Firewall', 'local_fire_department', 'gpp_bad');
    var sigDate    = a.defenderSignatureDate
      ? '<span class="text-body-sm text-on-surface-variant">Sig: ' + CS.esc(a.defenderSignatureDate) + '</span>'
      : '';

    var cpuBar  = gaugeBar(a.cpuPercent,      'CPU',  a.cpuPercent      > 85 ? 'bg-error' : a.cpuPercent      > 60 ? 'bg-amber-400' : 'bg-secondary');
    var ramBar  = gaugeBar(a.ramUsedPercent,  'RAM',  a.ramUsedPercent  > 85 ? 'bg-error' : a.ramUsedPercent  > 70 ? 'bg-amber-400' : 'bg-primary');
    var diskBar = gaugeBar(a.diskUsedPercent, 'Disk', a.diskUsedPercent > 90 ? 'bg-error' : a.diskUsedPercent > 75 ? 'bg-amber-400' : 'bg-tertiary');

    var ramDetail  = a.totalMemoryGb  > 0 ? fmtGb(a.availableMemoryGb) + ' / ' + fmtGb(a.totalMemoryGb)  + ' free' : '';
    var diskDetail = a.totalDiskGb    > 0 ? fmtGb(a.availableDiskGb)   + ' / ' + fmtGb(a.totalDiskGb)    + ' free' : '';

    var eventsHtml = '';
    if (a.failedLogons > 0 || a.defenderDetections > 0 || a.accountLockouts > 0) {
      eventsHtml =
        '<div class="mt-3 pt-3 border-t border-outline-variant/10 flex flex-wrap gap-2">' +
        (a.failedLogons > 0 ? eventPill(a.failedLogons + ' failed logon(s)', 'warning', 'warning') : '') +
        (a.accountLockouts > 0 ? eventPill(a.accountLockouts + ' lockout(s)', 'error', 'lock') : '') +
        (a.defenderDetections > 0 ? eventPill(a.defenderDetections + ' Defender detection(s)', 'error', 'pest_control') : '') +
        '</div>';
    }

    var heartbeat = a.collectedAt
      ? '<span class="text-body-sm text-on-surface-variant font-code-mono">Last: ' + CS.ago(a.collectedAt) + '</span>'
      : '';

    return '<div class="bg-surface-container/80 backdrop-blur-xl p-5 rounded-xl border border-outline-variant/20 shadow-sm flex flex-col gap-4">' +
      // Header
      '<div class="flex items-start justify-between">' +
      '<div>' +
      '<div class="flex items-center gap-2 mb-0.5">' + onlineDot + ' ' + onlineLabel + '</div>' +
      '<h3 class="text-headline-sm text-on-surface font-semibold">' + CS.esc(a.hostname) + '</h3>' +
      '<p class="text-body-sm text-on-surface-variant">' + CS.esc(a.osVersion || '') + '</p>' +
      '</div>' +
      '<div class="flex flex-col items-end gap-1">' + heartbeat + sigDate + '</div>' +
      '</div>' +
      // Security badges
      '<div class="flex flex-wrap gap-2">' + defBadge + fwBadge + '</div>' +
      // Performance gauges
      '<div class="flex flex-col gap-2">' + cpuBar + ramBar + diskBar + '</div>' +
      '<div class="flex justify-between text-body-sm text-on-surface-variant">' +
      '<span>' + ramDetail + '</span><span>' + diskDetail + '</span>' +
      '</div>' +
      // Event pills
      eventsHtml +
      '</div>';
  }

  /* ── UI helpers ─────────────────────────────────────────────── */
  function secBadge(ok, label, iconOn, iconOff) {
    var cls  = ok ? 'bg-secondary/10 text-secondary border-secondary/20' : 'bg-error/10 text-error border-error/20';
    var icon = ok ? iconOn : iconOff;
    return '<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-label-md border ' + cls + '">' +
      '<span class="material-symbols-outlined text-sm" style="font-variation-settings:\'FILL\' 1">' + icon + '</span>' +
      CS.esc(label) + (ok ? '' : ' OFF') + '</span>';
  }

  function gaugeBar(pct, label, colorClass) {
    var safe = Math.min(100, Math.max(0, pct || 0));
    return '<div>' +
      '<div class="flex justify-between text-body-sm mb-1">' +
      '<span class="text-on-surface-variant">' + label + '</span>' +
      '<span class="font-code-mono text-on-surface">' + safe.toFixed(1) + '%</span></div>' +
      '<div class="w-full h-1.5 rounded-full bg-surface-container-highest overflow-hidden">' +
      '<div class="h-full rounded-full transition-all ' + colorClass + '" style="width:' + safe + '%"></div></div></div>';
  }

  function eventPill(text, color, icon) {
    var cls = color === 'error' ? 'bg-error/10 text-error border-error/20' : 'bg-amber-500/10 text-amber-300 border-amber-500/20';
    return '<span class="inline-flex items-center gap-1 px-2 py-0.5 rounded-full text-label-md border ' + cls + '">' +
      '<span class="material-symbols-outlined text-xs">' + icon + '</span>' + CS.esc(text) + '</span>';
  }

  function fmtGb(gb) {
    return gb != null ? gb.toFixed(1) + ' GB' : '—';
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
})();
