/** @type {import('tailwindcss').Config} */
module.exports = {
  content: ['./Views/**/*.cshtml', './wwwroot/js/**/*.js'],
  theme: {
    extend: {
      colors: {
        ink: 'var(--text)',
        muted: 'var(--text-muted)',
        line: 'var(--border)',
        soft: 'var(--bg-soft)',
        primary: {
          DEFAULT: 'var(--primary)',
          hover: 'var(--primary-hover)',
          soft: 'var(--primary-soft)'
        },
        sky: {
          1: 'var(--sky-1)',
          2: 'var(--sky-2)',
          3: 'var(--sky-3)'
        },
        success: { DEFAULT: 'var(--success)', soft: 'var(--success-soft)' },
        danger: { DEFAULT: 'var(--danger)', soft: 'var(--danger-soft)' },
        warning: { DEFAULT: 'var(--warning)', soft: 'var(--warning-soft)' }
      },
      fontFamily: {
        sans: ['Inter', 'ui-sans-serif', 'system-ui', 'sans-serif']
      },
      boxShadow: {
        card: '0 1px 2px rgba(11,27,51,.04), 0 8px 24px rgba(31,107,255,.08)'
      },
      maxWidth: {
        app: '1200px'
      }
    }
  },
  plugins: []
};
