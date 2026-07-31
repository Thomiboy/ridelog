import { TranslocoDatePipe, TranslocoDecimalPipe } from '@jsverse/transloco-locale';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatTooltipModule } from '@angular/material/tooltip';
import { MatCardModule } from '@angular/material/card';
import type { ChartOptions } from 'chart.js';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { DurationPipe } from '../../core/format/duration.pipe';
import { SourceChips } from '../../shared/source-chips/source-chips';
import { Chart } from '../../shared/chart/chart';
import {
  availableChannels,
  buildComparisonMetricChart,
  buildMetricSeriesChart,
  channelAxisId,
  defaultChannels,
  hasGraphableSeries,
  toggleChannel,
  type MetricAxis,
  type MetricChannel,
} from './metric-series-chart';
import { buildHrZoneChart } from './hr-zone-chart';
import { WEATHER_AXIS_ID, summariseWeather, withHeadwindLayer } from './weather-layer';
import { compareRides, type MetricDelta } from './ride-comparison';
import { RidePicker } from './ride-picker';
import type { RideDetail as RideDetailDto, RideSummary } from '../../core/api/ride.models';

@Component({
  selector: 'app-ride-detail',
  imports: [
    TranslocoPipe,
    TranslocoDatePipe,
    TranslocoDecimalPipe,
    DurationPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatTooltipModule,
    MatCardModule,
    SourceChips,
    Chart,
    RidePicker,
  ],
  // DurationPipe is also injected (see displayMetric) to localize the comparison-panel duration.
  providers: [DurationPipe],
  templateUrl: './ride-detail.html',
  styleUrl: './ride-detail.scss',
})
export class RideDetail {
  private readonly route = inject(ActivatedRoute);
  private readonly router = inject(Router);
  private readonly ridesService = inject(RidesService);
  private readonly mapState = inject(MapState);
  private readonly sheetState = inject(SheetState);
  private readonly auth = inject(AuthService);
  private readonly transloco = inject(TranslocoService);
  private readonly language = inject(LanguageService);
  private readonly duration = inject(DurationPipe);

  readonly isAdmin = this.auth.isAdmin;

  /** Localized dataset labels for the elevation/HR/temperature graph; recomputes on language change. */
  private readonly metricLabels = computed(() => {
    this.language.current();
    return {
      elevation: this.transloco.translate('rideDetail.elevation'),
      heartRate: this.transloco.translate('rideDetail.card.heartRate'),
      temperature: this.transloco.translate('rideDetail.card.temperature'),
      speed: this.transloco.translate('rideDetail.card.speed'),
    };
  });

  readonly ride = signal<RideDetailDto | null>(null);

  /** The ride being compared against (full detail), and whether the picker is open. */
  readonly compareRide = signal<RideDetailDto | null>(null);
  readonly pickerOpen = signal(false);
  /** All cycling rides for the compare picker; null until first opened. */
  readonly allRides = signal<RideSummary[] | null>(null);

  /** X-axis of the metric graph: cumulative distance or elapsed time. */
  readonly metricAxis = signal<MetricAxis>('distance');

  /** The channels this ride actually recorded — the only ones worth offering in the picker. */
  readonly channelOptions = computed(() => availableChannels(this.ride()?.metricSeries ?? []));

  /** The user's picks, or null before they touch it (then the default pair for this ride applies). */
  private readonly pickedChannels = signal<MetricChannel[] | null>(null);

  /**
   * The two channels on the plot. Every ride opens on the default pair — the choice isn't carried
   * across rides — and a pick that this ride can't offer falls back to what it recorded.
   */
  readonly shownChannels = computed(() => {
    const options = this.channelOptions();
    const picked = this.pickedChannels()?.filter((channel) => options.includes(channel)) ?? [];
    return picked.length > 0 ? picked : defaultChannels(options);
  });

  readonly metricChart = computed(() => {
    const ride = this.ride();
    const series = ride?.metricSeries;
    if (!series || !hasGraphableSeries(series)) {
      return null;
    }

    const chart = buildMetricSeriesChart(series, this.metricAxis(), this.shownChannels(), this.metricLabels());

    // Weather rides along as a layer, never as a channel: nothing on the bike measured it, so it
    // stays out of the picker and off the channels' axes (docs/adr/0005).
    return withHeadwindLayer(chart, series, ride.weather, ride.startTime, this.transloco.translate('rideDetail.headwind'));
  });

  /** Labels the picker buttons, so the toggle reads in the active language. */
  channelLabel(channel: MetricChannel): string {
    return this.metricLabels()[channel];
  }

  isChannelShown(channel: MetricChannel): boolean {
    return this.shownChannels().includes(channel);
  }

  toggleChannel(channel: MetricChannel): void {
    this.pickedChannels.set(toggleChannel(this.shownChannels(), channel));
  }

  /** Per-metric comparison of the current ride against the selected one; null outside compare mode. */
  readonly deltas = computed<MetricDelta[] | null>(() => {
    const current = this.ride();
    const other = this.compareRide();
    return current && other ? compareRides(current, other) : null;
  });

  /** The overlaid two-ride graph in compare mode; the single-ride graph otherwise. */
  readonly graphChart = computed(() => {
    const other = this.compareRide();
    if (!other) {
      return this.metricChart();
    }
    const current = this.ride()?.metricSeries ?? [];
    const compare = other.metricSeries ?? [];
    return current.length > 0 || compare.length > 0
      ? buildComparisonMetricChart(current, compare, this.metricAxis(), this.shownChannels(), this.metricLabels())
      : null;
  });

  /** The weather card's figures; null when no lookup has stored any for this ride. */
  readonly weather = computed(() => summariseWeather(this.ride()?.weather));

  /** How much wind there was either way, since the direction is carried by the label beside it. */
  readonly absHeadwind = computed(() => Math.abs(this.weather()?.meanHeadwindKmh ?? 0));

  /**
   * Which way the wind went on balance. Under about 2 km/h either way there was no useful wind
   * along the route — on a loop that is the normal outcome — so it reads as a crosswind rather than
   * claiming a direction the numbers cannot support.
   */
  private readonly windDirection = computed<'head' | 'tail' | 'cross'>(() => {
    const mean = this.weather()?.meanHeadwindKmh ?? 0;
    if (Math.abs(mean) < 2) {
      return 'cross';
    }
    return mean > 0 ? 'head' : 'tail';
  });

  readonly windLabel = computed(() => `rideDetail.weather.${this.windDirection()}wind`);

  readonly windIcon = computed(() => (this.windDirection() === 'head' ? 'trending_up' : 'trending_flat'));

  readonly hrZoneChart = computed(() => {
    const zones = this.ride()?.hrZones;
    return zones && zones.some((z) => z.minutes > 0) ? buildHrZoneChart(zones) : null;
  });

  /**
   * One y-axis per selected channel — the first on the left, the second on the right — so each line
   * is read in its own units rather than sharing a scale with an unrelated metric.
   */
  private channelScales(): NonNullable<ChartOptions<'line'>['scales']> {
    return Object.fromEntries(
      this.shownChannels().map((channel, index) => [
        channelAxisId(channel),
        index === 0
          ? { type: 'linear' as const, position: 'left' as const }
          : { type: 'linear' as const, position: 'right' as const, grid: { drawOnChartArea: false } },
      ]),
    );
  }

  /**
   * The wind's own axis, hidden and unlabelled: the line is there to show when the rider was working
   * against it, not to be read off in km/h — that is what the weather card is for.
   */
  private weatherScale(): NonNullable<ChartOptions<'line'>['scales']> {
    return this.ride()?.weather?.length
      ? { [WEATHER_AXIS_ID]: { type: 'linear' as const, display: false, position: 'right' as const } }
      : {};
  }

  readonly graphOptions = computed<ChartOptions<'line'>>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: 'index' },
    scales: { ...this.channelScales(), ...this.weatherScale() },
  }));

  // Comparison overlay uses a real-value linear x-axis so rides of different length keep their ranges.
  readonly comparisonGraphOptions = computed<ChartOptions<'line'>>(() => ({
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: 'nearest' },
    scales: { x: { type: 'linear' }, ...this.channelScales() },
  }));

  /** Formats a comparison metric value with its unit (dash when absent). */
  displayMetric(key: string, value: number | null): string {
    if (value === null) {
      return '—';
    }
    switch (key) {
      case 'distance':
        return `${this.round(value, 1)} km`;
      case 'duration':
        return this.duration.transform(value);
      case 'avgSpeed':
      case 'maxSpeed':
        return `${this.round(value, 1)} km/h`;
      case 'avgHeartRate':
      case 'maxHeartRate':
        return `${Math.round(value)} bpm`;
      case 'elevation':
        return `${Math.round(value)} m`;
      case 'calories':
        return `${Math.round(value)} kcal`;
      default:
        return String(value);
    }
  }

  /** The signed delta with its unit (e.g. "+10 km", "−5 bpm"); dash when it can't be computed. */
  displayDelta(delta: MetricDelta): string {
    if (delta.delta === null) {
      return '—';
    }
    const sign = delta.delta > 0 ? '+' : delta.delta < 0 ? '−' : '';
    return `${sign}${this.displayMetric(delta.key, Math.abs(delta.delta))}`;
  }

  private round(value: number, digits: number): number {
    const factor = 10 ** digits;
    return Math.round(value * factor) / factor;
  }

  openPicker(): void {
    if (this.allRides() === null) {
      this.ridesService.getAllRides().subscribe((rides) => this.allRides.set(rides));
    }
    this.pickerOpen.set(true);
  }

  closePicker(): void {
    this.pickerOpen.set(false);
  }

  choose(other: RideSummary): void {
    this.pickerOpen.set(false);
    this.ridesService.getRide(other.id).subscribe((ride) => {
      this.compareRide.set(ride);
      // Overlay both routes on the background map (drops any ride without a polyline).
      const polylines = [this.ride()?.routePolyline, ride.routePolyline].filter((p): p is string => !!p);
      this.mapState.showRoutes(polylines);
    });
  }

  exitCompare(): void {
    this.compareRide.set(null);
    this.pickerOpen.set(false);
    const current = this.ride();
    this.mapState.showRoute(current?.routePolyline, current?.restStops ?? []);
  }

  setAxis(axis: MetricAxis): void {
    this.metricAxis.set(axis);
  }

  goToPrevious(): void {
    this.step(this.ride()?.previousId);
  }

  goToNext(): void {
    this.step(this.ride()?.nextId);
  }

  private step(id: string | null | undefined): void {
    if (id) {
      this.router.navigateByUrl(`/rides/${id}`);
    }
  }

  /** Admin: re-parse this ride's stored files, then reload it to show the refreshed metrics/graph. */
  reprocess(): void {
    const id = this.ride()?.id;
    if (!id) {
      return;
    }
    this.ridesService.reprocessRide(id).subscribe(() => this.load(id));
  }

  private load(id: string): void {
    this.ridesService.getRide(id).subscribe((ride) => {
      this.ride.set(ride);
      // The route (with its rest markers) draws on the global background map instead of an embedded one.
      this.mapState.showRoute(ride.routePolyline, ride.restStops ?? []);
    });
  }

  constructor() {
    // React to every id change (the stepper navigates between /rides/:id without recreating this
    // component, so reading the snapshot once would leave the page and map on the old ride).
    this.route.paramMap.pipe(takeUntilDestroyed()).subscribe((params) => {
      const id = params.get('id');
      if (!id) {
        return;
      }
      // Snap to half so the selected ride's route stays visible on the background map.
      this.sheetState.request('half');
      // Stepping to another ride leaves any comparison behind.
      this.compareRide.set(null);
      this.pickerOpen.set(false);
      this.load(id);
    });
  }
}
