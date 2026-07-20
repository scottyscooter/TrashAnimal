import type { ReactElement } from 'react';
import { render, type RenderOptions } from '@testing-library/react';
import { MemoryRouter } from 'react-router-dom';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

interface CustomRenderOptions extends Omit<RenderOptions, 'wrapper'> {
  initialRoute?: string;
}

function createTestQueryClient() {
  return new QueryClient({
    defaultOptions: {
      queries: { retry: false },
      mutations: { retry: false },
    },
  });
}

const AllProviders = ({
  children,
  initialRoute = '/'
}: {
  children: React.ReactNode;
  initialRoute?: string;
}) => {
  return (
    <QueryClientProvider client={createTestQueryClient()}>
      <MemoryRouter initialEntries={[initialRoute]}>
        {children}
      </MemoryRouter>
    </QueryClientProvider>
  );
};

const customRender = (
  ui: ReactElement,
  { initialRoute = '/', ...options }: CustomRenderOptions = {},
) => {
  return render(ui, {
    wrapper: ({ children }) => (
      <AllProviders initialRoute={initialRoute}>{children}</AllProviders>
    ),
    ...options,
  });
};

export * from '@testing-library/react';
export { customRender as render };
