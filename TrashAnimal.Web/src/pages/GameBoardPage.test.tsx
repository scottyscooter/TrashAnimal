import { describe, it, expect, vi, beforeEach } from 'vitest';
import { http, HttpResponse } from 'msw';
import userEvent from '@testing-library/user-event';
import { render, screen, waitFor } from '../test/test-utils';
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
  handCards: [{ cardId: 'aaaaaaaa-0000-0000-0000-000000000001', name: 'Shiny' }],
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
  ownStash: { faceDownCount: 0, faceUpCards: [] },
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

    const kittehImage = await screen.findByAltText('Kitteh');
    await user.click(kittehImage.closest('button')!);
    await user.click(screen.getByRole('button', { name: /stash 1 card/i }));

    await waitFor(() => {
      expect(capturedRequest).toEqual({ kind: 'doubleStash', playerSeat: 0, cardIds: ['c1'] });
    });
  });
});
