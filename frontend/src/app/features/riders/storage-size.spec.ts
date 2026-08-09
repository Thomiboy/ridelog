import { storageSize } from './storage-size';

describe('storageSize', () => {
  // Worked examples: a ride's raw files run 0.7–3 MB, and the shared budget is 32 GB.
  it('reads a size the way a person would say it', () => {
    expect(storageSize(0)).toBe('0 B');
    expect(storageSize(940)).toBe('940 B');
    expect(storageSize(3_500)).toBe('3.5 kB');
    expect(storageSize(3_500_000)).toBe('3.5 MB');
    expect(storageSize(12_000_000)).toBe('12 MB');
    expect(storageSize(2_400_000_000)).toBe('2.4 GB');
  });
});
