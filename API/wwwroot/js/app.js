/**
 * ConceptJewelleryPro - Luxury Web Portal Engine
 * Connects directly with ASP.NET Core API Service on localhost
 */

// ── State Management ────────────────────────────────────────────────────────
const State = {
  token: localStorage.getItem('cjp_jwt_token') || null,
  user: JSON.parse(localStorage.getItem('cjp_user_profile') || 'null'),
  serverUrl: localStorage.getItem('cjp_server_url') || window.location.origin,
  theme: localStorage.getItem('cjp_theme') || 'glassmorphism',
  currentView: 'dashboard',
  rates: {
    gold22k: 6850,
    gold18k: 5600,
    gold24k: 7450,
    silver: 88,
  },
  catalogue: [],
  invoiceItems: [],
  selectedCatalogueItem: null
};

// ── API Helper ──────────────────────────────────────────────────────────────
async function apiRequest(endpoint, options = {}) {
  const url = `${State.serverUrl.replace(/\/+$/, '')}${endpoint.startsWith('/') ? '' : '/'}${endpoint}`;
  const headers = options.headers || {};

  if (State.token) {
    headers['Authorization'] = `Bearer ${State.token}`;
  }

  if (!(options.body instanceof FormData) && !headers['Content-Type'] && options.body) {
    headers['Content-Type'] = 'application/json';
  }

  try {
    const response = await fetch(url, { ...options, headers });
    
    if (response.status === 401) {
      showToast('Session expired or unauthorized. Please login.', 'error');
      logout();
      return null;
    }

    const contentType = response.headers.get('content-type') || '';
    let data;
    if (contentType.includes('application/json')) {
      data = await response.json();
    } else {
      data = await response.text();
    }

    if (!response.ok) {
      const errorMsg = (typeof data === 'object' && data?.message) ? data.message : (data || 'Request failed');
      throw new Error(errorMsg);
    }

    return data;
  } catch (err) {
    console.error(`API Error [${endpoint}]:`, err);
    throw err;
  }
}

// ── Toast Notifications ─────────────────────────────────────────────────────
let _lastToastMsg = '';
let _lastToastTime = 0;
function showToast(message, type = 'info') {
  const now = Date.now();
  if (message === _lastToastMsg && (now - _lastToastTime < 2000)) {
    return; // Suppress duplicate toast bursts
  }
  _lastToastMsg = message;
  _lastToastTime = now;

  const container = document.getElementById('toast-container');
  if (!container) return;

  const toast = document.createElement('div');
  toast.className = `toast ${type}`;
  
  const icon = type === 'success' ? '✅' : type === 'error' ? '❌' : '✨';
  toast.innerHTML = `<span>${icon}</span><span>${message}</span>`;
  
  container.appendChild(toast);

  setTimeout(() => {
    toast.style.opacity = '0';
    toast.style.transform = 'translateX(100%)';
    toast.style.transition = 'all 0.3s ease';
    setTimeout(() => toast.remove(), 300);
  }, 4000);
}

// ── Theme Management ────────────────────────────────────────────────────────
function applyTheme(themeName) {
  State.theme = themeName;
  localStorage.setItem('cjp_theme', themeName);
  document.body.setAttribute('data-theme', themeName);

  document.querySelectorAll('.theme-card-option').forEach(el => {
    if (el.dataset.theme === themeName) {
      el.classList.add('border-gold');
      el.style.borderColor = 'var(--accent-gold)';
    } else {
      el.classList.remove('border-gold');
      el.style.borderColor = 'var(--border-subtle)';
    }
  });
}

// ── Authentication ──────────────────────────────────────────────────────────
async function handleLogin(e) {
  if (e) e.preventDefault();
  
  const usernameInput = document.getElementById('login-username');
  const passwordInput = document.getElementById('login-password');
  const btn = document.getElementById('login-submit-btn');

  const username = usernameInput?.value.trim();
  const password = passwordInput?.value.trim();

  if (!username || !password) {
    showToast('Please enter both username and password.', 'error');
    return;
  }

  btn.disabled = true;
  btn.innerText = 'Authenticating...';

  try {
    const res = await apiRequest('/api/Auth/login', {
      method: 'POST',
      body: JSON.stringify({ username, password })
    });

    if (res && res.token) {
      State.token = res.token;
      State.user = {
        username: res.username || username,
        role: res.role || 'Staff',
        expiresAt: res.expiresAt
      };

      localStorage.setItem('cjp_jwt_token', State.token);
      localStorage.setItem('cjp_user_profile', JSON.stringify(State.user));

      showToast(`Welcome back, ${State.user.username}!`, 'success');
      updateUserUI();
      navigateTo('dashboard');
    }
  } catch (err) {
    showToast(err.message || 'Login failed. Verify credentials.', 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = 'Sign In to Portal ✨';
  }
}

function fillDemoUser(user, pass) {
  const u = document.getElementById('login-username');
  const p = document.getElementById('login-password');
  if (u) u.value = user;
  if (p) p.value = pass;
  showToast(`Loaded test credentials for "${user}"`, 'info');
}

function logout() {
  State.token = null;
  State.user = null;
  localStorage.removeItem('cjp_jwt_token');
  localStorage.removeItem('cjp_user_profile');
  updateUserUI();
  navigateTo('login');
  showToast('Logged out successfully.', 'info');
}

function updateUserUI() {
  const userPill = document.getElementById('header-user-pill');
  const userName = document.getElementById('header-user-name');
  const userRole = document.getElementById('header-user-role');
  const userAvatar = document.getElementById('header-user-avatar');
  const logoutBtn = document.getElementById('header-logout-btn');

  if (State.token && State.user) {
    if (userPill) userPill.style.display = 'flex';
    if (logoutBtn) logoutBtn.style.display = 'flex';
    if (userName) userName.innerText = State.user.username.toUpperCase();
    if (userRole) userRole.innerText = State.user.role || 'Staff';
    if (userAvatar) userAvatar.innerText = (State.user.username[0] || 'U').toUpperCase();
    
    // Dashboard greeting
    const dashGreet = document.getElementById('dash-staff-name');
    if (dashGreet) dashGreet.innerText = State.user.username.toUpperCase();
    const dashRole = document.getElementById('dash-staff-role');
    if (dashRole) dashRole.innerText = `${State.user.role} • Showroom Portal`;
  } else {
    if (userPill) userPill.style.display = 'none';
    if (logoutBtn) logoutBtn.style.display = 'none';
  }
}

// ── View Router ─────────────────────────────────────────────────────────────
function navigateTo(viewId) {
  // If not logged in, force login view
  if (!State.token && viewId !== 'login') {
    viewId = 'login';
  }

  State.currentView = viewId;
  window.location.hash = viewId;

  // Hide all views
  document.querySelectorAll('.view-section').forEach(section => {
    section.style.display = 'none';
  });

  // Show target view
  const target = document.getElementById(`view-${viewId}`);
  if (target) {
    target.style.display = 'block';
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }

  // Update mobile bottom nav active item
  const bottomNav = document.querySelector('.mobile-bottom-nav');
  if (bottomNav) {
    if (viewId === 'login' || !State.token) {
      bottomNav.style.display = 'none';
    } else {
      bottomNav.style.display = '';
      document.querySelectorAll('.bottom-nav-item').forEach(item => {
        const itemClick = item.getAttribute('onclick') || '';
        if (itemClick.includes(`'${viewId}'`)) {
          item.classList.add('active');
        } else {
          item.classList.remove('active');
        }
      });
    }
  }

  if (viewId === 'catalogue') {
    loadCatalogueFilterOptions();
    fetchCatalogue();
  } else if (viewId === 'stock-summary') {
    fetchStockSummary();
  } else if (viewId === 'settings') {
    loadSettingsView();
  } else if (viewId === 'generate-invoice') {
    loadVoucherTypes();
  } else if (viewId === 'image-update') {
    // image-update view
  } else if (viewId === 'sticker-reprint') {
    loadPrintersList();
  }
}

// ── Module 1: Barcode Valuation ─────────────────────────────────────────────
let currentValuationBarcode = null;

async function lookupValuationBarcode() {
  const input = document.getElementById('val-barcode-input');
  const barcode = input?.value.trim();
  if (!barcode) {
    showToast('Please enter or scan a barcode.', 'error');
    return;
  }

  const btn = document.getElementById('val-search-btn');
  btn.disabled = true;
  btn.innerHTML = '⌛';

  try {
    const data = await apiRequest(`/api/Barcode/${encodeURIComponent(barcode)}`);
    currentValuationBarcode = data;
    renderValuationProduct(data);
    calculateValuation();
  } catch (err) {
    showToast(err.message || 'Barcode not found.', 'error');
    document.getElementById('val-product-card').style.display = 'none';
    document.getElementById('val-receipt-card').style.display = 'none';
  } finally {
    btn.disabled = false;
    btn.innerHTML = '🔍';
  }
}

function renderValuationProduct(item) {
  const card = document.getElementById('val-product-card');
  card.style.display = 'block';

  document.getElementById('val-item-name').innerText = item.itemName || 'Jewellery Article';
  document.getElementById('val-item-barcode').innerText = `#${item.barcode} • ${item.purity}`;
  document.getElementById('val-item-desc').innerText = item.description || 'Standard showroom stock item';
  document.getElementById('val-gross-wt').innerText = `${item.grossWt || 0}g`;
  document.getElementById('val-stone-wt').innerText = `${item.stoneWt || 0}g`;
  document.getElementById('val-net-wt').innerText = `${item.netWt || 0}g`;
  document.getElementById('val-rate-gram').innerText = `₹${(item.ratePerGram || State.rates.gold22k).toLocaleString()}/g`;

  const img = document.getElementById('val-item-img');
  if (item.imageUrl) {
    img.src = `${State.serverUrl.replace(/\/+$/, '')}${item.imageUrl}?t=${Date.now()}`;
    img.style.display = 'block';
    document.getElementById('val-img-placeholder').style.display = 'none';
  } else {
    img.style.display = 'none';
    document.getElementById('val-img-placeholder').style.display = 'flex';
  }

  // Auto-set making charges from item if present
  if (item.makingType === 'PERCENT' && item.making > 0) {
    document.getElementById('val-making-percent').value = item.making;
    document.getElementById('val-making-per-gram').value = '';
  } else if (item.mPerGr > 0) {
    document.getElementById('val-making-per-gram').value = item.mPerGr;
    document.getElementById('val-making-percent').value = '';
  } else if (item.making > 0) {
    document.getElementById('val-making-per-gram').value = item.making;
    document.getElementById('val-making-percent').value = '';
  }
}

async function calculateValuation() {
  if (!currentValuationBarcode) {
    showToast('Please look up a barcode first.', 'error');
    return;
  }

  const barcode = currentValuationBarcode.barcode;
  const makingPerGram = parseFloat(document.getElementById('val-making-per-gram').value) || undefined;
  const makingPercent = parseFloat(document.getElementById('val-making-percent').value) || undefined;
  const gstMode = document.querySelector('input[name="val-gst-mode"]:checked')?.value || 'ExclusiveGST';

  const payload = {
    barcode,
    gstMode,
    makingPerGram,
    makingPercent
  };

  try {
    const res = await apiRequest('/api/Valuation', {
      method: 'POST',
      body: JSON.stringify(payload)
    });

    renderValuationReceipt(res);
  } catch (err) {
    showToast(err.message || 'Failed to calculate valuation.', 'error');
  }
}

function renderValuationReceipt(res) {
  const card = document.getElementById('val-receipt-card');
  card.style.display = 'block';

  document.getElementById('receipt-barcode').innerText = `#${res.barcode} • ${res.purity}`;
  document.getElementById('receipt-metal-calc').innerText = `${res.netWt}g × ₹${res.ratePerGram?.toLocaleString()}`;
  document.getElementById('receipt-metal-val').innerText = `₹${res.metalValue?.toLocaleString()}`;
  document.getElementById('receipt-stone-val').innerText = `₹${(res.stoneValue || 0).toLocaleString()}`;
  document.getElementById('receipt-making-val').innerText = `₹${(res.makingAmount || 0).toLocaleString()}`;
  document.getElementById('receipt-subtotal').innerText = `₹${res.subTotal?.toLocaleString()}`;
  document.getElementById('receipt-gst-label').innerText = `GST (${res.gstPercent}%) [${res.gstMode}]`;
  document.getElementById('receipt-gst-val').innerText = `₹${res.gstAmount?.toLocaleString()}`;
  document.getElementById('receipt-final-price').innerText = `₹${res.finalPrice?.toLocaleString()}`;
}

// ── Module 2: Catalogue ─────────────────────────────────────────────────────
let currentCategory = 'All';
let catalogueFiltersLoaded = false;

async function loadCatalogueFilterOptions() {
  if (catalogueFiltersLoaded) return;

  try {
    const [itemNames, sizes] = await Promise.all([
      apiRequest('/api/Catalogue/itemnames').catch(() => []),
      apiRequest('/api/Catalogue/sizes').catch(() => [])
    ]);

    const itemSelect = document.getElementById('cat-itemname-select');
    if (itemSelect && Array.isArray(itemNames) && itemNames.length > 0) {
      const currentVal = itemSelect.value;
      itemSelect.innerHTML = '<option value="All">All Items</option>' +
        itemNames.map(name => `<option value="${escapeHtml(name)}">${escapeHtml(name)}</option>`).join('');
      if (currentVal && itemNames.includes(currentVal)) {
        itemSelect.value = currentVal;
      }
    }

    const sizeSelect = document.getElementById('cat-size-select');
    if (sizeSelect && Array.isArray(sizes) && sizes.length > 0) {
      const currentVal = sizeSelect.value;
      sizeSelect.innerHTML = '<option value="All">All Sizes</option>' +
        sizes.map(sz => `<option value="${escapeHtml(sz)}">${escapeHtml(sz)}</option>`).join('');
      if (currentVal && sizes.includes(currentVal)) {
        sizeSelect.value = currentVal;
      }
    }

    catalogueFiltersLoaded = true;
  } catch (err) {
    console.warn('Could not load catalogue filter options:', err);
  }
}

function onCatalogueItemNameChange() {
  const select = document.getElementById('cat-itemname-select');
  const val = select ? select.value : 'All';
  currentCategory = val;

  // Sync category pills: highlight if exact match or if "All", else remove active from pills
  document.querySelectorAll('.cat-pill').forEach(pill => {
    const pillCat = pill.dataset.category;
    if (val === 'All' && pillCat === 'All') {
      pill.classList.add('active');
    } else if (val !== 'All' && pillCat.toLowerCase() === val.toLowerCase()) {
      pill.classList.add('active');
    } else {
      pill.classList.remove('active');
    }
  });

  fetchCatalogue();
}

function resetCatalogueFilters() {
  const search = document.getElementById('cat-search-input');
  if (search) search.value = '';
  const itemSelect = document.getElementById('cat-itemname-select');
  if (itemSelect) itemSelect.value = 'All';
  const sizeSelect = document.getElementById('cat-size-select');
  if (sizeSelect) sizeSelect.value = 'All';
  const fromWt = document.getElementById('cat-from-wt');
  if (fromWt) fromWt.value = '';
  const toWt = document.getElementById('cat-to-wt');
  if (toWt) toWt.value = '';
  const purity = document.getElementById('cat-purity-select');
  if (purity) purity.value = 'All';

  currentCategory = 'All';
  document.querySelectorAll('.cat-pill').forEach(pill => {
    if (pill.dataset.category === 'All') pill.classList.add('active');
    else pill.classList.remove('active');
  });

  fetchCatalogue();
}

async function fetchCatalogue(params = {}) {
  const grid = document.getElementById('catalogue-items-grid');
  const loading = document.getElementById('catalogue-loading');
  const empty = document.getElementById('catalogue-empty');

  if (loading) loading.style.display = 'flex';
  if (grid) grid.style.display = 'none';
  if (empty) empty.style.display = 'none';

  const query = new URLSearchParams();
  const search = document.getElementById('cat-search-input')?.value.trim();
  const itemSelectVal = document.getElementById('cat-itemname-select')?.value;
  const size = document.getElementById('cat-size-select')?.value;
  const fromWt = document.getElementById('cat-from-wt')?.value.trim();
  const toWt = document.getElementById('cat-to-wt')?.value.trim();
  const purity = document.getElementById('cat-purity-select')?.value;

  // Item name filter
  if (itemSelectVal && itemSelectVal !== 'All') {
    query.append('itemName', itemSelectVal);
  } else if (currentCategory && currentCategory !== 'All') {
    query.append('itemName', currentCategory);
  }

  // Size filter
  if (size && size !== 'All') {
    query.append('size', size);
  }

  // Weight filters
  if (fromWt) query.append('fromWt', fromWt);
  if (toWt) query.append('toWt', toWt);

  // Search keyword or barcode
  if (search) {
    if (/^\d+$/.test(search)) {
      query.append('barcode', search);
    } else {
      if (!query.has('itemName')) {
        query.append('itemName', search);
      } else {
        query.append('description', search);
      }
    }
  }

  try {
    const data = await apiRequest(`/api/Catalogue?${query.toString()}`);
    let list = Array.isArray(data) ? data : [];

    if (purity && purity !== 'All') {
      list = list.filter(x => x.purity?.toLowerCase().includes(purity.toLowerCase()));
    }

    State.catalogue = list;
    renderCatalogueGrid(list);
  } catch (err) {
    console.warn('Catalogue error, using fallback stock list', err);
    renderCatalogueGrid([
      { barcode: '005008', itemName: 'EARRING', purity: '916HALL MARKED', grossWt: 5.39, netWt: 5.39, description: 'TARKISH ANTIQUE', mrp: 81332 },
      { barcode: '024216', itemName: 'CHOOR', purity: '916HALL MARKED', grossWt: 21.0, netWt: 21.0, description: 'PLASTER FINISH', mrp: 307146 }
    ]);
  } finally {
    if (loading) loading.style.display = 'none';
  }
}

let catalogueImageObserver = null;

function loadCatalogueThumb(wrapper) {
  const img = wrapper.querySelector('img.stock-thumb');
  const placeholder = wrapper.querySelector('.stock-thumb-placeholder');
  if (!img || img.dataset.loaded) return;
  const src = img.dataset.src;
  if (!src) return;
  img.dataset.loaded = '1';

  img.onload = function() {
    img.style.display = 'block';
    if (placeholder) placeholder.style.display = 'none';
  };
  img.onerror = function() {
    img.style.display = 'none';
    if (placeholder) placeholder.style.display = 'flex';
  };
  img.src = src;
}

function renderCatalogueGrid(items) {
  const grid = document.getElementById('catalogue-items-grid');
  const empty = document.getElementById('catalogue-empty');

  if (!items || items.length === 0) {
    if (grid) grid.style.display = 'none';
    if (empty) empty.style.display = 'block';
    return;
  }

  if (empty) empty.style.display = 'none';
  if (grid) {
    grid.style.display = 'grid';
    const serverBase = (State.serverUrl || window.location.origin).replace(/\/+$/, '');
    const cacheBust = Date.now();

    grid.innerHTML = items.map(item => {
      const imageUrl = `${serverBase}/api/Barcode/image/${encodeURIComponent(item.barcode)}?t=${cacheBust}`;
      const sizeBadge = (item.size && item.size !== '-') ? `<span style="background:rgba(212,175,55,0.18); color:var(--accent-gold); padding:2px 7px; border-radius:4px; font-size:0.75rem; font-weight:700; white-space:nowrap;">Size: ${escapeHtml(item.size)}</span>` : '';

      return `
        <div class="glass-card stock-card" onclick="openCatalogueDetail('${escapeHtml(item.barcode)}')">
          <div class="stock-thumb-wrapper" style="position:relative; width:100%; aspect-ratio:1; border-radius:10px; overflow:hidden; background:rgba(255,255,255,0.03); display:flex; align-items:center; justify-content:center;">
            <img class="stock-thumb" data-src="${imageUrl}" alt="${escapeHtml(item.itemName)}"
                 style="width:100%; height:100%; max-height:none; margin-bottom:0; border:none; object-fit:cover; display:none;" />
            <div class="stock-thumb-placeholder" style="display:flex; flex-direction:column; align-items:center; justify-content:center; width:100%; height:100%; max-height:none; margin-bottom:0; border:none;">
              <span style="font-size:2.5rem;">💎</span>
              <span style="font-size:0.75rem; margin-top:4px; color:var(--text-muted);">${escapeHtml(item.purity || '')}</span>
            </div>
          </div>
          <div style="display:flex; justify-content:space-between; align-items:flex-start; gap:0.5rem; margin-top:10px;">
            <div class="stock-card-name" style="font-weight:700; font-size:0.95rem;">${escapeHtml(item.itemName)}</div>
            ${sizeBadge}
          </div>
          <div class="stock-card-meta">#${escapeHtml(item.barcode)} • ${escapeHtml(item.purity)}</div>
          ${item.description ? `<p style="font-size:0.78rem; color:var(--text-muted); margin-bottom:8px; overflow:hidden; text-overflow:ellipsis; white-space:nowrap;">${escapeHtml(item.description)}</p>` : ''}
          <div class="stock-card-wt-row">
            <span>Gross: <strong>${item.grossWt}g</strong></span>
            <span style="color:var(--accent-gold);">Net: <strong>${item.netWt}g</strong></span>
          </div>
        </div>
      `;
    }).join('');

    if (catalogueImageObserver) {
      catalogueImageObserver.disconnect();
      catalogueImageObserver = null;
    }

    const wrappers = grid.querySelectorAll('.stock-thumb-wrapper');
    if ('IntersectionObserver' in window) {
      catalogueImageObserver = new IntersectionObserver((entries, observer) => {
        entries.forEach(entry => {
          if (entry.isIntersecting) {
            loadCatalogueThumb(entry.target);
            observer.unobserve(entry.target);
          }
        });
      }, { rootMargin: '400px 0px' });

      wrappers.forEach(w => catalogueImageObserver.observe(w));
    } else {
      wrappers.forEach(w => loadCatalogueThumb(w));
    }
  }
}

function filterCategory(cat) {
  currentCategory = cat;
  document.querySelectorAll('.cat-pill').forEach(pill => {
    if (pill.dataset.category === cat) pill.classList.add('active');
    else pill.classList.remove('active');
  });

  // Sync Item Name dropdown
  const select = document.getElementById('cat-itemname-select');
  if (select) {
    if (cat === 'All') {
      select.value = 'All';
    } else {
      // Find matching option in select
      let found = false;
      for (let i = 0; i < select.options.length; i++) {
        if (select.options[i].value.toLowerCase() === cat.toLowerCase() ||
            select.options[i].value.toLowerCase().includes(cat.toLowerCase())) {
          select.selectedIndex = i;
          found = true;
          break;
        }
      }
      if (!found) {
        select.value = 'All';
      }
    }
  }

  fetchCatalogue();
}

function openCatalogueDetail(barcode) {
  const item = State.catalogue.find(x => x.barcode === barcode);
  if (!item) return;

  State.selectedCatalogueItem = item;
  document.getElementById('modal-cat-title').innerText = item.itemName;
  document.getElementById('modal-cat-meta').innerText = `TAG #${item.barcode} • ${item.purity}`;
  document.getElementById('modal-cat-gross').innerText = `${item.grossWt}g`;
  document.getElementById('modal-cat-stone').innerText = `${item.stoneWt || 0}g`;
  document.getElementById('modal-cat-net').innerText = `${item.netWt}g`;
  document.getElementById('modal-cat-size').innerText = item.size || '-';
  document.getElementById('modal-cat-desc').innerText = item.description || 'Showroom master collection item.';

  const img = document.getElementById('modal-cat-img');
  if (img) {
    img.style.display = 'none';
    img.onload = function() { this.style.display = 'inline-block'; };
    img.onerror = function() { this.style.display = 'none'; };
    const serverBase = (State.serverUrl || window.location.origin).replace(/\/+$/, '');
    img.src = `${serverBase}/api/Barcode/image/${encodeURIComponent(item.barcode)}?t=${Date.now()}`;
  }

  document.getElementById('cat-detail-modal').classList.add('active');
}

function closeCatalogueDetail() {
  document.getElementById('cat-detail-modal').classList.remove('active');
}


// ── Module 3: Image Update (Capture Only) ──────────────────────────────────
let selectedUploadFile = null;

function handleImageSelect(e) {
  const file = e.target.files?.[0];
  if (!file) return;

  selectedUploadFile = file;
  const reader = new FileReader();
  reader.onload = (event) => {
    const preview = document.getElementById('img-upload-preview');
    if (preview) preview.src = event.target.result;
    const box = document.getElementById('img-preview-box');
    if (box) box.style.display = 'block';
  };
  reader.readAsDataURL(file);
}

async function uploadBarcodeImage() {
  const barcode = document.getElementById('img-barcode-input')?.value.trim();
  if (!barcode) {
    showToast('Please specify barcode number.', 'error');
    return;
  }

  if (!selectedUploadFile) {
    showToast('Please select or capture an image to upload.', 'error');
    return;
  }

  const btn = document.getElementById('img-upload-submit-btn');
  btn.disabled = true;
  btn.innerText = 'Uploading photo...';

  const formData = new FormData();
  formData.append('barcodeNo', barcode);
  formData.append('image', selectedUploadFile);

  try {
    const res = await apiRequest('/api/Barcode/upload', {
      method: 'POST',
      body: formData
    });

    showToast('Barcode photo uploaded and saved successfully! ✨', 'success');
    selectedUploadFile = null;
    const box = document.getElementById('img-preview-box');
    if (box) box.style.display = 'none';
    const fileInput = document.getElementById('img-file-input');
    if (fileInput) fileInput.value = '';
    const camInput = document.getElementById('img-camera-input');
    if (camInput) camInput.value = '';
    await lookupBarcodeForImage();
  } catch (err) {
    showToast(err.message || 'Failed to upload photo.', 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = '📸 Upload Photo to Server';
  }
}

async function lookupBarcodeForImage() {
  const barcode = document.getElementById('img-barcode-input')?.value.trim();
  if (!barcode) {
    showToast('Please enter or scan a barcode.', 'error');
    return;
  }

  try {
    const data = await apiRequest(`/api/Barcode/${encodeURIComponent(barcode)}`);
    const card = document.getElementById('img-product-info-card');
    if (card) card.style.display = 'block';

    const nameEl = document.getElementById('img-product-name');
    if (nameEl) nameEl.innerText = data.itemName || 'Jewellery Article';

    const purityEl = document.getElementById('img-product-purity');
    if (purityEl) purityEl.innerText = `${data.purity || '—'} • Net: ${data.netWt || 0}g`;

    const specsEl = document.getElementById('img-product-specs');
    if (specsEl) specsEl.innerText = `Gross: ${data.grossWt || 0}g • Size: ${data.size || '-'}`;

    const photoBadge = document.getElementById('img-photo-status');
    const photoContainer = document.getElementById('img-current-photo-container');
    const photoImg = document.getElementById('img-current-server-preview');

    if (data.imageUrl) {
      if (photoBadge) {
        photoBadge.innerText = 'Has Photo ✅';
        photoBadge.style.color = 'var(--status-success)';
      }
      if (photoContainer && photoImg) {
        photoImg.src = `${data.imageUrl}?t=${Date.now()}`;
        photoContainer.style.display = 'block';
      }
    } else {
      if (photoBadge) {
        photoBadge.innerText = 'No Photo Yet 📷';
        photoBadge.style.color = 'var(--accent-gold)';
      }
      if (photoContainer) photoContainer.style.display = 'none';
    }
  } catch (err) {
    showToast(err.message || 'Barcode not found.', 'error');
    const card = document.getElementById('img-product-info-card');
    if (card) card.style.display = 'none';
  }
}

// ── Module: Sticker Reprint ────────────────────────────────────────────────
let currentStickerItem = null;
let currentStickerType = 'WT'; // 'PC' or 'WT'

function setStickerType(type) {
  currentStickerType = type === 'PC' ? 'PC' : 'WT';

  const btnPc = document.getElementById('stk-btn-pc');
  const btnWt = document.getElementById('stk-btn-wt');
  const mrpGroup = document.getElementById('stk-mrp-group');
  const mcGroup = document.getElementById('stk-mc-group');
  const formatSelect = document.getElementById('stk-format-select');

  if (currentStickerType === 'PC') {
    if (btnPc) btnPc.classList.add('active');
    if (btnWt) btnWt.classList.remove('active');
    if (mrpGroup) mrpGroup.style.display = 'block';
    if (mcGroup) mcGroup.style.display = 'none';

    // Default respective barcode design for PC
    if (formatSelect) {
      formatSelect.value = 'StickerPC';
    }
  } else {
    if (btnWt) btnWt.classList.add('active');
    if (btnPc) btnPc.classList.remove('active');
    if (mrpGroup) mrpGroup.style.display = 'none';
    if (mcGroup) mcGroup.style.display = 'block';

    // Default respective barcode design for WT
    if (formatSelect) {
      formatSelect.value = 'StickerWt';
    }
  }
}

async function lookupBarcodeForSticker() {
  const input = document.getElementById('stk-barcode-input');
  const barcode = input?.value.trim();
  if (!barcode) {
    showToast('Please enter or scan a barcode.', 'error');
    return;
  }

  try {
    const data = await apiRequest(`/api/Barcode/${encodeURIComponent(barcode)}`);
    currentStickerItem = data;

    // Show details card and edit section
    const infoCard = document.getElementById('stk-product-info-card');
    const editSec = document.getElementById('stk-edit-section');
    if (infoCard) infoCard.style.display = 'block';
    if (editSec) editSec.style.display = 'block';

    // Populate info
    const nameEl = document.getElementById('stk-item-name');
    if (nameEl) nameEl.innerText = data.itemName || 'Jewellery Article';

    const metaEl = document.getElementById('stk-item-meta');
    if (metaEl) metaEl.innerText = `TAG #${data.barcode} • ${data.purity || '—'}`;

    const weightsEl = document.getElementById('stk-item-weights');
    if (weightsEl) weightsEl.innerText = `Gross: ${data.grossWt || 0}g • Net: ${data.netWt || 0}g • Stone: ${data.stoneWt || 0}g`;

    const descEl = document.getElementById('stk-item-desc');
    if (descEl) descEl.innerText = data.description || 'Showroom Stock';

    const dbBadge = document.getElementById('stk-db-type-badge');
    const currentDbBType = (data.bType || 'WT').toUpperCase();
    if (dbBadge) dbBadge.innerText = currentDbBType;

    const thumb = document.getElementById('stk-item-thumb');
    const thumbBox = document.getElementById('stk-item-img-box');
    if (data.imageUrl && thumb && thumbBox) {
      thumb.src = `${data.imageUrl}?t=${Date.now()}`;
      thumbBox.style.display = 'block';
    } else if (thumbBox) {
      thumbBox.style.display = 'none';
    }

    // Populate form fields
    const sizeInput = document.getElementById('stk-size-input');
    if (sizeInput) {
      sizeInput.value = (data.size && data.size !== '-') ? data.size : '';
    }

    const mrpInput = document.getElementById('stk-mrp-input');
    if (mrpInput) {
      mrpInput.value = data.mrp || '';
    }

    const mcInput = document.getElementById('stk-mc-input');
    if (mcInput) {
      mcInput.value = data.making || '';
    }

    // Set PC / WT mode based on item's type
    const initialType = currentDbBType === 'PC' ? 'PC' : 'WT';
    setStickerType(initialType);

    // If item has a specific saved sticker format, select it
    const formatSelect = document.getElementById('stk-format-select');
    if (formatSelect && data.stickerFormat) {
      const match = Array.from(formatSelect.options).find(o => o.value.toLowerCase() === data.stickerFormat.toLowerCase());
      if (match) {
        formatSelect.value = match.value;
      }
    }

    // Load printer list to ensure dropdown is fresh
    await loadPrintersList();
  } catch (err) {
    currentStickerItem = null;
    showToast(err.message || 'Barcode not found.', 'error');
    const infoCard = document.getElementById('stk-product-info-card');
    const editSec = document.getElementById('stk-edit-section');
    if (infoCard) infoCard.style.display = 'none';
    if (editSec) editSec.style.display = 'none';
  }
}

async function saveAndPrintSticker(printNow = true) {
  const barcode = document.getElementById('stk-barcode-input')?.value.trim();
  if (!barcode) {
    showToast('Please enter or scan barcode number.', 'error');
    return;
  }

  const bType = currentStickerType; // 'PC' or 'WT'
  const size = document.getElementById('stk-size-input')?.value.trim() || undefined;
  const stickerFormat = document.getElementById('stk-format-select')?.value || (bType === 'PC' ? 'StickerPC' : 'StickerWt');
  const printEngine = document.getElementById('stk-engine-select')?.value || 'PRN';
  const printerName = document.getElementById('stk-printer-select')?.value || undefined;
  const reprintSticker = document.getElementById('stk-reprint-queue-toggle')?.checked ?? true;

  let mrp = undefined;
  let making = undefined;

  if (bType === 'PC') {
    const mrpVal = document.getElementById('stk-mrp-input')?.value;
    mrp = (mrpVal !== '' && !isNaN(mrpVal)) ? parseFloat(mrpVal) : undefined;
  } else {
    const mcVal = document.getElementById('stk-mc-input')?.value;
    making = (mcVal !== '' && !isNaN(mcVal)) ? parseFloat(mcVal) : undefined;
  }

  const btn = document.getElementById(printNow ? 'stk-save-print-btn' : 'stk-save-only-btn');
  const originalBtnText = btn ? btn.innerText : '';
  if (btn) {
    btn.disabled = true;
    btn.innerText = printNow ? '⏳ Saving & Printing...' : '⏳ Saving...';
  }

  const payload = {
    barcode,
    bType,
    size,
    mrp,
    making,
    stickerFormat,
    printEngine,
    printerName,
    reprintSticker,
    printNow
  };

  try {
    const res = await apiRequest('/api/Barcode/update-advanced', {
      method: 'POST',
      body: JSON.stringify(payload)
    });

    if (res) {
      if (res.printStatus === 'Printed') {
        showToast(res.message || `Saved & printed using ${stickerFormat}.prn! 🏷️🖨️`, 'success');
      } else if (res.printStatus && res.printStatus.startsWith('Print failed')) {
        showToast(`Saved in database, but printing failed: ${res.printStatus}`, 'warning');
      } else {
        showToast(res.message || 'Barcode specifications updated successfully in database! 💾', 'success');
      }
    }

    await lookupBarcodeForSticker();
  } catch (err) {
    showToast(err.message || 'Failed to update barcode specifications.', 'error');
  } finally {
    if (btn) {
      btn.disabled = false;
      btn.innerText = originalBtnText;
    }
  }
}

function onStkPrinterChanged() {
  const label = document.getElementById('stk-printer-source-label');
  if (label) label.innerText = 'Selected';
}

// ── Module 4: Customer Requirement ──────────────────────────────────────────
let selectedReqImageFile = null;

function handleReqImage(e) {
  const file = e.target.files?.[0];
  if (!file) return;

  selectedReqImageFile = file;

  const previewContainer = document.getElementById('req-image-preview-container');
  const previewImg = document.getElementById('req-image-preview');
  const nameEl = document.getElementById('req-image-name');
  const sizeEl = document.getElementById('req-image-size');

  if (previewImg) {
    const reader = new FileReader();
    reader.onload = (ev) => {
      if (previewImg) previewImg.src = ev.target.result;
    };
    reader.readAsDataURL(file);
  }

  if (nameEl) nameEl.innerText = file.name;
  if (sizeEl) sizeEl.innerText = `${(file.size / 1024).toFixed(1)} KB`;
  if (previewContainer) previewContainer.style.display = 'flex';
}

function clearReqImage() {
  selectedReqImageFile = null;
  const cameraInput = document.getElementById('req-camera-input');
  const fileInput = document.getElementById('req-file-input');
  const previewContainer = document.getElementById('req-image-preview-container');
  const previewImg = document.getElementById('req-image-preview');

  if (cameraInput) cameraInput.value = '';
  if (fileInput) fileInput.value = '';
  if (previewImg) previewImg.src = '';
  if (previewContainer) previewContainer.style.display = 'none';
}

let phoneLookupTimer = null;
let lastLookedUpPhone = null;

function onReqPhoneInput(e) {
  if (phoneLookupTimer) clearTimeout(phoneLookupTimer);
  const val = e?.target?.value?.trim() || '';
  const digits = val.replace(/\D/g, '');
  const statusEl = document.getElementById('req-phone-status');

  if (digits.length < 5) {
    if (statusEl) statusEl.style.display = 'none';
    lastLookedUpPhone = null;
    return;
  }

  // Auto lookup when 10 digits entered
  if (digits.length >= 10) {
    phoneLookupTimer = setTimeout(() => {
      lookupCustomerByPhone();
    }, 280);
  }
}

async function lookupCustomerByPhone() {
  const phoneInput = document.getElementById('req-phone');
  const nameInput = document.getElementById('req-name');
  const statusEl = document.getElementById('req-phone-status');
  const phone = phoneInput?.value?.trim() || '';
  const digits = phone.replace(/\D/g, '');

  if (digits.length < 5) {
    if (statusEl) statusEl.style.display = 'none';
    return;
  }

  if (phone === lastLookedUpPhone) return;
  lastLookedUpPhone = phone;

  if (statusEl) {
    statusEl.innerHTML = '⌛';
    statusEl.style.display = 'inline';
    statusEl.style.color = 'var(--text-muted)';
  }

  try {
    const res = await apiRequest(`/api/CustomerRequirement/lookup-customer?phone=${encodeURIComponent(phone)}`);
    if (res && res.found && res.customerName) {
      if (nameInput) {
        nameInput.value = res.customerName;
      }
      if (statusEl) {
        statusEl.innerHTML = '✅ Found in DB';
        statusEl.style.display = 'inline';
        statusEl.style.color = 'var(--status-success)';
      }
      showToast(`Customer found: ${res.customerName} 👤`, 'info');
    } else {
      if (statusEl) {
        statusEl.innerHTML = '🆕 New';
        statusEl.style.display = 'inline';
        statusEl.style.color = 'var(--accent-gold)';
      }
    }
  } catch (err) {
    if (statusEl) statusEl.style.display = 'none';
  }
}

async function submitCustomerRequirement(e) {
  if (e) e.preventDefault();

  const orderCategory = document.getElementById('req-order-category')?.value || 'Requirement';
  const customerMobile = document.getElementById('req-phone')?.value.trim();
  const customerName = document.getElementById('req-name')?.value.trim();
  const itemName = document.getElementById('req-item')?.value.trim();
  const purity = document.getElementById('req-purity')?.value.trim();
  const weightRange = document.getElementById('req-weight')?.value.trim();
  const description = document.getElementById('req-desc')?.value.trim();
  const remarks = document.getElementById('req-remarks')?.value.trim();

  if (!customerMobile || !customerName || !itemName) {
    showToast('Please enter Mobile Number, Customer Name, and Item Name.', 'error');
    return;
  }

  const btn = document.getElementById('req-submit-btn');
  btn.disabled = true;
  btn.innerText = selectedReqImageFile ? 'Uploading photo...' : `Saving ${orderCategory}...`;

  let uploadedImagePath = null;
  if (selectedReqImageFile) {
    try {
      const formData = new FormData();
      formData.append('image', selectedReqImageFile);
      const uploadRes = await apiRequest('/api/CustomerRequirement/upload-image', {
        method: 'POST',
        body: formData
      });
      if (uploadRes && uploadRes.fileName) {
        uploadedImagePath = uploadRes.fileName;
      }
    } catch (uploadErr) {
      console.warn('Image upload error:', uploadErr);
      showToast('Image upload failed: ' + (uploadErr.message || 'Error saving photo'), 'error');
      btn.disabled = false;
      btn.innerText = 'Log Customer Requirement';
      return;
    }
  }

  btn.innerText = `Saving ${orderCategory} to ERP...`;

  const payload = {
    orderCategory,
    customerName,
    customerMobile,
    itemName,
    purity,
    weightRange,
    description,
    remarks,
    imagePath: uploadedImagePath
  };

  try {
    const res = await apiRequest('/api/CustomerRequirement', {
      method: 'POST',
      body: JSON.stringify(payload)
    });

    const successMsg = res?.message || `${orderCategory} logged successfully into ERP database! 📝`;
    showToast(successMsg, 'success');
    document.getElementById('customer-req-form').reset();
    clearReqImage();
    lastLookedUpPhone = null;
    const statusEl = document.getElementById('req-phone-status');
    if (statusEl) statusEl.style.display = 'none';
  } catch (err) {
    showToast(err.message || `Failed to log ${orderCategory.toLowerCase()}.`, 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = 'Log Customer Requirement';
  }
}

// ── Module 5: AI Biller ─────────────────────────────────────────────────────
let selectedAiBillFile = null;

function handleAiBillFile(e) {
  const file = e.target.files[0];
  if (!file) return;

  selectedAiBillFile = file;
  document.getElementById('ai-bill-filename').innerText = `Selected: ${file.name} (${(file.size / 1024).toFixed(1)} KB)`;
  document.getElementById('ai-bill-submit-btn').disabled = false;
}

async function parseAiBill() {
  if (!selectedAiBillFile) {
    showToast('Please select an invoice image or PDF first.', 'error');
    return;
  }

  const btn = document.getElementById('ai-bill-submit-btn');
  btn.disabled = true;
  btn.innerText = 'AI Parsing Invoice in Progress... ⚡';

  const formData = new FormData();
  formData.append('image', selectedAiBillFile);

  try {
    const data = await apiRequest('/api/Invoice/parse', {
      method: 'POST',
      body: formData
    });

    renderAiBillResult(data);
    showToast('Invoice parsed successfully! 🧾', 'success');
  } catch (err) {
    showToast(err.message || 'Failed to parse invoice with AI service.', 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = 'Scan & Extract with AI';
  }
}

function renderAiBillResult(res) {
  const card = document.getElementById('ai-bill-result-card');
  card.style.display = 'block';

  document.getElementById('ai-vendor-name').innerText = res.vendorName || 'Extracted Vendor';
  document.getElementById('ai-invoice-no').innerText = res.invoiceId || res.invoiceNumber || 'N/A';
  document.getElementById('ai-invoice-date').innerText = res.invoiceDate || new Date().toLocaleDateString();
  document.getElementById('ai-invoice-total').innerText = `₹${(res.invoiceTotal || 0).toLocaleString()}`;

  const tableBody = document.getElementById('ai-bill-items-tbody');
  if (tableBody) {
    const items = res.items || [];
    if (items.length > 0) {
      tableBody.innerHTML = items.map((it, idx) => `
        <tr style="border-bottom: 1px solid var(--border-subtle); padding: 8px 0;">
          <td style="padding: 8px 4px;">${idx + 1}</td>
          <td style="padding: 8px 4px;">${it.description || it.itemName || 'Article'}</td>
          <td style="padding: 8px 4px; text-align:center;">${it.quantity || 1}</td>
          <td style="padding: 8px 4px; text-align:right;">₹${(it.unitPrice || it.amount || 0).toLocaleString()}</td>
          <td style="padding: 8px 4px; text-align:right; font-weight:800; color:var(--accent-gold);">₹${(it.amount || it.totalPrice || 0).toLocaleString()}</td>
        </tr>
      `).join('');
    } else {
      tableBody.innerHTML = `<tr><td colspan="5" style="text-align:center; padding:12px; color:var(--text-muted);">No line items extracted</td></tr>`;
    }
  }
}

// ── Module 6: Generate Invoice / POS Billing ────────────────────────────────
let cachedVoucherTypes = [];

async function loadVoucherTypes() {
  const select = document.getElementById('inv-vou-type-select');
  if (!select) return;

  try {
    const types = await apiRequest('/api/Bill/voucher-types');
    if (Array.isArray(types) && types.length > 0) {
      cachedVoucherTypes = types;
      select.innerHTML = types.map(t => `
        <option value="${t.vouType}" data-short="${t.shortName}" data-vat="${t.vatPercent}">
          🧾 ${t.vouType} (${t.shortName}) ${t.vatPercent > 0 ? `[+${t.vatPercent}% GST]` : '[0% GST]'}
        </option>
      `).join('');
    }
  } catch (err) {
    console.warn('Could not load dynamic voucher types, keeping defaults:', err);
  }

  await onVouTypeChanged();
}

async function onVouTypeChanged() {
  const select = document.getElementById('inv-vou-type-select');
  const badge = document.getElementById('inv-next-vouno-badge');
  const vouType = select?.value;

  if (!vouType) return;

  if (badge) {
    badge.innerText = 'Next: Fetching...';
  }

  try {
    const res = await apiRequest(`/api/Bill/next-vouno?vouType=${encodeURIComponent(vouType)}`);
    if (res && res.vouNo) {
      State.nextVouNo = res.vouNo;
      if (badge) {
        badge.innerText = `Next: #${res.vouNo}`;
      }
    }
  } catch (err) {
    console.warn('Failed to fetch next VouNo:', err);
    if (badge) {
      badge.innerText = 'Next: (Auto on save)';
    }
  }
}

async function addInvoiceItemByBarcode() {
  const input = document.getElementById('inv-barcode-input');
  const code = input?.value.trim();
  if (!code) {
    showToast('Please enter a barcode.', 'error');
    return;
  }

  // Check if item already exists in cart
  if (State.invoiceItems.some(i => i.barcode?.toLowerCase() === code.toLowerCase())) {
    showToast(`Barcode ${code} is already in the invoice cart.`, 'error');
    return;
  }

  const select = document.getElementById('inv-vou-type-select');
  const selectedOpt = select?.selectedOptions[0];
  const vatRate = selectedOpt ? parseFloat(selectedOpt.dataset.vat) : 3;
  const gstMode = vatRate === 0 ? 'InclusiveGST' : 'ExclusiveGST';

  try {
    const data = await apiRequest('/api/Valuation', {
      method: 'POST',
      body: JSON.stringify({ barcode: code, gstMode })
    });

    State.invoiceItems.push({
      barcode: data.barcode,
      itemName: data.itemName,
      purity: data.purity,
      netWt: data.netWt,
      grossWt: data.grossWt || data.netWt,
      ratePerGram: data.ratePerGram,
      metalValue: data.metalValue || (data.netWt * data.ratePerGram),
      stoneValue: data.stoneValue || 0,
      making: data.makingAmount || 0,
      subTotal: data.subTotal || ((data.metalValue || (data.netWt * data.ratePerGram)) + (data.stoneValue || 0) + (data.makingAmount || 0)),
      gstAmount: vatRate === 0 ? 0 : (data.gstAmount || 0),
      finalPrice: vatRate === 0 ? data.subTotal : data.finalPrice
    });

    input.value = '';
    renderInvoiceItems();
    showToast(`Added ${data.itemName} (#${data.barcode})`, 'success');
  } catch (err) {
    showToast(err.message || 'Failed to fetch article for invoice.', 'error');
  }
}

function removeInvoiceItem(idx) {
  State.invoiceItems.splice(idx, 1);
  renderInvoiceItems();
}

function renderInvoiceItems() {
  const container = document.getElementById('inv-items-container');
  const list = State.invoiceItems;

  const totalNetWt = list.reduce((sum, i) => sum + (i.netWt || 0), 0);
  const totalTaxable = list.reduce((sum, i) => sum + (i.subTotal || 0), 0);
  const totalGst = list.reduce((sum, i) => sum + (i.gstAmount || 0), 0);
  const discount = Math.max(0, parseFloat(document.getElementById('inv-discount-input')?.value) || 0);

  const grandTotal = Math.max(0, Math.round(totalTaxable + totalGst - discount));

  const totalWtEl = document.getElementById('inv-total-wt');
  if (totalWtEl) totalWtEl.innerText = `${totalNetWt.toFixed(3)}g`;

  const taxableEl = document.getElementById('inv-taxable-amt');
  if (taxableEl) taxableEl.innerText = `₹${Math.round(totalTaxable).toLocaleString()}`;

  const gstEl = document.getElementById('inv-gst-amt');
  if (gstEl) gstEl.innerText = `₹${Math.round(totalGst).toLocaleString()}`;

  const dispDiscEl = document.getElementById('inv-disp-discount');
  if (dispDiscEl) dispDiscEl.innerText = discount > 0 ? `-₹${discount.toLocaleString()}` : '-₹0';

  const grandTotalEl = document.getElementById('inv-grand-total');
  if (grandTotalEl) grandTotalEl.innerText = `₹${grandTotal.toLocaleString()}`;

  if (list.length === 0) {
    container.innerHTML = `
      <div style="text-align:center; padding: 2rem; color:var(--text-muted); font-size:0.85rem;">
        No articles added to invoice yet. Enter barcode above.
      </div>
    `;
    return;
  }

  container.innerHTML = list.map((item, idx) => `
    <div style="display:flex; justify-content:space-between; align-items:center; padding: 0.85rem 0; border-bottom: 1px solid var(--border-subtle);">
      <div>
        <div style="font-weight:800; font-size:0.95rem;">${item.itemName}</div>
        <div style="font-size:0.78rem; color:var(--text-muted); margin-top:2px;">#${item.barcode} • ${item.purity} • ${item.netWt}g @ ₹${item.ratePerGram}/g</div>
      </div>
      <div style="text-align:right;">
        <div style="font-weight:900; color:var(--accent-gold); font-size:1.05rem;">₹${(item.finalPrice || 0).toLocaleString()}</div>
        <button onclick="removeInvoiceItem(${idx})" style="background:none; border:none; color:var(--status-error); font-size:0.75rem; font-weight:700; cursor:pointer; margin-top:4px;">Remove ✕</button>
      </div>
    </div>
  `).join('');
}

async function saveAndPrintInvoice() {
  if (State.invoiceItems.length === 0) {
    showToast('Add at least one article before saving.', 'error');
    return;
  }

  const btn = document.getElementById('inv-save-btn');
  const vouType = document.getElementById('inv-vou-type-select')?.value || 'GST RETAIL SALE';
  const customerName = document.getElementById('inv-cust-name')?.value.trim() || 'Walk-in Customer';
  const customerPhone = document.getElementById('inv-cust-phone')?.value.trim() || '';
  const paymentMode = document.getElementById('inv-payment-mode')?.value || 'Cash';
  const discount = Math.max(0, parseFloat(document.getElementById('inv-discount-input')?.value) || 0);

  btn.disabled = true;
  btn.innerText = '💾 Saving Bill to ERP...';

  const payload = {
    vouType: vouType,
    customerName: customerName,
    phone: customerPhone,
    paymentMode: paymentMode,
    discountAllowed: discount,
    items: State.invoiceItems.map(item => ({
      barcode: item.barcode,
      itemName: item.itemName,
      purity: item.purity,
      qty: 1,
      ntWt: item.netWt,
      grWt: item.grossWt || item.netWt,
      rate: item.ratePerGram,
      amount: item.subTotal,
      grossAmt: item.subTotal,
      making: item.making,
      mperGr: 0,
      metalValue: item.metalValue,
      stoneValue: item.stoneValue,
      vatAmt: item.gstAmount,
      disAmt: 0,
      disPer: 0
    }))
  };

  try {
    const res = await apiRequest('/api/Bill/save', {
      method: 'POST',
      body: JSON.stringify(payload)
    });

    showToast(`🎉 Bill #${res.vouNo} saved to ERP database!`, 'success');

    // Populate printable invoice modal with confirmed ERP voucher details
    const totalNetWt = State.invoiceItems.reduce((sum, i) => sum + (i.netWt || 0), 0);
    const printTitle = document.getElementById('print-inv-title');
    if (printTitle) printTitle.innerText = res.vouType.toUpperCase();

    const printCust = document.getElementById('print-cust-name');
    if (printCust) printCust.innerText = res.customerName || customerName;

    const printPhone = document.getElementById('print-cust-phone');
    if (printPhone) printPhone.innerText = customerPhone || '-';

    const printDate = document.getElementById('print-inv-date');
    if (printDate) printDate.innerText = new Date().toLocaleDateString('en-IN', { day: '2-digit', month: 'short', year: 'numeric' });

    const printNo = document.getElementById('print-inv-no');
    if (printNo) printNo.innerText = res.vouNo;

    const tbody = document.getElementById('print-inv-tbody');
    if (tbody) {
      tbody.innerHTML = State.invoiceItems.map((item, idx) => `
        <tr style="border-bottom: 1px solid #E2E8F0;">
          <td style="padding:8px 4px;">${idx + 1}</td>
          <td style="padding:8px 4px;">${item.itemName} (#${item.barcode})</td>
          <td style="padding:8px 4px; text-align:center;">${item.purity}</td>
          <td style="padding:8px 4px; text-align:right;">${item.netWt}g</td>
          <td style="padding:8px 4px; text-align:right;">₹${item.ratePerGram}/g</td>
          <td style="padding:8px 4px; text-align:right; font-weight:800;">₹${(item.finalPrice || 0).toLocaleString()}</td>
        </tr>
      `).join('');
    }

    const printWt = document.getElementById('print-total-wt');
    if (printWt) printWt.innerText = `${totalNetWt.toFixed(3)}g`;

    const printTot = document.getElementById('print-grand-total');
    if (printTot) printTot.innerText = `₹${(res.netAmt || 0).toLocaleString()}`;

    // Show print modal
    document.getElementById('print-invoice-modal').classList.add('active');

    // Reset cart and form
    State.invoiceItems = [];
    const bcInp = document.getElementById('inv-barcode-input');
    if (bcInp) bcInp.value = '';
    const custNameInp = document.getElementById('inv-cust-name');
    if (custNameInp) custNameInp.value = '';
    const custPhoneInp = document.getElementById('inv-cust-phone');
    if (custPhoneInp) custPhoneInp.value = '';
    const discInp = document.getElementById('inv-discount-input');
    if (discInp) discInp.value = '0';
    renderInvoiceItems();

    // Refresh next VouNo for the next bill
    await onVouTypeChanged();
  } catch (err) {
    showToast(err.message || 'Failed to save bill to ERP database.', 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = '💾 Save Bill to ERP & Print 🖨️';
  }
}

function openPrintInvoiceModal() {
  saveAndPrintInvoice();
}

function closePrintInvoiceModal() {
  document.getElementById('print-invoice-modal').classList.remove('active');
}

// ── Module 7: Documents ─────────────────────────────────────────────────────
let selectedDocFile = null;

function handleDocFile(e) {
  const file = e.target.files[0];
  if (!file) return;

  selectedDocFile = file;
  document.getElementById('doc-file-label').innerText = `File: ${file.name} (${(file.size / 1024).toFixed(1)} KB)`;
}

async function uploadDocument() {
  if (!selectedDocFile) {
    showToast('Please select a file to upload.', 'error');
    return;
  }

  const category = document.getElementById('doc-category-select')?.value || 'KYC / Aadhaar / PAN';
  const notes = document.getElementById('doc-notes-input')?.value.trim() || '';

  const btn = document.getElementById('doc-upload-btn');
  btn.disabled = true;
  btn.innerText = 'Uploading Document...';

  const formData = new FormData();
  formData.append('document', selectedDocFile);
  formData.append('category', category);
  formData.append('notes', notes);

  try {
    await apiRequest('/api/UploadDocs', {
      method: 'POST',
      body: formData
    });

    showToast('Document uploaded successfully to server Docs folder! 📁', 'success');
    selectedDocFile = null;
    document.getElementById('doc-file-input').value = '';
    document.getElementById('doc-notes-input').value = '';
    document.getElementById('doc-file-label').innerText = 'Select or drop document';
  } catch (err) {
    showToast(err.message || 'Failed to upload document.', 'error');
  } finally {
    btn.disabled = false;
    btn.innerText = 'Upload Document to ERP';
  }
}

// ── Module 8: Stock Summary ─────────────────────────────────────────────────
async function fetchStockSummary() {
  try {
    const data = await apiRequest('/api/Catalogue');
    const items = Array.isArray(data) ? data : [];

    const totalPieces = items.length;
    const totalGross = items.reduce((s, i) => s + (i.grossWt || 0), 0);
    const totalNet = items.reduce((s, i) => s + (i.netWt || 0), 0);

    document.getElementById('stock-total-pieces').innerText = totalPieces;
    document.getElementById('stock-total-gross').innerText = `${totalGross.toFixed(2)} g`;
    document.getElementById('stock-total-net').innerText = `${totalNet.toFixed(2)} g`;

    // Purity breakdown
    const p22k = items.filter(x => (x.purity || '').includes('916') || (x.purity || '').includes('22')).length;
    const p18k = items.filter(x => (x.purity || '').includes('750') || (x.purity || '').includes('18')).length;
    const pSilver = items.filter(x => (x.purity || '').toLowerCase().includes('silver')).length;

    document.getElementById('stock-purity-22k').innerText = `${p22k} items`;
    document.getElementById('stock-purity-18k').innerText = `${p18k} items`;
    document.getElementById('stock-purity-silver').innerText = `${pSilver} items`;
  } catch (err) {
    console.warn('Stock summary fetch error', err);
  }
}

// ── Module 9: Server & Settings ─────────────────────────────────────────────
async function loadSettingsView() {
  const urlInput = document.getElementById('settings-server-url');
  if (urlInput) urlInput.value = State.serverUrl;
  testServerConnection();
  loadSoftwarePathSettings();
  loadPrintersList();
}

async function saveServerUrl() {
  const urlInput = document.getElementById('settings-server-url');
  const url = urlInput?.value.trim();
  if (!url) {
    showToast('Please enter a server URL.', 'error');
    return;
  }

  State.serverUrl = url;
  localStorage.setItem('cjp_server_url', url);
  showToast('Server URL saved! Testing connection...', 'success');
  testServerConnection();
}

async function testServerConnection() {
  const statusBox = document.getElementById('settings-conn-status');
  const statusText = document.getElementById('settings-conn-text');
  const dbPathText = document.getElementById('settings-db-path');

  if (statusText) statusText.innerText = 'Testing connection...';

  try {
    const res = await apiRequest('/api/Diagnostics/status');
    if (res && res.status === 'Online') {
      if (statusBox) {
        statusBox.style.borderColor = 'var(--status-success)';
        statusBox.style.background = 'var(--status-success-bg)';
      }
      if (statusText) {
        statusText.innerHTML = `✅ <strong>Server Online & Active</strong> • ${res.activeWorkingProvider || 'Connected'}`;
        statusText.style.color = 'var(--status-success)';
      }
      if (dbPathText) {
        dbPathText.innerText = `Database: ${res.activeResolvedDatabasePath || 'Connected'} (${res.userRecordsInDb || 0} users)`;
      }
    }
  } catch (err) {
    if (statusBox) {
      statusBox.style.borderColor = 'var(--status-error)';
      statusBox.style.background = 'var(--status-error-bg)';
    }
    if (statusText) {
      statusText.innerHTML = `❌ <strong>Connection Error:</strong> ${err.message}`;
      statusText.style.color = 'var(--status-error)';
    }
  }
}

// ── Barcode Printer Settings & Helpers ─────────────────────────────────────
let cachedPrintersData = null;

async function loadPrintersList(forceRefresh = false) {
  if (cachedPrintersData && !forceRefresh) {
    populatePrinterDropdowns(cachedPrintersData);
    return;
  }

  try {
    const data = await apiRequest('/api/Barcode/printers');
    if (data) {
      cachedPrintersData = data;
      populatePrinterDropdowns(data);
    }
  } catch (err) {
    console.warn('Could not load printers list', err);
  }
}

function populatePrinterDropdowns(data) {
  const selects = [
    document.getElementById('stk-printer-select'),
    document.getElementById('adv-barcode-printer-select'),
    document.getElementById('settings-barcode-printer-select')
  ];

  const preferredPrinter = data.configuredPrinter || data.autoDetectedPrinter || '';
  const printers = data.installedPrinters || [];

  selects.forEach(select => {
    if (!select) return;
    const currentVal = select.value;
    select.innerHTML = '';

    if (printers.length === 0) {
      select.innerHTML = '<option value="">(No installed printers found)</option>';
      return;
    }

    printers.forEach(printer => {
      const opt = document.createElement('option');
      opt.value = printer;
      opt.text = printer;
      if (printer === data.configuredPrinter) {
        opt.text += ' ★ (Saved in Settings)';
      } else if (!data.configuredPrinter && printer === data.autoDetectedPrinter) {
        opt.text += ' (Auto-detected)';
      }
      select.appendChild(opt);
    });

    if (currentVal && printers.includes(currentVal)) {
      select.value = currentVal;
    } else if (preferredPrinter && printers.includes(preferredPrinter)) {
      select.value = preferredPrinter;
    }
  });

  const sourceLabels = [
    document.getElementById('stk-printer-source-label'),
    document.getElementById('adv-printer-source-label')
  ];
  sourceLabels.forEach(lbl => {
    if (lbl) {
      lbl.innerText = data.configuredPrinter ? 'Settings' : (data.autoDetectedPrinter ? 'Auto' : 'Windows');
    }
  });

  const hintEl = document.getElementById('settings-printer-status-hint');
  if (hintEl) {
    const eng = data.configuredPrintEngine || 'PRN';
    if (data.configuredPrinter) {
      hintEl.innerHTML = `Active default printer: <strong style="color:var(--accent-gold);">${data.configuredPrinter}</strong> (${eng} mode).`;
    } else if (data.autoDetectedPrinter) {
      hintEl.innerHTML = `No printer configured in settings yet. Currently falling back to: <strong style="color:var(--accent-gold);">${data.autoDetectedPrinter}</strong> (${eng} mode).`;
    }
  }

  const engineSelects = [
    document.getElementById('stk-engine-select'),
    document.getElementById('settings-barcode-engine-select')
  ];
  const configuredEngine = (data.configuredPrintEngine || 'PRN').toUpperCase();
  engineSelects.forEach(sel => {
    if (sel) {
      sel.value = configuredEngine === 'FRX' ? 'FRX' : 'PRN';
    }
  });
}

async function saveDefaultBarcodePrinter() {
  const select = document.getElementById('settings-barcode-printer-select');
  const engineSelect = document.getElementById('settings-barcode-engine-select');
  const printerName = select?.value?.trim();
  const printEngine = engineSelect?.value || 'PRN';
  if (!printerName) {
    showToast('Please select a printer to set as default.', 'error');
    return;
  }

  try {
    const res = await apiRequest('/api/Barcode/default-printer', {
      method: 'POST',
      body: JSON.stringify({ printerName, printEngine })
    });

    showToast(res?.message || `Barcode printer set to ${printerName}! 🏷️`, 'success');
    await loadPrintersList(true);
  } catch (err) {
    showToast(err.message || 'Failed to save printer settings.', 'error');
  }
}

async function loadSoftwarePathSettings() {
  const input = document.getElementById('settings-software-path-input');
  const statusBox = document.getElementById('settings-software-path-status');
  const statusText = document.getElementById('settings-software-path-status-text');
  const subinfoText = document.getElementById('settings-software-path-subinfo');

  if (!input) return;

  try {
    const data = await apiRequest('/api/Barcode/software-path');
    if (data) {
      if (data.softwarePath) {
        input.value = data.softwarePath;
      }
      if (statusBox && statusText) {
        if (data.exists) {
          statusBox.style.borderColor = 'var(--status-success)';
          statusBox.style.background = 'var(--status-success-bg)';
          statusText.innerHTML = `✅ <strong>Folder Verified on Server</strong>`;
          statusText.style.color = 'var(--status-success)';
          if (subinfoText) {
            subinfoText.innerHTML = data.prnExists
              ? `PRN Templates directory detected (${data.prnFilesCount || 0} PRN files found in <code>PrintTemplates\\BarcodePRN</code>)`
              : `Note: <code>PrintTemplates\\BarcodePRN</code> directory not found inside this folder.`;
          }
        } else {
          statusBox.style.borderColor = 'var(--accent-gold)';
          statusBox.style.background = 'var(--bg-input)';
          statusText.innerHTML = `⚠️ <strong>Path Configured:</strong> Directory not found on this local machine (will be used when deployed to production server).`;
          statusText.style.color = 'var(--accent-gold)';
          if (subinfoText) {
            subinfoText.innerText = `Target: ${data.softwarePath || 'Not set'}`;
          }
        }
      }
    }
  } catch (err) {
    if (statusText) {
      statusText.innerHTML = `Unable to fetch software path: ${err.message}`;
    }
  }
}

async function saveSoftwarePath() {
  const input = document.getElementById('settings-software-path-input');
  const path = input?.value?.trim();
  if (!path) {
    showToast('Please enter the ERP software executable folder path.', 'error');
    return;
  }

  try {
    const res = await apiRequest('/api/Barcode/software-path', {
      method: 'POST',
      body: JSON.stringify({ softwarePath: path })
    });

    showToast(res?.message || 'Software path saved successfully! 📁', 'success');
    await loadSoftwarePathSettings();
  } catch (err) {
    showToast(err.message || 'Failed to save software path.', 'error');
  }
}

function onAdvPrinterChanged() {
  const select = document.getElementById('adv-barcode-printer-select');
  const label = document.getElementById('adv-printer-source-label');
  if (label && select) {
    label.innerText = 'Selected';
  }
}

// ── Camera Barcode Scanner Controller ─────────────────────────────────────────
const ScannerState = {
  html5QrCode: null,
  targetInputId: null,
  onScanSuccessCallback: null,
  isOpen: false,
  isProcessingScan: false,
  currentCameraId: null,
  cameras: [],
  isTorchOn: false,
  hasTorch: false,
  isSwitching: false
};

// Play a crisp, pleasant audio beep on successful barcode scan using Web Audio API
let _lastScanSoundTime = 0;
function playScanSuccessSound() {
  const now = Date.now();
  if (now - _lastScanSoundTime < 1500) return; // Prevent duplicate beep sounds
  _lastScanSoundTime = now;

  try {
    const AudioCtx = window.AudioContext || window.webkitAudioContext;
    if (!AudioCtx) return;
    const ctx = new AudioCtx();
    const osc = ctx.createOscillator();
    const gain = ctx.createGain();
    
    osc.type = 'sine';
    osc.frequency.setValueAtTime(1800, ctx.currentTime);
    osc.frequency.exponentialRampToValueAtTime(2400, ctx.currentTime + 0.08);
    
    gain.gain.setValueAtTime(0.3, ctx.currentTime);
    gain.gain.exponentialRampToValueAtTime(0.01, ctx.currentTime + 0.12);
    
    osc.connect(gain);
    gain.connect(ctx.destination);
    
    osc.start();
    osc.stop(ctx.currentTime + 0.13);
  } catch (e) {
    // AudioContext blocked or not supported - silently ignore
  }

  // Haptic vibration feedback for mobile
  if (navigator.vibrate) {
    try { navigator.vibrate([60, 40, 60]); } catch (e) {}
  }
}

async function openCameraScanner(targetInputId, onScanSuccessCallback = null) {
  ScannerState.targetInputId = targetInputId;
  ScannerState.onScanSuccessCallback = onScanSuccessCallback;
  ScannerState.isOpen = true;
  ScannerState.isProcessingScan = false;
  ScannerState.isTorchOn = false;
  ScannerState.hasTorch = false;

  const modal = document.getElementById('camera-scanner-modal');
  const loadingOverlay = document.getElementById('scanner-loading-overlay');
  const loadingText = document.getElementById('scanner-loading-text');
  const statusBox = document.getElementById('scanner-status-text');
  const reticle = document.getElementById('scanner-reticle-overlay');
  const torchBtn = document.getElementById('scanner-torch-btn');

  if (modal) {
    modal.style.display = 'flex';
    modal.classList.add('active');
  }
  if (reticle) reticle.classList.remove('scanned-success');
  if (loadingOverlay) {
    loadingOverlay.style.display = 'flex';
    loadingOverlay.style.opacity = '1';
  }
  if (loadingText) loadingText.innerText = 'Requesting camera access...';
  if (statusBox) {
    statusBox.className = 'scanner-status-box';
    statusBox.innerText = 'Position jewellery tag barcode inside the golden frame';
  }
  if (torchBtn) torchBtn.style.display = 'none';

  // Check if Html5Qrcode library is loaded
  if (typeof Html5Qrcode === 'undefined') {
    if (loadingOverlay) loadingOverlay.style.display = 'none';
    if (statusBox) {
      statusBox.className = 'scanner-status-box error';
      statusBox.innerText = 'Scanner library is loading or blocked. Please use "From Photo" or enter manually.';
    }
    showToast('Scanner library not available. Please enter barcode manually.', 'error');
    return;
  }

  try {
    if (!ScannerState.html5QrCode) {
      ScannerState.html5QrCode = new Html5Qrcode('camera-scanner-reader', {
        verbose: false,
        experimentalFeatures: {
          useBarCodeDetectorIfSupported: true
        }
      });
    }

    // Get camera devices
    const devices = await Html5Qrcode.getCameras();
    if (!devices || devices.length === 0) {
      throw new Error('No camera found on this device.');
    }
    ScannerState.cameras = devices;

    // Prefer back/environment camera
    let preferredCameraId = devices[0].id;
    const backCamera = devices.find(c => {
      const label = (c.label || '').toLowerCase();
      return label.includes('back') || label.includes('rear') || label.includes('environment');
    });
    if (backCamera) {
      preferredCameraId = backCamera.id;
    } else if (devices.length > 1) {
      preferredCameraId = devices[devices.length - 1].id;
    }
    ScannerState.currentCameraId = preferredCameraId;

    await startScannerWithCamera(preferredCameraId);
  } catch (err) {
    console.error('Camera Init Error:', err);
    if (loadingOverlay) loadingOverlay.style.display = 'none';
    if (statusBox) {
      statusBox.className = 'scanner-status-box error';
      statusBox.innerHTML = `⚠️ ${err.message || 'Camera permission denied or camera not found'}.<br><small>Grant camera permission or tap "From Photo" to scan an image.</small>`;
    }
  }
}

async function startScannerWithCamera(cameraIdOrFacing) {
  const loadingOverlay = document.getElementById('scanner-loading-overlay');
  const loadingText = document.getElementById('scanner-loading-text');
  const statusBox = document.getElementById('scanner-status-text');
  const torchBtn = document.getElementById('scanner-torch-btn');

  if (loadingOverlay) {
    loadingOverlay.style.display = 'flex';
    loadingOverlay.style.opacity = '1';
  }
  if (loadingText) loadingText.innerText = 'Starting video feed...';

  const config = {
    fps: 15,
    qrbox: function(viewfinderWidth, viewfinderHeight) {
      const minEdge = Math.min(viewfinderWidth, viewfinderHeight);
      const edge = Math.floor(minEdge * 0.75);
      return { width: Math.max(edge, 200), height: Math.max(Math.floor(edge * 0.65), 140) };
    },
    aspectRatio: 1.0,
    showTorchButtonIfSupported: false,
    formatsToSupport: [
      Html5QrcodeSupportedFormats.CODE_128,
      Html5QrcodeSupportedFormats.CODE_39,
      Html5QrcodeSupportedFormats.CODE_93,
      Html5QrcodeSupportedFormats.EAN_13,
      Html5QrcodeSupportedFormats.EAN_8,
      Html5QrcodeSupportedFormats.UPC_A,
      Html5QrcodeSupportedFormats.UPC_E,
      Html5QrcodeSupportedFormats.CODABAR,
      Html5QrcodeSupportedFormats.ITF,
      Html5QrcodeSupportedFormats.QR_CODE,
      Html5QrcodeSupportedFormats.DATA_MATRIX
    ]
  };

  const cameraConstraint = cameraIdOrFacing ? { deviceId: { exact: cameraIdOrFacing } } : { facingMode: 'environment' };

  await ScannerState.html5QrCode.start(
    cameraConstraint,
    config,
    onBarcodeScannedSuccess,
    onBarcodeScanFailure
  );

  if (loadingOverlay) {
    loadingOverlay.style.opacity = '0';
    setTimeout(() => { if (loadingOverlay) loadingOverlay.style.display = 'none'; }, 300);
  }
  if (statusBox) {
    statusBox.className = 'scanner-status-box';
    statusBox.innerText = 'Scanning... Align tag barcode in the frame';
  }

  // Check torch capabilities
  try {
    const capabilities = ScannerState.html5QrCode.getRunningTrackCameraCapabilities();
    if (capabilities && capabilities.torchFeature && capabilities.torchFeature().isSupported()) {
      ScannerState.hasTorch = true;
      if (torchBtn) torchBtn.style.display = 'inline-flex';
    } else {
      ScannerState.hasTorch = false;
      if (torchBtn) torchBtn.style.display = 'none';
    }
  } catch (e) {
    if (torchBtn) torchBtn.style.display = 'none';
  }
}

function onBarcodeScannedSuccess(decodedText, decodedResult) {
  if (!ScannerState.isOpen || ScannerState.isProcessingScan) return;

  const cleanBarcode = (decodedText || '').trim();
  if (!cleanBarcode) return;

  // Immediately lock scanner so sub-millisecond future video frames don't re-trigger
  ScannerState.isProcessingScan = true;
  ScannerState.isOpen = false;

  // Immediately pause scanning video feed to prevent frame queuing
  try {
    if (ScannerState.html5QrCode && ScannerState.html5QrCode.isScanning) {
      ScannerState.html5QrCode.pause(true);
    }
  } catch (e) {
    // Ignore if pause is not supported by current stream
  }

  // Visual & sound feedback (once only!)
  playScanSuccessSound();
  const reticle = document.getElementById('scanner-reticle-overlay');
  if (reticle) reticle.classList.add('scanned-success');

  const statusBox = document.getElementById('scanner-status-text');
  if (statusBox) {
    statusBox.className = 'scanner-status-box success';
    statusBox.innerText = `Scanned: ${cleanBarcode}`;
  }

  showToast(`Barcode Scanned: ${cleanBarcode}`, 'success');

  // Fill the target input field
  const targetInput = document.getElementById(ScannerState.targetInputId);
  if (targetInput) {
    targetInput.value = cleanBarcode;
    // Dispatch input event so any listeners update
    targetInput.dispatchEvent(new Event('input', { bubbles: true }));
    targetInput.dispatchEvent(new Event('change', { bubbles: true }));
  }

  const callback = ScannerState.onScanSuccessCallback;

  // Close scanner cleanly after brief visual confirmation
  setTimeout(() => {
    closeCameraScanner();
    // Execute action
    if (typeof callback === 'function') {
      try {
        callback();
      } catch (err) {
        console.error('Scan callback error:', err);
      }
    }
  }, 250);
}

function onBarcodeScanFailure(errorMessage) {
  // Silent frame-by-frame failure while searching for barcode
}

async function closeCameraScanner() {
  ScannerState.isOpen = false;
  ScannerState.isProcessingScan = false;
  const modal = document.getElementById('camera-scanner-modal');
  if (modal) {
    modal.classList.remove('active');
    modal.style.display = 'none';
  }

  if (ScannerState.html5QrCode && ScannerState.html5QrCode.isScanning) {
    try {
      await ScannerState.html5QrCode.stop();
    } catch (e) {
      console.warn('Error stopping scanner:', e);
    }
  }

  // Clear reader html if needed
  const readerEl = document.getElementById('camera-scanner-reader');
  if (readerEl) readerEl.innerHTML = '';
  ScannerState.html5QrCode = null;
}

async function switchScannerCamera() {
  if (ScannerState.isSwitching || !ScannerState.isOpen) return;
  if (!ScannerState.cameras || ScannerState.cameras.length <= 1) {
    showToast('Only one camera is available on this device.', 'info');
    return;
  }

  ScannerState.isSwitching = true;
  try {
    if (ScannerState.html5QrCode && ScannerState.html5QrCode.isScanning) {
      await ScannerState.html5QrCode.stop();
    }

    // Toggle camera
    const currentIndex = ScannerState.cameras.findIndex(c => c.id === ScannerState.currentCameraId);
    const nextIndex = (currentIndex + 1) % ScannerState.cameras.length;
    ScannerState.currentCameraId = ScannerState.cameras[nextIndex].id;

    await startScannerWithCamera(ScannerState.currentCameraId);
  } catch (err) {
    console.error('Switch camera error:', err);
    showToast('Failed to switch camera.', 'error');
  } finally {
    ScannerState.isSwitching = false;
  }
}

async function toggleScannerTorch() {
  if (!ScannerState.hasTorch || !ScannerState.html5QrCode || !ScannerState.html5QrCode.isScanning) {
    showToast('Flashlight / Torch not supported on this camera.', 'info');
    return;
  }

  try {
    const capabilities = ScannerState.html5QrCode.getRunningTrackCameraCapabilities();
    const torchFeature = capabilities?.torchFeature?.();
    if (torchFeature && torchFeature.isSupported()) {
      const newState = !ScannerState.isTorchOn;
      await torchFeature.apply(newState);
      ScannerState.isTorchOn = newState;

      const torchBtn = document.getElementById('scanner-torch-btn');
      const torchLabel = document.getElementById('scanner-torch-label');
      if (torchBtn) {
        if (newState) torchBtn.classList.add('active');
        else torchBtn.classList.remove('active');
      }
      if (torchLabel) {
        torchLabel.innerText = newState ? 'Torch On' : 'Torch';
      }
    }
  } catch (err) {
    console.error('Torch error:', err);
    showToast('Could not toggle flashlight.', 'error');
  }
}

async function handleScannerFileInput(event) {
  const file = event.target?.files?.[0];
  if (!file) return;

  const statusBox = document.getElementById('scanner-status-text');
  if (statusBox) {
    statusBox.className = 'scanner-status-box';
    statusBox.innerText = 'Analyzing photo for barcode...';
  }

  try {
    if (ScannerState.html5QrCode && ScannerState.html5QrCode.isScanning) {
      await ScannerState.html5QrCode.stop();
    }

    const tempScanner = new Html5Qrcode('camera-scanner-reader', { verbose: false });
    const result = await tempScanner.scanFile(file, true);
    
    if (result) {
      onBarcodeScannedSuccess(result, null);
    } else {
      throw new Error('No readable barcode found in this photo.');
    }
  } catch (err) {
    console.error('File scan error:', err);
    if (statusBox) {
      statusBox.className = 'scanner-status-box error';
      statusBox.innerText = 'No barcode detected in image. Please try another angle or enter manually.';
    }
    showToast('No barcode found in image. Try another image or scan with camera.', 'error');
    // Restart camera if still open
    if (ScannerState.isOpen && ScannerState.currentCameraId) {
      startScannerWithCamera(ScannerState.currentCameraId).catch(() => {});
    }
  } finally {
    event.target.value = '';
  }
}

// ── App Initialization ──────────────────────────────────────────────────────
window.addEventListener('DOMContentLoaded', () => {
  applyTheme(State.theme);
  updateUserUI();

  // Route based on hash or login status
  const hash = window.location.hash.replace('#', '');
  if (State.token) {
    navigateTo(hash || 'dashboard');
    loadPrintersList();
  } else {
    navigateTo('login');
  }
});
