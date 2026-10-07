// Segmented control: chuyển tab bằng aria-selected (phần dữ liệu sẽ nối vào ở bước Dashboard).
document.querySelectorAll('[data-segmented]').forEach((group) => {
  group.addEventListener('click', (event) => {
    const tab = event.target.closest('[role="tab"]');
    if (!tab) return;
    group.querySelectorAll('[role="tab"]').forEach((t) => {
      t.setAttribute('aria-selected', String(t === tab));
    });
  });
});

// ---- Hộp thoại xác nhận (thay cho window.confirm của trình duyệt) ----
// Form có data-confirm="..." sẽ hiện hộp thoại này trước khi gửi.
// Tùy chọn: data-confirm-title, data-confirm-ok, data-confirm-note.
let confirmDialog = null;

function buildConfirmDialog() {
  const dialog = document.createElement('dialog');
  dialog.className = 'confirm-dialog';
  dialog.setAttribute('aria-labelledby', 'confirm-title');
  dialog.innerHTML =
    '<div class="confirm-body">' +
    '<div class="confirm-icon" aria-hidden="true">!</div>' +
    '<h2 id="confirm-title" class="confirm-title" data-confirm-title></h2>' +
    '<p class="confirm-message" data-confirm-message></p>' +
    '<p class="confirm-note" data-confirm-note></p>' +
    '</div>' +
    '<div class="confirm-actions">' +
    '<button type="button" class="btn btn-secondary btn-pill" data-confirm-cancel autofocus>Hủy</button>' +
    '<button type="button" class="btn btn-danger btn-pill" data-confirm-ok>Xóa</button>' +
    '</div>';
  document.body.appendChild(dialog);
  return dialog;
}

function askConfirm({ message, title, note, okText }) {
  if (typeof HTMLDialogElement === 'undefined' || typeof HTMLDialogElement.prototype.showModal !== 'function') {
    return Promise.resolve(window.confirm(message));
  }

  confirmDialog = confirmDialog || buildConfirmDialog();
  const dialog = confirmDialog;
  // Chỉ dùng textContent: nội dung có thể chứa tên do người dùng đặt.
  dialog.querySelector('[data-confirm-title]').textContent = title || 'Xác nhận xóa';
  dialog.querySelector('[data-confirm-message]').textContent = message;
  dialog.querySelector('[data-confirm-note]').textContent = note || 'Hành động này không thể hoàn tác.';
  dialog.querySelector('[data-confirm-ok]').textContent = okText || 'Xóa';

  return new Promise((resolve) => {
    let result = false;
    const ok = dialog.querySelector('[data-confirm-ok]');
    const cancel = dialog.querySelector('[data-confirm-cancel]');

    const onOk = () => { result = true; dialog.close(); };
    const onCancel = () => { result = false; dialog.close(); };
    const onBackdrop = (event) => { if (event.target === dialog) onCancel(); };
    const onClose = () => {
      ok.removeEventListener('click', onOk);
      cancel.removeEventListener('click', onCancel);
      dialog.removeEventListener('click', onBackdrop);
      dialog.removeEventListener('close', onClose);
      resolve(result);
    };

    ok.addEventListener('click', onOk);
    cancel.addEventListener('click', onCancel);
    dialog.addEventListener('click', onBackdrop);
    dialog.addEventListener('close', onClose);
    dialog.showModal();
    cancel.focus();
  });
}

document.addEventListener('submit', (event) => {
  const form = event.target.closest('form[data-confirm]');
  if (!form || form.dataset.confirmed === 'true') return;

  event.preventDefault();
  askConfirm({
    message: form.dataset.confirm,
    title: form.dataset.confirmTitle,
    note: form.dataset.confirmNote,
    okText: form.dataset.confirmOk
  }).then((accepted) => {
    if (!accepted) return;
    form.dataset.confirmed = 'true';
    HTMLFormElement.prototype.submit.call(form);
  });
});

// Nút gợi ý: data-fill-target="#id" data-fill-value="123" -> điền giá trị vào ô nhập.
document.addEventListener('click', (event) => {
  const button = event.target.closest('[data-fill-target]');
  if (!button) return;
  const input = document.querySelector(button.dataset.fillTarget);
  if (!input) return;
  input.value = button.dataset.fillValue ?? '';
  input.dispatchEvent(new Event('input', { bubbles: true }));
  input.focus();
});

// Ô nhập tiền (data-money): tự thêm dấu chấm ngăn cách hàng nghìn khi gõ, ví dụ 1250000 -> 1.250.000.
document.querySelectorAll('input[data-money]').forEach((input) => {
  const format = () => {
    const digits = input.value.replace(/\D/g, '').slice(0, 12);
    input.value = digits ? Number(digits).toLocaleString('vi-VN') : '';
  };
  input.addEventListener('input', format);
  format();
});

// Xóa thông báo flash sau vài giây (không áp dụng cho lỗi).
document.querySelectorAll('.alert-success').forEach((el) => {
  setTimeout(() => el.remove(), 5000);
});
