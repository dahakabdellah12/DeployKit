let token = localStorage.getItem('dk_token');
const baseUrl = window.location.origin;
let currentAppKey = null;
let currentPackages = [];
let idleTimer = null;
const IDLE_TIMEOUT = 30 * 60 * 1000;
let authMode = 'login';

function resetIdleTimer() {
  if (idleTimer) clearTimeout(idleTimer);
  idleTimer = setTimeout(() => {
    alert('⚠️ انتهت الجلسة بسبب عدم النشاط. سجّل دخولك مرة أخرى.');
    logout();
  }, IDLE_TIMEOUT);
}

async function api(method, path, body, extraHeaders) {
  const controller = new AbortController();
  const timeoutId = setTimeout(() => controller.abort(), 15000);
  const opts = { method, headers: { 'Authorization': 'Bearer ' + token, ...extraHeaders }, signal: controller.signal };
  if (body) {
    opts.headers['Content-Type'] = 'application/json';
    opts.body = JSON.stringify(body);
  }
  try {
    const res = await fetch(baseUrl + path, opts);
    clearTimeout(timeoutId);
    if (res.status === 401) {
      localStorage.removeItem('dk_token');
      showLogin();
      throw new Error('انتهت الجلسة. سجل دخولك مرة أخرى');
    }
    if (!res.ok) { const e = await res.json().catch(()=>({})); throw new Error(e.error || res.statusText); }
    return res.status === 204 ? null : res.json();
  } catch(e) {
    clearTimeout(timeoutId);
    if (e.name === 'AbortError') throw new Error('انتهت مهلة الطلب. حاول مرة أخرى');
    throw e;
  }
}

function showLogin() {
  token = '';
  localStorage.removeItem('dk_token');
  document.getElementById('app').classList.add('hidden');
  document.getElementById('login').classList.remove('hidden');
}

async function logout() {
  try { await api('POST', '/v1/admin/logout'); } catch {}
  showLogin();
}

function esc(s) { if (!s) return ''; return s.replace(/&/g,'&amp;').replace(/</g,'&lt;').replace(/>/g,'&gt;').replace(/"/g,'&quot;'); }

function copyText(text) {
  navigator.clipboard.writeText(text).then(() => {
    alert('✅ تم النسخ!');
  }).catch(() => {
    const ta = document.createElement('textarea');
    ta.value = text;
    document.body.appendChild(ta);
    ta.select();
    document.execCommand('copy');
    document.body.removeChild(ta);
    alert('✅ تم النسخ!');
  });
}

// ---------- Auth ----------
function toggleAuth() {
  authMode = authMode === 'login' ? 'setup' : 'login';
  if (authMode === 'login') {
    document.getElementById('authTitle').textContent = 'تسجيل الدخول';
    document.getElementById('authBtn').textContent = 'دخول';
    document.getElementById('authToggle').textContent = 'ليس لديك حساب؟ إنشاء حساب جديد';
  } else {
    document.getElementById('authTitle').textContent = 'إنشاء حساب المسؤول';
    document.getElementById('authBtn').textContent = 'إنشاء الحساب';
    document.getElementById('authToggle').textContent = 'لديك حساب؟ تسجيل الدخول';
  }
  document.getElementById('authError').classList.add('hidden');
}

async function submitAuth() {
  const username = document.getElementById('authUser').value.trim();
  const password = document.getElementById('authPass').value.trim();
  const error = document.getElementById('authError');
  if (!username || !password) { error.textContent = 'الرجاء ملء جميع الحقول'; error.classList.remove('hidden'); return; }
  try {
    const endpoint = authMode === 'login' ? '/v1/admin/login' : '/v1/admin/setup';
    const res = await fetch(baseUrl + endpoint, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ username, password })
    }).then(r => r.json());
    if (res.error) { error.textContent = res.error; error.classList.remove('hidden'); return; }
    token = res.token;
    localStorage.setItem('dk_token', token);
    document.getElementById('login').classList.add('hidden');
    document.getElementById('app').classList.remove('hidden');
    loadApps();
    resetIdleTimer();
  } catch(e) { error.textContent = '❌ ' + e.message; error.classList.remove('hidden'); }
}

// ---------- Apps list ----------
async function loadApps() {
  try {
    const apps = await api('GET', '/v1/admin/apps');
    renderApps(apps || []);
  } catch(e) { alert(e.message); }
}

function renderApps(apps) {
  const body = document.getElementById('appsBody');
  const empty = document.getElementById('appsEmpty');
  body.innerHTML = '';
  if (!apps || apps.length === 0) { empty.classList.remove('hidden'); return; }
  empty.classList.add('hidden');
  apps.forEach(a => {
    body.innerHTML += `<tr>
      <td><a href="#" class="app-link" data-app-key="${esc(a.appKey)}" style="color:#60a5fa;text-decoration:none">${esc(a.appName)}</a></td>
      <td style="font-family:monospace;font-size:12px">${esc(a.appKey)}</td>
      <td><span class="badge badge-green">v${esc(a.currentVersion)}</span></td>
      <td>${a.packageCount}</td>
      <td style="font-size:12px;color:#a1a1aa">${new Date(a.createdAt).toLocaleDateString('ar-SA')}</td>
      <td><button class="btn btn-danger btn-sm btn-delete-app" data-app-key="${esc(a.appKey)}">حذف</button></td>
    </tr>`;
  });

  const stats = document.getElementById('statsRow');
  stats.innerHTML = `
    <div class="card stat"><div class="stat-value">${apps.length}</div><div class="stat-label">تطبيق مسجل</div></div>
    <div class="card stat"><div class="stat-value">${apps.reduce((s,a) => s + a.packageCount, 0)}</div><div class="stat-label">تحديث مرفوع</div></div>
  `;
}

// ---------- App detail ----------
async function showApp(key) {
  try {
    const d = await api('GET', `/v1/admin/apps/${key}`);
    if (!d) return;
    currentAppKey = key;
    currentPackages = d.packages || [];
    document.getElementById('appsBody').parentElement.parentElement.classList.add('hidden');
    document.getElementById('appDetail').classList.remove('hidden');
    const html = `
      <div class="detail-grid">
        <div class="card">
          <h2>${esc(d.appName)}</h2>
          <div style="font-size:12px;color:#a1a1aa;margin-top:8px">🔑 API Key</div>
          <div class="key-display">${esc(d.appKey)}</div>
          <span class="copy-hint" data-copy="${esc(d.appKey)}">📋 نسخ المفتاح</span>
          <div style="margin-top:8px"><span class="badge badge-green">v${esc(d.currentVersion)}</span></div>
          <div style="margin-top:8px;font-size:12px;color:#71717a">${new Date(d.createdAt).toLocaleDateString('ar-SA')}</div>
          <div style="margin-top:12px"><button class="btn btn-sm btn-regenerate" data-app-key="${esc(d.appKey)}" style="background:#1e3a5f;color:#93c5fd">🔄 إعادة توليد المفتاح</button></div>
        </div>
        <div class="card">
          <h2>حذف التطبيق</h2>
          <div style="font-size:13px;color:#a1a1aa;margin-bottom:12px">هذا سيحذف التطبيق وكل تحديثاته</div>
          <button class="btn btn-danger btn-sm btn-delete-app-detail" data-app-key="${esc(d.appKey)}">حذف التطبيق</button>
        </div>
      </div>
      <div class="card">
        <h2>التحديثات (${d.packages.length})</h2>
        <table>
          <thead><tr><th>#</th><th>الإصدار</th><th>رابط التحميل</th><th>النوع</th><th>إجباري</th><th>التاريخ</th><th></th></tr></thead>
          <tbody>
            ${d.packages.length === 0 ? '<tr><td colspan="7" class="empty">لا توجد تحديثات</td></tr>' :
            d.packages.map(p => `<tr>
              <td>${p.id}</td>
              <td><span class="badge badge-green">v${esc(p.version)}</span></td>
              <td><a href="${esc(p.downloadUrl)}" target="_blank" style="color:#60a5fa;font-size:12px">تحميل</a></td>
              <td><span class="badge badge-blue">كاملة</span></td>
              <td>${p.isMandatory ? '<span class="badge badge-yellow">إجباري</span>' : '<span class="badge badge-gray">اختياري</span>'}</td>
              <td style="font-size:12px;color:#a1a1aa">${new Date(p.createdAt).toLocaleDateString('ar-SA')}</td>
              <td style="white-space:nowrap">
                <button class="btn btn-sm btn-edit-pkg" data-pkg-id="${p.id}" style="background:#1e3a5f;color:#93c5fd;margin-left:4px">تعديل</button>
                <button class="btn btn-danger btn-sm btn-delete-pkg" data-pkg-id="${p.id}">حذف</button>
              </td>
            </tr>`).join('')}
          </tbody>
        </table>
      </div>
      ${d.packages.length > 0 ? `<div class="card"><h2>ملاحظات الإصدار الأخير</h2><pre style="font-size:13px;color:#a1a1aa;white-space:pre-wrap;margin-top:8px">${esc(d.packages[0].releaseNotes) || 'لا توجد ملاحظات'}</pre></div>` : ''}
    `;
    document.getElementById('detailContent').innerHTML = html;
  } catch(e) { alert(e.message); }
}

function hideDetail() {
  document.getElementById('appDetail').classList.add('hidden');
  document.getElementById('appsBody').parentElement.parentElement.classList.remove('hidden');
}

async function deleteApp(key) {
  if (!confirm('حذف هذا التطبيق وكل تحديثاته؟')) return;
  try { await api('DELETE', `/v1/admin/apps/${key}`); loadApps(); } catch(e) { alert(e.message); }
}

async function deletePackage(id) {
  if (!confirm('حذف هذا التحديث؟')) return;
  try {
    await api('DELETE', `/v1/admin/packages/${id}`);
    if (!document.getElementById('appDetail').classList.contains('hidden'))
      document.getElementById('detailContent').innerHTML = '';
    loadApps();
    hideDetail();
  } catch(e) { alert(e.message); }
}

async function regenerateKey(key) {
  if (!confirm('تأكيد إعادة توليد مفتاح API؟ المفتاح القديم سيتوقف فوراً')) return;
  try {
    const res = await api('POST', `/v1/admin/apps/${key}/regenerate-key`);
    if (res && res.appKey) {
      alert('✅ تم إعادة توليد المفتاح بنجاح');
      showApp(res.appKey);
    }
  } catch(e) { alert(e.message); }
}

// ---------- Register modal ----------
function showRegisterModal() {
  document.getElementById('registerModal').classList.remove('hidden');
  document.getElementById('registerStatus').classList.add('hidden');
  document.getElementById('registerResult').classList.add('hidden');
  document.getElementById('regName').value = '';
  document.getElementById('regName').focus();
}
function hideRegisterModal() { document.getElementById('registerModal').classList.add('hidden'); }

async function registerApp() {
  const name = document.getElementById('regName').value.trim();
  const status = document.getElementById('registerStatus');
  const result = document.getElementById('registerResult');
  if (!name) { status.textContent = 'الرجاء إدخال اسم التطبيق'; status.style.color = '#f87171'; status.classList.remove('hidden'); return; }
  try {
    const res = await api('POST', '/v1/admin/register', { appName: name });
    if (!res) return;
    result.innerHTML = `<div style="color:#86efac;margin-bottom:8px">✅ تم التسجيل!</div>
      <div>الاسم: ${esc(res.appName)}</div>
      <div style="margin-top:12px">🔑 API Key:</div>
      <div class="key-display">${esc(res.appKey)}</div>
      <span class="copy-hint" data-copy="${esc(res.appKey)}">📋 نسخ المفتاح</span>`;
    result.classList.remove('hidden');
    status.classList.add('hidden');
    document.getElementById('regName').value = '';
    loadApps();
  } catch(e) { status.textContent = '❌ ' + e.message; status.style.color = '#f87171'; status.classList.remove('hidden'); }
}

// ---------- Upload modal ----------
function showUploadModal() { document.getElementById('uploadModal').classList.remove('hidden'); }
function hideUploadModal() { document.getElementById('uploadModal').classList.add('hidden'); document.getElementById('uploadStatus').classList.add('hidden'); }

async function uploadUpdate() {
  const key = document.getElementById('upKey').value.trim();
  const version = document.getElementById('upVersion').value.trim();
  const url = document.getElementById('upUrl').value.trim();
  const notes = document.getElementById('upNotes').value.trim();
  const mandatory = document.getElementById('upMandatory').checked;
  const status = document.getElementById('uploadStatus');

  if (!key || !version || !url) { status.textContent = 'الرجاء ملء جميع الحقول المطلوبة'; status.style.color = '#f87171'; status.classList.remove('hidden'); return; }

  try {
    let query = `version=${encodeURIComponent(version)}&url=${encodeURIComponent(url)}`;
    if (notes) query += `&notes=${encodeURIComponent(notes)}`;
    if (mandatory) query += '&mandatory=true';
    const res = await api('POST', `/v1/upload?${query}`, null, { 'X-Api-Key': key });
    if (!res) return;
    status.textContent = `✅ تم الإضافة! v${version} — ${res.isMandatory ? 'إجباري' : 'اختياري'} (حزمة كاملة)`;
    status.style.color = '#86efac';
    status.classList.remove('hidden');
    document.getElementById('upKey').value = '';
    document.getElementById('upVersion').value = '';
    document.getElementById('upUrl').value = '';
    document.getElementById('upNotes').value = '';
    document.getElementById('upMandatory').checked = false;
    loadApps();
  } catch(e) { status.textContent = '❌ ' + e.message; status.style.color = '#f87171'; status.classList.remove('hidden'); }
}

// ---------- Edit modal ----------
function showEditModal(pkgId) {
  const pkg = currentPackages.find(p => p.id === pkgId);
  if (!pkg) return;
  document.getElementById('editVersion').value = pkg.version;
  document.getElementById('editUrl').value = pkg.downloadUrl;
  document.getElementById('editNotes').value = pkg.releaseNotes || '';
  document.getElementById('editMandatory').checked = pkg.isMandatory;
  document.getElementById('editModal').dataset.pkgId = pkgId;
  document.getElementById('editStatus').classList.add('hidden');
  document.getElementById('editModal').classList.remove('hidden');
}
function hideEditModal() {
  document.getElementById('editModal').classList.add('hidden');
  document.getElementById('editStatus').classList.add('hidden');
}
async function saveEditPackage() {
  const pkgId = parseInt(document.getElementById('editModal').dataset.pkgId);
  const version = document.getElementById('editVersion').value.trim();
  const url = document.getElementById('editUrl').value.trim();
  const notes = document.getElementById('editNotes').value.trim();
  const mandatory = document.getElementById('editMandatory').checked;
  const status = document.getElementById('editStatus');

  if (!version || !url) { status.textContent = 'الإصدار والرابط مطلوبان'; status.style.color = '#f87171'; status.classList.remove('hidden'); return; }

  try {
    const res = await api('PATCH', `/v1/admin/packages/${pkgId}`, {
      version, downloadUrl: url, releaseNotes: notes, isMandatory: mandatory
    });
    if (!res) return;
    status.textContent = '✅ تم الحفظ!';
    status.style.color = '#86efac';
    status.classList.remove('hidden');
    setTimeout(() => { hideEditModal(); if (currentAppKey) showApp(currentAppKey); }, 800);
  } catch(e) { status.textContent = '❌ ' + e.message; status.style.color = '#f87171'; status.classList.remove('hidden'); }
}

// ---------- Init ----------
async function init() {
  if (token) {
    try {
      const apps = await api('GET', '/v1/admin/apps');
      document.getElementById('login').classList.add('hidden');
      document.getElementById('app').classList.remove('hidden');
      renderApps(apps || []);
      resetIdleTimer();
    } catch {
      showLogin();
    }
  }
}

// ---------- Event listeners ----------
document.addEventListener('DOMContentLoaded', () => {
  ['click', 'keydown', 'mousemove', 'scroll', 'touchstart'].forEach(ev =>
    document.addEventListener(ev, resetIdleTimer)
  );

  document.getElementById('authUser').addEventListener('keydown', e => { if (e.key === 'Enter') submitAuth(); });
  document.getElementById('authPass').addEventListener('keydown', e => { if (e.key === 'Enter') submitAuth(); });
  document.getElementById('authBtn').addEventListener('click', submitAuth);
  document.getElementById('authToggle').addEventListener('click', e => { e.preventDefault(); toggleAuth(); });

  document.getElementById('showRegisterBtn').addEventListener('click', showRegisterModal);
  document.getElementById('showUploadBtn').addEventListener('click', showUploadModal);
  document.getElementById('logoutBtn').addEventListener('click', logout);

  document.getElementById('backBtn').addEventListener('click', hideDetail);

  document.getElementById('regName').addEventListener('keydown', e => { if (e.key === 'Enter') registerApp(); });
  document.getElementById('hideRegisterBtn').addEventListener('click', hideRegisterModal);
  document.getElementById('registerBtn').addEventListener('click', registerApp);

  document.getElementById('hideUploadBtn').addEventListener('click', hideUploadModal);
  document.getElementById('uploadBtn').addEventListener('click', uploadUpdate);

  document.getElementById('hideEditBtn').addEventListener('click', hideEditModal);
  document.getElementById('saveEditBtn').addEventListener('click', saveEditPackage);

  document.getElementById('appsTable').addEventListener('click', e => {
    const link = e.target.closest('.app-link');
    if (link) { e.preventDefault(); showApp(link.dataset.appKey); return; }
    const del = e.target.closest('.btn-delete-app');
    if (del) { deleteApp(del.dataset.appKey); return; }
  });

  document.getElementById('detailContent').addEventListener('click', e => {
    const copy = e.target.closest('[data-copy]');
    if (copy) { copyText(copy.dataset.copy); return; }
    const regen = e.target.closest('.btn-regenerate');
    if (regen) { regenerateKey(regen.dataset.appKey); return; }
    const delApp = e.target.closest('.btn-delete-app-detail');
    if (delApp) {
      if (confirm('متأكد؟')) { deleteApp(delApp.dataset.appKey); hideDetail(); }
      return;
    }
    const editPkg = e.target.closest('.btn-edit-pkg');
    if (editPkg) { showEditModal(parseInt(editPkg.dataset.pkgId)); return; }
    const delPkg = e.target.closest('.btn-delete-pkg');
    if (delPkg) { deletePackage(parseInt(delPkg.dataset.pkgId)); return; }
  });

  document.getElementById('registerResult').addEventListener('click', e => {
    const copy = e.target.closest('[data-copy]');
    if (copy) copyText(copy.dataset.copy);
  });

  init();
});
