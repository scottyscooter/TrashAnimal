import { useNavigate } from 'react-router-dom'

const DEMO_GAME_ID = 'demo-game'

function HomePage() {
  const navigate = useNavigate()

  return (
    <section>
      <h1>TrashAnimal</h1>
      <p>Home / Create-Join page</p>
      <button type="button" onClick={() => navigate(`/games/${DEMO_GAME_ID}/lobby`)}>
        Create game
      </button>
    </section>
  )
}

export default HomePage
