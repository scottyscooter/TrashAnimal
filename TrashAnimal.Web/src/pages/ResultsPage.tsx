import { useNavigate, useParams } from 'react-router-dom'

function ResultsPage() {
  const navigate = useNavigate()
  const { gameId } = useParams()

  return (
    <section>
      <h1>Results</h1>
      <p>Final scoreboard for game {gameId}</p>
      <button type="button" onClick={() => navigate('/')}>
        Play again
      </button>
    </section>
  )
}

export default ResultsPage
