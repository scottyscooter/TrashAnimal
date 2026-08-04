# Full-Screen Landscape Mode for Mobile

## Context
Portrait mode is unplayable; landscape is the only viable layout. Currently, when users rotate to landscape on mobile, browser chrome (address bar, tabs, etc.) still consumes valuable screen real estate. Solution: use Fullscreen API + Screen Orientation API to hide browser UI and lock to landscape, maximizing game viewport.

## Solution Overview

**1. New Hook: `useFullscreenLandscape`**
- Manage fullscreen state + orientation lock
- Expose: `enterFullscreen()`, `isFullscreen`, `supportsFullscreen`
- Calls `document.documentElement.requestFullscreen()` + `screen.orientation.lock('landscape')`
- Listens to `fullscreenchange` event to detect exit (ESC key, etc.)
- Export from `src/hooks/` alongside `useLandscapeBreakpoint.ts`

**2. New Component: `<PortraitOverlay />`** (or inline in GameBoardPage)
- Shown when: `useIsPhoneLandscape()` === false (portrait mode detected)
- Renders:
  - Background theme image (full viewport, same as game board background)
  - Centered text window with:
    - Message: "Rotate your screen to landscape and tap the button to play"
    - Button: "Enter Fullscreen Landscape" → calls `enterFullscreen()`
- Absolutely positioned, `z-index` above game content to block interactions
- Dismissed immediately when device rotates to landscape AND user taps button

**3. UI Integration in GameBoardPage**
- Add `useFullscreenLandscape` hook
- Before rendering game content, check: `if (!useIsPhoneLandscape()) return <PortraitOverlay />`
- Game board renders normally when landscape is active
- **Scope: GameBoardPage only** — no changes to Lobby, other pages

**4. Viewport Meta Tag**
- Add `viewport-fit=cover` for notch/safe-area support (optional but improves fullscreen look)
- Current: `<meta name="viewport" content="width=device-width, initial-scale=1.0">`
- New: `<meta name="viewport" content="width=device-width, initial-scale=1.0, viewport-fit=cover">`

## Implementation Files

**New:**
- `src/hooks/useFullscreenLandscape.ts` — hook managing fullscreen + orientation state

**Modify:**
- `src/pages/GameBoardPage.tsx` — integrate hook, show fullscreen entry/exit UI
- `index.html` — update viewport meta tag (add `viewport-fit=cover`)
- Optional: new component `<FullscreenPrompt />` if the logic is complex

## Verification

1. **Manual test on real device (or Chrome DevTools mobile emulation):**
   - Open game in portrait → see rotation prompt
   - Rotate to landscape → see fullscreen entry button
   - Tap fullscreen → check that browser chrome is gone, game fills screen
   - Verify `screen.orientation` is locked to landscape (device rotation lock appears in OS if supported)
   - Press ESC → fullscreen exits, browser chrome returns
   - Rotate back to portrait → see rotation prompt again

2. **Browser support fallback:**
   - Test on older/unsupported browser → no fullscreen button shown, but game still playable in responsive landscape mode

3. **E2E tests (if time):**
   - May need to mock/skip fullscreen API in Playwright tests (fullscreen doesn't work in headless)
   - Or add new spec checking for fullscreen button presence on phone landscape

## Known Constraints

- **User gesture required:** Fullscreen can only be triggered by user interaction (click/tap), not automatically on load
- **Browser support:** Fullscreen API is well-supported (Chrome, Firefox, Safari 16+); `screen.orientation.lock()` support varies (strong on Android, weaker on iOS — iOS often returns a rejected promise but doesn't error)
- **iOS quirk:** iOS Safari may not actually lock orientation (OS-level setting takes precedence), but fullscreen still hides chrome
- **Exit on iOS:** User can swipe-down to partially reveal address bar even in fullscreen; not much we can do about OS behavior
