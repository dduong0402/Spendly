// Dashboard: lấy dữ liệu từ /api/stats/* rồi vẽ thẻ tổng, biểu đồ và danh sách.
// Lưu ý bảo mật: dữ liệu do người dùng nhập (ghi chú, tên danh mục) luôn gán bằng textContent, không dùng innerHTML.
(() => {
  const root = document.getElementById('dashboard');
  if (!root) return;

  const $ = (id) => document.getElementById(id);

  const TODAY = root.dataset.today; // yyyy-MM-dd theo giờ Việt Nam (do server cung cấp)
  const MIN_DATE = root.dataset.minDate;

  const state = {
    period: 'week',
    date: null, // null = hôm nay
    requestId: 0,
    hasLoaded: false,
    summary: null,
    trendChart: null,
    categoryChart: null
  };

  // ---------- Định dạng ----------
  const moneyFormat = new Intl.NumberFormat('vi-VN');
  const formatMoney = (n) => `${moneyFormat.format(n)} ₫`;
  const trimDecimal = (x) => (Math.round(x * 10) / 10).toString().replace('.', ',');
  const formatCompact = (n) => {
    if (n >= 1e9) return `${trimDecimal(n / 1e9)} tỷ`;
    if (n >= 1e6) return `${trimDecimal(n / 1e6)} tr`;
    if (n >= 1e3) return `${Math.round(n / 1e3)}k`;
    return String(n);
  };
  const formatDate = (iso) => iso.split('-').reverse().join('/');
  const pad = (n) => String(n).padStart(2, '0');
  const cssVar = (name) => getComputedStyle(document.documentElement).getPropertyValue(name).trim();
  const clampDate = (iso) => (iso > TODAY ? TODAY : iso < MIN_DATE ? MIN_DATE : iso); // so sánh chuỗi ISO là đúng thứ tự thời gian

  const el = (tag, className, text) => {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  };

  // ---------- Gọi API ----------
  async function getJson(path, extra) {
    const params = new URLSearchParams({ period: state.period, ...(state.date ? { date: state.date } : {}), ...extra });
    const response = await fetch(`/api/stats/${path}?${params}`, {
      headers: { Accept: 'application/json' },
      credentials: 'same-origin'
    });
    if (response.status === 401) {
      window.location.href = `/Account/Login?returnUrl=${encodeURIComponent('/Dashboard')}`;
      throw new Error('unauthorized');
    }
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    return response.json();
  }

  async function load() {
    const requestId = ++state.requestId;
    setLoading(true);
    $('dashboard-error').classList.add('hidden');
    if (!state.hasLoaded) $('dashboard-loading').classList.remove('hidden');

    try {
      const [summary, trend, categories, top, recent] = await Promise.all([
        getJson('summary'),
        getJson('trend'),
        getJson('by-category'),
        getJson('top-expenses', { take: 5 }),
        getJson('recent-expenses', { take: 5 })
      ]);
      if (requestId !== state.requestId) return; // đã có yêu cầu mới hơn
      state.summary = summary;
      state.hasLoaded = true;
      $('dashboard-body').classList.remove('hidden');
      render(summary, trend, categories, top, recent);
    } catch (error) {
      if (requestId === state.requestId && error.message !== 'unauthorized') {
        $('dashboard-error').classList.remove('hidden');
      }
    } finally {
      if (requestId === state.requestId) {
        $('dashboard-loading').classList.add('hidden');
        setLoading(false);
      }
    }
  }

  function setLoading(isLoading) {
    root.setAttribute('aria-busy', String(isLoading));
    $('dashboard-body').classList.toggle('opacity-60', isLoading);
  }

  // ---------- Vẽ giao diện ----------
  function render(summary, trend, categories, top, recent) {
    renderPeriodBar(summary);
    renderCards(summary, top);

    const hasData = summary.transactionCount > 0;
    $('dashboard-empty').classList.toggle('hidden', hasData);
    $('dashboard-content').classList.toggle('hidden', !hasData);
    if (!hasData) {
      destroyCharts();
      return;
    }

    renderTrend(trend);
    renderCategories(categories);
    renderExpenseList($('recent-list'), recent);
    renderExpenseList($('top-list'), top);
  }

  function renderPeriodBar(summary) {
    $('range-label').textContent = summary.rangeLabel;
    $('period-prev').disabled = false;
    $('period-next').disabled = !summary.hasNext;
    $('period-current').classList.toggle('hidden', !summary.isCurrent);
    $('period-today').classList.toggle('hidden', summary.isCurrent);
    document.querySelectorAll('#period-tabs [role="tab"]').forEach((tab) => {
      tab.setAttribute('aria-selected', String(tab.dataset.period === state.period));
    });
  }

  function renderCards(summary, top) {
    $('stat-total').textContent = formatMoney(summary.totalAmount);

    const change = $('stat-change');
    change.className = 'badge mt-2';
    if (summary.changePercent === null || summary.changePercent === undefined) {
      change.classList.add('hidden');
    } else {
      const value = summary.changePercent;
      const arrow = value > 0 ? '↑' : value < 0 ? '↓' : '';
      // Chi tiêu tăng là tín hiệu cần chú ý (đỏ), giảm là tốt (xanh).
      if (value > 0) change.classList.add('badge-danger');
      if (value < 0) change.classList.add('badge-success');
      change.textContent = `${arrow} ${trimDecimal(Math.abs(value))}% ${summary.comparisonLabel}`.trim();
    }

    const label = $('stat2-label');
    const value = $('stat2-value');
    const hint = $('stat2-hint');
    if (summary.period === 'day') {
      label.textContent = 'Khoản chi lớn nhất';
      value.textContent = top.length ? formatMoney(top[0].amount) : '—';
      hint.textContent = top.length ? top[0].categoryName : 'Chưa có';
    } else {
      label.textContent = 'Trung bình mỗi ngày';
      value.textContent = formatMoney(summary.averagePerDay);
      hint.textContent = summary.isCurrent ? 'Tính đến hôm nay' : 'Cả kỳ';
    }

    $('stat-count').textContent = String(summary.transactionCount);
    $('stat-count-hint').textContent = summary.period === 'day' ? 'Trong ngày' : summary.period === 'week' ? 'Trong tuần' : 'Trong tháng';
  }

  function destroyCharts() {
    if (state.trendChart) { state.trendChart.destroy(); state.trendChart = null; }
    if (state.categoryChart) { state.categoryChart.destroy(); state.categoryChart = null; }
  }

  function renderTrend(points) {
    const primary = cssVar('--primary');
    const muted = cssVar('--text-muted');
    const border = cssVar('--border');

    $('trend-title').textContent =
      state.period === 'day' ? '7 ngày gần nhất' :
      state.period === 'week' ? 'Chi tiêu theo ngày trong tuần' : 'Chi tiêu theo ngày trong tháng';

    if (state.trendChart) state.trendChart.destroy();

    state.trendChart = new Chart($('trend-chart'), {
      type: 'line',
      data: {
        labels: points.map((p) => p.label),
        datasets: [{
          label: 'Chi tiêu',
          // Ngày chưa tới để trống (null) thay vì 0 để đường biểu đồ dừng lại ở hôm nay.
          data: points.map((p) => (p.isFuture ? null : p.amount)),
          borderColor: primary,
          borderWidth: 3,
          tension: 0.35,
          pointRadius: points.length > 14 ? 0 : 4,
          pointHoverRadius: 6,
          pointBackgroundColor: '#ffffff',
          pointBorderColor: primary,
          pointBorderWidth: 3,
          fill: true,
          backgroundColor: (context) => {
            const { ctx, chartArea } = context.chart;
            if (!chartArea) return null;
            const gradient = ctx.createLinearGradient(0, chartArea.top, 0, chartArea.bottom);
            gradient.addColorStop(0, `${primary}40`);
            gradient.addColorStop(1, `${primary}00`);
            return gradient;
          }
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        interaction: { mode: 'index', intersect: false },
        plugins: {
          legend: { display: false },
          tooltip: {
            callbacks: {
              title: (items) => formatDate(points[items[0].dataIndex].date),
              label: (item) => formatMoney(item.parsed.y)
            }
          }
        },
        scales: {
          x: { grid: { display: false }, ticks: { color: muted, maxTicksLimit: 10 } },
          y: {
            beginAtZero: true,
            grid: { color: border },
            border: { display: false },
            ticks: { color: muted, maxTicksLimit: 5, callback: (v) => formatCompact(v) }
          }
        }
      }
    });
  }

  function renderCategories(items) {
    if (state.categoryChart) state.categoryChart.destroy();

    state.categoryChart = new Chart($('category-chart'), {
      type: 'doughnut',
      data: {
        labels: items.map((c) => c.name),
        datasets: [{
          data: items.map((c) => c.amount),
          backgroundColor: items.map((c) => c.color),
          borderColor: '#ffffff',
          borderWidth: 2
        }]
      },
      options: {
        responsive: true,
        maintainAspectRatio: false,
        cutout: '68%',
        plugins: {
          legend: { display: false },
          tooltip: { callbacks: { label: (item) => `${item.label}: ${formatMoney(item.parsed)}` } }
        }
      }
    });

    const legend = $('category-legend');
    legend.replaceChildren();
    items.forEach((c) => {
      const row = el('li', 'flex items-center justify-between gap-3 text-sm');
      const left = el('span', 'flex min-w-0 items-center gap-2 text-ink');
      const dot = el('span', 'h-2.5 w-2.5 shrink-0 rounded-full');
      dot.style.backgroundColor = c.color;
      left.append(dot, el('span', 'truncate', `${c.emoji} ${c.name}`));
      const right = el('span', 'stat-number shrink-0 text-sm', `${trimDecimal(c.percent)}%`);
      right.title = formatMoney(c.amount);
      row.append(left, right);
      legend.append(row);
    });
  }

  function renderExpenseList(list, items) {
    list.replaceChildren();
    if (!items.length) {
      list.append(el('li', 'py-4 text-sm', 'Không có dữ liệu.'));
      return;
    }
    items.forEach((item) => {
      const row = el('li', 'flex items-center justify-between gap-3 py-3');

      const left = el('div', 'flex min-w-0 items-center gap-3');
      const chip = el('span', 'flex h-9 w-9 shrink-0 items-center justify-center rounded-xl text-base', item.categoryEmoji);
      chip.style.backgroundColor = `${item.categoryColor}26`;
      chip.setAttribute('aria-hidden', 'true');
      const text = el('div', 'min-w-0');
      text.append(
        el('p', 'truncate text-sm font-medium text-ink', item.note || item.categoryName),
        el('p', 'text-xs', `${item.categoryName} · ${formatDate(item.spentAt)}`)
      );
      left.append(chip, text);

      row.append(left, el('span', 'stat-number shrink-0 text-sm', formatMoney(item.amount)));
      list.append(row);
    });
  }

  // ---------- Chọn nhanh kỳ ----------
  const picker = {
    panel: $('period-picker'),
    button: $('range-button'),
    monthPanel: $('picker-month'),
    datePanel: $('picker-date'),
    yearSelect: $('picker-year'),
    monthGrid: $('picker-months'),
    dateInput: $('picker-date-input'),
    dateLabel: $('picker-date-label'),
    dateHint: $('picker-date-hint')
  };

  const isPickerOpen = () => !picker.panel.classList.contains('hidden');

  function openPicker() {
    if (!state.summary) return; // chưa có dữ liệu thì chưa biết đang xem kỳ nào
    buildPicker();
    picker.panel.classList.remove('hidden');
    picker.button.setAttribute('aria-expanded', 'true');
    (state.period === 'month' ? picker.yearSelect : picker.dateInput).focus();
  }

  function closePicker(returnFocus) {
    if (!isPickerOpen()) return;
    picker.panel.classList.add('hidden');
    picker.button.setAttribute('aria-expanded', 'false');
    if (returnFocus) picker.button.focus();
  }

  function pick(iso) {
    state.date = clampDate(iso);
    closePicker(true);
    load();
  }

  function buildPicker() {
    const isMonth = state.period === 'month';
    picker.monthPanel.classList.toggle('hidden', !isMonth);
    picker.datePanel.classList.toggle('hidden', isMonth);

    if (isMonth) {
      buildMonthPicker();
    } else {
      picker.dateInput.min = MIN_DATE;
      picker.dateInput.max = TODAY;
      picker.dateInput.value = state.date ?? TODAY;
      picker.dateLabel.textContent = state.period === 'week' ? 'Chọn một ngày trong tuần' : 'Chọn ngày';
      picker.dateHint.textContent = state.period === 'week'
        ? 'Chọn ngày bất kỳ, hệ thống sẽ hiển thị cả tuần (Thứ Hai đến Chủ Nhật) chứa ngày đó.'
        : '';
    }
  }

  function buildMonthPicker() {
    const [todayYear, todayMonth] = TODAY.split('-').map(Number);
    const minYear = Number(MIN_DATE.slice(0, 4));
    const [shownYear, shownMonth] = state.summary.from.split('-').map(Number);

    picker.yearSelect.replaceChildren();
    for (let year = todayYear; year >= minYear; year--) {
      const option = el('option', '', String(year));
      option.value = String(year);
      picker.yearSelect.append(option);
    }
    picker.yearSelect.value = String(shownYear);

    const renderMonths = () => {
      const year = Number(picker.yearSelect.value);
      picker.monthGrid.replaceChildren();
      for (let month = 1; month <= 12; month++) {
        const isSelected = year === shownYear && month === shownMonth;
        const isFuture = year === todayYear && month > todayMonth;
        const button = el('button', `btn px-0 py-2 ${isSelected ? 'btn-primary' : 'btn-secondary'}`, `Th${month}`);
        button.type = 'button';
        button.disabled = isFuture;
        button.setAttribute('aria-label', `Tháng ${month}/${year}`);
        button.addEventListener('click', () => pick(`${year}-${pad(month)}-01`));
        picker.monthGrid.append(button);
      }
    };

    picker.yearSelect.onchange = renderMonths;
    renderMonths();
  }

  picker.button.addEventListener('click', () => (isPickerOpen() ? closePicker(false) : openPicker()));

  picker.dateInput.addEventListener('change', () => {
    if (picker.dateInput.value) pick(picker.dateInput.value);
  });

  document.addEventListener('click', (event) => {
    if (isPickerOpen() && !event.target.closest('#period-bar')) closePicker(false);
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape') closePicker(true);
  });

  // ---------- Sự kiện ----------
  document.querySelectorAll('#period-tabs [role="tab"]').forEach((tab) => {
    tab.addEventListener('click', () => {
      if (state.period === tab.dataset.period) return;
      state.period = tab.dataset.period;
      state.date = null; // đổi kỳ thì quay về hôm nay
      closePicker(false);
      load();
    });
  });

  $('period-prev').addEventListener('click', () => {
    if (!state.summary) return;
    state.date = state.summary.previousDate;
    closePicker(false);
    load();
  });

  $('period-next').addEventListener('click', () => {
    if (!state.summary || !state.summary.hasNext) return;
    state.date = state.summary.nextDate;
    closePicker(false);
    load();
  });

  $('period-today').addEventListener('click', () => {
    state.date = null;
    closePicker(false);
    load();
  });

  $('dashboard-retry').addEventListener('click', load);

  load();
})();
