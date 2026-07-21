import { useTheme, type Theme } from '../hooks/useTheme';

const THEME_ORDER: Theme[] = ['system', 'light', 'dark'];

const THEME_LABEL: Record<Theme, string> = {
  system: 'System',
  light: 'Light',
  dark: 'Dark',
};

function ThemeToggle() {
  const { theme, setTheme } = useTheme();

  function handleClick() {
    const nextTheme = THEME_ORDER[(THEME_ORDER.indexOf(theme) + 1) % THEME_ORDER.length];
    setTheme(nextTheme);
  }

  return (
    <button
      type="button"
      onClick={handleClick}
      className="self-end rounded-md border border-[var(--border)] px-3 py-1 text-xs"
      aria-label={`Theme: ${THEME_LABEL[theme]}. Click to change.`}
    >
      {THEME_LABEL[theme]}
    </button>
  );
}

export default ThemeToggle;
