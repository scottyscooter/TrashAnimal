export const queryKeys = {
  lobby: (lobbyId: string) => ['lobby', lobbyId] as const,
  gameView: (gameId: string, playerSeat: number) => ['gameView', gameId, playerSeat] as const,
  gameResult: (gameId: string) => ['gameResult', gameId] as const,
};
