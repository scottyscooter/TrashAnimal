import { test, expect, devices, type Page, type Browser } from '@playwright/test';

// `devices['iPad (gen 9) landscape']` (named in the mobile-landscape plan's Verification section)
// does not exist in the Playwright version pinned in this repo (1.61.1) — its iPad descriptors are
// 'iPad (gen 5/6/7/11) landscape', 'iPad Mini landscape', and 'iPad Pro 11 landscape'. The plan's own
// Verification section lists 'iPad Pro 11 landscape' as an accepted alternative, but its 1194x834
// viewport reads more like a large tablet/small laptop than the mid-size ~1024x768 tablet the plan's
// analysis targets. 'iPad (gen 11) landscape' (944x656) sits inside the plan's stated tablet-landscape
// range (~1024–1366w × 600–800h is close enough at 944w, and squarely inside 600–800h) and keeps
// `min-height: 600px` comfortably satisfied, so it's used here instead.
const TABLET_LANDSCAPE_DEVICE = withoutDefaultBrowserType(devices['iPad (gen 11) landscape']);
const PHONE_LANDSCAPE_DEVICE = withoutDefaultBrowserType(devices['iPhone 14 Pro landscape']);

/**
 * Playwright's built-in device descriptors carry a `defaultBrowserType` field (only meaningful when
 * defining a whole `projects` entry from a device preset in `playwright.config.ts`) that this repo's
 * `playwright.config.ts` intentionally doesn't use — its `chromium`/`firefox`/`webkit` projects are
 * defined from `devices['Desktop Chrome'|'Desktop Firefox'|'Desktop Safari']` instead, so every
 * project always resolves to the same browser regardless of which mobile device a given test spreads
 * in. Passing `defaultBrowserType` through `test.use()` inside a `test.describe` block (rather than at
 * the top of the file or in the config) is rejected outright by this Playwright version ("Cannot
 * use({ defaultBrowserType }) in a describe group, because it forces a new worker"), so it has to be
 * stripped before spreading a device descriptor into `test.use()` here.
 */
function withoutDefaultBrowserType<T extends { defaultBrowserType?: unknown }>(
  device: T,
): Omit<T, 'defaultBrowserType'> {
  const { defaultBrowserType: _defaultBrowserType, ...rest } = device;
  return rest;
}

async function createLobby(page: Page, nickname: string): Promise<string> {
  await page.goto('/');
  await page.getByLabel(/nickname/i).fill(nickname);
  await page.getByRole('button', { name: /create game/i }).click();
  await expect(page).toHaveURL(/\/games\/[0-9a-f-]+\/lobby$/);

  const match = page.url().match(/\/games\/([0-9a-f-]+)\/lobby/);
  const lobbyId = match?.[1];
  if (!lobbyId) throw new Error(`Could not extract lobbyId from ${page.url()}`);
  return lobbyId;
}

async function joinLobby(page: Page, lobbyId: string, nickname: string): Promise<void> {
  await page.goto(`/games/${lobbyId}/lobby`);
  await page.getByLabel(/nickname/i).fill(nickname);
  await page.getByRole('button', { name: /join lobby/i }).click();
}

/**
 * Seats `hostPage` (the device-emulated page under test) as the game's host — always seat 0, which
 * `GameSession.CurrentPlayerIndex` starts at (see `TrashAnimal/GameSession.cs`), so the host is
 * always the active player immediately after the game starts, with `RollDie` allowed and no
 * token/steal/YumYum prompts in the way yet. One guest browser context is spun up per name in
 * `opponentNicknames`, joined, and closed again immediately — the game session's player list lives
 * server-side once a seat is taken, so the guest connection doesn't need to stay open for the rest
 * of the test.
 */
async function startGameWithOpponents(
  hostPage: Page,
  browser: Browser,
  opponentNicknames: string[],
): Promise<void> {
  const lobbyId = await createLobby(hostPage, 'Alice');

  for (const nickname of opponentNicknames) {
    const guestContext = await browser.newContext();
    const guestPage = await guestContext.newPage();
    await joinLobby(guestPage, lobbyId, nickname);
    await expect(hostPage.getByText(nickname, { exact: true })).toBeVisible();
    await guestContext.close();
  }

  await hostPage.getByRole('button', { name: /start game/i }).click();
  // Anchored on `$` (no trailing path) so this doesn't also match the `/games/:id/lobby` route.
  await expect(hostPage).toHaveURL(/\/games\/[0-9a-f-]+$/);
}

/** Hand-card elements are the only board elements rendered with an explicit `role="button"`
 * attribute (`PlayerHand.tsx`) — every other clickable element on the board (Roll/Stop, opponent
 * tabs, deck/discard, stash) is a native `<button>`, which Playwright's `role=button` matching also
 * catches but which never sets the *attribute* `role="button"` explicitly. Scoping to the attribute
 * selector isolates hand cards specifically. */
function handCardLocator(page: Page) {
  return page.locator('div[role="button"][aria-disabled]');
}

test.describe('phone landscape', () => {
  test.use({ ...PHONE_LANDSCAPE_DEVICE });

  test('board loads with turn indicator, opponent tabs, deck/discard, hand, tokens, and Roll/Stop all visible on load', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    // Turn indicator pill, top-center (host is seat 0, always first to act).
    await expect(page.getByText('YOUR TURN')).toBeInViewport();

    // Opponent index tabs, stacked left edge — phone-landscape-only replacement for OpponentRail.
    await expect(page.getByRole('button', { name: /view bob/i })).toBeInViewport();

    // Game log button, top-right.
    await expect(page.getByRole('button', { name: /open game log/i })).toBeInViewport();

    // Deck/discard flanking the turn indicator.
    await expect(page.getByText('DECK', { exact: true })).toBeInViewport();
    await expect(page.getByText('DISCARD', { exact: true })).toBeInViewport();

    // Tokens bar, bottom-center under the hand.
    await expect(page.getByText('YOUR TOKENS')).toBeInViewport();

    // Roll/Stop controls, bottom-right.
    await expect(page.getByRole('button', { name: 'Stop rolling' })).toBeInViewport();
    await expect(page.getByRole('button', { name: /^roll$/i })).toBeInViewport();

    // Hand: 2 players -> 3 starting cards each (StartingHandCounts[0] in TrashAnimal.Api/appsettings.json).
    const handCards = handCardLocator(page);
    await expect(handCards).toHaveCount(3);
    for (const card of await handCards.all()) {
      await expect(card).toBeInViewport();
    }
  });

  test('tapping an opponent index tab opens OpponentDetailModal targeted at that specific opponent', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob', 'Cleo']);

    await page.getByRole('button', { name: /view cleo/i }).click();
    const cleoDialog = page.getByRole('dialog');
    await expect(cleoDialog).toBeVisible();
    await expect(cleoDialog).toContainText('Cleo');
    await expect(cleoDialog).not.toContainText('Bob');

    await page.getByRole('button', { name: 'Close' }).click();
    await expect(cleoDialog).not.toBeVisible();

    // A different tab targets a different opponent — this isn't just "a modal opened", the content
    // tracks which tab was tapped, independent of which opponent is contextually highlighted.
    await page.getByRole('button', { name: /view bob/i }).click();
    const bobDialog = page.getByRole('dialog');
    await expect(bobDialog).toBeVisible();
    await expect(bobDialog).toContainText('Bob');
    await expect(bobDialog).not.toContainText('Cleo');
  });

  test('opening the game log blurs/locks the background so a tap meant for a background control does not reach it', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    const rollButton = page.getByRole('button', { name: /^roll$/i });
    await expect(rollButton).toBeVisible();

    await page.getByRole('button', { name: /open game log/i }).click();
    const logPanel = page.getByRole('dialog', { name: /game log/i });
    await expect(logPanel).toBeVisible();

    // Primary evidence the background wrapper is blurred/locked: `GameBoardPage.tsx` applies this
    // exact inline `filter` (and `pointer-events: none`) to the background wrapper only while
    // `isGameLogOpen` is true, and removes the `style` attribute entirely otherwise.
    await expect(page.locator('div[style*="blur(10px)"]')).toHaveCount(1);

    // Behavioral check: a tap at Roll's on-screen position, while the log is open, should not roll a
    // die. `GameBoardPage.tsx` renders a full-screen click-catcher (z-30) above the blurred,
    // `pointer-events: none` background wrapper (which holds Roll, z-20) and below the log panel
    // itself (z-40) — a real mouse click at Roll's coordinates hits the catcher, not Roll, so it
    // closes the log instead of rolling. We assert both halves: the log closes (the click landed
    // somewhere) and Roll's own action never fired (the button still reads "ROLL", not "ADVANCE" or
    // "NEW TURN", proving no die was rolled and no token was added to the tray).
    const rollBox = await rollButton.boundingBox();
    if (!rollBox) throw new Error('Roll button has no bounding box while the game log is open');
    await page.mouse.click(rollBox.x + rollBox.width / 2, rollBox.y + rollBox.height / 2);

    await expect(logPanel).not.toBeVisible();
    await expect(page.locator('div[style*="blur(10px)"]')).toHaveCount(0);
    await expect(rollButton).toHaveText(/^roll$/i);
  });

  test('closing the game log via its close button returns the board to a fully interactive state', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    await page.getByRole('button', { name: /open game log/i }).click();
    const logPanel = page.getByRole('dialog', { name: /game log/i });
    await expect(logPanel).toBeVisible();
    await expect(page.locator('div[style*="blur(10px)"]')).toHaveCount(1);

    await page.getByRole('button', { name: 'Close game log' }).click();
    await expect(logPanel).not.toBeVisible();
    await expect(page.locator('div[style*="blur(10px)"]')).toHaveCount(0);

    // Board is interactive again: a control that was inert while the log was open now responds.
    await page.getByRole('button', { name: /view bob/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('closing the game log via the background click-catcher returns the board to a fully interactive state', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    await page.getByRole('button', { name: /open game log/i }).click();
    const logPanel = page.getByRole('dialog', { name: /game log/i });
    await expect(logPanel).toBeVisible();

    // A point clearly outside the log panel's ~28%-wide, right-anchored bounds (top-left corner,
    // where nothing else is positioned) — still within the full-screen click-catcher.
    await page.mouse.click(15, 15);

    await expect(logPanel).not.toBeVisible();
    await expect(page.locator('div[style*="blur(10px)"]')).toHaveCount(0);

    await page.getByRole('button', { name: /view bob/i }).click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });

  test('hand cards remain visible and clickable while the board is otherwise idle', async ({ page, browser }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    const handCards = handCardLocator(page);
    await expect(handCards).toHaveCount(3);

    const firstCard = handCards.first();
    await expect(firstCard).toBeVisible();
    await expect(firstCard).toBeInViewport();

    // Whether a given starting card is actually playable this early depends on the random deal
    // (`aria-disabled` mirrors `HandCardView.playableAs`), so this only asserts the card is a live,
    // clickable element — not that clicking it necessarily dispatches a command. Cards can be hovered
    // and receive a click without erroring regardless of playability (PlayerHand's hover handlers are
    // always live; only `activate()` gates on `isPlayable`).
    await firstCard.hover();
    await firstCard.click();
  });
});

test.describe('tablet landscape', () => {
  test.use({ ...TABLET_LANDSCAPE_DEVICE });

  test('OpponentRail and the desktop-style GameLogPanel remain visible, not replaced by phone-landscape triggers', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    // OpponentRail's always-visible tile (not the OpponentIndexTabs trigger).
    await expect(page.getByText('Bob', { exact: true })).toBeVisible();

    // GameLogPanel's always-visible glass sidebar.
    await expect(page.getByText('GAME LOG', { exact: true })).toBeVisible();
    await expect(page.getByLabel('Game log')).toBeVisible();
  });

  test('no opponent index tabs or game log button are shown (phone-landscape-only triggers)', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    await expect(page.getByRole('button', { name: /view bob/i })).toBeHidden();
    await expect(page.getByRole('button', { name: /open game log/i })).toBeHidden();
  });
});

test.describe('portrait / desktop regression', () => {
  // Deliberately no `test.use(devices[...])` override here — this runs at the `chromium` project's
  // default desktop viewport (1280x720, per `playwright.config.ts`'s `devices['Desktop Chrome']`).
  //
  // This test caught a real gap when it was first written: `index.css`'s `tablet-landscape` custom
  // variant originally read `@media (orientation: landscape) and (min-height: 600px)` — no upper
  // bound on width, and no distinction from a mouse-driven desktop window. An ordinary desktop
  // browser at 1280x720 is landscape-orientation with height >= 600px, so it satisfied that query
  // too, and `OpponentRail`'s `tablet-landscape:w-[180px]` was narrowing it there, unintentionally.
  // Fixed by adding `(pointer: coarse)` to both `phone-landscape` and `tablet-landscape` (in
  // index.css and the matching `useIsPhoneLandscape`/`useIsTabletLandscape` query strings) — real
  // tablets/phones and Playwright's touch-emulated devices report `pointer: coarse`, desktop/laptop
  // browsers with a mouse or trackpad report `pointer: fine`, so this now correctly excludes desktop
  // regardless of its window dimensions.
  test('OpponentRail renders at its original desktop position and width, unaffected by landscape work', async ({
    page,
    browser,
  }) => {
    await startGameWithOpponents(page, browser, ['Bob']);

    // Scoped to `HAND`/`STASH` badge text (unique to `OpponentTile`'s button, unlike
    // `OpponentIndexTabs`' single-initial "View Bob" button) so this can't accidentally match the
    // phone-landscape-only trigger even if DOM ordering ever changes.
    const opponentRailTile = page.getByRole('button', { name: /bob/i }).filter({ hasText: 'HAND' });
    await expect(opponentRailTile).toBeVisible();

    const box = await opponentRailTile.boundingBox();
    if (!box) throw new Error('OpponentRail tile has no bounding box');
    // OpponentRail's unconditional width is `w-[236px]` (`OpponentRail.tsx`); only
    // `tablet-landscape:w-[180px]` narrows it, and that variant no longer matches a desktop
    // viewport now that it requires `pointer: coarse` too — see the fix noted above.
    expect(box.width).toBeGreaterThan(200);

    // Phone-landscape-only triggers must not appear outside landscape/short viewports either.
    await expect(page.getByRole('button', { name: /view bob/i })).toBeHidden();
    await expect(page.getByRole('button', { name: /open game log/i })).toBeHidden();
  });

  // A genuinely portrait viewport (width < height) can never satisfy either custom variant's
  // `orientation: landscape` clause, so — unlike the default-desktop-viewport test above — this one
  // gives an unambiguous "did the landscape work touch non-landscape rendering at all" signal,
  // independent of the width/height breakpoint caveat noted above.
  test('OpponentRail and GameLogPanel keep their normal full-width positions in a portrait viewport', async ({
    page,
    browser,
  }) => {
    await page.setViewportSize({ width: 810, height: 1080 });
    await startGameWithOpponents(page, browser, ['Bob']);

    const opponentRailTile = page.getByRole('button', { name: /bob/i }).filter({ hasText: 'HAND' });
    await expect(opponentRailTile).toBeVisible();
    const box = await opponentRailTile.boundingBox();
    if (!box) throw new Error('OpponentRail tile has no bounding box');
    expect(box.width).toBeGreaterThan(200);

    await expect(page.getByText('GAME LOG', { exact: true })).toBeVisible();
    await expect(page.getByRole('button', { name: /view bob/i })).toBeHidden();
    await expect(page.getByRole('button', { name: /open game log/i })).toBeHidden();
  });
});
