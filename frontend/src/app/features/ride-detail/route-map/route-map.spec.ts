import { TestBed } from '@angular/core/testing';
import { vi, type Mock } from 'vitest';

vi.mock('leaflet', () => {
  const line: Record<string, unknown> = { getBounds: vi.fn(() => 'BOUNDS'), remove: vi.fn() };
  line['addTo'] = vi.fn(() => line);
  const map = { setView: vi.fn(), fitBounds: vi.fn(), remove: vi.fn() };
  const tile = { addTo: vi.fn() };
  return {
    map: vi.fn(() => map),
    tileLayer: vi.fn(() => tile),
    polyline: vi.fn(() => line),
    __map: map,
    __line: line,
  };
});

import * as L from 'leaflet';
import { RouteMap } from './route-map';

describe('RouteMap', () => {
  beforeEach(() => vi.clearAllMocks());

  function render(polyline: string | null) {
    TestBed.configureTestingModule({ imports: [RouteMap] });
    const fixture = TestBed.createComponent(RouteMap);
    fixture.componentRef.setInput('polyline', polyline);
    fixture.detectChanges(); // triggers ngAfterViewInit → map init + draw
    return fixture;
  }

  it('draws the decoded route and fits the map to its bounds', () => {
    render('_p~iF~ps|U_ulLnnqC_mqNvxq`@');

    expect(L.polyline).toHaveBeenCalled();
    const coords = (L.polyline as unknown as Mock).mock.calls[0][0] as [number, number][];
    expect(coords[0][0]).toBeCloseTo(38.5, 4);
    expect(coords[0][1]).toBeCloseTo(-120.2, 4);

    expect((L as unknown as { __map: { fitBounds: Mock } }).__map.fitBounds).toHaveBeenCalledWith('BOUNDS');
  });

  it('adds an OpenStreetMap tile layer with attribution', () => {
    render('_p~iF~ps|U_ulLnnqC_mqNvxq`@');

    expect(L.tileLayer).toHaveBeenCalled();
    const options = (L.tileLayer as unknown as Mock).mock.calls[0][1] as { attribution: string };
    expect(options.attribution).toContain('OpenStreetMap');
  });

  it('does not draw a route when there is no polyline', () => {
    render(null);

    expect(L.polyline).not.toHaveBeenCalled();
  });
});
