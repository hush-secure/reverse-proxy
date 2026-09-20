import { useEffect, useRef, useState } from 'react';
import { Palette, Check } from 'lucide-react';
import { THEMES, type ThemeId } from '../hooks/useTheme';
import styles from './ThemeSwitcher.module.scss';

interface ThemeSwitcherProps {
  theme: ThemeId;
  onChange: (id: ThemeId) => void;
}

export default function ThemeSwitcher({ theme, onChange }: ThemeSwitcherProps) {
  const [open, setOpen] = useState(false);
  const ref = useRef<HTMLDivElement>(null);
  const current = THEMES.find((t) => t.id === theme) ?? THEMES[0];

  useEffect(() => {
    function handleClickOutside(e: MouseEvent) {
      if (ref.current && !ref.current.contains(e.target as Node)) setOpen(false);
    }
    document.addEventListener('mousedown', handleClickOutside);
    return () => document.removeEventListener('mousedown', handleClickOutside);
  }, []);

  return (
    <div className={styles.wrapper} ref={ref}>
      <button
        type="button"
        className={styles.trigger}
        onClick={() => setOpen((v) => !v)}
        aria-expanded={open}
      >
        <Palette size={16} />
        <span
          className={styles.swatch}
          style={{ background: current.swatch }}
          aria-hidden
        />
        <span className={styles.label}>{current.label}</span>
      </button>

      {open && (
        <div className={styles.menu} role="listbox">
          {THEMES.map((t) => (
            <button
              key={t.id}
              type="button"
              role="option"
              aria-selected={t.id === theme}
              className={styles.option}
              onClick={() => {
                onChange(t.id);
                setOpen(false);
              }}
            >
              <span
                className={styles.optionPreview}
                style={{ background: t.surface, borderColor: t.swatch }}
              >
                <span className={styles.optionDot} style={{ background: t.swatch }} />
              </span>
              <span className={styles.optionLabel}>{t.label}</span>
              {t.id === theme && <Check size={14} className={styles.optionCheck} />}
            </button>
          ))}
        </div>
      )}
    </div>
  );
}
