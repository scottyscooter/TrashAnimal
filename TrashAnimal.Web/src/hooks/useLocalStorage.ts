import { useCallback, useSyncExternalStore } from 'react';

type Listener = () => void;

const listenersByKey = new Map<string, Set<Listener>>();

interface CacheEntry<T> {
  raw: string | null;
  value: T;
}

const cacheByKey = new Map<string, CacheEntry<unknown>>();

function subscribe(key: string, listener: Listener): () => void {
  let listeners = listenersByKey.get(key);
  if (!listeners) {
    listeners = new Set();
    listenersByKey.set(key, listeners);
  }
  listeners.add(listener);
  return () => listeners!.delete(listener);
}

function notify(key: string): void {
  listenersByKey.get(key)?.forEach((listener) => listener());
}

/**
 * Reads and parses the stored value, caching by the raw string so repeated calls between writes
 * return the same reference — required by useSyncExternalStore (a fresh object/array reference on
 * every call would make it think the value changed on every render, even when it didn't).
 */
function readValue<T>(key: string, initialValue: T): T {
  let raw: string | null;
  try {
    raw = window.localStorage.getItem(key);
  } catch {
    raw = null;
  }

  const cached = cacheByKey.get(key) as CacheEntry<T> | undefined;
  if (cached && cached.raw === raw) {
    return cached.value;
  }

  let value: T;
  if (raw === null) {
    value = initialValue;
  } else {
    try {
      value = JSON.parse(raw) as T;
    } catch {
      value = initialValue;
    }
  }

  cacheByKey.set(key, { raw, value });
  return value;
}

/**
 * Generic localStorage-backed state, kept in sync across every hook instance reading the same key
 * within the tab via useSyncExternalStore + a module-level pub-sub. Plain useState would leave
 * sibling instances of the same key (e.g. JoinForm and LobbyPage both reading
 * 'trashanimal:identity') unaware of each other's writes until an unrelated re-render happened to
 * re-read localStorage. Setting `null`/`undefined` removes the key entirely.
 */
export function useLocalStorage<T>(key: string, initialValue: T): [T, (value: T) => void] {
  const getSnapshot = useCallback(() => readValue(key, initialValue), [key, initialValue]);
  const subscribeToKey = useCallback((listener: Listener) => subscribe(key, listener), [key]);

  const storedValue = useSyncExternalStore(subscribeToKey, getSnapshot, getSnapshot);

  const setValue = useCallback(
    (value: T) => {
      try {
        if (value === null || value === undefined) {
          window.localStorage.removeItem(key);
        } else {
          window.localStorage.setItem(key, JSON.stringify(value));
        }
      } catch {
        // localStorage unavailable (private browsing, quota exceeded) — notify still fires below,
        // which re-reads and self-heals every subscriber back to whatever is actually stored.
      }
      notify(key);
    },
    [key],
  );

  return [storedValue, setValue];
}
