import { useNavigate, useParams } from 'react-router-dom'

function LobbyPage() {
  const navigate = useNavigate()
  const { lobbyId } = useParams()

  return (
    <section>
      <h1>Lobby</h1>
      <p>Waiting room for game to begin {lobbyId}</p>
      <button type="button" onClick={() => navigate(`/games/${lobbyId}`)}>
        Start game
      </button>
    </section>
  )
}

export default LobbyPage
