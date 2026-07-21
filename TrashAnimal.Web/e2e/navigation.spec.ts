import { test, expect } from '@playwright/test';

test.describe('Navigation Flow', () => {
  test('create a lobby from Home and land on the Lobby page', async ({ page }) => {
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('TrashAnimal');

    await page.getByLabel(/nickname/i).fill('Alice');
    await page.getByRole('button', { name: /create game/i }).click();

    await expect(page).toHaveURL(/\/games\/[0-9a-f-]+\/lobby$/);
    await expect(page.locator('h1')).toContainText('Lobby');
    await expect(page.getByLabel(/lobby share link/i)).toBeVisible();
  });

  test('navigate from game board to results directly', async ({ page }) => {
    await page.goto('/games/demo-game');
    await page.click('button:has-text("End game")');

    await expect(page).toHaveURL('/games/demo-game/result');
    await expect(page.locator('h1')).toContainText('Results');
  });

  test('navigate from results to home directly', async ({ page }) => {
    await page.goto('/games/demo-game/result');
    await page.click('button:has-text("Play again")');

    await expect(page).toHaveURL('/');
    await expect(page.locator('h1')).toContainText('TrashAnimal');
  });

  test('game board and results pages preserve the game id in the URL', async ({ page }) => {
    const gameId = 'test-game-123';
    await page.goto(`/games/${gameId}`);
    expect(page.url()).toContain(gameId);

    await page.click('button:has-text("End game")');
    expect(page.url()).toContain(gameId);
  });
});
