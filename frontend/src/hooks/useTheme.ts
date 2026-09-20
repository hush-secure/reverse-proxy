import { useCallback, useEffect, useState } from 'react';

export interface ThemeMeta {
  id: string;
  label: string;
  swatch: string; 
  surface: string;
}

export const THEMES = [
  { id: 'night', label: 'Night (Default)', swatch: '#F4C430', surface: '#0A0A0A' },
  { id: 'Amoled', label: 'Amoled', swatch: '#E01010', surface: '#000000' },
  { id: 'CyberNeon', label: 'Cyber Neon', swatch: '#00D4FF', surface: '#080C14' },
  { id: 'day', label: 'Day', swatch: '#D4AF37', surface: '#FFF9F0' },
  { id: 'Ember', label: 'Ember', swatch: '#F2622E', surface: '#100E0C' },
  { id: 'WhatsappDark', label: 'WhatsApp Dark', swatch: '#00A884', surface: '#0B141A' },
  { id: 'WhatsappLight', label: 'WhatsApp Light', swatch: '#008069', surface: '#F0F2F5' },
] as const satisfies readonly ThemeMeta[];

export type ThemeId = (typeof THEMES)[number]['id'];

const STORAGE_KEY = 'proxy-dashboard-theme';

function readStoredTheme(): ThemeId {
  if (typeof window === 'undefined') return 'night';
  const stored = window.localStorage.getItem(STORAGE_KEY) as ThemeId | null;
  return stored && THEMES.some((t) => t.id === stored) ? stored : 'night';
}

export function useTheme() {
  const [theme, setThemeState] = useState<ThemeId>(readStoredTheme);

  useEffect(() => {
    const root = document.documentElement;
    if (theme === 'night') {
      root.removeAttribute('data-theme');
    } else {
      root.setAttribute('data-theme', theme);
    }
    window.localStorage.setItem(STORAGE_KEY, theme);
  }, [theme]);

  const setTheme = useCallback((id: ThemeId) => setThemeState(id), []);

  return { theme, setTheme, themes: THEMES };
}