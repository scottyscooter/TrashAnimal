import { test, expect, type Page } from '@playwright/test';

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

async function joinLobby(page: Page, lobbyId: string, nickname: string) {
  await page.goto(`/games/${lobbyId}/lobby`);
  await page.getByLabel(/nickname/i).fill(nickname);
  await page.getByRole('button', { name: /join lobby/i }).click();
}

test.describe('Lobby flow (real API)', () => {
  test('host creates a lobby, a second player joins, host starts the game', async ({ browser }) => {
    const hostContext = await browser.newContext();
    const hostPage = await hostContext.newPage();
    const guestContext = await browser.newContext();
    const guestPage = await guestContext.newPage();

    try {
      const lobbyId = await createLobby(hostPage, 'Alice');

      // Host is alone: not enough seats to start yet.
      await expect(hostPage.getByRole('button', { name: /start game/i })).toHaveCount(0);

      await joinLobby(guestPage, lobbyId, 'Bob');

      // Both players see two seats once Bob joins (live update via LobbyHub).
      await expect(hostPage.getByText('Bob')).toBeVisible();
      await expect(guestPage.getByText('Alice')).toBeVisible();

      // Only the host (seat 0) sees the Start Game button.
      await expect(hostPage.getByRole('button', { name: /start game/i })).toBeVisible();
      await expect(guestPage.getByRole('button', { name: /start game/i })).toHaveCount(0);

      await hostPage.getByRole('button', { name: /start game/i }).click();

      // Both players are pushed to the real game board once the lobby starts.
      await expect(hostPage).toHaveURL(/\/games\/[0-9a-f-]+$/);
      await expect(guestPage).toHaveURL(/\/games\/[0-9a-f-]+$/);
    } finally {
      await hostContext.close();
      await guestContext.close();
    }
  });

  test('seated player keeps their seat after refreshing the lobby page', async ({ page }) => {
    const lobbyId = await createLobby(page, 'Alice');

    await page.reload();

    await expect(page.getByRole('heading', { name: /lobby/i })).toBeVisible();
    await expect(page.getByText('Alice')).toBeVisible();
    await expect(page.getByLabel(/nickname/i)).toHaveCount(0);
    expect(page.url()).toContain(lobbyId);
  });

  test('a 5th player is rejected once the lobby is full', async ({ browser }) => {
    const hostContext = await browser.newContext();
    const hostPage = await hostContext.newPage();
    const lobbyId = await createLobby(hostPage, 'Alice');

    const guestContexts = await Promise.all([browser.newContext(), browser.newContext(), browser.newContext()]);
    const guestPages = await Promise.all(guestContexts.map((context) => context.newPage()));

    try {
      for (const [index, guestPage] of guestPages.entries()) {
        await joinLobby(guestPage, lobbyId, `Guest${index}`);
      }

      const overflowContext = await browser.newContext();
      const overflowPage = await overflowContext.newPage();
      try {
        await joinLobby(overflowPage, lobbyId, 'Overflow');
        await expect(overflowPage.getByRole('alert')).toContainText(/full/i);
      } finally {
        await overflowContext.close();
      }
    } finally {
      await hostContext.close();
      await Promise.all(guestContexts.map((context) => context.close()));
    }
  });

  test('joining with a taken nickname shows an inline error and refocuses the field', async ({ browser }) => {
    const hostContext = await browser.newContext();
    const hostPage = await hostContext.newPage();
    const lobbyId = await createLobby(hostPage, 'Alice');

    const guestContext = await browser.newContext();
    const guestPage = await guestContext.newPage();

    try {
      await guestPage.goto(`/games/${lobbyId}/lobby`);
      const nicknameInput = guestPage.getByLabel(/nickname/i);
      await nicknameInput.fill('Alice');
      await guestPage.getByRole('button', { name: /join lobby/i }).click();

      await expect(guestPage.getByRole('alert')).toContainText(/taken/i);
      await expect(nicknameInput).toBeFocused();
    } finally {
      await hostContext.close();
      await guestContext.close();
    }
  });
});
