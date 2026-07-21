import { useEffect } from 'react'
import { useNavigate, useParams } from 'react-router-dom'
import { useLobby } from '../hooks/useLobby'
import { useLobbySignalR } from '../hooks/useLobbySignalR'
import { useClientIdentity } from '../hooks/useClientIdentity'
import ShareLink from '../components/ShareLink'
import PlayerList from '../components/PlayerList'
import JoinForm from '../components/JoinForm'
import StartGameButton from '../components/StartGameButton'

const HOST_SEAT_INDEX = 0

function LobbyPage() {
  const { lobbyId } = useParams()
  const navigate = useNavigate()
  const lobbyQuery = useLobby(lobbyId ?? '')
  useLobbySignalR(lobbyId ?? '')
  const { identity } = useClientIdentity(lobbyId)

  const lobby = lobbyQuery.data

  useEffect(() => {
    if (lobby?.isStarted && lobby.gameId) {
      navigate(`/games/${lobby.gameId}`)
    }
  }, [lobby?.isStarted, lobby?.gameId, navigate])

  if (!lobbyId) {
    return null
  }

  if (lobbyQuery.isLoading) {
    return (
      <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
        <h1>Lobby</h1>
        <p>Loading lobby…</p>
      </section>
    )
  }

  if (lobbyQuery.isError || !lobby) {
    return (
      <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
        <h1>Lobby</h1>
        <p role="alert">This lobby could not be found.</p>
      </section>
    )
  }

  const isHost = identity?.seatIndex === HOST_SEAT_INDEX

  return (
    <section className="mx-auto flex max-w-md flex-col gap-6 px-4 py-12">
      <h1>Lobby</h1>
      <ShareLink lobbyId={lobbyId} />
      <PlayerList seats={lobby.seats} mySeatIndex={identity?.seatIndex ?? null} />
      {identity ? (
        isHost && <StartGameButton lobbyId={lobbyId} clientToken={identity.clientToken} />
      ) : (
        <JoinForm lobbyId={lobbyId} />
      )}
    </section>
  )
}

export default LobbyPage
