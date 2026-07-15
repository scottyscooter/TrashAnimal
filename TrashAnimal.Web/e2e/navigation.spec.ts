import { test, expect } from '@playwright/test';

test.describe('Navigation Flow', () => {
  test('complete game flow: Home → Lobby → GameBoard → Results → Home', async ({ page }) => {
    // Start at home
    await page.goto('/');
    await expect(page.locator('h1')).toContainText('TrashAnimal');

    // Navigate to lobby
    await page.click('button:has-text("Create game")');
    await expect(page).toHaveURL('/games/demo-game/lobby');
    await expect(page.locator('h1')).toContainText('Lobby');
    await expect(page.locator('p')).toContainText('demo-game');

    // Navigate to game board
    await page.click('button:has-text("Start game")');
    await expect(page).toHaveURL('/games/demo-game');
    await expect(page.locator('h1')).toContainText('Game Board');
    await expect(page.locator('p')).toContainText('demo-game');

    // Navigate to results
    await page.click('button:has-text("End game")');
    await expect(page).toHaveURL('/games/demo-game/result');
    await expect(page.locator('h1')).toContainText('Results');
    await expect(page.locator('p')).toContainText('demo-game');

    // Return to home
    await page.click('button:has-text("Play again")');
    await expect(page).toHaveURL('/');
    await expect(page.locator('h1')).toContainText('TrashAnimal');
  });

  test('navigate from home to lobby directly', async ({ page }) => {
    await page.goto('/');
    await page.click('button:has-text("Create game")');

    await expect(page).toHaveURL('/games/demo-game/lobby');
    await expect(page.locator('h1')).toContainText('Lobby');
  });

  test('navigate from lobby to game board directly', async ({ page }) => {
    await page.goto('/games/demo-game/lobby');
    await page.click('button:has-text("Start game")');

    await expect(page).toHaveURL('/games/demo-game');
    await expect(page.locator('h1')).toContainText('Game Board');
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

  test('page parameters are preserved during navigation', async ({ page }) => {
    // Test with a different game ID
    const gameId = 'test-game-123';
    await page.goto(`/games/${gameId}/lobby`);

    expect(page.url()).toContain(gameId);
    await expect(page.locator('p')).toContainText(gameId);

    await page.click('button:has-text("Start game")');
    expect(page.url()).toContain(gameId);

    await page.click('button:has-text("End game")');
    expect(page.url()).toContain(gameId);
  });
});
