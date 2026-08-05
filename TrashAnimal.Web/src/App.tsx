import { Route, Routes } from 'react-router-dom'
import HomePage from './pages/HomePage'
import LobbyPage from './pages/LobbyPage'
import GameBoardPage from './pages/GameBoardPage'
import ResultsPage from './pages/ResultsPage'
import DevPreviewPage from './dev/DevPreviewPage'

function App() {
  return (
    <Routes>
      <Route path="/" element={<HomePage />} />
      <Route path="/games/:lobbyId/lobby" element={<LobbyPage />} />
      <Route path="/games/:gameId" element={<GameBoardPage />} />
      <Route path="/games/:gameId/result" element={<ResultsPage />} />
      {import.meta.env.DEV && <Route path="/dev-preview/:componentKey" element={<DevPreviewPage />} />}
    </Routes>
  )
}

export default App
