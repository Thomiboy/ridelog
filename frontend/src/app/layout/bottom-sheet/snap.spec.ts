import { SNAP_HEIGHTS, chooseSnap } from './snap';

describe('chooseSnap', () => {
  // Snap heights (viewport fractions): collapsed 0.12, half 0.55, full 0.92.
  it('exposes the agreed snap heights', () => {
    expect(SNAP_HEIGHTS.collapsed).toBeCloseTo(0.12, 2);
    expect(SNAP_HEIGHTS.half).toBeCloseTo(0.55, 2);
    expect(SNAP_HEIGHTS.full).toBeCloseTo(0.92, 2);
  });

  it('snaps a low drag to collapsed', () => {
    expect(chooseSnap(0.2)).toBe('collapsed'); // 0.08 from collapsed vs 0.35 from half
  });

  it('snaps a mid drag to half', () => {
    expect(chooseSnap(0.4)).toBe('half');
  });

  it('snaps just above half to half', () => {
    expect(chooseSnap(0.7)).toBe('half'); // 0.15 from half vs 0.22 from full
  });

  it('snaps a high drag to full', () => {
    expect(chooseSnap(0.8)).toBe('full'); // 0.25 from half vs 0.12 from full
  });
});
