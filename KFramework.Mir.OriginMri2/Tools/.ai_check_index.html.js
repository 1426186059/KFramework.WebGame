
const $ = id => document.getElementById(id);
const esc = s => String(s == null ? '' : s).replace(/&/g, '&amp;').replace(/"/g, '&quot;');
let maps = [], monsters = [];
let mapFilter = 'all';     // 左侧 Toggle：'all' 全部 / 'has' 仅显示有安全区 / 'sp' 仅显示有出生点(StartPoint)
let current = null;       // 当前地图详情
let flags = null;         // Uint8Array 解码后的 nibble 数据
let cellPx = 4;
let renderMode = 'grid';  // 'grid' 灰白网格 | 'real' 真实地图(CDN 瓦片)
let realImg = null;       // 已加载的真实地图 Image 对象
let realReady = false;    // 真实地图是否就绪（切换按钮是否点亮）
let selectedMv = -1;      // 当前选中的传送点条目索引（-1 表示无）
let selectedNpc = -1;     // 当前选中的 NPC 条目索引（-1 表示无）
let selectedMon = -1;     // 当前选中的怪物条目索引（-1 表示无）

const BOOL_FIELDS = [
  ['noTeleport','NoTeleport'], ['noReconnect','NoReconnect'], ['noRecall','NoRecall'],
  ['noDrug','NoDrug'], ['noFight','NoFight'], ['noDropMonster','NoDropMonster'],
  ['noNames','NoNames'], ['noMount','NoMount'], ['noGroup','NoGroup'],
  ['requiredGroup','RequiredGroup'], ['fight','Fight'], ['fire','Fire'],
  ['noTownTeleport','NoTownTeleport'], ['gt','GT']
];

function b64ToBytes(b64) {
  const bin = atob(b64);
  const a = new Uint8Array(bin.length);
  for (let i = 0; i < bin.length; i++) a[i] = bin.charCodeAt(i);
  return a;
}

// 判断某格是否可走：解码标志字节的第 3 位(bit 8)未置位即可走（与 drawMap 一致）。
// flags 为空（地图网格解码失败）时按“可走”处理，避免误报红字。
function isWalkable(x, y) {
  const m = current && current.map;
  if (!m || !m.cols || !m.rows) return true;
  if (x < 0 || y < 0 || x >= m.cols || y >= m.rows) return false;
  if (!flags || flags.length < m.cols * m.rows) return true;
  const idx = y * m.cols + x;
  if (idx < 0 || idx >= flags.length) return false;
  return (flags[idx] & 8) === 0;
}
// 不可走坐标的红色加粗样式（NPC/怪物/传送点列表复用）
function coordStyle(x, y) {
  return isWalkable(x, y) ? '' : ' style="color:#e53935;font-weight:bold" title="坐标不可走"';
}

function setStatus(t) { $('status').textContent = t; }
function setTb(t) { $('tbStatus').textContent = t; }

async function init() {
  const [mRes, monRes] = await Promise.all([fetch('/api/maps'), fetch('/api/monsters')]);
  const m = await mRes.json(); maps = m.maps;
  const mo = await monRes.json(); monsters = mo.monsters;

  $('saveBtn').onclick = save;
  $('srcBtn').onclick = () => window.open('/sources.html', '_blank');

  buildBoolFields();
  await loadBgmOptions();
  renderList('');
  wireTabs();
  wireTables();
  setTb(`已加载 ${maps.length} 张地图 / ${monsters.length} 个怪物 / ${bgmOptions.length} 首地图音乐`);
}

// 原始传奇 5config.soundnormal[type3] 的地图背景音乐，填到常规页 Music 下拉
let bgmOptions = []; // [{ file, source, mapIds }]
async function loadBgmOptions() {
  const sel = $('g_music');
  try {
    const res = await fetch('/api/bgms');
    const j = await res.json();
    bgmOptions = j.bgms || [];
  } catch (e) { bgmOptions = []; }
  sel.innerHTML = '';
  const none = document.createElement('option');
  none.value = '0'; none.textContent = '0 - 无背景音乐';
  sel.appendChild(none);
  for (const b of bgmOptions) {
    const o = document.createElement('option');
    o.value = String(b.file);
    o.textContent = `${b.file} - ${b.source}`;
    sel.appendChild(o);
  }
}

function buildBoolFields() {
  const box = $('g_bools');
  box.innerHTML = '';
  BOOL_FIELDS.forEach(([k, label]) => {
    const id = 'g_' + k;
    const wrap = document.createElement('div');
    wrap.className = 'gfield';
    wrap.innerHTML = `<label>${label}</label><input type="checkbox" id="${id}" />`;
    box.appendChild(wrap);
  });
}

function renderList(filter) {
  const f = (filter || '').trim().toLowerCase();
  const ul = $('maplist'); ul.innerHTML = '';
  maps.forEach(m => {
    // 左侧 Toggle 过滤：全部 / 仅显示有安全区 / 仅显示有出生点(StartPoint)
    if (mapFilter === 'has' && (m.safeZoneCount || 0) === 0) return;
    if (mapFilter === 'sp' && (m.startPointCount || 0) === 0) return;
    // 文字搜索：id / 标题 / 文件名
    if (f && !(`${m.index}`.includes(f)) && !m.title.toLowerCase().includes(f) && !m.fileName.toLowerCase().includes(f)) return;
    const li = document.createElement('li');
    li.dataset.id = m.index;
    li.innerHTML = `<span class="id">[${m.index}]</span> ${m.title || m.fileName}`;
    if (current && current.index === m.index) li.classList.add('active');
    li.onclick = () => selectMap(m.index);
    ul.appendChild(li);
  });
}

async function selectMap(id) {
  setStatus('加载中...');
  const res = await fetch('/api/maps/' + id);
  if (!res.ok) { setStatus('加载失败: ' + (await res.text())); return; }
  current = await res.json();
  if (!current.npcs) current.npcs = [];
  if (!current.movements) current.movements = [];
  selectedMv = -1;
  selectedNpc = -1;
  selectedMon = -1;
  flags = current.map.flagsBase64 ? b64ToBytes(current.map.flagsBase64) : null;
  fitZoom();
  loadRealMap(current.index);
  renderList($('filter').value);
  renderGeneral();
  renderSZ(); renderMon(); renderNpc(); renderMv();
  drawMap();
  setStatus(`已加载：${current.title || current.fileName} (id=${current.index})`);
}

async function reloadMap() { await selectMap(current.index); }

function renderGeneral() {
  const g = current.general;
  $('g_title').value = g.title || '';
  $('g_fileName').value = g.fileName || '';
  $('g_miniMap').value = g.miniMap || 0;
  $('g_bigMap').value = g.bigMap || 0;
  // Music 下拉：若当前值不在原始 BGM 列表里（历史/自定义值），临时加一个选项以保留原值
  {
    const sel = $('g_music');
    const val = String(g.music || 0);
    if (![...sel.options].some(o => o.value === val)) {
      const o = document.createElement('option');
      o.value = val; o.textContent = `${val} - 自定义（非原始传奇 BGM）`;
      sel.appendChild(o);
    }
    sel.value = val;
  }
  $('g_light').value = g.light || 0;
  $('g_noReconnectMap').value = g.noReconnectMap || '';
  BOOL_FIELDS.forEach(([k]) => { $('g_' + k).checked = !!g[k]; });
}

function wireTabs() {
  document.querySelectorAll('#tabs button').forEach(b => {
    b.onclick = () => {
      document.querySelectorAll('#tabs button').forEach(x => x.classList.remove('active'));
      document.querySelectorAll('.tabpane').forEach(x => x.classList.remove('active'));
      b.classList.add('active');
      $('tab-' + b.dataset.tab).classList.add('active');
    };
  });
}

function wireTables() {
  // 安全区
  $('addSZ').onclick = () => { current.safeZones.push({ x: Math.floor(current.map.cols/2), y: Math.floor(current.map.rows/2), size: 10, sizeY: 10, rotation: 0, start: false }); renderSZ(); drawMap(); };
  // 怪物
  $('addMon').onclick = () => { const mi = monsters.length ? monsters[0].index : 0; current.respawns.push({ monsterIndex: mi, x: Math.floor(current.map.cols/2), y: Math.floor(current.map.rows/2), count: 1, spread: 0, delay: 0, direction: 0 }); renderMon(); drawMap(); };
  // 传送点
  $('addMv').onclick = () => { current.movements.push({ destMapIndex: current.index, sourceX: Math.floor(current.map.cols/2), sourceY: Math.floor(current.map.rows/2), destX: 0, destY: 0, needHole: false, needMove: false }); renderMv(); drawMap(); };
  // 委托编辑（安全区/怪物/传送点：本地修改，保存时随地图一起写入 MapInfo_Fix）
  bindBody('szBody', current => current.safeZones, ['x','y','size','sizeY','rotation'], ['start']);
  bindBody('monBody', current => current.respawns, ['monsterIndex','x','y','count','spread','delay','direction'], []);
  bindBody('mvBody', current => current.movements, ['destMapIndex','sourceX','sourceY','destX','destY'], ['needHole','needMove']);
  // NPC：可编辑 名称 / X / Y，保存本图时一并覆写全局 NPCInfo 表（按 Index 更新，不影响其它地图的 NPC）
  bindBody('npcBody', current => current.npcs, ['x','y','rate','image'], []);
}

function bindBody(bodyId, getArr, numFields, boolFields) {
  const body = $(bodyId);
  const onEdit = e => {
    const t = e.target;
    if (t.dataset.i === undefined) return;
    const i = +t.dataset.i; const f = t.dataset.f;
    const arr = getArr(current);
    if (t.type === 'checkbox') arr[i][f] = t.checked;
    else if (numFields.includes(f)) arr[i][f] = parseInt(t.value, 10) || 0;
    else arr[i][f] = t.value;
    drawMap();
  };
  // 'input' 覆盖文本框/数字框，'change' 覆盖下拉框（select）
  body.addEventListener('input', onEdit);
  body.addEventListener('change', onEdit);
  body.addEventListener('click', e => {
    if (e.target.dataset.del !== undefined) {
      const i = +e.target.dataset.del;
      getArr(current).splice(i, 1);
      if (bodyId === 'szBody') renderSZ(); else if (bodyId === 'monBody') renderMon(); else renderMv();
      drawMap();
    }
  });
}

// ---- 怪物选择：点击按钮弹出滑动列表 ----
let pickerRow = -1;
const monPicker = $('monPicker');
// 面板水平居中于条目（按钮）中心 X，竖直贴在其下方（溢出则翻到上方）
function positionPicker(panel, anchor) {
  const r = anchor.getBoundingClientRect();
  const w = panel.offsetWidth || 360;
  let left = r.left + r.width / 2 - w / 2;
  left = Math.max(8, Math.min(left, window.innerWidth - w - 8));
  let top = r.bottom + 4;
  const ph = panel.offsetHeight || window.innerHeight * 0.7;
  if (top + ph > window.innerHeight) top = Math.max(8, r.top - ph - 4);
  panel.style.left = left + 'px';
  panel.style.top = top + 'px';
}
function openMonPicker(i, anchor) {
  pickerRow = i;
  monPicker.innerHTML = '<div class="pick-head">选择怪物（共 ' + monsters.length + '）</div>' +
    '<div class="pick-search"><input class="pick-input" placeholder="搜索 id / 名称..." /><button class="pick-go">搜索</button></div>' +
    '<div class="pick-list"></div>';
  renderMonList('');
  positionPicker(monPicker, anchor);
  monPicker.classList.add('show');
}
function renderMonList(q) {
  q = (q || '').trim().toLowerCase();
  const list = monPicker.querySelector('.pick-list');
  const items = monsters.filter(m => !q || String(m.index).includes(q) || (m.name || '').toLowerCase().includes(q));
  list.innerHTML = items.length
    ? items.map(m => `<button class="pick-item" data-idx="${m.index}">${m.index} - ${m.name}${m.isBoss ? ' [Boss]' : ''}</button>`).join('')
    : '<div class="pick-empty">无匹配结果</div>';
}
function closeMonPicker() { monPicker.classList.remove('show'); pickerRow = -1; }

$('monBody').addEventListener('click', e => {
  const btn = e.target.closest('.monpick');
  if (btn) { openMonPicker(+btn.dataset.i, btn); e.stopPropagation(); }
});
monPicker.addEventListener('click', e => {
  const item = e.target.closest('.pick-item');
  if (!item) return;
  const idx = +item.dataset.idx;
  if (pickerRow >= 0) {
    current.respawns[pickerRow].monsterIndex = idx;
    renderMon();
    drawMap();
  }
  closeMonPicker();
});
monPicker.addEventListener('input', e => { if (e.target.classList.contains('pick-input')) renderMonList(e.target.value); });
monPicker.addEventListener('click', e => { if (e.target.classList.contains('pick-go')) renderMonList(monPicker.querySelector('.pick-input').value); });
document.addEventListener('click', e => {
  if (monPicker.classList.contains('show') && !monPicker.contains(e.target) && !e.target.closest('.monpick')) closeMonPicker();
});

function renderSZ() {
  const body = $('szBody');
  body.innerHTML = current.safeZones.map((z, i) =>
  `<tr>
    <td><input type="number" data-i="${i}" data-f="x" value="${z.x}"></td>
    <td><input type="number" data-i="${i}" data-f="y" value="${z.y}"></td>
    <td><input type="number" data-i="${i}" data-f="size" value="${z.size}"></td>
    <td><input type="number" data-i="${i}" data-f="sizeY" value="${z.sizeY || z.size}"></td>
    <td><input type="number" data-i="${i}" data-f="rotation" value="${z.rotation || 0}"></td>
    <td><input type="checkbox" data-i="${i}" data-f="start" ${z.start ? 'checked' : ''}></td>
    <td><button class="del" data-del="${i}">删</button></td>
  </tr>`).join('');
}

function renderMon() {
  const body = $('monBody');
  body.innerHTML = current.respawns.map((r, i) => {
    const mn = monsters.find(m => m.index === r.monsterIndex);
    const label = mn ? `${mn.index} - ${mn.name}${mn.isBoss ? ' [Boss]' : ''}` : `(未知: ${r.monsterIndex})`;
    const sel = (i === selectedMon);
    return `<tr class="${sel ? 'sel' : ''}">
      <td class="rowpick" data-row="${i}" title="选中此项（地图标记高亮放大）">${i + 1}</td>
      <td><button class="monpick" data-i="${i}">${label} ▾</button></td>
      <td><input type="number" data-i="${i}" data-f="x" value="${r.x}"${coordStyle(r.x, r.y)}></td>
      <td><input type="number" data-i="${i}" data-f="y" value="${r.y}"${coordStyle(r.x, r.y)}></td>
      <td><input type="number" data-i="${i}" data-f="count" value="${r.count}"></td>
      <td><input type="number" data-i="${i}" data-f="spread" value="${r.spread}"></td>
      <td><input type="number" data-i="${i}" data-f="delay" value="${r.delay}"></td>
      <td><input type="number" data-i="${i}" data-f="direction" value="${r.direction}"></td>
      <td><button class="del" data-del="${i}">删</button></td>
    </tr>`;
  }).join('');
}

function renderNpc() {
  const body = $('npcBody');
  body.innerHTML = current.npcs.map((n, i) =>
    `<tr class="${i === selectedNpc ? 'sel' : ''}">
      <td class="rowpick" data-row="${i}" title="选中此项（地图标记高亮放大）">${i + 1}</td>
      <td>${n.index}</td>
      <td><input type="text" value="${esc(n.fileName || '')}" disabled></td>
      <td><input type="text" data-i="${i}" data-f="name" value="${esc(n.name || '')}"></td>
      <td><input type="text" data-i="${i}" data-f="nameUI" value="${esc(n.nameUI || '')}" placeholder="(空)"></td>
      <td><input type="number" data-i="${i}" data-f="x" value="${n.x}"${coordStyle(n.x, n.y)}></td>
      <td><input type="number" data-i="${i}" data-f="y" value="${n.y}"${coordStyle(n.x, n.y)}></td>
      <td><input type="number" value="${n.rate}" disabled></td>
      <td><input type="number" value="${n.image}" disabled></td>
    </tr>`).join('');
}

// ---- 目标地图选择：Button + 居中滚动列表 ----
let mvPickerRow = -1;
const mvPicker = $('mvPicker');
function openMvPicker(i, anchor) {
  mvPickerRow = i;
  mvPicker.innerHTML = '<div class="pick-head">选择目标地图（共 ' + maps.length + '）</div>' +
    '<div class="pick-search"><input class="pick-input" placeholder="搜索 id / 名称..." /><button class="pick-go">搜索</button></div>' +
    '<div class="pick-list"></div>';
  renderMvList('');
  positionPicker(mvPicker, anchor);
  mvPicker.classList.add('show');
}
function renderMvList(q) {
  q = (q || '').trim().toLowerCase();
  const list = mvPicker.querySelector('.pick-list');
  const items = maps.filter(m => !q || String(m.index).includes(q) || (m.title || m.fileName || '').toLowerCase().includes(q));
  list.innerHTML = items.length
    ? items.map(m => `<button class="mv-item" data-idx="${m.index}">${m.index} - ${m.title || m.fileName}</button>`).join('')
    : '<div class="pick-empty">无匹配结果</div>';
}
function closeMvPicker() { mvPicker.classList.remove('show'); mvPickerRow = -1; }

$('mvBody').addEventListener('click', e => {
  const btn = e.target.closest('.mvpick');
  if (btn) { openMvPicker(+btn.dataset.i, btn); e.stopPropagation(); }
});
// 点击行号：选中该传送点（高亮列表行 + 地图标记）
$('mvBody').addEventListener('click', e => {
  const rp = e.target.closest('.rowpick');
  if (rp) { selectedMv = +rp.dataset.row; renderMv(); drawMap(); }
});
// 点击 NPC 列表的 ID 列：选中并高亮地图标记
$('npcBody').addEventListener('click', e => {
  const rp = e.target.closest('.rowpick');
  if (rp) { selectedNpc = +rp.dataset.row; renderNpc(); drawMap(); }
});
// 点击怪物列表的序号列：选中并高亮地图标记（与传送点/NPC 一致）
$('monBody').addEventListener('click', e => {
  const rp = e.target.closest('.rowpick');
  if (rp) { selectedMon = +rp.dataset.row; renderMon(); drawMap(); }
});
mvPicker.addEventListener('click', e => {
  const item = e.target.closest('.mv-item');
  if (!item) return;
  const idx = +item.dataset.idx;
  if (mvPickerRow >= 0) {
    current.movements[mvPickerRow].destMapIndex = idx;
    renderMv();
    drawMap();
  }
  closeMvPicker();
});
mvPicker.addEventListener('input', e => { if (e.target.classList.contains('pick-input')) renderMvList(e.target.value); });
mvPicker.addEventListener('click', e => { if (e.target.classList.contains('pick-go')) renderMvList(mvPicker.querySelector('.pick-input').value); });
document.addEventListener('click', e => {
  if (mvPicker.classList.contains('show') && !mvPicker.contains(e.target) && !e.target.closest('.mvpick')) closeMvPicker();
});

function renderMv() {
  const body = $('mvBody');
  body.innerHTML = current.movements.map((m, i) => {
    const mm = maps.find(x => x.index === m.destMapIndex);
    const label = mm ? `${m.destMapIndex} - ${mm.title || mm.fileName}` : `未知(${m.destMapIndex})`;
    return `<tr class="${i === selectedMv ? 'sel' : ''}">
      <td class="rowpick" data-row="${i}" title="选中此项（地图标记高亮）">${i + 1}</td>
      <td><button class="mvpick" data-i="${i}">${label} ▾</button></td>
      <td><input type="number" data-i="${i}" data-f="sourceX" value="${m.sourceX}"${coordStyle(m.sourceX, m.sourceY)}></td>
      <td><input type="number" data-i="${i}" data-f="sourceY" value="${m.sourceY}"${coordStyle(m.sourceX, m.sourceY)}></td>
      <td><input type="number" data-i="${i}" data-f="destX" value="${m.destX}"></td>
      <td><input type="number" data-i="${i}" data-f="destY" value="${m.destY}"></td>
      <td><input type="checkbox" data-i="${i}" data-f="needHole" ${m.needHole ? 'checked' : ''}></td>
      <td><input type="checkbox" data-i="${i}" data-f="needMove" ${m.needMove ? 'checked' : ''}></td>
      <td><button class="del" data-del="${i}">删</button></td>
    </tr>`;
  }).join('');
}

function drawMap() {
  const c = $('canvas');
  const m = current && current.map;
  if (!m || !m.cols) { setStatus('无地图数据或无法解码'); return; }
  const cols = m.cols, rows = m.rows;
  // 真实地图瓦片为 48x32（非正方形），横/纵需按比例分别缩放以保持宽高比，否则会被拉变形
  let cellX = cellPx, cellY = cellPx;
  if (renderMode === 'real' && realImg && realImg.naturalWidth) {
    const iw = realImg.naturalWidth / cols, ih = realImg.naturalHeight / rows;
    cellY = cellX * (ih / iw);
  }
  const sc = Math.min(cellX, cellY);
  c.width = cols * cellX; c.height = rows * cellY;
  const ctx = c.getContext('2d');
  ctx.imageSmoothingEnabled = false;

  // 底层：真实地图（CDN 瓦片拼接）或灰白可走网格
  if (renderMode === 'real' && realImg) {
    ctx.drawImage(realImg, 0, 0, c.width, c.height);
  } else {
    // 离屏按像素绘制（高效）
    const off = document.createElement('canvas');
    off.width = cols; off.height = rows;
    const octx = off.getContext('2d');
    const img = octx.createImageData(cols, rows);
    const d = img.data;
    for (let i = 0; i < cols * rows; i++) {
      // 解码器每格存 1 字节的 nibble 值（0~15），直接取用即可，无需再拆高低位
      const nib = flags ? flags[i] : 0;
      const walk = (nib & 8) === 0;
      const safe = (nib & 2) !== 0;
      let r, g, bl;
      if (walk) { r = 235; g = 232; bl = 222; } else { r = 64; g = 64; bl = 64; }
      if (safe) { if (walk) { r = 200; g = 232; bl = 200; } else { r = 40; g = 80; bl = 40; } }
      const p = i * 4; d[p] = r; d[p + 1] = g; d[p + 2] = bl; d[p + 3] = 255;
    }
    octx.putImageData(img, 0, 0);
    ctx.drawImage(off, 0, 0, c.width, c.height);
  }

  // 安全区（支持矩形 + 旋转）：以中心点旋转绘制半宽/半高矩形
  if (layerVisible.safe) {
    const deg2rad = Math.PI / 180;
    current.safeZones.forEach(z => {
      const hw = z.size * cellX;
      const hh = ((z.sizeY && z.sizeY > 0) ? z.sizeY : z.size) * cellY;
      const cx = z.x * cellX + cellX / 2, cy = z.y * cellY + cellY / 2;
      ctx.save();
      ctx.translate(cx, cy);
      ctx.rotate((z.rotation || 0) * deg2rad);
      ctx.lineWidth = z.start ? 3 : 2;
      ctx.strokeStyle = z.start ? '#00ff00' : '#00cc00';
      ctx.strokeRect(-hw, -hh, 2 * hw, 2 * hh);
      ctx.restore();
    });
  }
  // 怪物（醒目圆形：普通=红，Boss=紫；选中项黄底红环高亮放大并标注名称）
  if (layerVisible.mon) {
    current.respawns.forEach((r, i) => {
      const mn = monsters.find(m => m.index === r.monsterIndex);
      const boss = mn ? mn.isBoss : false;
      const sel = (i === selectedMon);
      const cx = r.x * cellX + cellX / 2, cy = r.y * cellY + cellY / 2;
      const rad = sel ? Math.max(6, sc * 0.85) : Math.max(3, sc * 0.5);
      ctx.beginPath(); ctx.arc(cx, cy, rad, 0, Math.PI * 2);
      if (sel) { ctx.fillStyle = '#ffeb3b'; ctx.fill(); ctx.lineWidth = 2.5; ctx.strokeStyle = '#ff00aa'; ctx.stroke(); }
      else { ctx.fillStyle = boss ? '#d500f9' : '#ff1744'; ctx.fill(); ctx.lineWidth = 1.5; ctx.strokeStyle = '#fff'; ctx.stroke(); }
      if (sel && mn && mn.name && sc >= 6) {
        ctx.fillStyle = '#7a0033';
        ctx.font = Math.max(9, Math.floor(sc)) + 'px sans-serif';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText(mn.name, cx, cy - rad - Math.max(4, sc * 0.4));
      }
    });
  }
  // NPC（青色圆形；选中项黄底蓝环高亮并标注名称）
  if (layerVisible.npc) {
    current.npcs.forEach((n, i) => {
      const cx = n.x * cellX + cellX / 2, cy = n.y * cellY + cellY / 2;
      const sel = (i === selectedNpc);
      const rad = sel ? Math.max(6, sc * 0.85) : Math.max(3, sc * 0.5);
      ctx.beginPath(); ctx.arc(cx, cy, rad, 0, Math.PI * 2);
      if (sel) { ctx.fillStyle = '#ffeb3b'; ctx.fill(); ctx.lineWidth = 2.5; ctx.strokeStyle = '#ff00aa'; ctx.stroke(); }
      else { ctx.fillStyle = '#00e5ff'; ctx.fill(); ctx.lineWidth = 1.5; ctx.strokeStyle = '#00363a'; ctx.stroke(); }
      if (sc >= 8 && n.name) {
        ctx.fillStyle = sel ? '#7a0033' : '#00363a';
        ctx.font = Math.max(9, Math.floor(sc)) + 'px sans-serif';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText(n.name, cx, cy - rad - Math.max(4, sc * 0.4));
      }
    });
  }
  // 传送点（醒目橙色圆形 = 源坐标；选中项黄底粉环高亮并标注目标地图编号）
  if (layerVisible.mv) {
    current.movements.forEach((mv, i) => {
      const cx = mv.sourceX * cellX + cellX / 2, cy = mv.sourceY * cellY + cellY / 2;
      const sel = (i === selectedMv);
      const rad = sel ? Math.max(6, sc * 0.85) : Math.max(3, sc * 0.5);
      ctx.beginPath(); ctx.arc(cx, cy, rad, 0, Math.PI * 2);
      if (sel) { ctx.fillStyle = '#ffeb3b'; ctx.fill(); ctx.lineWidth = 2.5; ctx.strokeStyle = '#ff00aa'; ctx.stroke(); }
      else { ctx.fillStyle = '#ff9100'; ctx.fill(); ctx.lineWidth = 1.5; ctx.strokeStyle = '#fff'; ctx.stroke(); }
      if (sc >= 8) {
        ctx.fillStyle = sel ? '#7a0033' : '#fff';
        ctx.font = Math.max(9, Math.floor(sc)) + 'px sans-serif';
        ctx.textAlign = 'center'; ctx.textBaseline = 'middle';
        ctx.fillText(String(mv.destMapIndex), cx, cy - rad - Math.max(4, sc * 0.4));
      }
    });
  }
  setStatus(`${current.title || current.fileName} (id=${current.index})  网格 ${cols}×${rows}  可走 ${m.walkable}`);
}

async function save() {
  if (!current) return;
  const g = current.general;
  g.title = $('g_title').value;
  g.fileName = $('g_fileName').value;
  g.miniMap = parseInt($('g_miniMap').value, 10) || 0;
  g.bigMap = parseInt($('g_bigMap').value, 10) || 0;
  g.music = parseInt($('g_music').value, 10) || 0;
  g.light = parseInt($('g_light').value, 10) || 0;
  g.noReconnectMap = $('g_noReconnectMap').value;
  BOOL_FIELDS.forEach(([k]) => { g[k] = $('g_' + k).checked; });

  // NPC：把当前图编辑后的 NPC（名称 / X / Y 等）一并提交，后端按 Index 覆写 NPCInfo 表（不影响其它地图）
  const payload = {
    general: g,
    safeZones: current.safeZones,
    respawns: current.respawns.map(r => ({ monsterIndex: r.monsterIndex, x: r.x, y: r.y, count: r.count, spread: r.spread, delay: r.delay, direction: r.direction })),
    movements: current.movements.map(m => ({ destMapIndex: m.destMapIndex, sourceX: m.sourceX, sourceY: m.sourceY, destX: m.destX, destY: m.destY, needHole: m.needHole, needMove: m.needMove })),
    npcs: current.npcs.map(n => ({ index: n.index, fileName: n.fileName, name: n.name, nameUI: n.nameUI || '', x: n.x, y: n.y, rate: n.rate, image: n.image }))
  };
  setTb('保存中...');
  const res = await fetch('/api/maps/' + current.index, {
    method: 'POST', headers: { 'Content-Type': 'application/json' }, body: JSON.stringify(payload)
  });
  if (res.ok) { setTb('已保存 ✅'); }
  else { setTb('保存失败: ' + (await res.text())); }
}

$('filter').addEventListener('input', e => renderList(e.target.value));
// 安全区 Toggle：切换“全部地图 / 有安全区”并重渲染列表
document.querySelectorAll('.seg-btn').forEach(b => {
  b.addEventListener('click', () => {
    document.querySelectorAll('.seg-btn').forEach(x => x.classList.remove('active'));
    b.classList.add('active');
    mapFilter = b.dataset.f;
    renderList($('filter').value);
  });
});

// 图层显隐开关：安全区 / NPC / 怪物 / 传送点 各自独立
const layerVisible = { safe: true, npc: true, mon: true, mv: true };
document.querySelectorAll('#layerBar .lt').forEach(b => {
  b.addEventListener('click', () => {
    const k = b.dataset.layer;
    layerVisible[k] = !layerVisible[k];
    b.classList.toggle('on', layerVisible[k]);
    drawMap();
  });
});

// ---------- 缩放 ----------
function fitZoom() {
  const m = current && current.map;
  if (!m || !m.cols) return;
  cellPx = Math.max(1, Math.floor(560 / Math.max(m.cols, m.rows)));
  $('zoomLbl').textContent = cellPx + 'px';
}
function setZoom(px) {
  cellPx = Math.max(1, Math.min(64, px));
  $('zoomLbl').textContent = cellPx + 'px';
  drawMap();
}

// ---------- 真实地图（CDN 瓦片拼接）异步加载 + 按钮置灰/点亮 ----------
async function loadRealMap(id) {
  realReady = false; realImg = null;
  updateViewBtns();
  try {
    const res = await fetch('/api/mapimage/' + id);
    if (!res.ok) { updateViewBtns('无真实地图'); return; }
    const blob = await res.blob();
    const url = URL.createObjectURL(blob);
    const im = new Image();
    im.onload = () => { if (id !== current.index) return; realImg = im; realReady = true; updateViewBtns(); if (renderMode === 'real') drawMap(); };
    im.onerror = () => { realReady = false; updateViewBtns('图加载失败'); };
    im.src = url;
  } catch (e) { updateViewBtns('加载失败'); }
}
function updateViewBtns(msg) {
  const g = $('viewGrid'), r = $('viewReal');
  g.classList.toggle('on', renderMode === 'grid');
  r.disabled = !realReady;
  r.classList.toggle('on', realReady && renderMode === 'real');
  r.textContent = realReady ? '真实地图' : (msg || '真实地图(加载中…)');
}

// 视图切换：网格 / 真实地图
$('viewGrid').onclick = () => { renderMode = 'grid'; updateViewBtns(); drawMap(); };
$('viewReal').onclick = () => { if (!realReady) return; renderMode = 'real'; updateViewBtns(); drawMap(); };
// 缩放按钮
$('zoomIn').onclick = () => setZoom(cellPx + (cellPx < 8 ? 1 : Math.max(1, Math.round(cellPx * 0.25))));
$('zoomOut').onclick = () => setZoom(cellPx - (cellPx <= 2 ? 1 : Math.max(1, Math.round(cellPx * 0.2))));
$('zoomFit').onclick = () => fitZoom();

// ---------- 全局设置：独立页面 ----------
$('globalBtn').onclick = () => window.open('/globalsettings.html', '_blank');

init();
