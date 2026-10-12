/* cs-users.js — Live data binding for users.html
 * GET  /api/users/summary  → metric cards
 * GET  /api/users          → table (paginated, filterable)
 * POST /api/users          → provision user modal
 * PATCH /api/users/:id/status → suspend / activate
 */
(function () {
  'use strict';

  var page = 1, pageSize = 10, totalPages = 1;
  var filterRole = '', filterStatus = '', filterSearch = '';

  CS.ready.then(function (live) {
    if (!live) return;
    loadSummary();
    loadUsers();
    bindFilters();
  });

  /* ── Summary cards ── */
  function loadSummary() {
    CS.api('/api/users/summary').then(function (s) {
      setText('cs-total-users', CS.fmt(s.total));
      setText('cs-admin-count', CS.fmt(s.adminCount));
      setText('cs-analyst-count', CS.fmt(s.activeAnalysts));
      setText('cs-pending-count', CS.fmt(s.pendingOrSuspended));
    }).catch(function (e) { CS.toast('Users summary: ' + e.message, 'error'); });
  }

  /* ── Users table ── */
  function loadUsers() {
    var params = '?page=' + page + '&pageSize=' + pageSize;
    if (filterRole) params += '&role=' + encodeURIComponent(filterRole);
    if (filterStatus) params += '&status=' + encodeURIComponent(filterStatus);
    if (filterSearch) params += '&search=' + encodeURIComponent(filterSearch);

    CS.api('/api/users' + params).then(function (res) {
      totalPages = Math.max(1, Math.ceil(res.total / res.pageSize));
      setText('cs-users-showing', 'Showing ' + ((page - 1) * pageSize + 1) + '–' + Math.min(page * pageSize, res.total) + ' of ' + CS.fmt(res.total) + ' users');

      var tbody = document.getElementById('cs-users-tbody');
      if (!tbody) return;

      if (!res.items || res.items.length === 0) {
        tbody.innerHTML = '<tr><td colspan="6" class="py-10 text-center text-on-surface-variant">No users found.</td></tr>';
        renderPagination();
        return;
      }

      tbody.innerHTML = res.items.map(function (u) {
        var roleBadge = roleBadgeHtml(u.role);
        var statusBadge = statusBadgeHtml(u.status);
        var perms = (u.permissions || []).slice(0, 2).map(function (p) {
          return '<span class="px-2 py-0.5 rounded bg-surface-container-high text-on-surface-variant text-body-sm font-code-mono">' + CS.esc(p) + '</span>';
        }).join('');
        if ((u.permissions || []).length > 2) perms += '<span class="px-2 py-0.5 rounded bg-surface-container-high text-on-surface-variant text-body-sm font-code-mono">+' + (u.permissions.length - 2) + '</span>';

        var actionBtn = u.status === 'Active'
          ? '<button onclick="csSuspendUser(' + u.id + ')" class="p-2 rounded-lg bg-surface-container-high hover:bg-error hover:text-on-error text-on-surface-variant transition-all" title="Suspend Account"><span class="material-symbols-outlined text-sm">block</span></button>'
          : '<button onclick="csActivateUser(' + u.id + ')" class="p-2 rounded-lg bg-surface-container-high hover:bg-secondary hover:text-on-secondary text-on-surface-variant transition-all" title="Activate Account"><span class="material-symbols-outlined text-sm">check_circle</span></button>';

        return '<tr class="hover:bg-surface-container/40 transition-colors group">' +
          '<td class="py-4 px-6"><div class="flex items-center gap-3">' +
          '<div class="w-10 h-10 rounded-full bg-primary-container/30 flex items-center justify-center text-primary font-semibold text-sm border border-primary/20">' + initials(u.fullName) + '</div>' +
          '<div class="flex flex-col"><span class="font-headline-sm text-on-surface">' + CS.esc(u.fullName) + '</span>' +
          '<span class="text-body-sm text-on-surface-variant font-code-mono">' + CS.esc(u.email) + '</span></div></div></td>' +
          '<td class="py-4 px-6">' + roleBadge + '</td>' +
          '<td class="py-4 px-6">' + statusBadge + '</td>' +
          '<td class="py-4 px-6"><div class="flex items-center gap-1">' + (perms || '<span class="text-on-surface-variant text-body-sm italic">None</span>') + '</div></td>' +
          '<td class="py-4 px-6 text-on-surface-variant font-code-mono text-body-sm">' + (u.lastActiveAt ? CS.ago(u.lastActiveAt) : 'Never') + '</td>' +
          '<td class="py-4 px-6 text-right"><div class="flex items-center justify-end gap-2">' +
          '<button class="p-2 rounded-lg bg-surface-container-high hover:bg-primary hover:text-on-primary text-on-surface-variant transition-all" title="Edit User"><span class="material-symbols-outlined text-sm">edit</span></button>' +
          actionBtn + '</div></td></tr>';
      }).join('');

      renderPagination();
    }).catch(function (e) { CS.toast('Users: ' + e.message, 'error'); });
  }

  /* ── Status actions ── */
  window.csSuspendUser = function (id) {
    CS.api('/api/users/' + id + '/status', { method: 'PATCH', body: { status: 'Suspended' } })
      .then(function () { CS.toast('User suspended.'); loadUsers(); loadSummary(); })
      .catch(function (e) { CS.toast(e.message, 'error'); });
  };

  window.csActivateUser = function (id) {
    CS.api('/api/users/' + id + '/status', { method: 'PATCH', body: { status: 'Active' } })
      .then(function () { CS.toast('User activated.'); loadUsers(); loadSummary(); })
      .catch(function (e) { CS.toast(e.message, 'error'); });
  };

  /* ── Provision user (modal) ── */
  window.csProvisionUser = function () {
    var nameEl = document.getElementById('cs-new-fullname');
    var emailEl = document.getElementById('cs-new-email');
    var roleEl = document.getElementById('cs-new-role');
    if (!nameEl || !emailEl || !roleEl) return;

    var fullName = nameEl.value.trim();
    var email = emailEl.value.trim();
    var roleMap = {
      'Security Analyst (Tier 1)': 'SecurityAnalyst',
      'Security Analyst (Tier 2)': 'SecurityAnalyst',
      'Administrator': 'Administrator',
      'Viewer': 'Viewer',
    };
    var role = roleMap[roleEl.value] || 'Viewer';

    if (!fullName || !email) { CS.toast('Fill in name and email.', 'error'); return; }

    CS.api('/api/users', { method: 'POST', body: { fullName: fullName, email: email, role: role } })
      .then(function (res) {
        CS.toast('User created. Temp password: ' + res.temporaryPassword);
        window.closeModal && window.closeModal();
        loadUsers(); loadSummary();
      })
      .catch(function (e) { CS.toast(e.message, 'error'); });
  };

  /* ── Filters ── */
  function bindFilters() {
    var roleFilter = document.getElementById('cs-role-filter');
    var statusFilter = document.getElementById('cs-status-filter');
    var searchInput = document.getElementById('cs-user-search');

    if (roleFilter) roleFilter.addEventListener('change', function () {
      var map = { 'All Roles': '', 'Administrator': 'Administrator', 'Security Analyst': 'SecurityAnalyst', 'Viewer': 'Viewer' };
      filterRole = map[this.value] || ''; page = 1; loadUsers();
    });
    if (statusFilter) statusFilter.addEventListener('change', function () {
      var map = { 'All Statuses': '', 'Active': 'Active', 'Suspended': 'Suspended', 'Pending': 'Pending' };
      filterStatus = map[this.value] || ''; page = 1; loadUsers();
    });
    if (searchInput) {
      var t;
      searchInput.addEventListener('input', function () {
        clearTimeout(t);
        var v = this.value;
        t = setTimeout(function () { filterSearch = v; page = 1; loadUsers(); }, 350);
      });
    }
  }

  /* ── Pagination ── */
  function renderPagination() {
    var el = document.getElementById('cs-users-pagination');
    if (!el) return;
    var btns = '';
    var start = Math.max(1, page - 1), end = Math.min(totalPages, page + 1);
    for (var p = start; p <= end; p++) {
      btns += '<button onclick="csUsersPage(' + p + ')" class="px-3 py-1.5 rounded-lg ' +
        (p === page ? 'bg-primary text-on-primary font-semibold' : 'bg-surface-container-high hover:bg-surface-container-highest text-on-surface') +
        '">' + p + '</button>';
    }
    el.innerHTML =
      '<button onclick="csUsersPage(' + (page - 1) + ')" ' + (page <= 1 ? 'disabled' : '') + ' class="px-3 py-1.5 rounded-lg bg-surface-container-high hover:bg-surface-container-highest disabled:opacity-50">Previous</button>' +
      btns +
      (totalPages > end ? '<span class="px-2">...</span><button onclick="csUsersPage(' + totalPages + ')" class="px-3 py-1.5 rounded-lg bg-surface-container-high">' + totalPages + '</button>' : '') +
      '<button onclick="csUsersPage(' + (page + 1) + ')" ' + (page >= totalPages ? 'disabled' : '') + ' class="px-3 py-1.5 rounded-lg bg-surface-container-high hover:bg-surface-container-highest disabled:opacity-50">Next</button>';
  }

  window.csUsersPage = function (p) {
    p = Math.max(1, Math.min(totalPages, p));
    if (p === page) return;
    page = p; loadUsers();
  };

  /* ── Helpers ── */
  function roleBadgeHtml(role) {
    var map = {
      Administrator: 'bg-primary/10 text-primary border-primary/20',
      SecurityAnalyst: 'bg-secondary/10 text-secondary border-secondary/20',
      Viewer: 'bg-surface-container-high text-on-surface-variant border-outline-variant/30',
    };
    var icon = role === 'Administrator' ? 'admin_panel_settings' : role === 'SecurityAnalyst' ? 'shield' : 'visibility';
    var label = role === 'SecurityAnalyst' ? 'Security Analyst' : role;
    return '<span class="inline-flex items-center gap-1.5 px-3 py-1 rounded-full text-label-md border ' + (map[role] || '') + '">' +
      '<span class="material-symbols-outlined text-xs">' + icon + '</span>' + CS.esc(label) + '</span>';
  }

  function statusBadgeHtml(s) {
    var map = {
      Active: 'bg-secondary/10 text-secondary border-secondary/20',
      Suspended: 'bg-error/10 text-error border-error/20',
      Pending: 'bg-tertiary/10 text-tertiary border-tertiary/20',
    };
    var dot = s === 'Active' ? 'bg-secondary animate-pulse' : s === 'Suspended' ? 'bg-error' : 'bg-tertiary';
    return '<span class="inline-flex items-center gap-1.5 px-2.5 py-1 rounded-full text-body-sm border ' + (map[s] || '') + '">' +
      '<span class="w-1.5 h-1.5 rounded-full ' + dot + '"></span>' + CS.esc(s) + '</span>';
  }

  function initials(name) {
    if (!name) return '?';
    return name.split(' ').slice(0, 2).map(function (w) { return w[0]; }).join('').toUpperCase();
  }

  function setText(id, val) {
    var el = document.getElementById(id);
    if (el) el.textContent = val;
  }
})();
