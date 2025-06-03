// --- Tab switching ---
const btnChat = document.getElementById('btn-chat'),
      btnDash = document.getElementById('btn-dash'),
      paneChat = document.getElementById('pane-chat'),
      paneDash = document.getElementById('pane-dash');

btnChat.onclick = () => {
  btnChat.classList.add('active');
  btnDash.classList.remove('active');
  paneChat.style.display = 'flex';
  paneDash.style.display = 'none';
};
btnDash.onclick = () => {
  btnDash.classList.add('active');
  btnChat.classList.remove('active');
  paneChat.style.display = 'none';
  paneDash.style.display = 'block';
  initDash();
};

// --- Chat logic ---
const chatDiv   = document.getElementById('chat'),
      chatIn    = document.getElementById('chat-input'),
      chatBtn   = document.getElementById('chat-send'),
      converter = new showdown.Converter(),
      CHAT_API  = 'http://localhost:5000/api/Chat/ask';
      //CHAT_API  = 'http://192.168.1.168:5000/api/Chat/ask';

function appendMsg(cls, text) {
  const box = document.createElement('div');
  box.className = 'msg ' + cls;
  box.innerHTML = converter.makeHtml(text);
  chatDiv.appendChild(box);
  chatDiv.scrollTop = chatDiv.scrollHeight;
}

function showTyping() {
  const t = document.createElement('div');
  t.className = 'typing'; t.id = '_typing';
  t.innerHTML = '<div class="dot"></div><div class="dot"></div><div class="dot"></div>';
  chatDiv.appendChild(t);
  chatDiv.scrollTop = chatDiv.scrollHeight;
}
function hideTyping() {
  const t = document.getElementById('_typing');
  if (t) t.remove();
}

async function sendChat() {
  const prompt = chatIn.value.trim();
  if (!prompt) return;
  appendMsg('user', prompt);
  chatIn.value = '';
  chatBtn.disabled = true;
  showTyping();
  try {
    const res = await fetch(CHAT_API, {
      method:'POST',
      headers:{'Content-Type':'application/json'},
      body: JSON.stringify({ prompt })
    });
    const txt = await res.text();
    if (!res.ok) throw new Error(txt);
    const { reply } = JSON.parse(txt);
    appendMsg('bot', reply||'[no reply]');
  } catch (err) {
    appendMsg('bot', '⚠ ' + err.message);
  } finally {
    hideTyping();
    chatBtn.disabled = false;
  }
}

chatBtn.addEventListener('click', sendChat);
chatIn.addEventListener('keydown', e => { if (e.key==='Enter') sendChat(); });

// --- Dashboard logic ---
const API = 'http://localhost:5000';
//const API = 'http://192.168.1.168:5000';
let c1_oee, c1_gs, c2_oee, c2_gs;

async function getJSON(url) {
  const r = await fetch(url);
  if (!r.ok) throw new Error(`${url} -> ${r.status}`);
  return await r.json();
}

async function fetchErrorLogs() {
  const res = await fetch(`${API}/api/ErrorLog?skip=0&limit=100`);
  return await res.json();           // Array<ErrorLog>
}

async function initDash() {
  document.getElementById('sel-line1').innerHTML = '';
  document.getElementById('sel-line2').innerHTML = '';
  document.getElementById('sel-line1').appendChild(new Option('Chọn Order',''));
  document.getElementById('sel-line2').appendChild(new Option('Chọn Order',''));
  const logs = await getJSON(`${API}/api/OrderLog?skip=0&limit=100`);
  logs.filter(o=>o.line==='Line 1')
      .forEach(o=> document.getElementById('sel-line1').appendChild(new Option(o.orderID,o.orderID)));
  logs.filter(o=>o.line==='Line 2')
      .forEach(o=> document.getElementById('sel-line2').appendChild(new Option(o.orderID,o.orderID)));
  document.getElementById('sel-line1').onchange = ()=>draw('line1');
  document.getElementById('sel-line2').onchange = ()=>draw('line2');
  const s1 = document.getElementById('sel-line1');
  if (s1.options.length) { s1.selectedIndex=0; draw('line1'); }
  const s2 = document.getElementById('sel-line2');
  if (s2.options.length) { s2.selectedIndex=0; draw('line2'); }
}

async function draw(lineKey) {
  const sel = document.getElementById(`sel-${lineKey}`);
  const orderId = sel.value;
  if (!orderId) return;

  const batches = await getJSON(`${API}/api/OEEValue/all`);
  const batch = batches.find(b =>
    b.orderID===orderId &&
    b.line === (lineKey==='line1'?'Line 1':'Line 2')
  );
  if (!batch) return;

  const stations = batch.stations.map(s => s.station);
  const availabilityVals = batch.stations.map(s => s.availability);
  const performanceVals = batch.stations.map(s => s.performance);
  const qualityVals     = batch.stations.map(s => s.quality);
  const oeeVals   = batch.stations.map(s => s.oee);
  const goodVals  = batch.stations.map(s => s.goodCount);
  const scrapVals = batch.stations.map(s => s.scrapCount);

  const is1 = lineKey==='line1';
  const ctxO = document.getElementById(is1?'chart-line1-oee':'chart-line2-oee').getContext('2d');
  const ctxG = document.getElementById(is1?'chart-line1-gs' :'chart-line2-gs').getContext('2d');

  if(is1 && c1_oee) c1_oee.destroy();
  if(!is1&& c2_oee) c2_oee.destroy();
  if(is1 && c1_gs ) c1_gs .destroy();
  if(!is1&& c2_gs ) c2_gs .destroy();

  const cfgMetrics = {
  type: 'bar',
  data: {
    labels: stations,
    datasets: [
      {
        label: 'Availability',
        data: availabilityVals,
        backgroundColor: '#ff9800',
        hoverBackgroundColor: '#ffc107'
      },
      {
        label: 'Performance',
        data: performanceVals,
        backgroundColor: '#2196f3',
        hoverBackgroundColor: '#64b5f6'
      },
      {
        label: 'Quality',
        data: qualityVals,
        backgroundColor: '#9c27b0',
        hoverBackgroundColor: '#ba68c8'
      },
      {
        label: `OEE ${orderId}`,
        data: oeeVals,
        backgroundColor: '#4caf50',
        hoverBackgroundColor: '#81c784'
      }
    ]
  },
  options: {
    responsive: true,
    scales: {
      y: {
        beginAtZero: true,
        max: 100,
        title: { display: true, text: '%' }
      }
    },
    plugins: {
      tooltip: {
        enabled: true,
        mode: 'index',
        intersect: false
      },
      legend: {
        position: 'bottom'
      }
    },
    hover: {
      mode: 'nearest',
      intersect: true
    },
    // khi hover lên bar thì chuyển chuột thành pointer
    onHover: (event, chartElements) => {
      event.native.target.style.cursor = chartElements.length ? 'pointer' : 'default';
    }
  }
};
  const cfgG = {
    type:'bar',
    data:{ labels: stations, datasets:[
      { label:'Good',  data:goodVals,  backgroundColor:'#2196f3' },
      { label:'Scrap', data:scrapVals, backgroundColor:'#f44336' }
    ]},
    options:{ scales:{ y:{ beginAtZero:true, title:{display:true,text:'Số lượng'} } } }
  };

  if (is1) {
    c1_oee = new Chart(ctxO, cfgMetrics);
    c1_gs  = new Chart(ctxG, cfgG);
  } else {
    c2_oee = new Chart(ctxO, cfgMetrics);
    c2_gs  = new Chart(ctxG, cfgG);
  }
}

// --- Error log ---
// 1. Lấy toàn bộ logs từ API
async function fetchErrorLogs() {
  const res = await fetch(`${API}/api/ErrorLog/all`);
  if (!res.ok) {
    console.error('API error', res.status, await res.text());
    return [];
  }
  return res.json();
}

// 2. Tạo config cho Chart.js dựa trên line (1 hoặc 2) và type
function buildConfig(logs, line, type) {
  // --- Filter theo line ---
  const filtered = logs.filter(l => l.station.endsWith(line));

  // --- Tính toán dữ liệu ---
  const countBySt = {}, sumDurBySt = {}, trend = {}, bins = {'<1s':0,'1–5s':0,'5–20s':0,'>20s':0};
  filtered.forEach(l => {
    countBySt[l.station] = (countBySt[l.station]||0) + 1;
    sumDurBySt[l.station] = (sumDurBySt[l.station]||0) + l.durationSec;
    const day = new Date(l.errorStart).toLocaleDateString();
    trend[day] = (trend[day]||0) + 1;
    const d = l.durationSec;
    if (d<1) bins['<1s']++;
    else if (d<5) bins['1–5s']++;
    else if (d<20) bins['5–20s']++;
    else bins['>20s']++;
  });

  const stationsRaw = Object.keys(countBySt);
  const stations = stationsRaw.map(s=>s.replace(/\d+$/, ''));
  const counts   = stationsRaw.map(s=>countBySt[s]);
  const avgDur   = stationsRaw.map(s=>(sumDurBySt[s]/countBySt[s]).toFixed(2));

  const dates = Object.keys(trend).sort((a,b)=>new Date(a)-new Date(b));
  const dailyCount = dates.map(d=>trend[d]);

  const binLabels = Object.keys(bins);
  const binCounts = binLabels.map(b=>bins[b]);

  const commonOpts = {
    responsive: true,
    maintainAspectRatio: false,
    plugins: {
      tooltip: { mode:'index', intersect:false },
      legend:  { position:'bottom' }
    },
    hover: {
      onHover: (e, elems) => {
        e.native.target.style.cursor = elems.length ? 'pointer' : 'default';
      }
    }
  };

  const configs = {
    count: {
      type:'bar',
      data:{ labels:stations, datasets:[{ label:'Error Count', data:counts, backgroundColor:'#f44336', hoverBackgroundColor:'#e57373' }] },
      options:{ ...commonOpts, scales:{ y:{ beginAtZero:true, title:{ display:true,text:'Count' } } } }
    },
    avgdur: {
      type:'bar',
      data:{ labels:stations, datasets:[{ label:'Avg Duration (s)', data:avgDur, backgroundColor:'#3f51b5', hoverBackgroundColor:'#7986cb' }] },
      options:{ ...commonOpts, scales:{ y:{ beginAtZero:true, title:{ display:true,text:'Seconds' } } } }
    },
    trend: {
      type:'line',
      data:{
        labels:dates,
        datasets:[{
          label:'Errors per Day',
          data:dailyCount,
          fill:false,
          tension:0.4,
          borderColor:'#ff9800',
          pointBackgroundColor:'#ffb74d'
        }]
      },
      options:{
        ...commonOpts,
        scales:{
          x:{ title:{ display:true,text:'Date' } },
          y:{ beginAtZero:true,title:{display:true,text:'Count'} }
        }
      }
    },
    hist: {
      type:'bar',
      data:{ labels:binLabels, datasets:[{ label:'Duration Dist.', data:binCounts, backgroundColor:'#009688', hoverBackgroundColor:'#4db6ac' }] },
      options:{ ...commonOpts, scales:{ y:{ beginAtZero:true, title:{display:true,text:'Count'} } } }
    }
  };

  return configs[type];
}

// 3. Khởi tạo 1 section (line 1 hoặc line 2)
function initSection(logs, sectionId, line) {
  const section   = document.getElementById(sectionId);
  const tabs      = section.querySelectorAll('.tab');
  const canvas    = section.querySelector('.chart-error-canvas');
  const ctx       = canvas.getContext('2d');
  let currentChart, selectedType = tabs[0].dataset.chart;

  function update() {
    const cfg = buildConfig(logs, line, selectedType);
    const existing = Chart.getChart(canvas);
    if (existing) existing.destroy();
    currentChart = new Chart(ctx, cfg);
  }

  // gắn sự kiện cho mỗi tab trong section
  tabs.forEach(tab => {
    tab.addEventListener('click', () => {
      section.querySelector('.tab.active').classList.remove('active');
      tab.classList.add('active');
      selectedType = tab.dataset.chart;
      update();
    });
  });

  // vẽ lần đầu
  update();
}

// 4. Chạy khi DOM sẵn sàng
document.addEventListener('DOMContentLoaded', async () => {
  const logs = await fetchErrorLogs();
  if (!logs.length) return;
  initSection(logs, 'line1-section', '1');
  initSection(logs, 'line2-section', '2');
});




async function fetchOrderLogs() {
  return await getJSON(`${API}/api/OrderLog?skip=0&limit=100`);
}

async function fetchPendingOrders() {
  return await getJSON(`${API}/api/Order/all`);
}

function buildOrderLogsConfig(logs, type) {
  // chuẩn bị data
  const byDay   = {};
  const byLine  = { 'Line 1':0, 'Line 2':0 };
  const planAct = {}; // {orderID: {plan, actual}}
  const byMat   = {};
  logs.forEach(o => {
    // throughput per day
    const d = new Date(o.timeStamp).toLocaleDateString();
    byDay[d] = (byDay[d]||0) + 1;
    // orders per line
    byLine[o.line] = (byLine[o.line]||0) + 1;
    // plan vs actual
    planAct[o.orderID] = planAct[o.orderID] || { plan:0, actual:0 };
    planAct[o.orderID].plan   += o.plannedQTY;
    planAct[o.orderID].actual += (o.actualQTY||0);
    // by final material
    byMat[o.finalMaterial] = (byMat[o.finalMaterial]||0) + 1;
  });

  // xây mảng labels/data
  const dates     = Object.keys(byDay).sort((a,b)=>new Date(a)-new Date(b));
  const dailyCnt  = dates.map(d=>byDay[d]);
  const lines     = Object.keys(byLine);
  const lineCnt   = lines.map(l=>byLine[l]);
  const orders    = Object.keys(planAct);
  const planData  = orders.map(id=>planAct[id].plan);
  const actData   = orders.map(id=>planAct[id].actual);
  const mats      = Object.keys(byMat);
  const matCnt    = mats.map(m=>byMat[m]);

  const common = {
    responsive: true,
    plugins: { tooltip:{mode:'index',intersect:false}, legend:{position:'bottom'} },
    hover: { onHover:(e,el)=> e.native.target.style.cursor = el.length?'pointer':'default' }
  };

  const configs = {
    throughput: {
      type:'line',
      data:{ labels:dates, datasets:[{ label:'Orders/Day', data:dailyCnt, fill:false, borderColor:'#3e95cd' }] },
      options:{ ...common, scales:{ x:{title:{display:true,text:'Date'}}, y:{beginAtZero:true,title:{display:true,text:'Count'}} } }
    },
    byline: {
      type:'bar',
      data:{ labels:lines, datasets:[{ label:'Orders', data:lineCnt, backgroundColor:['#8e5ea2','#3cba9f'] }] },
      options:{ ...common, scales:{ y:{beginAtZero:true,title:{display:true,text:'Count'}} } }
    },
    planactual: {
      type:'bar',
      data:{ labels:orders, datasets:[
        { label:'Planned', data:planData, stack:'a'},
        { label:'Actual',  data:actData,  stack:'a'}
      ]},
      options:{ ...common, scales:{ x:{stacked:true}, y:{stacked:true,beginAtZero:true,title:{display:true,text:'QTY'}} } }
    },
    material: {
      type:'pie',
      data:{ labels:mats, datasets:[{ data:matCnt }] },
      options:{ ...common }
    }
  };

  return configs[type];
}

// 
function buildPendingConfig(orders, type) {
  // chuẩn bị các nhóm dữ liệu
  const byMat = {}, byUoM = {}, qtyRanges = {'<100':0,'100-500':0,'>500':0};
  const sumByMat = {};
  let totalQty = 0;

  orders.forEach(o => {
    // count theo material
    byMat[o.finalMaterial] = (byMat[o.finalMaterial]||0) + 1;
    // count theo UoM
    byUoM[o.uoM] = (byUoM[o.uoM]||0) + 1;
    // tổng PlannedQTY
    const m = o.finalMaterial;
    sumByMat[m] = (sumByMat[m]||0) + o.plannedQTY;
    totalQty += o.plannedQTY;
    // phân khoảng PlannedQTY
    const q = o.plannedQTY;
    if (q < 100)             qtyRanges['<100']++;
    else if (q <= 500)       qtyRanges['100-500']++;
    else                     qtyRanges['>500']++;
  });

  // tạo labels & data array
  const mats = Object.keys(byMat),      matCnt  = mats.map(m => byMat[m]);
  const uoms = Object.keys(byUoM),      uomCnt  = uoms.map(u => byUoM[u]);
  const ranges = Object.keys(qtyRanges), rngCnt = ranges.map(r => qtyRanges[r]);
  const sumQtyMat = mats.map(m => sumByMat[m]);
  const commonOpts = {
    responsive: true,
    plugins: {
      tooltip:{ mode:'index', intersect:false },
      legend:{ position:'bottom' }
    },
    hover: {
      onHover: (e, elems) => {
        e.native.target.style.cursor = elems.length ? 'pointer' : 'default';
      }
    }
  };

  const configs = {
    bymaterial: {
      type: 'bar',
      data: { labels: mats, datasets:[{ label:'Orders', data: matCnt }] },
      options: { ...commonOpts, scales:{ y:{ beginAtZero:true, title:{display:true,text:'Count'} } } }
    },
    byuom: {
      type: 'bar',
      data: { labels: uoms, datasets:[{ label:'Orders', data: uomCnt }] },
      options: { ...commonOpts, scales:{ y:{ beginAtZero:true, title:{display:true,text:'Count'} } } }
    },
    qtyrange: {
      type: 'bar',
      data: { labels: ranges, datasets:[{ label:'Orders', data: rngCnt }] },
      options: { ...commonOpts, scales:{ y:{ beginAtZero:true, title:{display:true,text:'Count'} } } }
    },
    materialqty: {
      type: 'bar',
      data: {
        labels: mats,
        datasets: [{ label:'Planned QTY', data: sumQtyMat }]
      },
      options: {
        ...commonOpts,
        scales:{ y:{ beginAtZero:true, title:{display:true,text:'QTY'} } }
      }
    },
    totalqty: {
      type: 'doughnut',
      data: { labels:['Planned QTY'], datasets:[{ data:[totalQty] }] },
      options: {
        ...commonOpts,
        circumference: 180,
        rotation: -90,
        plugins: {
          ...commonOpts.plugins,
          legend:{ position:'bottom' },
          tooltip:{ callbacks:{
            label: ctx => `Total: ${totalQty}`
          }}
        }
      }
    }
  };

  return configs[type];
}


function initTabbedSection(fetcher, buildFn, sectionId, canvasClass) {
  const section = document.getElementById(sectionId);
  const tabs    = section.querySelectorAll('.tab');
  const canvas  = section.querySelector(canvasClass);
  const ctx     = canvas.getContext('2d');
  let chart, selType = tabs[0].dataset.chart;

  fetcher().then(data => {
    function update() {
      const cfg = buildFn(data, selType);
      if (chart) chart.destroy();
      chart = new Chart(ctx, cfg);
    }
    tabs.forEach(tab => tab.addEventListener('click', () => {
      section.querySelector('.tab.active').classList.remove('active');
      tab.classList.add('active');
      selType = tab.dataset.chart;
      update();
    }));
    update();
  });
}
document.addEventListener('DOMContentLoaded', async () => {
  // 1. Lấy về toàn bộ ErrorLog
  const logs = await fetchErrorLogs();
  if (!logs.length) return;    // nếu không có logs thì thôi

  // 2. Khởi tạo ErrorLog chart cho Line 1 & Line 2
  initSection(logs, 'line1-section', '1');
  initSection(logs, 'line2-section', '2');

  // 3. Khởi tạo chart cho OrderLogs
  initTabbedSection(fetchOrderLogs,  buildOrderLogsConfig, 'orderlogs-section', '.chart-orderlogs-canvas');

  // 4. Khởi tạo chart cho Pending Orders
  initTabbedSection(fetchPendingOrders, buildPendingConfig,    'pending-section',   '.chart-pending-canvas');
});

