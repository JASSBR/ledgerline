import { TestBed } from '@angular/core/testing';
import { Money } from './money';

describe('Money', () => {
  function render(value: number, signed = false) {
    const fixture = TestBed.createComponent(Money);
    fixture.componentRef.setInput('value', value);
    fixture.componentRef.setInput('signed', signed);
    fixture.detectChanges();
    return fixture.nativeElement as HTMLElement;
  }

  it('sets cents and currency smaller than the euros, and reads as one amount', () => {
    const element = render(6400.5);

    expect(element.textContent!.replace(/\s/g, '')).toBe('6400,50€');
    expect(element.querySelector('.money')!.getAttribute('aria-label')).toMatch(/6\s?400,50\s€/);
    const minor = [...element.querySelectorAll('.minor')].map((part) => part.textContent).join('');
    expect(minor.replace(/\s/g, '')).toBe(',50€');
  });

  it('signs credits on demand', () => {
    expect(render(12, true).textContent!.replace(/\s/g, '')).toBe('+12,00€');
    expect(render(-12, true).textContent!.replace(/\s/g, '')).toMatch(/^-12,00€$/);
  });
});
