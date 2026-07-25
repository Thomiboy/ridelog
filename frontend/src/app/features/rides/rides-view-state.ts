import { Injectable, signal } from '@angular/core';

/** Which Rides view is showing: the paged list, the all-routes coverage map, or the calendar. */
export type RidesView = 'list' | 'map' | 'calendar';

/**
 * The Rides page's current view, held outside the component so it survives navigating to a ride's
 * detail and back — returning restores the view you left from. Defaults to the calendar.
 */
@Injectable({ providedIn: 'root' })
export class RidesViewState {
  readonly view = signal<RidesView>('calendar');
}
