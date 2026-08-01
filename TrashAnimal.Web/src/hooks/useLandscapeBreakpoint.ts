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
 * Phone landscape: landscape orientation with max-height of 600px.
 * Implemented via useSyncExternalStore subscribing to matchMedia changes.
 */
export function useIsPhoneLandscape(): boolean {
  const query = '(orientation: landscape) and (max-height: 600px)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: Listener) => subscribeToQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}

/**
 * Returns true when the viewport is in tablet landscape orientation.
 * Tablet landscape: landscape orientation with min-height of 600px or greater.
 * Implemented via useSyncExternalStore subscribing to matchMedia changes.
 */
export function useIsTabletLandscape(): boolean {
  const query = '(orientation: landscape) and (min-height: 600px)';
  const getSnapshot = useCallback(() => window.matchMedia(query).matches, [query]);
  const subscribe = useCallback((listener: Listener) => subscribeToQuery(query, listener), [query]);

  return useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
}
