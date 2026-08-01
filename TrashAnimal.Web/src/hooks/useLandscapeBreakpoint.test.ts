import { describe, it, expect, beforeEach, vi } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useIsPhoneLandscape, useIsTabletLandscape } from './useLandscapeBreakpoint';

describe('useLandscapeBreakpoint', () => {
  let mockMatchMedia: ReturnType<typeof vi.fn>;
  let mediaQueryLists: Map<string, Partial<MediaQueryList>>;

  beforeEach(() => {
    mediaQueryLists = new Map();

    mockMatchMedia = vi.fn((query: string) => {
      if (!mediaQueryLists.has(query)) {
        const mql: any = {
          media: query,
          onchange: null,
          addListener: vi.fn(),
          removeListener: vi.fn(),
          addEventListener: vi.fn(),
          removeEventListener: vi.fn(),
          dispatchEvent: vi.fn(),
        };

        Object.defineProperty(mql, 'matches', {
          writable: true,
          configurable: true,
          value: false,
        });

        mediaQueryLists.set(query, mql);
      }
      return mediaQueryLists.get(query) as MediaQueryList;
    });

    Object.defineProperty(window, 'matchMedia', {
      writable: true,
      value: mockMatchMedia,
    });
  });

  describe('useIsPhoneLandscape', () => {
    it('returns false when the query does not match', () => {
      const { result } = renderHook(() => useIsPhoneLandscape());
      expect(result.current).toBe(false);
    });

    it('returns true when the phone landscape query matches', () => {
      const query = '(orientation: landscape) and (max-height: 600px)';

      // Pre-populate the MediaQueryList with matches: true by calling mockMatchMedia first
      mockMatchMedia(query);
      const mql = mediaQueryLists.get(query) as any;
      Object.defineProperty(mql, 'matches', {
        writable: true,
        configurable: true,
        value: true,
      });

      const { result } = renderHook(() => useIsPhoneLandscape());
      expect(result.current).toBe(true);
    });

    it('uses the correct media query string', () => {
      renderHook(() => useIsPhoneLandscape());
      expect(mockMatchMedia).toHaveBeenCalledWith('(orientation: landscape) and (max-height: 600px)');
    });

    it('updates when the media query changes', () => {
      const query = '(orientation: landscape) and (max-height: 600px)';
      const { result, rerender } = renderHook(() => useIsPhoneLandscape());

      expect(result.current).toBe(false);

      // Simulate media query match change
      const mql = mediaQueryLists.get(query);
      if (mql && typeof mql.addEventListener === 'function') {
        act(() => {
          (mql as any).matches = true;
          // Trigger the change listener
          const listeners = (mql.addEventListener as any).mock.calls;
          if (listeners.length > 0) {
            const changeListener = listeners[0][1];
            changeListener();
          }
        });
      }

      rerender();
      expect(result.current).toBe(true);
    });
  });

  describe('useIsTabletLandscape', () => {
    it('returns false when the query does not match', () => {
      const { result } = renderHook(() => useIsTabletLandscape());
      expect(result.current).toBe(false);
    });

    it('returns true when the tablet landscape query matches', () => {
      const query = '(orientation: landscape) and (min-height: 600px)';

      // Pre-populate the MediaQueryList with matches: true by calling mockMatchMedia first
      mockMatchMedia(query);
      const mql = mediaQueryLists.get(query) as any;
      Object.defineProperty(mql, 'matches', {
        writable: true,
        configurable: true,
        value: true,
      });

      const { result } = renderHook(() => useIsTabletLandscape());
      expect(result.current).toBe(true);
    });

    it('uses the correct media query string', () => {
      renderHook(() => useIsTabletLandscape());
      expect(mockMatchMedia).toHaveBeenCalledWith('(orientation: landscape) and (min-height: 600px)');
    });

    it('updates when the media query changes', () => {
      const query = '(orientation: landscape) and (min-height: 600px)';
      const { result, rerender } = renderHook(() => useIsTabletLandscape());

      expect(result.current).toBe(false);

      // Simulate media query match change
      const mql = mediaQueryLists.get(query);
      if (mql && typeof mql.addEventListener === 'function') {
        act(() => {
          (mql as any).matches = true;
          // Trigger the change listener
          const listeners = (mql.addEventListener as any).mock.calls;
          if (listeners.length > 0) {
            const changeListener = listeners[0][1];
            changeListener();
          }
        });
      }

      rerender();
      expect(result.current).toBe(true);
    });
  });
});
