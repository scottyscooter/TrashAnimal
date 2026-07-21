import { useEffect } from 'react';
import { useLocalStorage } from './useLocalStorage';

export type Theme = 'light' | 'dark' | 'system';

const STORAGE_KEY = 'trashanimal:theme';

/**
 * Persists the chosen theme and stamps it onto `<html data-theme>`. `index.css` defines
 * `:root[data-theme="dark"]`/`:root[data-theme="light"]` overrides alongside the existing
 * `prefers-color-scheme` media query, so 'system' (no attribute) keeps following the OS setting.
 */
export function useTheme() {
  const [theme, setTheme] = useLocalStorage<Theme>(STORAGE_KEY, 'system');

  useEffect(() => {
    const root = document.documentElement;
    if (theme === 'system') {
      root.removeAttribute('data-theme');
    } else {
      root.setAttribute('data-theme', theme);
    }
  }, [theme]);

  return { theme, setTheme };
}
