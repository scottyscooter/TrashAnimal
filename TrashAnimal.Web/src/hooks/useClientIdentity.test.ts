import { describe, it, expect, beforeEach } from 'vitest';
import { renderHook, act } from '@testing-library/react';
import { useClientIdentity } from './useClientIdentity';

describe('useClientIdentity', () => {
  beforeEach(() => {
    localStorage.clear();
  });

  it('returns null identity when nothing is stored', () => {
    const { result } = renderHook(() => useClientIdentity('lobby-1'));
    expect(result.current.identity).toBeNull();
  });

  it('stores and returns an identity matching the given lobbyId', () => {
    const { result } = renderHook(() => useClientIdentity('lobby-1'));

    act(() => {
      result.current.setIdentity('lobby-1', 0, 'token-abc');
    });

    expect(result.current.identity).toEqual({ lobbyId: 'lobby-1', seatIndex: 0, clientToken: 'token-abc' });
  });

  it('does not return identity stored for a different lobby', () => {
    const { result, rerender } = renderHook(({ lobbyId }) => useClientIdentity(lobbyId), {
      initialProps: { lobbyId: 'lobby-1' },
    });

    act(() => {
      result.current.setIdentity('lobby-1', 0, 'token-abc');
    });

    rerender({ lobbyId: 'lobby-2' });

    expect(result.current.identity).toBeNull();
  });

  it('clears the stored identity', () => {
    const { result } = renderHook(() => useClientIdentity('lobby-1'));

    act(() => {
      result.current.setIdentity('lobby-1', 0, 'token-abc');
    });
    act(() => {
      result.current.clearIdentity();
    });

    expect(result.current.identity).toBeNull();
  });

  it('returns null identity when no lobbyId is given to match against', () => {
    const { result } = renderHook(() => useClientIdentity());

    act(() => {
      result.current.setIdentity('lobby-1', 0, 'token-abc');
    });

    expect(result.current.identity).toBeNull();
  });
});
