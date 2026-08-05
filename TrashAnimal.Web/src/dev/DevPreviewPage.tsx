import { Suspense } from 'react';
import { useParams } from 'react-router-dom';
import { DEV_PREVIEWS, type DevPreviewKey } from './previews/registry';

/** Dev-only route (see App.tsx — never registered outside import.meta.env.DEV) rendering a single
 * component in isolation at a fixed URL, e.g. /dev-preview/stash-modal?scenario=allDistinct, so its
 * boundary visual states can be checked or screenshotted without reaching them through a real game
 * flow. Register new components in ./previews/registry.ts. */
function DevPreviewPage() {
  const { componentKey } = useParams<{ componentKey: string }>();
  const Preview = componentKey && componentKey in DEV_PREVIEWS ? DEV_PREVIEWS[componentKey as DevPreviewKey] : null;

  if (!Preview) {
    return (
      <div style={{ padding: 24, fontFamily: 'sans-serif' }}>
        <p>No dev preview registered for "{componentKey}".</p>
        <p>Available: {Object.keys(DEV_PREVIEWS).join(', ')}</p>
      </div>
    );
  }

  return (
    <Suspense fallback={null}>
      <Preview />
    </Suspense>
  );
}

export default DevPreviewPage;
