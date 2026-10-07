// Form danh mục: chọn ĐÚNG 1 emoji bất kỳ (bảng chọn có tìm kiếm, hoặc dán/gõ trực tiếp).
// Server vẫn kiểm tra lại (CategoryIcons.IsValid); đoạn này chỉ để trải nghiệm tốt hơn.
import '/lib/emoji-picker-element/index.js';
import vi from '/lib/emoji-picker-element/i18n/vi.js';

const input = document.getElementById('Icon');
const colorInput = document.getElementById('Color');
const preview = document.getElementById('icon-preview');
const toggle = document.getElementById('icon-picker-toggle');
const panel = document.getElementById('icon-picker-panel');
const picker = document.getElementById('icon-picker');
const clientError = document.getElementById('icon-client-error');

if (input && preview && toggle && panel && picker) {
  picker.i18n = vi;
  picker.dataSource = '/lib/emoji-picker-element/data.json';

  const segmenter = 'Segmenter' in Intl ? new Intl.Segmenter(undefined, { granularity: 'grapheme' }) : null;
  const emojiPattern = /\p{Extended_Pictographic}|\p{Regional_Indicator}/u;

  // Chỉ giữ ký tự hiển thị đầu tiên (một emoji ghép như 👍🏽 hay 🇻🇳 vẫn tính là 1).
  const firstGrapheme = (value) => {
    const text = value.trim();
    if (!text) return '';
    if (segmenter) {
      for (const part of segmenter.segment(text)) return part.segment;
    }
    return Array.from(text)[0];
  };

  const refresh = () => {
    preview.textContent = input.value || '📌';
    if (colorInput) preview.style.backgroundColor = `${colorInput.value}26`;
    const invalid = input.value !== '' && !emojiPattern.test(input.value);
    clientError.classList.toggle('hidden', !invalid);
    input.classList.toggle('input-validation-error', invalid);
  };

  const setOpen = (open) => {
    panel.classList.toggle('hidden', !open);
    toggle.setAttribute('aria-expanded', String(open));
  };

  input.addEventListener('input', () => {
    const single = firstGrapheme(input.value);
    if (single !== input.value) input.value = single;
    refresh();
  });

  if (colorInput) colorInput.addEventListener('input', refresh);

  toggle.addEventListener('click', () => setOpen(panel.classList.contains('hidden')));

  picker.addEventListener('emoji-click', (event) => {
    input.value = event.detail.unicode ?? '';
    refresh();
    setOpen(false);
    toggle.focus();
  });

  document.addEventListener('click', (event) => {
    if (!panel.classList.contains('hidden') && !event.target.closest('#icon-picker-panel') && event.target !== toggle) {
      setOpen(false);
    }
  });

  document.addEventListener('keydown', (event) => {
    if (event.key === 'Escape' && !panel.classList.contains('hidden')) {
      setOpen(false);
      toggle.focus();
    }
  });

  refresh();
}
