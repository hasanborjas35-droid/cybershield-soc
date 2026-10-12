/* CyberShield - Alerts page wired to the API (live mode only). */
(function () {
  'use strict';
  var PAGE_SIZE = 10;
  var state = { page: 1, total: 0, items: [], current: null };
  var $ = function (id) { return document.getElementById(id); };

  CS.ready.then(function (live) {
    if (!live) return;                       // demo mode: keep the static design content
    init();
  });

  /* ---------- init ---------- */
  function init() {
    /* replace the demo handlers of the static page with the live ones */
    window.closeSlideOver = closeSlideOver; window.copyToClipboard = copy;
    window.triggerAction = triggerAction; window.openBulkActionModal = bulkIsolate;
    window.openSlideOver = function (id) { if (typeof id === 'number') openSlideOver(id); };
    var hex = $('so-hex'); if (hex) hex.style.display = 'none';
    $('so-assign').classList.remove('hidden');
    /* the small captions under the stat numbers are design placeholders - replace with truthful ones */
    [['st-active', 'Open, not yet resolved'], ['st-critical', 'Requiring immediate triage'], ['st-invest', 'Currently being worked'], ['st-blocked', 'Blocked by response actions']].forEach(function (p) {
      var cap = $(p[0]).nextElementSibling; if (cap && cap.lastChild) cap.lastChild.textContent = p[1];
    });   // sample packet dump is demo-only
    var range = $('f-range'); if (range) range.value = '30d';   // seed data is a few days old
    ['f-sev', 'f-status', 'f-range'].forEach(function (id) { $(id).addEventListener('change', function () { state.page = 1; loadList(); }); });
    var t; $('f-search').addEventListener('input', function () { clearTimeout(t); t = setTimeout(function () { state.page = 1; loadList(); }, 300); });
    $('chk-all').addEventListener('change', function (e) {
      document.querySelectorAll('.row-chk').forEach(function (c) { c.checked = e.target.checked; });
    });
    $('btn-export').onclick = exportIocs;
    $('btn-bulk').onclick = bulkIsolate;
    $('so-assign').onclick = assignToMe;
    loadSummary(); loadList();
    setInterval(function () { if ($('slideOver').classList.contains('pointer-events-none')) { loadSummary(); loadList(true); } }, 30000);
  }

  function loadSummary() {
    CS.api('/api/alerts/summary').then(function (s) {
      $('st-active').textContent = CS.fmt(s.activeAlerts);
      $('st-critical').textContent = CS.fmt(s.criticalSeverity);
      $('st-invest').textContent = CS.fmt(s.underInvestigation);
      $('st-blocked').textContent = CS.fmt(s.autoBlocked);
    }).catch(function (e) { CS.toast(e.message, 'error'); });
  }

  /* ---------- list ---------- */
  function loadList(quiet) {
    var q = new URLSearchParams({ page: state.page, pageSize: PAGE_SIZE });
    if ($('f-sev').value) q.set('severity', $('f-sev').value);
    if ($('f-status').value) q.set('status', $('f-status').value);
    var r = $('f-range').value; if (r && r !== 'custom') q.set('range', r);
    var s = $('f-search').value.trim(); if (s) q.set('search', s);
    if (!quiet) $('alerts-body').innerHTML = '<tr><td colspan="8" class="p-8 text-center text-on-surface-variant">Loading alerts...</td></tr>';
    CS.api('/api/alerts?' + q).then(function (res) {
      state.items = res.items; state.total = res.total; renderRows(); renderPager();
    }).catch(function (e) {
      $('alerts-body').innerHTML = '<tr><td colspan="8" class="p-8 text-center text-error">' + CS.esc(e.message) + '</td></tr>';
    });
  }

  function renderRows() {
    if (!state.items.length) {
      $('alerts-body').innerHTML = '<tr><td colspan="8" class="p-8 text-center text-on-surface-variant">No alerts match these filters.</td></tr>';
      $('chk-all').checked = false; return;
    }
    $('alerts-body').innerHTML = state.items.map(function (a) {
      return '<tr class="hover:bg-surface-container-high/40 cursor-pointer transition-colors group" data-id="' + a.id + '">' +
        '<td class="p-4"><input class="row-chk rounded accent-secondary" type="checkbox" data-id="' + a.id + '" aria-label="Select ' + CS.esc(a.code) + '"/></td>' +
        '<td class="p-4">' + CS.sevBadge(a.severity) + '</td>' +
        '<td class="p-4"><div class="font-headline-sm text-on-surface group-hover:text-secondary transition-colors">' + CS.esc(a.signature) + '</div>' +
        '<div class="text-body-sm text-on-surface-variant font-code-mono mt-0.5">' + CS.esc(a.code) + (a.details ? ' &middot; ' + CS.esc(a.details.slice(0, 60)) + (a.details.length > 60 ? '...' : '') : '') + '</div></td>' +
        '<td class="p-4 font-code-mono text-secondary">' + CS.esc(a.sourceIp) + '</td>' +
        '<td class="p-4 font-code-mono text-on-surface-variant">' + CS.esc(a.destinationTarget) + '</td>' +
        '<td class="p-4">' + CS.alertStatusBadge(a.status) + '</td>' +
        '<td class="p-4 text-on-surface-variant text-body-sm">' + CS.esc(CS.ago(a.createdAt)) + '</td>' +
        '<td class="p-4 text-right"><button class="btn-view p-2 rounded-xl hover:bg-surface-container text-on-surface-variant hover:text-on-surface transition-colors" aria-label="View ' + CS.esc(a.code) + '">' +
        '<span class="material-symbols-outlined text-sm">visibility</span></button></td></tr>';
    }).join('');
    $('chk-all').checked = false;
    document.querySelectorAll('#alerts-body tr[data-id]').forEach(function (tr) {
      tr.addEventListener('click', function (e) {
        if (e.target.closest('.row-chk')) return;
        openSlideOver(Number(tr.dataset.id));
      });
    });
  }

  function renderPager() {
    var pages = Math.max(1, Math.ceil(state.total / PAGE_SIZE));
    var from = state.total ? (state.page - 1) * PAGE_SIZE + 1 : 0, to = Math.min(state.total, state.page * PAGE_SIZE);
    $('pg-info').textContent = 'Showing ' + from + '-' + to + ' of ' + CS.fmt(state.total) + ' alerts';
    var base = 'px-3 py-1.5 rounded-xl text-label-md transition-colors ';
    var idle = base + 'bg-surface-container text-on-surface hover:bg-surface-container-high';
    var nums = [];
    for (var p = 1; p <= pages; p++) if (p === 1 || p === pages || Math.abs(p - state.page) <= 1) nums.push(p);
    var html = '<button data-p="' + (state.page - 1) + '" class="' + base + 'bg-surface-container text-on-surface-variant hover:text-on-surface disabled:opacity-50" ' + (state.page <= 1 ? 'disabled' : '') + '>Previous</button>';
    var last = 0;
    nums.forEach(function (n) {
      if (n - last > 1) html += '<span class="text-on-surface-variant">...</span>';
      html += n === state.page
        ? '<button class="' + base + 'bg-primary-container text-on-primary-container font-semibold" aria-current="page">' + n + '</button>'
        : '<button data-p="' + n + '" class="' + idle + '">' + n + '</button>';
      last = n;
    });
    html += '<button data-p="' + (state.page + 1) + '" class="' + idle + ' disabled:opacity-50" ' + (state.page >= pages ? 'disabled' : '') + '>Next</button>';
    $('pg-btns').innerHTML = html;
    $('pg-btns').querySelectorAll('button[data-p]').forEach(function (b) {
      b.onclick = function () { state.page = Number(b.dataset.p); loadList(); };
    });
  }

  /* ---------- slide-over ---------- */
  function setPanel(open) {
    var so = $('slideOver'), panel = $('slideOverPanel');
    if (open) { so.classList.remove('pointer-events-none', 'opacity-0'); panel.classList.remove('translate-x-full'); }
    else { so.classList.add('pointer-events-none', 'opacity-0'); panel.classList.add('translate-x-full'); }
  }
  function closeSlideOver() { setPanel(false); }

  function openSlideOver(id) {
    CS.api('/api/alerts/' + id).then(function (d) {
      state.current = d; renderPanel(d); setPanel(true);
    }).catch(function (e) { CS.toast(e.message, 'error'); });
  }

  function renderPanel(d) {
    var a = d.alert;
    $('so-sev').innerHTML = CS.sevBadge(a.severity).replace('text-label-md', 'text-label-md') + '';
    $('so-code').textContent = a.code;
    $('slideOverPanel').dataset.alert = a.code;
    $('so-title').textContent = a.signature;
    $('so-desc').textContent = a.details || 'No additional details were recorded for this alert.';
    $('so-assignee').textContent = a.assignedToName || 'Unassigned';
    var closed = a.status === 'Resolved' || a.status === 'Dismissed';
    ['so-isolate', 'so-escalate', 'so-dismiss'].forEach(function (id) { $(id).disabled = false; $(id).classList.toggle('opacity-50', false); });
    $('so-escalate').disabled = a.incidentId != null;
    $('so-escalate').classList.toggle('opacity-50', a.incidentId != null);
    $('so-escalate').title = a.incidentId != null ? 'Already escalated to an incident' : '';
    $('so-dismiss').disabled = closed; $('so-dismiss').classList.toggle('opacity-50', closed);
    $('so-isolate').disabled = a.deviceId == null; $('so-isolate').classList.toggle('opacity-50', a.deviceId == null);
    $('so-isolate').title = a.deviceId == null ? 'This alert is not linked to a managed device' : '';

    $('so-iocs').innerHTML = d.iocs.length ? d.iocs.map(function (i) {
      return '<div class="p-3 rounded-xl bg-surface-container/50 border border-outline-variant/20 flex items-center justify-between">' +
        '<div class="flex flex-col min-w-0"><span class="text-label-md text-on-surface-variant">' + CS.esc(i.type) + '</span>' +
        '<span class="text-code-mono text-secondary text-sm break-all">' + CS.esc(i.value) + '</span></div>' +
        '<button class="p-2 rounded-lg bg-surface-container hover:bg-surface-container-high text-on-surface-variant hover:text-on-surface transition-colors shrink-0" data-copy="' + CS.esc(i.value) + '" title="Copy" aria-label="Copy">' +
        '<span class="material-symbols-outlined text-sm">content_copy</span></button></div>';
    }).join('') : '<p class="text-body-sm text-on-surface-variant">No indicators recorded for this alert.</p>';
    $('so-iocs').querySelectorAll('[data-copy]').forEach(function (b) {
      b.onclick = function () { copy(b.dataset.copy); };
    });

    var steps = [];
    steps.push(a.deviceId != null ? '<strong class="text-on-surface">Isolate the endpoint</strong> linked to this alert to prevent lateral movement.' : '<strong class="text-on-surface">Identify the affected host</strong> for <span class="font-code-mono text-secondary">' + CS.esc(a.sourceIp) + '</span> and link it to this alert.');
    steps.push('<strong class="text-on-surface">Block the destination</strong> <span class="font-code-mono text-secondary">' + CS.esc(a.destinationTarget) + '</span> at the perimeter firewall / DNS sinkhole.');
    if (d.iocs.length) steps.push('<strong class="text-on-surface">Hunt for the ' + d.iocs.length + ' recorded indicator' + (d.iocs.length > 1 ? 's' : '') + '</strong> across other endpoints.');
    steps.push(a.incidentId != null ? '<strong class="text-on-surface">Continue in the linked incident</strong> and document findings.' : '<strong class="text-on-surface">Escalate</strong> to an incident if the activity is confirmed malicious.');
    $('so-playbook').innerHTML = steps.map(function (s) { return '<li>' + s + '</li>'; }).join('');
  }

  function copy(text) {
    var done = function () { CS.toast('Copied to clipboard'); };
    if (navigator.clipboard && navigator.clipboard.writeText) navigator.clipboard.writeText(text).then(done, function () { CS.toast('Copy failed', 'error'); });
    else { var ta = document.createElement('textarea'); ta.value = text; document.body.appendChild(ta); ta.select(); try { document.execCommand('copy'); done(); } catch (e) { CS.toast('Copy failed', 'error'); } ta.remove(); }
  }

  /* ---------- actions ---------- */
  function act(btnId, path, okMsg) {
    var a = state.current && state.current.alert; if (!a) return;
    var b = $(btnId); b.disabled = true;
    CS.api('/api/alerts/' + a.id + path, { method: 'POST' }).then(function (res) {
      CS.toast(typeof okMsg === 'function' ? okMsg(res) : okMsg);
      setPanel(false); loadSummary(); loadList(true);
    }).catch(function (e) { CS.toast(e.message, 'error'); b.disabled = false; });
  }
  function triggerAction(name) {
    var a = state.current && state.current.alert; if (!a) return;
    if (name === 'Isolate') act('so-isolate', '/isolate', 'Host isolated. ' + a.code + ' marked as Blocked.');
    else if (name === 'Escalate') act('so-escalate', '/escalate', function (inc) { return a.code + ' escalated to incident ' + inc.code + '.'; });
    else if (name === 'Dismiss') act('so-dismiss', '/dismiss', a.code + ' dismissed.');
  }

  function assignToMe() {
    var a = state.current && state.current.alert; if (!a || !CS.user) return;
    CS.api('/api/alerts/' + a.id + '/assign', { method: 'POST', body: { userId: CS.user.id } }).then(function (res) {
      $('so-assignee').textContent = res.assignedToName || 'Unassigned';
      state.current.alert = res; CS.toast('Assigned to you.'); loadList(true);
    }).catch(function (e) { CS.toast(e.message, 'error'); });
  }

  function selectedIds() {
    return Array.prototype.map.call(document.querySelectorAll('.row-chk:checked'), function (c) { return Number(c.dataset.id); });
  }

  function bulkIsolate() {
    var ids = selectedIds();
    if (!ids.length) { CS.toast('Select at least one alert first.', 'error'); return; }
    if (!window.confirm('Isolate the hosts linked to ' + ids.length + ' selected alert(s)? Alerts without a linked device will be skipped.')) return;
    var ok = 0, skipped = 0, failed = 0;
    ids.reduce(function (p, id) {
      return p.then(function () {
        return CS.api('/api/alerts/' + id + '/isolate', { method: 'POST' }).then(function () { ok++; }, function (e) {
          if (/not linked/i.test(e.message)) skipped++; else failed++;
        });
      });
    }, Promise.resolve()).then(function () {
      CS.toast('Isolated ' + ok + (skipped ? ', skipped ' + skipped + ' (no device)' : '') + (failed ? ', failed ' + failed : '') + '.', failed ? 'error' : undefined);
      loadSummary(); loadList(true);
    });
  }

  function exportIocs() {
    var ids = selectedIds(); if (!ids.length) ids = state.items.map(function (a) { return a.id; });
    if (!ids.length) { CS.toast('No alerts to export.', 'error'); return; }
    Promise.all(ids.map(function (id) { return CS.api('/api/alerts/' + id); })).then(function (all) {
      var q = function (v) { return '"' + String(v == null ? '' : v).replace(/"/g, '""') + '"'; };
      var rows = [['alert_code', 'severity', 'signature', 'ioc_type', 'ioc_value']];
      all.forEach(function (d) { d.iocs.forEach(function (i) { rows.push([d.alert.code, d.alert.severity, d.alert.signature, i.type, i.value]); }); });
      if (rows.length === 1) { CS.toast('The selected alerts have no recorded IOCs.', 'error'); return; }
      var blob = new Blob([rows.map(function (r) { return r.map(q).join(','); }).join('\r\n')], { type: 'text/csv;charset=utf-8' });
      var a = document.createElement('a'); a.href = URL.createObjectURL(blob); a.download = 'cybershield-iocs.csv';
      document.body.appendChild(a); a.click(); a.remove(); setTimeout(function () { URL.revokeObjectURL(a.href); }, 1000);
      CS.toast('Exported ' + (rows.length - 1) + ' IOCs.');
    }).catch(function (e) { CS.toast(e.message, 'error'); });
  }
})();
