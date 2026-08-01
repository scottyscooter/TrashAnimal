import { useCallback, useSyncExternalStore } from 'react';

type Listener = () => void;

const queryByString = new Map<string, MediaQueryList>();
const listenersByQuery = new Map<string, Set<Listener>>();

function subscribeToQuery(query: string, listener: Listener): () => void {
  let mql = queryByString.get(query);
  if (!mql) {
    mql = window.matchMedia(query);
    queryByString.set(query, mql);
  }

  let listeners = listenersByQuery.get(query);
  if (!listeners) {
    listeners = new Set();
    listenersByQuery.set(query, listeners);
  }

  listeners.add(listener);

  // Add event listener on the first listener for this query
  if (listeners.size === 1) {
    mql.addEventListener('change', () => {
      listenersByQuery.get(query)?.forEach((l) => l());
    });
  }

  return () => {
    listeners!.delete(listener);
  };
}

/**
 * Returns true when the viewport is in phone landscape orientation.
 * Phone landscape: landscape orientation with max-height of 600px, on a touch (coarse-pointer)
 * device — the pointer check keeps this from also matching a resized desktop/laptop window,
 * which is otherwise indistinguishable from a phone by dimensions alone. Must stay in sync with
 * the `phone-landscape` custom variant in index.css.
 * Implemented via useSyncExternalStore subscribing to matchMedia changes.
 */
export function useIsPhoneLandscape(): boolean {
  const query = '(orientation: landscape) and (max-height: 600px) and (pointer: coarse)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: Listener) => subscribeToQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

/**
 * Returns true when the viewport is in tablet landscape orientation.
 * Tablet landscape: landscape orientation with min-height of 600px or greater, on a touch
 * (coarse-pointer) device — without the pointer check this also matches virtually every ordinary
 * desktop/laptop window, since almost all desktop displays are landscape and taller than 600px.
 * Must stay in sync with the `tablet-landscape` custom variant in index.css.
 * Implemented via useSyncExternalStore subscribing to matchMedia changes.
 */
export function useIsTabletLandscape(): boolean {
  const query = '(orientation: landscape) and (min-height: 600px) and (pointer: coarse)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: Listener) => subscribeToQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
