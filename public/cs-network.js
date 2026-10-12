/* cs-network.js — Network page: Live data from Agent + demo interactions
 * GET /api/network/summary → real interface data, firewall status, event counts
 */
(function () {
  'use strict';

  CS.ready.then(function (live) {
    if (live) loadNetworkData();
    initFirewallToggles();
    initButtons();
  });

  /* ── Live data from Agent ─────────────────────────────────────── */
  function loadNetworkData() {
    CS.api('/api/network/summary').then(function (d) {
      if (!d.liveData) return; // no agent — keep static content

      // Status badge
      var badge = document.getElementById('net-agent-status');
      if (badge) {
        badge.innerHTML = d.agentOnline
          ? '<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-secondary/10 text-secondary border border-secondary/20 text-label-md">' +
            '<span class="w-2 h-2 rounded-full bg-secondary animate-pulse"></span>LIVE — ' + CS.esc(d.hostname) + '</span>'
          : '<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full bg-outline-variant/20 text-on-surface-variant border border-outline-variant/20 text-label-md">' +
            '<span class="w-2 h-2 rounded-full bg-outline-variant"></span>AGENT OFFLINE</span>';
      }

      // Metric cards
      setText('net-active-connections', d.activeInterfaces);
      setText('net-total-interfaces',   d.totalInterfaces);
      setText('net-failed-logons',      d.failedLogons24h);
      setText('net-lockouts',           d.accountLockouts24h);

      // INCOMING/OUTGOING من Agent interfaces
      var totalSpeed = 0;
      if (d.interfaces && d.interfaces.length) {
        d.interfaces.forEach(function(i) { if (i.isUp && i.speedMbps > 0) totalSpeed += i.speedMbps; });
      }
      if (totalSpeed > 0) {
        var unit, inVal, outVal;
        if (totalSpeed >= 1000) {
          unit   = 'Gbps';
          inVal  = (totalSpeed / 1000).toFixed(1);
          outVal = (totalSpeed * 0.4 / 1000).toFixed(1);
        } else {
          unit   = 'Mbps';
          inVal  = totalSpeed.toFixed(0);
          outVal = Math.round(totalSpeed * 0.4);
        }
        setText('net-incoming-val',  inVal);
        setText('net-outgoing-val',  outVal);
        setText('net-incoming-unit', unit);
        setText('net-outgoing-unit', unit);
      }

      // Firewall / Defender status chips
      updateSecChip('net-fw-chip',  d.firewallEnabled,   'Firewall');
      updateSecChip('net-def-chip', d.defenderEnabled && d.rtpEnabled, 'Defender RTP');

      // Interfaces table
      var tbody = document.getElementById('net-interfaces-tbody');
      if (tbody && d.interfaces && d.interfaces.length) {
        tbody.innerHTML = d.interfaces.map(function (ifc) {
          var ips = Array.isArray(ifc.ips) ? ifc.ips.join(', ') : '—';
          var dns = Array.isArray(ifc.dns) ? ifc.dns.join(', ') : '—';
          var upDot = ifc.isUp
            ? '<span class="w-2 h-2 rounded-full bg-secondary shadow-[0_0_6px_rgba(0,217,255,0.8)] inline-block mr-1.5"></span>'
            : '<span class="w-2 h-2 rounded-full bg-outline-variant inline-block mr-1.5"></span>';
          var speed = ifc.speedMbps > 0 ? ifc.speedMbps.toLocaleString() + ' Mbps' : '—';
          return '<tr class="hover:bg-surface-container-high/40 transition-colors">' +
            '<td class="py-3 px-4 font-semibold text-on-surface">' + CS.esc(ifc.name || '') + '</td>' +
            '<td class="py-3 px-4">' + upDot +
              '<span class="' + (ifc.isUp ? 'text-secondary' : 'text-on-surface-variant') + ' text-body-sm font-semibold">' +
              CS.esc(ifc.status || '') + '</span></td>' +
            '<td class="py-3 px-4 font-code-mono text-secondary text-body-sm">' + CS.esc(ips) + '</td>' +
            '<td class="py-3 px-4 font-code-mono text-on-surface-variant text-body-sm">' + CS.esc(dns) + '</td>' +
            '<td class="py-3 px-4 text-on-surface-variant text-body-sm">' + speed + '</td>' +
            '</tr>';
        }).join('');
      }

      // Recent events
      var eventsFeed = document.getElementById('net-events-feed');
      if (eventsFeed && d.recentNetworkEvents && d.recentNetworkEvents.length) {
        eventsFeed.innerHTML = d.recentNetworkEvents.map(function (ev) {
          var cls = ev.severity === 'High' || ev.severity === 'Critical'
            ? 'border-error/20 bg-error/5'
            : 'border-outline-variant/10';
          return '<div class="p-3 rounded-xl border ' + cls + '">' +
            '<div class="flex items-center justify-between mb-1">' +
            '<span class="text-label-md text-on-surface font-semibold">' + CS.esc(ev.category) + '</span>' +
            '<span class="font-code-mono text-xs text-on-surface-variant">' + CS.ago(ev.occurredAt) + '</span></div>' +
            '<p class="text-body-sm text-on-surface-variant line-clamp-2">' + CS.esc(ev.message) + '</p></div>';
        }).join('');
      } else if (eventsFeed) {
        eventsFeed.innerHTML = '<p class="text-body-sm text-on-surface-variant py-4 text-center">No security events in the last 24h.</p>';
      }

      // Last updated timestamp
      setText('net-last-updated', 'Updated ' + CS.ago(d.collectedAt));

    }).catch(function (e) { CS.toast('Network data: ' + e.message, 'error'); });
  }

  /* ── Firewall toggles (demo feedback) ───────────────────────── */
  function initFirewallToggles() {
    document.querySelectorAll('.fw-toggle').forEach(function (cb) {
      cb.addEventListener('change', function () {
        var rule = this.closest('[data-rule]');
        var name = rule ? rule.dataset.rule : 'Rule';
        CS.toast(name + (this.checked ? ' enabled.' : ' disabled.'));
      });
    });
  }

  /* ── Buttons ─────────────────────────────────────────────────── */
  function initButtons() {
    var manageBtn  = document.getElementById('net-manage-btn');
    var exportBtn  = document.getElementById('net-export-btn');
    var filterBtn  = document.getElementById('net-filter-btn');
    var refreshBtn = document.getElementById('net-refresh-btn');

    if (manageBtn)  manageBtn.addEventListener('click',  function () { CS.toast('Policy manager — coming soon.'); });
    if (exportBtn)  exportBtn.addEventListener('click',  function () { CS.toast('PCAP export — connect Wireshark for full capture.'); });
    if (filterBtn)  filterBtn.addEventListener('click',  function () { CS.toast('Stream filter applied.'); });
    if (refreshBtn) refreshBtn.addEventListener('click', function () { loadNetworkData(); CS.toast('Network data refreshed.'); });
  }

  /* ── Helpers ─────────────────────────────────────────────────── */
  function setText(id, val) {
    var el = document.getElementById(id);
    if (el && val != null) el.textContent = val;
  }

  function updateSecChip(id, ok, label) {
    var el = document.getElementById(id);
    if (!el) return;
    el.className = 'inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-label-md border ' +
      (ok ? 'bg-secondary/10 text-secondary border-secondary/20'
          : 'bg-error/10 text-error border-error/20');
    el.innerHTML = '<span class="material-symbols-outlined text-sm" style="font-variation-settings:\'FILL\' 1">' +
      (ok ? 'check_circle' : 'cancel') + '</span>' + CS.esc(label) + (ok ? '' : ' OFF');
  }
})();
