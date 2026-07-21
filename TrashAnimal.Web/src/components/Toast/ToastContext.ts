import { createContext } from 'react';

export interface ToastMessage {
  id: string;
  text: string;
  variant: 'error' | 'info';
}

export interface ToastContextValue {
  showToast: (text: string, variant?: ToastMessage['variant']) => void;
}

export const ToastContext = createContext<ToastContextValue | null>(null);
