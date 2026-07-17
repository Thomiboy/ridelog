import { decodePolyline } from './polyline-decoder';

describe('decodePolyline', () => {
  it('decodes the reference route from the Google polyline spec', () => {
    // Same worked example the backend encoder is verified against.
    const points = decodePolyline('_p~iF~ps|U_ulLnnqC_mqNvxq`@');

    expect(points.length).toBe(3);
    expect(points[0][0]).toBeCloseTo(38.5, 4);
    expect(points[0][1]).toBeCloseTo(-120.2, 4);
    expect(points[2][0]).toBeCloseTo(43.252, 4);
    expect(points[2][1]).toBeCloseTo(-126.453, 4);
  });

  it('decodes an empty string to an empty route', () => {
    expect(decodePolyline('')).toEqual([]);
  });
});
