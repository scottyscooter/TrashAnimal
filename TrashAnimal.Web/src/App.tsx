import { Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import LobbyPage from './pages/LobbyPage'
import GameBoardPage from './pages/GameBoardPage'
import ResultsPage from './pages/ResultsPage'

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/games/:gameId/lobby" element={<LobbyPage />} />
      <Route path="/games/:gameId" element={<GameBoardPage />} />
      <Route path="/games/:gameId/result" element={<ResultsPage />} />
    </Routes>
  )
}

export default App
