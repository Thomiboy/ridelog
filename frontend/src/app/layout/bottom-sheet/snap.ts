export type SnapState = 'collapsed' | 'half' | 'full';

/** Sheet heights as viewport fractions for the three agreed snap points. */
export const SNAP_HEIGHTS: Record<SnapState, number> = {
  collapsed: 0.12,
  half: 0.55,
  full: 0.92,
};

/** Picks the snap state whose height is nearest to where the drag ended. */
export function chooseSnap(heightFraction: number): SnapState {
  return (Object.entries(SNAP_HEIGHTS) as [SnapState, number][]).reduce(
    (best, [state, height]) =>
      Math.abs(height - heightFraction) < Math.abs(SNAP_HEIGHTS[best] - heightFraction) ? state : best,
    'collapsed' as SnapState,
  );
}
