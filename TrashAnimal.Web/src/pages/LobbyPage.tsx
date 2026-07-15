import { useNavigate, useParams } from 'react-router-dom'

function LobbyPage() {
  const navigate = useNavigate()
  const { gameId } = useParams()

  return (
    <section>
      <h1>Lobby</h1>
      <p>Waiting room for game {gameId}</p>
      <button type="button" onClick={() => navigate(`/games/${gameId}`)}>
        Start game
      </button>
    </section>
  )
}

export default LobbyPage
