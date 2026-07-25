import en from '../../../../public/assets/i18n/en.json';
import hu from '../../../../public/assets/i18n/hu.json';

/** Every leaf key path in a translation object, e.g. "rides.view.map". */
function keyPaths(value: unknown, prefix = ''): string[] {
  if (value === null || typeof value !== 'object') {
    return [prefix];
  }
  return Object.entries(value as Record<string, unknown>).flatMap(([key, child]) =>
    keyPaths(child, prefix ? `${prefix}.${key}` : key),
  );
}

describe('translation parity', () => {
  it('the Hungarian bundle has exactly the same keys as the English one', () => {
    expect(keyPaths(hu).sort()).toEqual(keyPaths(en).sort());
  });
});
