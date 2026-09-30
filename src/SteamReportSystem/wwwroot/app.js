const byId = id => document.getElementById(id);
const integer = value => value === null || value === undefined ? '—' : Number(value).toLocaleString('zh-CN');
let report = null;
let catalog = [];

function utcDate(offsetDays) {
    const date = new Date(Date.now() + offsetDays * 86400000);
    return date.toISOString().slice(0, 10);
}

async function getJson(url, init) {
    const response = await fetch(url, init);
    let body;
    try { body = await response.json(); } catch { body = {}; }
    if (!response.ok) throw new Error(body.error || `请求失败：HTTP ${response.status}`);
    return body;
}

function cell(row, value, className) {
    const td = row.insertCell();
    td.textContent = value;
    if (className) td.className = className;
    return td;
}

function currentQuery() {
    const query = new URLSearchParams({start: byId('start').value, end: byId('end').value});
    if (byId('baseline').value) query.set('baselineId', byId('baseline').value);
    return query.toString();
}

async function loadBaselines() {
    const list = await getJson('/api/baselines');
    const selected = byId('baseline').value;
    const select = byId('baseline');
    select.replaceChildren(new Option('不使用基线', ''));
    for (const baseline of list) {
        const option = new Option(`${new Date(baseline.createdAt).toLocaleString('zh-CN')} · ${baseline.reason}` +
            (baseline.isAvailable ? '' : '（不可用）') +
            (baseline.inferredZeroStats?.length ? `（推定 0：${baseline.inferredZeroStats.length} 项）` : ''), baseline.id);
        option.disabled = !baseline.isAvailable;
        select.add(option);
    }
    select.value = list.some(item => item.id === selected && item.isAvailable) ? selected : '';
}

async function loadAudit() {
    const records = await getJson('/api/audit');
    const container = byId('audit-result');
    container.replaceChildren();
    if (records.length === 0) { container.textContent = '尚无基线操作。'; return; }
    const table = document.createElement('table');
    const head = table.createTHead().insertRow();
    ['时间', '操作员', '操作', '原因', '结果', '当前是否可用'].forEach(label => cell(head, label));
    const body = table.createTBody();
    for (const record of records) {
        const row = body.insertRow();
        cell(row, new Date(record.occurredAt).toLocaleString('zh-CN'));
        cell(row, record.operatorId);
        cell(row, record.action);
        cell(row, record.reason);
        cell(row, record.result);
        const status = cell(row, record.isAvailable ? '可用' : '不可用');
        if (!record.isAvailable) {
            status.title = record.unavailableReason || '没有可用的基线';
        }
    }
    container.append(table);
}

async function loadReport() {
    const button = byId('load-report');
    const error = byId('error');
    error.hidden = true;
    button.disabled = true;
    try {
        report = await getJson(`/api/report?${currentQuery()}`);
        byId('report').hidden = false;
        byId('appid').textContent = report.appId;
        byId('fetched').textContent = new Date(report.fetchedAt).toISOString().replace('T', ' ').slice(0, 19);
        byId('tutorial').textContent = integer(report.tutorialCompleted);
        byId('tutorial-period').textContent = integer(report.periodTotals.tutorial_completed);
        const warnings = byId('warnings');
        warnings.hidden = report.warnings.length === 0;
        warnings.replaceChildren(...report.warnings.map(message => {
            const paragraph = document.createElement('p'); paragraph.textContent = message; return paragraph;
        }));
        const body = byId('levels').tBodies[0];
        body.replaceChildren();
        for (const level of report.levels) {
            const id = String(level.levelId).padStart(2, '0');
            const row = body.insertRow();
            cell(row, id === '14' ? '14 · 教程关' : id);
            cell(row, integer(level.battleStarts));
            cell(row, integer(level.battleStartsAfterBaseline), level.hasAnomaly ? 'anomaly' : '');
            cell(row, integer(report.periodTotals[`level_${id}_battle_starts`]));
            cell(row, integer(level.reached));
            cell(row, integer(level.reachedAfterBaseline), level.hasAnomaly ? 'anomaly' : '');
            cell(row, integer(report.periodTotals[`level_${id}_reached`]));
        }
        byId('export').href = `/api/report.csv?${currentQuery()}`;
        renderTrend();
    } catch (failure) {
        byId('report').hidden = true;
        error.textContent = failure.message;
        error.hidden = false;
    } finally { button.disabled = false; }
}

function renderTrend() {
    if (!report) return;
    const name = byId('metric').value;
    const body = byId('daily-table').tBodies[0];
    body.replaceChildren();
    const values = [];
    for (const day of report.daily) {
        const value = Object.hasOwn(day.values, name) ? day.values[name] : null;
        values.push(value);
        const row = body.insertRow();
        cell(row, day.date);
        cell(row, integer(value));
    }
    drawChart(values);
}

function drawChart(values) {
    const container = byId('chart');
    container.replaceChildren();
    const available = values.filter(value => typeof value === 'number');
    if (available.length === 0) { container.textContent = '所选日期内没有返回每日值。'; return; }
    const svg = document.createElementNS('http://www.w3.org/2000/svg', 'svg');
    svg.setAttribute('viewBox', '0 0 900 260');
    svg.setAttribute('role', 'img');
    svg.setAttribute('aria-label', '按 UTC 日期排列的每日变化趋势');
    const max = Math.max(1, ...available);
    const min = Math.min(0, ...available);
    const height = 200;
    const x = index => 45 + index * 825 / Math.max(1, values.length - 1);
    const y = value => 220 - (value - min) * height / (max - min);
    for (let i = 0; i <= 4; i++) {
        const line = document.createElementNS(svg.namespaceURI, 'line');
        line.setAttribute('x1', 45); line.setAttribute('x2', 870);
        line.setAttribute('y1', 20 + i * 50); line.setAttribute('y2', 20 + i * 50);
        line.setAttribute('stroke', '#e4ebef'); svg.append(line);
    }
    let segment = [];
    const flush = () => {
        if (segment.length === 0) return;
        const polyline = document.createElementNS(svg.namespaceURI, 'polyline');
        polyline.setAttribute('points', segment.join(' '));
        polyline.setAttribute('fill', 'none');
        polyline.setAttribute('stroke', '#18739b');
        polyline.setAttribute('stroke-width', '3');
        svg.append(polyline); segment = [];
    };
    values.forEach((value, index) => {
        if (typeof value === 'number') segment.push(`${x(index)},${y(value)}`);
        else flush();
    });
    flush();
    for (const [label, location] of [[integer(max), 18], [integer(min), 222]]) {
        const text = document.createElementNS(svg.namespaceURI, 'text');
        text.setAttribute('x', 4); text.setAttribute('y', location);
        text.setAttribute('font-size', 12); text.setAttribute('fill', '#607789');
        text.textContent = label; svg.append(text);
    }
    container.append(svg);
    const caption = document.createElement('div');
    caption.className = 'note';
    caption.textContent = `${report.startDate} — ${report.endDate}（UTC）；区间已返回日期合计：${integer(report.periodTotals[byId('metric').value])}`;
    container.append(caption);
}

async function createBaseline() {
    const button = byId('create-baseline');
    const message = byId('baseline-message');
    button.disabled = true;
    message.textContent = '正在读取 41 项全局统计并核对空值…';
    try {
        const created = await getJson('/api/baselines', {
            method: 'POST',
            headers: {'Content-Type': 'application/json', 'X-Requested-With': 'SteamReportSystem'},
            body: JSON.stringify({reason: byId('reason').value})
        });
        message.textContent = `基线已建立：${created.id}` +
            (created.inferredZeroStats?.length ?
                `；其中 ${created.inferredZeroStats.length} 项全局值为空，基线按推定 0 保存。` : '');
        await loadBaselines();
        byId('baseline').value = created.id;
        await loadAudit();
        await loadReport();
    } catch (failure) { message.textContent = failure.message; }
    finally { button.disabled = false; }
}

async function checkCatalog() {
    const result = byId('catalog-result');
    result.textContent = '正在检查…';
    try {
        const check = await getJson('/api/catalog/check');
        result.textContent = `已找到 ${check.found.length}/41 项。` +
            (check.missing.length ? `缺失：${check.missing.join('、')}。` : '') +
            ` ${check.warnings.join(' ')}`;
    } catch (failure) { result.textContent = failure.message; }
}

async function probeGlobalStat() {
    const result = byId('probe-result');
    const button = byId('probe-global');
    result.textContent = '正在查询…';
    button.disabled = true;
    try {
        const apiName = byId('probe-stat').value;
        const url = `/api/diagnostics/global-stat/${encodeURIComponent(apiName)}`;
        const withoutDates = await getJson(url);
        const dates = new URLSearchParams({start: byId('start').value, end: byId('end').value});
        const withDates = await getJson(`${url}?${dates}`);
        const describe = probe => {
            const entry = !probe.hasGlobalStats ? '未返回 globalstats' :
                !probe.entryPresent ? '未返回该统计项' :
                probe.hasTotal ? `total = ${integer(probe.total)}` :
                `${probe.entryKind}，字段：${probe.entryFields.join('、') || '无'}`;
            return `result=${probe.resultCode ?? '未提供'}，${entry}`;
        };
        result.textContent = `AppID ${withoutDates.appId} · ${apiName}。` +
            `不带日期：${describe(withoutDates)}；带日期：${describe(withDates)}。`;
    } catch (failure) { result.textContent = failure.message; }
    finally { button.disabled = false; }
}

async function loadPlayer() {
    const container = byId('player-result');
    container.textContent = '正在查询…';
    try {
        const steamId = byId('steam-id').value.trim();
        const player = await getJson(`/api/player/${encodeURIComponent(steamId)}`);
        const table = document.createElement('table');
        const header = table.createTHead().insertRow();
        cell(header, 'API Name'); cell(header, '玩家统计值');
        const body = table.createTBody();
        for (const stat of player.stats) {
            const row = body.insertRow(); cell(row, stat.apiName); cell(row, integer(stat.value));
        }
        container.replaceChildren(table);
    } catch (failure) { container.textContent = failure.message; }
}

async function initialize() {
    byId('start').value = utcDate(-30);
    byId('end').value = utcDate(-1);
    byId('load-report').addEventListener('click', loadReport);
    byId('metric').addEventListener('change', renderTrend);
    byId('create-baseline').addEventListener('click', createBaseline);
    byId('check-catalog').addEventListener('click', checkCatalog);
    byId('probe-global').addEventListener('click', probeGlobalStat);
    byId('load-player').addEventListener('click', loadPlayer);
    try {
        const [status, definitions] = await Promise.all([getJson('/api/status'), getJson('/api/catalog')]);
        catalog = definitions;
        byId('connection').textContent = `AppID ${status.appId || '未配置'} · ` +
            (status.hasPublisherKey ? '已找到 Key 环境变量' : '尚未设置 Key 环境变量');
        byId('start').max = status.currentUtcDate;
        byId('end').max = status.currentUtcDate;
        byId('date-note').textContent = `当前 UTC 日期为 ${status.currentUtcDate}；` +
            '北京时间早上 8 点前，UTC 仍是前一天。当天数据可能尚未完整；空白不代表 0。';
        const metric = byId('metric');
        const probeStat = byId('probe-stat');
        for (const item of catalog) {
            metric.add(new Option(item.displayName, item.apiName));
            probeStat.add(new Option(item.apiName, item.apiName));
        }
        probeStat.value = 'level_14_battle_starts';
        await Promise.all([loadBaselines(), loadAudit()]);
        if (status.appId && status.hasPublisherKey) await loadReport();
    } catch (failure) { byId('connection').textContent = failure.message; }
}

initialize();
