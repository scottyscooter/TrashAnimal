import { describe, it, expect } from 'vitest';
import { render, screen } from '../../test/test-utils';
import CardCountBadge from './CardCountBadge';

describe('CardCountBadge', () => {
  it('renders the count and wraps children in a relative container', () => {
    render(
      <CardCountBadge count={5}>
        <img src="/card.png" alt="Nanners" />
      </CardCountBadge>,
    );

    expect(screen.getByText('5')).toBeInTheDocument();
    const image = screen.getByRole('img', { name: 'Nanners' });
    expect(image.parentElement).toHaveClass('relative', 'w-fit');
  });

  it.each([
    ['small', 'h-6', 'w-6'],
    ['medium', 'h-6', 'w-6'],
    ['compact', 'h-7', 'w-7'],
    ['large', 'h-9', 'w-9'],
  ] as const)('applies the %s size tier classes', (size, heightClass, widthClass) => {
    render(
      <CardCountBadge count={3} size={size}>
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('3')).toHaveClass(heightClass, widthClass);
  });

  it('defaults to the medium size tier', () => {
    render(
      <CardCountBadge count={2}>
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('2')).toHaveClass('h-6', 'w-6');
  });

  it('includes phone-landscape responsive classes by default', () => {
    render(
      <CardCountBadge count={4} size="large">
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('4').className).toContain('phone-landscape:h-7');
  });

  it('omits phone-landscape responsive classes when includeResponsive is false', () => {
    render(
      <CardCountBadge count={4} size="large" includeResponsive={false}>
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('4').className).not.toContain('phone-landscape:');
  });

  it('applies the compact tier phone-landscape shrink to h-6/w-6', () => {
    render(
      <CardCountBadge count={4} size="compact">
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('4').className).toContain('phone-landscape:h-6');
  });

  it('applies the small tier phone-landscape shrink to h-5/w-5', () => {
    render(
      <CardCountBadge count={4} size="small">
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('4').className).toContain('phone-landscape:h-5');
  });

  it('the medium tier has no built-in responsive variant (matches its original usages)', () => {
    render(
      <CardCountBadge count={4} size="medium">
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('4').className).not.toContain('phone-landscape:');
  });

  it('defaults to the gold color scheme', () => {
    render(
      <CardCountBadge count={1}>
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('1')).toHaveStyle({ background: 'var(--gb-gold)' });
  });

  it('applies the green color scheme when requested', () => {
    render(
      <CardCountBadge count={1} color="green">
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('1')).toHaveStyle({ background: 'var(--gb-green)' });
  });

  it('wraps a button child without breaking its click handler', () => {
    let clicked = false;
    render(
      <CardCountBadge count={7}>
        <button type="button" onClick={() => (clicked = true)}>
          Open stash
        </button>
      </CardCountBadge>,
    );

    screen.getByRole('button', { name: 'Open stash' }).click();
    expect(clicked).toBe(true);
    expect(screen.getByText('7')).toBeInTheDocument();
  });

  it('wraps a div child', () => {
    render(
      <CardCountBadge count={9}>
        <div data-testid="card-back" />
      </CardCountBadge>,
    );

    expect(screen.getByTestId('card-back')).toBeInTheDocument();
    expect(screen.getByText('9')).toBeInTheDocument();
  });

  it('positions the badge with negative bottom/right offsets inside the wrapper', () => {
    render(
      <CardCountBadge count={6}>
        <img src="/card.png" alt="card" />
      </CardCountBadge>,
    );

    expect(screen.getByText('6')).toHaveClass('absolute', '-bottom-2', '-right-2');
  });
});
