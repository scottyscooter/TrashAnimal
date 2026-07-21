import { describe, it, expect, beforeEach } from 'vitest';
import { render, screen, fireEvent } from '../test/test-utils';
import ThemeToggle from './ThemeToggle';

describe('ThemeToggle', () => {
  beforeEach(() => {
    localStorage.clear();
    document.documentElement.removeAttribute('data-theme');
  });

  it('starts on System and cycles through Light and Dark on click', () => {
    render(<ThemeToggle />);

    const button = screen.getByRole('button', { name: /theme: system/i });
    expect(button).toHaveTextContent('System');

    fireEvent.click(button);
    expect(screen.getByRole('button', { name: /theme: light/i })).toHaveTextContent('Light');

    fireEvent.click(screen.getByRole('button', { name: /theme: light/i }));
    expect(screen.getByRole('button', { name: /theme: dark/i })).toHaveTextContent('Dark');

    fireEvent.click(screen.getByRole('button', { name: /theme: dark/i }));
    expect(screen.getByRole('button', { name: /theme: system/i })).toHaveTextContent('System');
  });
});
