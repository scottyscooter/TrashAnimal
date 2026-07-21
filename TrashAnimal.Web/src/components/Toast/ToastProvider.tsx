import { useCallback, useState, type ReactNode } from 'react';
import { ToastContext, type ToastMessage } from './ToastContext';

const AUTO_DISMISS_MS = 5000;

let nextToastId = 0;

function ToastProvider({ children }: { children: ReactNode }) {
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const showToast = useCallback((text: string, variant: ToastMessage['variant'] = 'error') => {
    const id = `toast-${nextToastId++}`;
    setToasts((current) => [...current, { id, text, variant }]);
    setTimeout(() => {
      setToasts((current) => current.filter((toast) => toast.id !== id));
    }, AUTO_DISMISS_MS);
  }, []);

  return (
    <ToastContext.Provider value={{ showToast }}>
      {children}
      <div className="fixed bottom-4 right-4 z-50 flex flex-col gap-2" role="status" aria-live="polite">
        {toasts.map((toast) => (
          <div
            key={toast.id}
            className={`rounded-lg px-4 py-2 text-sm text-white shadow-lg ${
              toast.variant === 'error' ? 'bg-red-600' : 'bg-neutral-800'
            }`}
          >
            {toast.text}
          </div>
        ))}
      </div>
    </ToastContext.Provider>
  );
}

export default ToastProvider;
