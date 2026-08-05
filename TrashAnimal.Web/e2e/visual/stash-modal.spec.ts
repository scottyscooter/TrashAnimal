import { test, expect, devices, type Page } from '@playwright/test';

// See the identically-named helper in e2e/game-board-landscape.spec.ts: `defaultBrowserType` is
// only meaningful for a whole `projects` entry, and this repo's playwright.config.ts always pins
// chromium/firefox/webkit explicitly instead — passing it through test.use() is rejected.
function withoutDefaultBrowserType<T extends { defaultBrowserType?: unknown }>(
  device: T,
): Omit<T, 'defaultBrowserType'> {
  const { defaultBrowserType: _defaultBrowserType, ...rest } = device;
  return rest;
}

const PHONE_LANDSCAPE_DEVICE = withoutDefaultBrowserType(devices['iPhone 14 Pro landscape']);

/**
 * Boundary card-count states for StashModal's grouped card grid (CARDS_PER_ROW = 3): none, a
 * single card, exactly one full row, and the max possible (one of every distinct CardName —
 * 8 groups, filling three rows with the last partial). These are exactly the states that exposed
 * three real layout bugs in one review round (count-badge clipping via the container's forced
 * overflow-x, the dialog shrinking narrower than its own title at low counts, and the last row
 * being cut off in phone-landscape) — see StashModal.tsx and Modal.tsx for the fixes. A single
 * "looks fine" sample dataset would not have caught any of them.
 */
const SCENARIOS = ['empty', 'one', 'three', 'allDistinct'] as const;

async function gotoStashPreview(page: Page, scenario: string) {
  await page.goto(`/TrashAnimal/dev-preview/stash-modal?scenario=${scenario}`);
  await expect(page.getByRole('dialog')).toBeVisible();
}

test.describe('StashModal visual regression — desktop', () => {
  for (const scenario of SCENARIOS) {
    test(`${scenario} cards`, async ({ page }) => {
      await gotoStashPreview(page, scenario);
      await expect(page).toHaveScreenshot(`stash-modal-desktop-${scenario}.png`);
    });
  }
});

test.describe('StashModal visual regression — phone landscape', () => {
  test.use({ ...PHONE_LANDSCAPE_DEVICE });

  for (const scenario of SCENARIOS) {
    test(`${scenario} cards`, async ({ page }) => {
      await gotoStashPreview(page, scenario);
      await expect(page).toHaveScreenshot(`stash-modal-phone-landscape-${scenario}.png`);
    });
  }
});
