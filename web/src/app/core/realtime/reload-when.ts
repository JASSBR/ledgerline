import { effect, untracked } from '@angular/core';

interface Reloadable {
  reload(): boolean;
}

/**
 * Refreshes resources when `trigger` changes, keeping what is on screen while the new data loads.
 * Changing a resource's request instead would reset it to "loading" with no value: the view would flash a skeleton
 * and destroy whatever the user was doing (an open form, a file being picked). reload() keeps the current value.
 * Must be called in an injection context (a component field initialiser).
 */
export function reloadWhen(trigger: () => unknown, ...resources: Reloadable[]): void {
  let initial = true;
  effect(() => {
    trigger();
    if (initial) {
      initial = false;
      return;
    }
    untracked(() => resources.forEach((resource) => resource.reload()));
  });
}
