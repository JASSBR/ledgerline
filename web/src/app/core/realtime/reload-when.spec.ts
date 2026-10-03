import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { reloadWhen } from './reload-when';

describe('reloadWhen', () => {
  it('reloads on each change of the trigger, but not on creation', () => {
    const trigger = signal(0);
    const resource = { reload: vi.fn(() => true) };

    @Component({ template: '' })
    class Host {
      constructor() {
        reloadWhen(trigger, resource);
      }
    }

    TestBed.createComponent(Host);
    TestBed.tick();
    expect(resource.reload).not.toHaveBeenCalled();

    trigger.set(1);
    TestBed.tick();
    trigger.set(2);
    TestBed.tick();
    expect(resource.reload).toHaveBeenCalledTimes(2);
  });
});
