import { TestBed } from '@angular/core/testing';
import { ThemeService } from './theme';

describe('ThemeService', () => {
  beforeEach(() => localStorage.clear());

  it('applies the theme to the document and remembers the choice', () => {
    localStorage.setItem('ledgerline.theme', 'light');
    const theme = TestBed.inject(ThemeService);

    theme.toggle();
    TestBed.tick();

    expect(document.documentElement.dataset['theme']).toBe('dark');
    expect(localStorage.getItem('ledgerline.theme')).toBe('dark');
  });
});
