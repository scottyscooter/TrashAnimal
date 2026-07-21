import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useTheme } from './useTheme';

describe('useTheme', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('defaults to system with no data-theme attribute set', () => {
    const { result } = renderHook(() => useTheme());
    expect(result.current.theme).toBe('system');
    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
  });

  it('sets the data-theme attribute when switched to dark', () => {
    const { result } = renderHook(() => useTheme());

    act(() => {
      result.current.setTheme('dark');
    });

    expect(document.documentElement.getAttribute('data-theme')).toBe('dark');
  });

  it('sets the data-theme attribute when switched to light', () => {
    const { result } = renderHook(() => useTheme());

    act(() => {
      result.current.setTheme('light');
    });

    expect(document.documentElement.getAttribute('data-theme')).toBe('light');
  });

  it('removes the data-theme attribute when switched back to system', () => {
    const { result } = renderHook(() => useTheme());

    act(() => {
      result.current.setTheme('dark');
    });
    act(() => {
      result.current.setTheme('system');
    });

    expect(document.documentElement.hasAttribute('data-theme')).toBe(false);
  });

  it('persists the chosen theme across remounts', () => {
    const { result, unmount } = renderHook(() => useTheme());

    act(() => {
      result.current.setTheme('dark');
    });
    unmount();

    const { result: secondResult } = renderHook(() => useTheme());
    expect(secondResult.current.theme).toBe('dark');
  });
});
