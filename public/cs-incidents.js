/* cs-incidents.js — Live data binding for incidents.html
 * GET /api/incidents/pipeline  → pipeline columns + KPI cards
 * GET /api/incidents           → registry table
 * GET /api/incidents/:id       → detail panel + timeline + notes
 * PATCH /api/incidents/:id/status
 * POST  /api/incidents/:id/notes
 */
(function () {
  'use strict';

  var selectedId = null;
  var regPage = 1, regTotal = 1;

  CS.ready.then(function (live) {
    if (!live) return;
    loadPipeline();
    loadRegistry();
  });

  /* ══════════════════════════════════════
     Pipeline
  ══════════════════════════════════════ */
  function loadPipeline() {
    CS.api('/api/incidents/pipeline?perColumn=3').then(function (p) {
      setText('cs-active-incidents', CS.fmt(p.activeIncidents));
      setText('cs-mttr', p.meanTimeToResolveMinutes + 'm');
      setText('cs-critical-sev', CS.fmt(p.criticalSeverity));
      setText('cs-inc-today', '+' + (p.todayCount || 0) + ' today');

      p.columns.forEach(function (col) {
        var el = document.getElementById('cs-pipeline-' + col.status.toLowerCase());
        if (!el) return;
        // Update count badge
        var badge = el.querySelector('[data-col-count]');
        if (badge) badge.textContent = col.count;

        var cards = el.querySelector('[data-col-cards]');
        if (!cards) return;
        cards.innerHTML = col.items.map(function (i) {
          var sevCls = i.severity === 'Critical' ? 'text-error border-error/20' :
            i.severity === 'High' ? 'text-amber-300 border-warning/20' : 'text-primary border-primary/20';
          var sevBadge = '<span class="px-1.5 py-0.5 rounded ' + severityBg(i.severity) + ' text-label-md">' + i.severity + '</span>';
          return '<div class="p-3 bg-surface-container-low rounded-xl hover:bg-surface-container transition-all cursor-pointer border ' + sevCls + '" onclick="csSelectIncident(' + i.id + ')">' +
            '<div class="flex items-center justify-between text-body-sm text-on-surface-variant mb-1">' +
            '<span class="font-code-mono font-semibold ' + (col.status === 'Open' ? 'text-error' : col.status === 'Investigating' ? 'text-primary' : col.status === 'Contained' ? 'text-secondary' : 'text-emerald-400') + '">' + CS.esc(i.code) + '</span>' +
            '<span>' + CS.ago(i.createdAt) + '</span></div>' +
            '<p class="text-body-md text-on-surface font-semibold line-clamp-1">' + CS.esc(i.title) + '</p>' +
            '<div class="flex items-center justify-between mt-2 pt-2 border-t border-outline-variant/10 text-body-sm text-on-surface-variant">' +
            '<span>' + CS.esc(i.assignedToName ? 'Analyst: ' + i.assignedToName : 'Unassigned') + '</span>' +
            sevBadge + '</div></div>';
        }).join('');
      });
    }).catch(function (e) { CS.toast('Pipeline: ' + e.message, 'error'); });
  }

  /* ══════════════════════════════════════
     Registry table
  ══════════════════════════════════════ */
  function loadRegistry() {
    CS.api('/api/incidents?page=' + regPage + '&pageSize=6').then(function (res) {
      regTotal = Math.max(1, Math.ceil(res.total / res.pageSize));
      var tbody = document.getElementById('cs-incidents-tbody');
      if (!tbody) return;
      tbody.innerHTML = res.items.map(function (i) {
        return '<tr class="hover:bg-surface-container-high/50 transition-colors cursor-pointer group" onclick="csSelectIncident(' + i.id + ')">' +
          '<td class="py-4 px-4"><div class="flex flex-col">' +
          '<span class="' + codeColor(i.status) + ' font-code-mono text-body-sm font-semibold">' + CS.esc(i.code) + '</span>' +
          '<span class="text-on-surface font-semibold group-hover:text-secondary transition-colors">' + CS.esc(i.title) + '</span></div></td>' +
          '<td class="py-4 px-4">' + severityPill(i.severity) + '</td>' +
          '<td class="py-4 px-4"><span class="' + (i.assignedToName ? 'text-on-surface' : 'text-on-surface-variant italic') + '">' + CS.esc(i.assignedToName || 'Unassigned') + '</span></td>' +
          '<td class="py-4 px-4 text-on-surface-variant font-code-mono text-body-sm">' + formatDate(i.createdAt) + '</td>' +
          '<td class="py-4 px-4">' + statusPill(i.status) + '</td>' +
          '<td class="py-4 px-4 text-right"><button class="p-1.5 hover:bg-surface-container-highest rounded-lg text-on-surface-variant hover:text-on-surface"><span class="material-symbols-outlined text-sm">more_vert</span></button></td></tr>';
      }).join('');
    }).catch(function (e) { CS.toast('Incidents: ' + e.message, 'error'); });
  }

  /* ══════════════════════════════════════
     Detail panel
  ══════════════════════════════════════ */
  window.csSelectIncident = function (id) {
    selectedId = id;
    CS.api('/api/incidents/' + id).then(function (d) {
      var i = d.incident;
      var panel = document.getElementById('cs-incident-detail');
      if (!panel) return;

      setText('cs-detail-code', i.code);
      setText('cs-detail-title', i.title);
      setText('cs-detail-description', d.description || '');
      setText('cs-detail-asset', d.affectedAsset || i.affectedAsset || '—');
      setText('cs-detail-mitre', d.mitreTechnique || '—');
      setText('cs-detail-containment', d.containmentStatus || 'Pending');

      // severity badge
      var badge = document.getElementById('cs-detail-sev-badge');
      if (badge) badge.innerHTML = severityPill(i.severity);

      // Timeline
      var timeline = document.getElementById('cs-detail-timeline');
      if (timeline && d.timeline) {
        timeline.innerHTML = d.timeline.map(function (t) {
          return '<div class="relative"><span class="absolute -left-[29px] top-1 w-3.5 h-3.5 rounded-full bg-primary border-2 border-surface"></span>' +
            '<div class="text-body-sm text-on-surface-variant font-code-mono mb-0.5">' + formatDateTime(t.occurredAt) + '</div>' +
            '<p class="text-body-md text-on-surface font-semibold">' + CS.esc(t.title) + '</p>' +
            '<p class="text-body-sm text-on-surface-variant mt-1">' + CS.esc(t.description || '') + '</p></div>';
        }).join('');
      }

      // Notes
      renderNotes(d.notes || []);

      panel.classList.remove('hidden');
    }).catch(function (e) { CS.toast('Incident detail: ' + e.message, 'error'); });
  };

  function renderNotes(notes) {
    var container = document.getElementById('cs-detail-notes');
    if (!container) return;
    container.innerHTML = notes.map(function (n) {
      return '<div class="p-3 bg-surface rounded-xl border border-outline-variant/10">' +
        '<div class="flex items-center justify-between mb-1.5">' +
        '<span class="text-label-md text-primary font-semibold">' + CS.esc(n.authorName) + '</span>' +
        '<span class="text-body-sm text-on-surface-variant font-code-mono">' + formatTime(n.createdAt) + '</span></div>' +
        '<p class="text-body-sm text-on-surface">' + CS.esc(n.body) + '</p></div>';
    }).join('');
  }

  /* ── Send note ── */
  window.csSendNote = function () {
    if (!selectedId) return;
    var input = document.getElementById('cs-note-input');
    var body = input ? input.value.trim() : '';
    if (!body) return;
    CS.api('/api/incidents/' + selectedId + '/notes', { method: 'POST', body: { body: body } })
      .then(function () {
        input.value = '';
        // Reload detail to get fresh notes
        window.csSelectIncident(selectedId);
      })
      .catch(function (e) { CS.toast(e.message, 'error'); });
  };

  /* ── Keyboard: Enter sends note ── */
  document.addEventListener('DOMContentLoaded', function () {
    var input = document.getElementById('cs-note-input');
    if (input) input.addEventListener('keydown', function (e) { if (e.key === 'Enter') window.csSendNote(); });
  });

  /* ══════════════════════════════════════
     Helpers
  ══════════════════════════════════════ */
  function severityPill(s) {
    var cls = s === 'Critical' ? 'bg-error/15 text-error border-error/30' :
      s === 'High' ? 'bg-amber-500/20 text-amber-300 border-amber-500/30' :
      s === 'Medium' ? 'bg-blue-500/20 text-blue-300 border-blue-500/30' : 'bg-surface-container-high text-on-surface-variant border-outline-variant/30';
    return '<span class="px-2.5 py-1 rounded-full text-label-md border ' + cls + '">' + (s || '').toUpperCase() + '</span>';
  }

  function severityBg(s) {
    return s === 'Critical' ? 'bg-error/20 text-error' :
      s === 'High' ? 'bg-amber-500/25 text-amber-300' :
      s === 'Medium' ? 'bg-blue-500/25 text-blue-300' : 'bg-surface-container text-on-surface-variant';
  }

  function statusPill(s) {
    var map = {
      Open: 'bg-error/10 text-error',
      Investigating: 'bg-primary-container/20 text-primary',
      Contained: 'bg-secondary/15 text-secondary',
      Resolved: 'bg-emerald-400/10 text-emerald-400',
    };
    return '<span class="px-2.5 py-1 rounded-full text-label-md font-semibold ' + (map[s] || '') + '">' + (s || '').toUpperCase() + '</span>';
  }

  function codeColor(status) {
    return status === 'Open' ? 'text-error' : status === 'Investigating' ? 'text-primary' :
      status === 'Contained' ? 'text-secondary' : 'text-emerald-400';
  }

  function formatDate(s) {
    var d = CS.date(s); return d ? d.toLocaleString('en-GB', { dateStyle: 'short', timeStyle: 'short' }) : '—';
  }
  function formatDateTime(s) {
    var d = CS.date(s); return d ? d.toISOString().replace('T', ' ').slice(0, 19) + ' UTC' : '—';
  }
  function formatTime(s) {
    var d = CS.date(s); return d ? d.toLocaleTimeString('en-GB', { hour: '2-digit', minute: '2-digit' }) : '—';
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
})();
