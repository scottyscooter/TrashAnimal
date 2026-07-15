import { describe, it, expect } from 'vitest';
import { render, screen } from './test-utils';

describe('Example Test Suite', () => {
  it('renders text content', () => {
    render(<div>Hello World</div>);
    expect(screen.getByText('Hello World')).toBeInTheDocument();
  });

  it('performs basic assertions', () => {
    const value = 2 + 2;
    expect(value).toBe(4);
  });
});
