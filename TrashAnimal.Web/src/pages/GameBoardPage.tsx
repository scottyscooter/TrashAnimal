import { useNavigate, useParams } from 'react-router-dom'

function GameBoardPage() {
  const navigate = useNavigate()
  const { gameId } = useParams()

  return (
    <section>
      <h1>Game Board</h1>
      <p>Playing game {gameId}</p>
      <button type="button" onClick={() => navigate(`/games/${gameId}/result`)}>
        End game
      </button>
    </section>
  )
}

export default GameBoardPage
