/* cs-threats.js — Live data binding for threats.html
 * GET /api/threats/summary → headline numbers + categories + origins
 * GET /api/alerts?pageSize=4&sort=createdAt → live alert stream sidebar
 */
(function () {
  'use strict';

  CS.ready.then(function (live) {
    if (!live) return;

    // Headline metrics + categories + origins
    CS.api('/api/threats/summary').then(function (s) {

      /* ── Metric cards ── */
      setText('cs-active-threats-24h', CS.fmt(s.totalEvents));
      setText('cs-critical-incidents', CS.fmt(s.criticalActive));

      /* ── Threat Categories grid ── */
      var grid = document.getElementById('cs-threat-categories');
      if (grid && s.categories && s.categories.length) {
        grid.innerHTML = s.categories.map(function (c) {
          var sev = c.severity;
          var sevCls = sev === 'Critical' ? 'bg-error/20 text-error border-error/30' :
            sev === 'High' ? 'bg-warning/20 text-tertiary border-tertiary/30' :
            'bg-primary/20 text-primary border-primary/30';
          var trendCls = c.trendPercent >= 0 ? 'text-error' : 'text-secondary';
          var trendIcon = c.trendPercent >= 0 ? 'arrow_upward' : 'arrow_downward';
          var icon = categoryIcon(c.name);
          return '<div class="bg-surface-container-lowest/80 p-space-md rounded-xl border border-secondary/10 hover:border-secondary/40 transition-all group flex flex-col justify-between">' +
            '<div><div class="flex items-center justify-between mb-3">' +
            '<span class="material-symbols-outlined text-secondary text-2xl" style="font-variation-settings:\'FILL\' 1">' + icon + '</span>' +
            '<span class="px-2 py-0.5 rounded text-label-md border ' + sevCls + '">' + sev.toUpperCase() + '</span></div>' +
            '<h3 class="text-headline-sm text-on-surface group-hover:text-secondary transition-colors">' + CS.esc(c.name) + '</h3>' +
            '<p class="text-body-sm text-on-surface-variant mt-1">Threat events detected across monitored perimeters.</p></div>' +
            '<div class="mt-4 pt-3 border-t border-outline-variant/10 flex items-center justify-between">' +
            '<span class="text-code-mono text-on-surface-variant">' + CS.fmt(c.events) + ' events</span>' +
            '<span class="' + trendCls + ' text-body-sm font-semibold flex items-center gap-1">' +
            (c.trendPercent >= 0 ? '+' : '') + c.trendPercent + '% ' +
            '<span class="material-symbols-outlined text-xs">' + trendIcon + '</span></span></div></div>';
        }).join('');
      }

      /* ── Origin Ranking list ── */
      var origList = document.getElementById('cs-origin-ranking');
      if (origList && s.origins && s.origins.length) {
        origList.innerHTML = s.origins.slice(0, 4).map(function (o) {
          var pctCls = o.rank === 1 ? 'text-error' : o.rank === 2 ? 'text-warning' : o.rank <= 3 ? 'text-primary' : 'text-secondary';
          return '<div class="flex items-center justify-between p-3 rounded-xl bg-surface-container-lowest/80 border border-outline-variant/10">' +
            '<div class="flex items-center gap-3">' +
            '<div class="w-8 h-8 rounded-lg bg-surface-container-high flex items-center justify-center font-headline-md text-on-surface text-sm">0' + o.rank + '</div>' +
            '<div><div class="text-label-md text-on-surface">' + CS.esc(o.asn) + '</div>' +
            '<div class="text-body-sm text-on-surface-variant">' + CS.esc(o.topCategory) + '</div></div></div>' +
            '<span class="text-code-mono ' + pctCls + ' font-semibold">' + o.percent + '%</span></div>';
        }).join('');
      }

    }).catch(function (e) { CS.toast('Threats summary: ' + e.message, 'error'); });

    /* ── Live Alert Stream sidebar ── */
    CS.api('/api/alerts?pageSize=4&sort=createdAt').then(function (res) {
      var feed = document.getElementById('cs-alert-stream');
      if (!feed || !res.items || !res.items.length) return;
      feed.innerHTML = res.items.map(function (a) {
        var iconCls = a.severity === 'Critical' ? 'bg-error/10 text-error' :
          a.severity === 'High' ? 'bg-tertiary/10 text-tertiary' : 'bg-primary/10 text-primary';
        var icon = a.severity === 'Critical' ? 'error' : a.severity === 'High' ? 'warning' : 'info';
        return '<div class="p-3 rounded-xl bg-surface-container-lowest/90 border border-outline-variant/20 flex items-start gap-3">' +
          '<div class="w-8 h-8 rounded-lg ' + iconCls + ' flex items-center justify-center shrink-0 mt-0.5">' +
          '<span class="material-symbols-outlined text-sm" style="font-variation-settings:\'FILL\' 1">' + icon + '</span></div>' +
          '<div class="flex-1 min-w-0"><div class="flex items-center justify-between">' +
          '<span class="text-label-md text-on-surface truncate">' + CS.esc(a.title || a.category || 'Alert') + '</span>' +
          '<span class="text-code-mono text-xs ' + (a.severity === 'Critical' ? 'text-error' : a.severity === 'High' ? 'text-tertiary' : 'text-primary') + '">' + CS.ago(a.createdAt) + '</span></div>' +
          '<p class="text-body-sm text-on-surface-variant truncate">' + CS.esc(a.sourceIp ? 'IP: ' + a.sourceIp : a.description || '') + '</p></div></div>';
      }).join('');
    }).catch(function () { /* alerts sidebar is non-critical */ });
  });

  function categoryIcon(name) {
    if (!name) return 'crisis_alert';
    var n = name.toLowerCase();
    if (n.includes('malware') || n.includes('ransomware')) return 'pest_control';
    if (n.includes('phish')) return 'phishing';
    if (n.includes('brute') || n.includes('credential')) return 'key_visualizer';
    if (n.includes('ddos') || n.includes('flood')) return 'hub';
    if (n.includes('login') || n.includes('suspicious')) return 'person_alert';
    if (n.includes('exfil') || n.includes('data')) return 'upload_file';
    return 'crisis_alert';
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
})();
