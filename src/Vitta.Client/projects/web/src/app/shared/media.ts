import { DestroyRef, inject, Signal, signal } from '@angular/core';

/** Signal que acompanha uma media query (ex.: celular = '(max-width: 720px)'). */
export function mediaQuery(query: string): Signal<boolean> {
  const list = matchMedia(query);
  const matches = signal(list.matches);
  const onChange = (e: MediaQueryListEvent) => matches.set(e.matches);
  list.addEventListener('change', onChange);
  inject(DestroyRef).onDestroy(() => list.removeEventListener('change', onChange));
  return matches.asReadonly();
}

export const MOBILE_QUERY = '(max-width: 720px)';
