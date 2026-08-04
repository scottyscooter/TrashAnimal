import { useCallback, useSyncExternalStore } from 'react';

/**
 * Unlike `useLocalStorage`, this has no module-level pub-sub: `window.matchMedia` already gives
 * every caller of the same query a live-synced `MediaQueryList` with its own native `change`
 * event — there's nothing to coordinate across hook instances the way `useLocalStorage` has to for
 * same-tab localStorage writes (which the browser doesn't notify same-tab listeners about at all).
 * Each hook instance owns exactly one `MediaQueryList` + one listener, added and removed with it.
 *
 * Also re-checks on `resize`/`orientationchange`, not just the `MediaQueryList`'s own `change`
 * event. The `phone-landscape`/`tablet-landscape` Tailwind variants these hooks mirror are plain
 * CSS `@media` rules, applied by the browser's style engine synchronously with layout; this hook's
 * `change` event, by contrast, is dispatched by the browser and then handled by React on its own
 * schedule, which can commit a render after the CSS has already switched breakpoints. That gap lets
 * a component that mixes CSS-variant classes (container position/size) with this hook's value (e.g.
 * per-card size/spacing computed in JS) paint one frame where the two disagree. `resize`/
 * `orientationchange` fire independently of the `change` event and give React another chance to
 * re-run `getSnapshot` and catch up, without changing what value is ultimately returned.
 */
function subscribeToMediaQuery(query: string, listener: () => void): () => void {
  const mql = window.matchMedia(query);
  mql.addEventListener('change', listener);
  window.addEventListener('resize', listener);
  window.addEventListener('orientationchange', listener);
  return () => {
    mql.removeEventListener('change', listener);
    window.removeEventListener('resize', listener);
    window.removeEventListener('orientationchange', listener);
  };
}

/**
 * Returns true when the viewport is in phone landscape orientation: landscape orientation, at
 * most 599px tall, on a touch (coarse-pointer) device. The pointer check keeps this from matching
 * a resized desktop/laptop window, which is otherwise indistinguishable from a phone by dimensions
 * alone; the 599px (not 600px) ceiling keeps this mutually exclusive with `useIsTabletLandscape`'s
 * 600px floor. Must stay in sync with the `phone-landscape` custom variant in index.css.
 */
export function useIsPhoneLandscape(): boolean {
  const query = '(orientation: landscape) and (max-height: 599px) and (pointer: coarse)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: () => void) => subscribeToMediaQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

/**
 * Returns true when the viewport is in tablet landscape orientation: landscape orientation, at
 * least 600px tall, on a touch (coarse-pointer) device — without the pointer check this also
 * matches virtually every ordinary desktop/laptop window, since almost all desktop displays are
 * landscape and taller than 600px. Must stay in sync with the `tablet-landscape` custom variant in
 * index.css.
 */
export function useIsTabletLandscape(): boolean {
  const query = '(orientation: landscape) and (min-height: 600px) and (pointer: coarse)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: () => void) => subscribeToMediaQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
