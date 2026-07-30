import { describe, it, expect } from 'vitest';
import userEvent from '@testing-library/user-event';
import { render, screen, fireEvent } from '../test/test-utils';
import InfoBadge from './InfoBadge';

describe('InfoBadge', () => {
  it('renders children unwrapped with no badge when info is falsy', () => {
    render(
      <InfoBadge info={null}>
        <span>Card content</span>
      </InfoBadge>,
    );

    expect(screen.getByText('Card content')).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: /more information/i })).not.toBeInTheDocument();
  });

  it('shows the info bubble on hover and hides it on mouse-leave', async () => {
    const user = userEvent.setup();
    render(
      <InfoBadge info="Explanation text">
        <span>Card content</span>
      </InfoBadge>,
    );

    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();

    const badge = screen.getByRole('button', { name: /more information/i });
    await user.hover(badge.parentElement!);
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Explanation text');

    await user.unhover(badge.parentElement!);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('shows the info bubble on keyboard focus and hides it on blur', async () => {
    const user = userEvent.setup();
    render(
      <>
        <InfoBadge info="Explanation text">
          <span>Card content</span>
        </InfoBadge>
        <button type="button">Elsewhere</button>
      </>,
    );

    await user.tab();
    expect(await screen.findByRole('tooltip')).toHaveTextContent('Explanation text');

    await user.tab();
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });

  it('pins the bubble open on click until dismissed by re-click, outside click, or Escape', async () => {
    const user = userEvent.setup();
    render(
      <>
        <InfoBadge info="Explanation text">
          <span>Card content</span>
        </InfoBadge>
        <button type="button">Outside</button>
      </>,
    );

    const badge = screen.getByRole('button', { name: /more information/i });

    // Use fireEvent for the re-click assertions so we don't also simulate the mouse hovering over
    // the badge (which would keep the bubble visible via hover regardless of the pin state).
    fireEvent.click(badge);
    expect(await screen.findByRole('tooltip')).toBeInTheDocument();

    fireEvent.click(badge);
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();

    fireEvent.click(badge);
    expect(await screen.findByRole('tooltip')).toBeInTheDocument();

    // A real click sequence (mousedown + click) outside the badge's container should dismiss it.
    await user.click(screen.getByRole('button', { name: 'Outside' }));
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();

    fireEvent.click(badge);
    expect(await screen.findByRole('tooltip')).toBeInTheDocument();

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
  });
});
