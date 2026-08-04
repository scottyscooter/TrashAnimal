import { useFullscreenLandscape } from '../../hooks/useFullscreenLandscape'
import DayNightBackground from './DayNightBackground'

export default function PortraitOverlay() {
  const { enterFullscreen } = useFullscreenLandscape()

  return (
    <div className="gb-root">
      <DayNightBackground />
      <div className="fixed inset-0 flex items-center justify-center">
        <div className="rounded-2xl bg-black/80 px-6 py-8 text-center backdrop-blur-sm">
          <h2 className="mb-4 text-lg font-bold text-white">Rotate Your Device</h2>
          <p className="mb-6 text-sm text-gray-200">
            This game is best played in landscape mode. Please rotate your device and tap the button below.
          </p>
          <button
            type="button"
            onClick={enterFullscreen}
            className="rounded-lg bg-blue-600 px-6 py-3 font-semibold text-white hover:bg-blue-700 active:bg-blue-800"
          >
            Enter Fullscreen Landscape
          </button>
        </div>
      </div>
    </div>
  )
}
