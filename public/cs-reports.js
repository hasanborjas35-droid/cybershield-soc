/* cs-reports.js — Reports page: real data from GET /api/reports/summary */
(function () {
  'use strict';

  CS.ready.then(function (live) {
    if (live) loadReports();
    initButtons();
  });

  function loadReports() {
    CS.api('/api/reports/summary').then(function (d) {

      // ── Security Overview mini-cards ────────────────────────────
      // Risk Index: lower is better — derived from open critical+high alerts vs total
      var totalOpen = d.alerts.openAlerts || 0;
      var riskIndex = totalOpen > 0 ? (d.alerts.criticalOpen * 3 + (totalOpen - d.alerts.criticalOpen)) / 10 : 0;
      setText('rpt-risk-index',      riskIndex.toFixed(2));
      setText('rpt-active-endpoints', CS.fmt(d.devices.totalDevices));
      setText('rpt-avg-mttr',         d.incidents.mttrMinutes + 'm');
      setText('rpt-score-badge',      d.postureScore + ' / 100');
      setText('rpt-posture-score', d.postureScore + '%');
      setText('rpt-posture-grade', d.postureGrade);
      var scoreEl = document.getElementById('rpt-posture-score');
      if (scoreEl) {
        scoreEl.className = scoreEl.className.replace(/text-\w+/g, '');
        scoreEl.classList.add(d.postureScore >= 80 ? 'text-secondary' : d.postureScore >= 60 ? 'text-amber-400' : 'text-error');
      }

      // ── Alerts ──────────────────────────────────────────────────
      setText('rpt-total-alerts',    CS.fmt(d.alerts.totalAlerts));
      setText('rpt-open-alerts',     CS.fmt(d.alerts.openAlerts));
      setText('rpt-resolved-alerts', CS.fmt(d.alerts.resolvedAlerts));
      setText('rpt-critical-alerts', CS.fmt(d.alerts.criticalOpen));
      setText('rpt-alerts-7d',       CS.fmt(d.alerts.alerts7d));
      setText('rpt-alerts-30d',      CS.fmt(d.alerts.alerts30d));

      // Severity bars
      var sevContainer = document.getElementById('rpt-sev-bars');
      if (sevContainer && d.alerts.bySeverity && d.alerts.bySeverity.length) {
        var total = d.alerts.bySeverity.reduce(function(s,x){ return s + x.Count; }, 0);
        var colors = { Critical:'bg-error', High:'bg-amber-400', Medium:'bg-primary', Low:'bg-secondary' };
        sevContainer.innerHTML = d.alerts.bySeverity.map(function(s) {
          var pct = total > 0 ? Math.round(100 * s.Count / total) : 0;
          var cls = colors[s.Severity] || 'bg-outline';
          return '<div><div class="flex justify-between text-body-sm mb-1">' +
            '<span class="text-on-surface">' + CS.esc(s.Severity) + '</span>' +
            '<span class="font-code-mono text-on-surface-variant">' + CS.fmt(s.Count) + ' (' + pct + '%)</span></div>' +
            '<div class="w-full h-2 rounded-full bg-surface-container-highest overflow-hidden">' +
            '<div class="h-full rounded-full ' + cls + '" style="width:' + pct + '%"></div></div></div>';
        }).join('');
      }

      // Daily trend sparkline (text fallback)
      var trendEl = document.getElementById('rpt-daily-trend');
      if (trendEl && d.alerts.dailyTrend) {
        trendEl.innerHTML = d.alerts.dailyTrend.map(function(t) {
          return '<div class="flex flex-col items-center gap-1 flex-1">' +
            '<span class="text-body-sm font-semibold text-on-surface">' + t.count + '</span>' +
            '<div class="w-full rounded-t-sm bg-primary/60" style="height:' + Math.max(4, t.count * 4) + 'px"></div>' +
            '<span class="text-body-sm text-on-surface-variant" style="font-size:10px">' + CS.esc(t.date) + '</span></div>';
        }).join('');
      }

      // ── Incidents ───────────────────────────────────────────────
      setText('rpt-total-inc',    CS.fmt(d.incidents.totalInc));
      setText('rpt-open-inc',     CS.fmt(d.incidents.openInc));
      setText('rpt-invest-inc',   CS.fmt(d.incidents.investigInc));
      setText('rpt-resolved-inc', CS.fmt(d.incidents.resolvedInc));
      setText('rpt-mttr',         d.incidents.mttrMinutes + 'm');
      setText('rpt-inc-7d',       CS.fmt(d.incidents.inc7d));

      // ── Devices ─────────────────────────────────────────────────
      setText('rpt-total-devices',   CS.fmt(d.devices.totalDevices));
      setText('rpt-online-devices',  CS.fmt(d.devices.onlineDevices));
      setText('rpt-offline-devices', CS.fmt(d.devices.offlineDevices));
      setText('rpt-at-risk',         CS.fmt(d.devices.atRisk));
      setText('rpt-quarantined',     CS.fmt(d.devices.quarantined));
      setText('rpt-healthy',         CS.fmt(d.devices.healthy));

      // OS distribution
      var osContainer = document.getElementById('rpt-os-dist');
      if (osContainer && d.devices.byOs && d.devices.byOs.length) {
        var totalDev = d.devices.byOs.reduce(function(s,x){ return s + x.Count; }, 0);
        osContainer.innerHTML = d.devices.byOs.map(function(o) {
          var pct = totalDev > 0 ? Math.round(100 * o.Count / totalDev) : 0;
          return '<div><div class="flex justify-between text-body-sm mb-1">' +
            '<span class="text-on-surface">' + CS.esc(o.Os) + '</span>' +
            '<span class="font-code-mono text-on-surface-variant">' + pct + '%</span></div>' +
            '<div class="w-full h-2 rounded-full bg-surface-container-highest overflow-hidden">' +
            '<div class="h-full rounded-full bg-primary" style="width:' + pct + '%"></div></div></div>';
        }).join('');
      }

      // ── Threats ─────────────────────────────────────────────────
      setText('rpt-total-threats',  CS.fmt(d.threats.totalThreats));
      setText('rpt-active-threats', CS.fmt(d.threats.activeThreats));
      setText('rpt-total-events',   CS.fmt(d.threats.totalEvents));

      var threatList = document.getElementById('rpt-threat-categories');
      if (threatList && d.threats.byCategory && d.threats.byCategory.length) {
        threatList.innerHTML = d.threats.byCategory.map(function(t) {
          return '<div class="flex items-center justify-between p-3 rounded-xl bg-surface-container-lowest/80 border border-outline-variant/10">' +
            '<span class="text-body-md text-on-surface">' + CS.esc(t.Category) + '</span>' +
            '<span class="font-code-mono text-secondary text-body-sm">' + CS.fmt(t.Events) + ' events</span></div>';
        }).join('');
      }

      // Threat Breakdown bars — replace static bars with real data
      var breakdown = document.getElementById('rpt-threat-breakdown');
      if (breakdown && d.threats.byCategory && d.threats.byCategory.length) {
        var totalEvt = d.threats.byCategory.reduce(function(s, x) { return s + x.Events; }, 0);
        var barColors = ['bg-primary', 'bg-error', 'bg-tertiary-container', 'bg-secondary', 'bg-amber-400'];
        breakdown.innerHTML = d.threats.byCategory.slice(0, 5).map(function(t, i) {
          var pct = totalEvt > 0 ? Math.round(100 * t.Events / totalEvt) : 0;
          return '<div><div class="flex justify-between text-body-md mb-1">' +
            '<span class="text-on-surface">' + CS.esc(t.Category) + '</span>' +
            '<span class="text-code-mono text-on-surface-variant">' + pct + '% (' + CS.fmt(t.Events) + ')</span></div>' +
            '<div class="w-full h-2 rounded-full bg-surface-container-lowest overflow-hidden">' +
            '<div class="h-full ' + (barColors[i] || 'bg-primary') + ' rounded-full" style="width:' + pct + '%"></div></div></div>';
        }).join('');
      }

      // ── Agent ────────────────────────────────────────────────────
      setText('rpt-agents',      d.agent.registeredAgents);
      setText('rpt-online-agt',  d.agent.onlineAgents);
      setText('rpt-cpu',         d.agent.cpuPercent + '%');
      setText('rpt-defender',    d.agent.defenderEnabled ? (d.agent.rtpEnabled ? 'ON + RTP' : 'ON (RTP off)') : 'OFF');
      setText('rpt-firewall',    d.agent.firewallEnabled ? 'Enabled' : 'DISABLED');
      setText('rpt-failed-logons', d.agent.failedLogons);
      setText('rpt-detections',    d.agent.detections);

      // Generated at
      setText('rpt-generated-at', 'Generated ' + CS.ago(d.generatedAt));

    }).catch(function (e) { CS.toast('Reports: ' + e.message, 'error'); });
  }

  function initButtons() {
    document.querySelectorAll('.rpt-export-btn').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var orig = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="material-symbols-outlined text-sm animate-spin">sync</span><span>Exporting...</span>';
        setTimeout(function () {
          CS.toast('Report exported. (Connect a PDF/CSV library for real file generation.)');
          btn.disabled = false; btn.innerHTML = orig;
        }, 1200);
      });
    });
    document.querySelectorAll('.rpt-generate-btn').forEach(function (btn) {
      btn.addEventListener('click', function () {
        var orig = btn.innerHTML;
        btn.disabled = true;
        btn.innerHTML = '<span class="material-symbols-outlined text-sm animate-spin">sync</span><span>Generating...</span>';
        setTimeout(function () {
          loadReports();
          CS.toast('Report refreshed with latest data.');
          btn.disabled = false; btn.innerHTML = orig;
        }, 800);
      });
    });
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el && val != null) el.textContent = val;
  }
})();
