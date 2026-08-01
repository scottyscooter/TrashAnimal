import { describe, it, expect, vi, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor, within } from '../test/test-utils';
import * as router from 'react-router-dom';
import { server } from '../test/msw/server';
import { API_BASE_URL } from '../api/httpClient';
import type { GameCommandRequest, GameCommandResponse, GameView, PlayerViewResponse } from '../api/types';
import GameBoardPage from './GameBoardPage';

const GAME_ID = '22222222-2222-2222-2222-222222222222';

vi.mock('react-router-dom', async () => {
  const actual = await vi.importActual('react-router-dom');
  return {
    ...actual,
    useNavigate: vi.fn(),
    useParams: vi.fn(),
  };
});

function storeIdentity(seatIndex: number) {
  localStorage.setItem(
    'trashanimal:identity',
    JSON.stringify({ lobbyId: '11111111-1111-1111-1111-111111111111', seatIndex, clientToken: 'tok', gameId: GAME_ID }),
  );
}

const BASE_VIEW: GameView = {
  state: 'RollPhase',
  currentPlayerIndex: 0,
  currentPlayerName: 'Alice',
  isBusted: false,
  forcedRollRemaining: false,
  phaseOneTokens: [],
  handCards: [
    { cardId: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Shiny', playableAs: 'PlayShiny', unplayableReason: null },
  ],
  yumYumResponderIndex: null,
  yumYumResponderName: null,
  stealPhase: null,
  tokenPhase: null,
  opponents: [
    {
      seatIndex: 1,
      name: 'Bob',
      handCount: 4,
      stashFaceDownCount: 1,
      stashFaceUpCards: [{ cardId: 'bbbbbbbb-0000-0000-0000-000000000001', name: 'Kitteh' }],
    },
  ],
  deckCount: 30,
  discardPile: [],
  ownStash: { faceDownCards: [], faceUpCards: [] },
  log: [],
};

function mockView(view: GameView, allowedActions: PlayerViewResponse['allowedActions']) {
  server.use(
    http.get(`${API_BASE_URL}/games/:gameId/view`, () =>
      HttpResponse.json<PlayerViewResponse>({ view, allowedActions, revision: 1 }),
    ),
  );
}

describe('GameBoardPage', () => {
  beforeEach(() => {
    localStorage.clear();
    vi.mocked(router.useParams).mockReturnValue({ gameId: GAME_ID } as ReturnType<typeof router.useParams>);
    vi.mocked(router.useNavigate).mockReturnValue(vi.fn());
  });

  it('shows a message when the local seat cannot be resolved (no stored identity)', () => {
    render(<GameBoardPage />);
    expect(screen.getByRole('alert')).toHaveTextContent(/could not find your seat/i);
  });

  it("shows YOUR TURN and an enabled Roll button when it's the local player's turn", async () => {
    storeIdentity(0);
    mockView(BASE_VIEW, ['RollDie']);

    render(<GameBoardPage />);

    expect(await screen.findByText(/your turn/i)).toBeInTheDocument();
    const rollButton = screen.getByRole('button', { name: 'ROLL' });
    expect(rollButton).toBeEnabled();
  });

  it('renders the opponent tile with hand and stash counts', async () => {
    storeIdentity(0);
    mockView(BASE_VIEW, ['RollDie']);

    render(<GameBoardPage />);

    expect(await screen.findByText('Bob')).toBeInTheDocument();
    expect(screen.getByText(/HAND 4/)).toBeInTheDocument();
    expect(screen.getByText(/STASH 2/)).toBeInTheDocument();
  });

  it('dispatches RollDie when the Roll button is clicked', async () => {
    storeIdentity(0);
    mockView(BASE_VIEW, ['RollDie']);
    const user = userEvent.setup();

    let capturedRequest: GameCommandRequest | undefined;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedRequest = (await request.json()) as GameCommandRequest;
        return HttpResponse.json<GameCommandResponse>({
          succeeded: true,
          errorMessage: null,
          view: BASE_VIEW,
          allowedActions: ['RollDie'],
        });
      }),
    );

    render(<GameBoardPage />);
    await user.click(await screen.findByRole('button', { name: 'ROLL' }));

    await waitFor(() => {
      expect(capturedRequest).toEqual({ kind: 'action', playerSeat: 0, action: 'RollDie' });
    });
  });

  it('renders the Recycle token-phase step from RecycleReplacementOptions alone, since it has no allowedActions', async () => {
    storeIdentity(0);
    mockView(
      {
        ...BASE_VIEW,
        state: 'TokenPhase',
        tokenPhase: {
          step: 'RecycleChoosingReplacement',
          remainingTokens: [],
          activeToken: 'Recycle',
          banditRevealedCardName: null,
          banditCurrentResponderIndex: null,
          stashableHandCardsForCurrentPrompt: [],
          recycleReplacementOptions: ['Bandit', 'Steal'],
        },
      },
      [],
    );

    render(<GameBoardPage />);

    expect(await screen.findByText(/pick a replacement token/i)).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /bandit/i })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /steal/i })).toBeInTheDocument();
  });

  it('submits selected cardIds for a DoubleStash pick', async () => {
    storeIdentity(0);
    mockView(
      {
        ...BASE_VIEW,
        state: 'TokenPhase',
        tokenPhase: {
          step: 'DoubleStashChoosingCards',
          remainingTokens: [],
          activeToken: 'DoubleStash',
          banditRevealedCardName: null,
          banditCurrentResponderIndex: null,
          stashableHandCardsForCurrentPrompt: [
            { cardId: 'c1', name: 'Kitteh' },
            { cardId: 'c2', name: 'Doggo' },
          ],
          recycleReplacementOptions: [],
        },
      },
      ['TokenDoubleStashSubmit'],
    );
    const user = userEvent.setup();

    let capturedRequest: GameCommandRequest | undefined;
    server.use(
      http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
        capturedRequest = (await request.json()) as GameCommandRequest;
        return HttpResponse.json<GameCommandResponse>({
          succeeded: true,
          errorMessage: null,
          view: BASE_VIEW,
          allowedActions: ['RollDie'],
        });
      }),
    );

    render(<GameBoardPage />);

    await screen.findByAltText('Kitteh');
    await user.click(screen.getByRole('button', { name: /add kitteh/i }));
    await user.click(screen.getByRole('button', { name: /stash 1 card/i }));

    await waitFor(() => {
      expect(capturedRequest).toEqual({ kind: 'doubleStash', playerSeat: 0, cardIds: ['c1'] });
    });
  });

  describe('Steal token resolution', () => {
    const TOKEN_PHASE_VIEW: GameView = {
      ...BASE_VIEW,
      state: 'TokenPhase',
      tokenPhase: {
        step: 'ChoosingNextToken',
        remainingTokens: ['Steal'],
        activeToken: null,
        banditRevealedCardName: null,
        banditCurrentResponderIndex: null,
        stashableHandCardsForCurrentPrompt: [],
        recycleReplacementOptions: [],
      },
    };

    it('dispatches resolveTokenSteal with victimSeat: null and shows a toast when no opponent has any cards', async () => {
      storeIdentity(0);
      mockView(
        {
          ...TOKEN_PHASE_VIEW,
          opponents: [{ ...BASE_VIEW.opponents[0], handCount: 0 }],
        },
        ['ResolveTokenSteal'],
      );
      const user = userEvent.setup();

      let capturedRequest: GameCommandRequest | undefined;
      server.use(
        http.post(`${API_BASE_URL}/games/:gameId/commands`, async ({ request }) => {
          capturedRequest = (await request.json()) as GameCommandRequest;
          return HttpResponse.json<GameCommandResponse>({
            succeeded: true,
            errorMessage: null,
            infoMessage: 'No opponents had any cards to steal — the token resolved with no effect.',
            view: TOKEN_PHASE_VIEW,
            allowedActions: [],
          });
        }),
      );

      render(<GameBoardPage />);

      await user.click(await screen.findByRole('button', { name: /steal/i }));

      expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

      await waitFor(() => {
        expect(capturedRequest).toEqual({ kind: 'resolveTokenSteal', playerSeat: 0, victimSeat: null });
      });

      expect(
        await screen.findByText(/no opponents had any cards to steal/i),
      ).toBeInTheDocument();
    });

    it('opens the victim picker excluding opponents with an empty hand when some opponents have cards', async () => {
      storeIdentity(0);
      mockView(
        {
          ...TOKEN_PHASE_VIEW,
          opponents: [
            { ...BASE_VIEW.opponents[0], seatIndex: 1, name: 'Bob', handCount: 0 },
            { ...BASE_VIEW.opponents[0], seatIndex: 2, name: 'Carol', handCount: 3 },
          ],
        },
        ['ResolveTokenSteal'],
      );
      const user = userEvent.setup();

      render(<GameBoardPage />);

      await user.click(await screen.findByRole('button', { name: /steal/i }));

      const dialog = await screen.findByRole('dialog');
      expect(within(dialog).getByRole('button', { name: /carol/i })).toBeInTheDocument();
      expect(within(dialog).queryByRole('button', { name: /bob/i })).not.toBeInTheDocument();
    });
  });

  describe('game log focus modal', () => {
    it('opens the log panel and moves focus into it when the game log button is clicked', async () => {
      storeIdentity(0);
      mockView(BASE_VIEW, ['RollDie']);
      const user = userEvent.setup();

      render(<GameBoardPage />);

      const openButton = await screen.findByRole('button', { name: /open game log/i });
      await user.click(openButton);

      const logPanel = await screen.findByRole('dialog', { name: /game log/i });
      expect(logPanel).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /close game log/i })).toHaveFocus();
    });

    it('closes the log panel and restores focus to the trigger when its close button is clicked', async () => {
      storeIdentity(0);
      mockView(BASE_VIEW, ['RollDie']);
      const user = userEvent.setup();

      render(<GameBoardPage />);

      const openButton = await screen.findByRole('button', { name: /open game log/i });
      await user.click(openButton);
      await screen.findByRole('dialog', { name: /game log/i });

      await user.click(screen.getByRole('button', { name: /close game log/i }));

      expect(screen.queryByRole('dialog', { name: /game log/i })).not.toBeInTheDocument();
      expect(openButton).toHaveFocus();
    });

    it('marks the rest of the board inert while the log panel is open, and lifts it on close', async () => {
      storeIdentity(0);
      mockView(BASE_VIEW, ['RollDie']);
      const user = userEvent.setup();

      render(<GameBoardPage />);
      const rollButton = await screen.findByRole('button', { name: 'ROLL' });
      expect(rollButton.closest('[inert]')).toBeNull();

      await user.click(screen.getByRole('button', { name: /open game log/i }));
      await screen.findByRole('dialog', { name: /game log/i });
      expect(rollButton.closest('[inert]')).not.toBeNull();

      await user.click(screen.getByRole('button', { name: /close game log/i }));
      expect(rollButton.closest('[inert]')).toBeNull();
    });
  });
});
