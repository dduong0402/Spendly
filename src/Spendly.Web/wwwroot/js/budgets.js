// Gợi ý hạn mức theo danh mục: khi chọn danh mục + kỳ, hiện gợi ý từ lịch sử (dữ liệu nằm trong data-* của <option>).
(function () {
  const category = document.getElementById('cb-category');
  const period = document.getElementById('cb-period');
  const panel = document.getElementById('cb-suggestion');
  if (!category || !period || !panel) return;

  const fmt = (n) => Number(n).toLocaleString('vi-VN');

  function el(tag, className, text) {
    const node = document.createElement(tag);
    if (className) node.className = className;
    if (text !== undefined) node.textContent = text;
    return node;
  }

  function fillButton(label, value) {
    const b = el('button', 'btn btn-secondary btn-pill px-3 py-1.5', label + ': ' + fmt(value) + ' ₫');
    b.type = 'button';
    b.dataset.fillTarget = '#cb-amount';
    b.dataset.fillValue = String(value);
    return b;
  }

  function render() {
    panel.replaceChildren();
    const option = category.options[category.selectedIndex];
    if (!option || !option.value) {
      panel.classList.add('hidden');
      return;
    }

    const key = period.value === 'Week' ? 'w' : 'm';
    const word = period.value === 'Week' ? 'tuần' : 'tháng';
    const d = option.dataset;
    const suggested = Number(d[key + 'Suggested'] || 0);

    panel.classList.remove('hidden');
    const box = el('div', 'rounded-xl bg-primary-soft px-4 py-3 text-sm');
    box.appendChild(el('p', 'font-semibold text-ink', 'Gợi ý theo lịch sử'));

    if (!suggested) {
      box.appendChild(el('p', 'mt-1', 'Chưa đủ dữ liệu ' + word + ' đã qua của danh mục này để gợi ý.'));
      panel.appendChild(box);
      return;
    }

    const count = d[key + 'Count'];
    box.appendChild(el('p', 'mt-1',
      'Trung bình ' + count + ' ' + word + ' gần nhất: ' + fmt(d[key + 'Average']) + ' ₫' +
      (d[key + 'Samples'] ? ' (' + d[key + 'Samples'].split('|').join(', ') + ')' : '')));

    const actions = el('div', 'mt-3 flex flex-wrap gap-2');
    actions.appendChild(fillButton('Sát thực tế', suggested));
    const saving = Number(d[key + 'Saving'] || 0);
    if (saving) actions.appendChild(fillButton('Tiết kiệm ~10%', saving));
    box.appendChild(actions);
    panel.appendChild(box);
  }

  category.addEventListener('change', render);
  period.addEventListener('change', render);
  render();
})();
