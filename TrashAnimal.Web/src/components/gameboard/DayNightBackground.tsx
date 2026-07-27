import { useEffect, useState } from 'react';
import { useTheme } from '../../hooks/useTheme';
import { DAY_BACKGROUND_IMAGE, NIGHT_BACKGROUND_IMAGE } from '../../pages/GameBoard/assetMaps';

/** Resolves 'system' against the OS media query so day/night can be a plain boolean. */
function useIsDarkResolved(): boolean {
  const { theme } = useTheme();
  const [systemPrefersDark, setSystemPrefersDark] = useState(
    () => typeof window !== 'undefined' && window.matchMedia('(prefers-color-scheme: dark)').matches,
  );

  useEffect(() => {
    const query = window.matchMedia('(prefers-color-scheme: dark)');
    const listener = (event: MediaQueryListEvent) => setSystemPrefersDark(event.matches);
    query.addEventListener('change', listener);
    return () => query.removeEventListener('change', listener);
  }, []);

  return theme === 'dark' || (theme === 'system' && systemPrefersDark);
}

/**
 * Full-bleed cross-fading day/night background. Reuses the app-wide light/dark theme concept
 * (useTheme) rather than a separate day/night toggle — light maps to day, dark maps to night.
 */
function DayNightBackground() {
  const isNight = useIsDarkResolved();

  return (
    <div className="fixed inset-0 -z-10 overflow-hidden" aria-hidden="true">
      <img
        src={DAY_BACKGROUND_IMAGE}
        alt=""
        className="absolute inset-0 h-full w-full object-cover transition-opacity duration-[600ms]"
        style={{ opacity: isNight ? 0 : 1 }}
      />
      <img
        src={NIGHT_BACKGROUND_IMAGE}
        alt=""
        className="absolute inset-0 h-full w-full object-cover transition-opacity duration-[600ms]"
        style={{ opacity: isNight ? 1 : 0 }}
      />
      <div
        className="absolute inset-0 transition-colors duration-[600ms]"
        style={{ background: isNight ? 'rgba(5,10,30,.4)' : 'rgba(10,20,40,.08)' }}
      />
    </div>
  );
}

export default DayNightBackground;
