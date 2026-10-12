/* CyberShield - shared API client, login overlay and UI helpers.
 *
 * Live mode:  the page is served by the CyberShield API (or CS_API_BASE is set) -> login + real data.
 * Demo mode:  no API reachable (e.g. the static published site) -> pages keep their original static content.
 */
(function () {
  'use strict';
  var TOKEN_KEY = 'cs_token', USER_KEY = 'cs_user';
  var CS = (window.CS = window.CS || {});
  CS.base = (window.CS_API_BASE || '').replace(/\/$/, '');
  CS.live = false;
  CS.token = localStorage.getItem(TOKEN_KEY);
  try { CS.user = JSON.parse(localStorage.getItem(USER_KEY) || 'null'); } catch (e) { CS.user = null; }

  /* ---------- helpers ---------- */
  CS.esc = function (v) {
    return String(v == null ? '' : v).replace(/[&<>"']/g, function (c) {
      return { '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c];
    });
  };
  CS.fmt = function (n) { return Number(n || 0).toLocaleString('en-US'); };
  /* API timestamps are UTC but serialized without a "Z" */
  CS.date = function (s) {
    if (!s) return null;
    return new Date(/([zZ]|[+-]\d\d:?\d\d)$/.test(s) ? s : s + 'Z');
  };
  CS.ago = function (s) {
    var d = CS.date(s); if (!d) return '-';
    var sec = Math.max(0, Math.round((Date.now() - d.getTime()) / 1000));
    if (sec < 60) return 'Just now (' + d.toLocaleTimeString('en-GB') + ')';
    var m = Math.floor(sec / 60); if (m < 60) return m + (m === 1 ? ' min ago' : ' mins ago');
    var h = Math.floor(m / 60); if (h < 24) return h + (h === 1 ? ' hour ago' : ' hours ago');
    var dd = Math.floor(h / 24); if (dd < 30) return dd + (dd === 1 ? ' day ago' : ' days ago');
    return d.toLocaleDateString('en-GB');
  };

  var BADGE = 'inline-flex items-center px-2.5 py-1 rounded-full text-label-md ';
  CS.sevBadge = function (sev) {
    var s = String(sev);
    if (s === 'Critical') return '<span class="' + BADGE + 'bg-error/10 text-error border border-error/30 shadow-[0_0_10px_rgba(255,180,171,0.2)]"><span class="w-1.5 h-1.5 rounded-full bg-error mr-1.5 animate-pulse"></span>Critical</span>';
    if (s === 'High') return '<span class="' + BADGE + 'bg-tertiary/10 text-tertiary border border-tertiary/30"><span class="w-1.5 h-1.5 rounded-full bg-tertiary mr-1.5"></span>High</span>';
    return '<span class="' + BADGE + 'bg-surface-container-highest text-on-surface-variant border border-outline-variant/30"><span class="w-1.5 h-1.5 rounded-full bg-outline mr-1.5"></span>' + CS.esc(s) + '</span>';
  };
  CS.alertStatusBadge = function (st) {
    var s = String(st);
    if (s === 'New') return '<span class="' + BADGE + 'bg-secondary/10 text-secondary border border-secondary/30">New</span>';
    if (s === 'Investigating') return '<span class="' + BADGE + 'bg-primary/10 text-primary border border-primary/30">Investigating</span>';
    if (s === 'Blocked') return '<span class="' + BADGE + 'bg-secondary/10 text-secondary border border-secondary/30 shadow-[0_0_10px_rgba(0,217,255,0.2)]">Blocked</span>';
    return '<span class="' + BADGE + 'bg-surface-container-highest text-on-surface border border-outline-variant/30">' + CS.esc(s) + '</span>';
  };

  /* ---------- toast ---------- */
  CS.toast = function (msg, kind) {
    var box = document.getElementById('cs-toasts');
    if (!box) {
      box = document.createElement('div'); box.id = 'cs-toasts';
      box.className = 'fixed top-20 right-4 z-[120] flex flex-col gap-2 max-w-sm';
      document.body.appendChild(box);
    }
    var t = document.createElement('div');
    var tone = kind === 'error' ? 'border-error/40 text-error' : 'border-secondary/40 text-secondary';
    t.className = 'px-4 py-3 rounded-xl bg-surface-container-high border shadow-2xl text-body-md transition-opacity duration-300 ' + tone;
    t.setAttribute('role', 'status');
    t.textContent = msg; box.appendChild(t);
    setTimeout(function () { t.style.opacity = '0'; setTimeout(function () { t.remove(); }, 350); }, kind === 'error' ? 6000 : 3500);
  };

  /* ---------- API ---------- */
  CS.api = function (path, opts) {
    opts = opts || {};
    var headers = { Accept: 'application/json' };
    if (CS.token) headers.Authorization = 'Bearer ' + CS.token;
    if (opts.body !== undefined) headers['Content-Type'] = 'application/json';
    return fetch(CS.base + path, {
      method: opts.method || 'GET', headers: headers,
      body: opts.body !== undefined ? JSON.stringify(opts.body) : undefined,
    }).then(function (r) {
      if (r.status === 401 && path.indexOf('/api/auth/login') !== 0) { CS.logout(true); throw new Error('Session expired. Please sign in again.'); }
      if (r.status === 403) throw new Error('You do not have permission to do this.');
      if (r.status === 204) return null;
      return r.text().then(function (txt) {
        var data = null; try { data = txt ? JSON.parse(txt) : null; } catch (e) { /* not json */ }
        if (!r.ok) {
          var msg = data && (data.error || data.title) || ('Request failed (' + r.status + ')');
          if (data && data.errors) msg = Object.keys(data.errors).map(function (k) { return data.errors[k].join(' '); }).join(' ');
          throw new Error(msg);
        }
        return data;
      });
    });
  };

  CS.logout = function (silent) {
    localStorage.removeItem(TOKEN_KEY); localStorage.removeItem(USER_KEY);
    CS.token = null; CS.user = null;
    if (!silent) location.reload(); else setTimeout(function () { location.reload(); }, 800);
  };

  /* ---------- login overlay ---------- */
  function showLogin(resolve) {
    var o = document.createElement('div');
    o.id = 'cs-login';
    o.className = 'fixed inset-0 z-[200] bg-surface flex items-center justify-center p-4';
    o.innerHTML =
      '<form class="w-full max-w-md bg-surface-container/80 backdrop-blur-xl rounded-xl p-8 border border-outline-variant/20 shadow-[0_0_40px_rgba(22,119,255,0.15)]" novalidate>' +
      '<div class="flex items-center gap-3 mb-6"><span class="material-symbols-outlined text-secondary-container text-4xl" style="font-variation-settings:\'FILL\' 1">shield</span>' +
      '<div><h1 class="text-headline-md text-on-surface">CyberShield</h1><p class="text-body-sm text-on-surface-variant">Security Operations Center</p></div></div>' +
      '<label class="block text-label-md text-on-surface-variant uppercase tracking-wider mb-1" for="cs-email">Email</label>' +
      '<input id="cs-email" type="email" autocomplete="username" required class="w-full mb-4 bg-surface-container-lowest text-on-surface px-4 py-2.5 rounded-xl text-body-md outline-none focus:ring-1 focus:ring-secondary"/>' +
      '<label class="block text-label-md text-on-surface-variant uppercase tracking-wider mb-1" for="cs-pass">Password</label>' +
      '<input id="cs-pass" type="password" autocomplete="current-password" required class="w-full mb-2 bg-surface-container-lowest text-on-surface px-4 py-2.5 rounded-xl text-body-md outline-none focus:ring-1 focus:ring-secondary"/>' +
      '<p id="cs-login-err" class="text-body-sm text-error min-h-[1.25rem] mb-3" role="alert"></p>' +
      '<button id="cs-login-btn" type="submit" class="w-full px-4 py-2.5 rounded-xl bg-primary-container text-on-primary-container font-semibold hover:opacity-90 transition-all text-label-md shadow-[0_0_15px_rgba(82,141,255,0.3)]">Sign in</button>' +
      '</form>';
    document.body.appendChild(o);
    var form = o.querySelector('form'), err = o.querySelector('#cs-login-err'), btn = o.querySelector('#cs-login-btn');
    o.querySelector('#cs-email').focus();
    form.addEventListener('submit', function (ev) {
      ev.preventDefault();
      var email = o.querySelector('#cs-email').value.trim(), pass = o.querySelector('#cs-pass').value;
      if (!email || !pass) { err.textContent = 'Enter your email and password.'; return; }
      btn.disabled = true; btn.textContent = 'Signing in...'; err.textContent = '';
      CS.api('/api/auth/login', { method: 'POST', body: { email: email, password: pass } })
        .then(function (res) {
          CS.token = res.token; CS.user = res.user;
          localStorage.setItem(TOKEN_KEY, res.token); localStorage.setItem(USER_KEY, JSON.stringify(res.user));
          o.remove(); mountUserChip(); resolve(true);
        })
        .catch(function (e) { err.textContent = e.message; btn.disabled = false; btn.textContent = 'Sign in'; });
    });
  }

  function mountUserChip() {
    if (!CS.user || document.getElementById('cs-user-chip')) return;
    var c = document.createElement('div');
    c.id = 'cs-user-chip';
    c.className = 'fixed bottom-4 right-4 z-40 flex items-center gap-3 px-4 py-2 rounded-xl bg-surface-container-high/90 backdrop-blur border border-outline-variant/20 text-body-sm text-on-surface-variant shadow-xl';
    c.innerHTML = '<span class="material-symbols-outlined text-sm text-secondary">account_circle</span>' +
      '<span><strong class="text-on-surface">' + CS.esc(CS.user.fullName) + '</strong> &middot; ' + CS.esc(CS.user.role) + '</span>' +
      '<button type="button" class="text-secondary hover:underline" id="cs-signout">Sign out</button>';
    document.body.appendChild(c);
    c.querySelector('#cs-signout').onclick = function () { CS.logout(); };
  }

  /* ---------- boot ---------- */
  CS.ready = new Promise(function (resolve) {
    var ctl = ('AbortController' in window) ? new AbortController() : null;
    var timer = setTimeout(function () { if (ctl) ctl.abort(); }, 3000);
    fetch(CS.base + '/health', { signal: ctl ? ctl.signal : undefined, headers: { Accept: 'application/json' } })
      .then(function (r) { return r.ok ? r.json() : null; })
      .then(function (h) {
        clearTimeout(timer);
        if (!h || h.status !== 'healthy') { resolve(false); return; }   // demo mode
        CS.live = true;
        if (!CS.token) { showLogin(resolve); return; }
        CS.api('/api/auth/me').then(function (u) { CS.user = u; mountUserChip(); resolve(true); })
          .catch(function () { /* logout() already reloads on 401 */ });
      })
      .catch(function () { clearTimeout(timer); resolve(false); });
  });
})();
