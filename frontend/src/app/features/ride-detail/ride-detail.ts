import { DatePipe, DecimalPipe } from '@angular/common';
import { Component, computed, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { TranslocoPipe, TranslocoService } from '@jsverse/transloco';
import { MatButtonModule } from '@angular/material/button';
import { MatIconModule } from '@angular/material/icon';
import { MatCardModule } from '@angular/material/card';
import type { ChartOptions } from 'chart.js';
import { RidesService } from '../../core/api/rides.service';
import { MapState } from '../../core/map/map-state';
import { AuthService } from '../../core/auth/auth.service';
import { LanguageService } from '../../core/i18n/language.service';
import { SheetState } from '../../layout/bottom-sheet/sheet-state';
import { formatDuration } from '../../core/format/duration';
import { SourceChips } from '../../shared/source-chips/source-chips';
import { Chart } from '../../shared/chart/chart';
import { buildComparisonMetricChart, buildMetricSeriesChart, hasGraphableSeries, type MetricAxis } from './metric-series-chart';
import { buildHrZoneChart } from './hr-zone-chart';
import { compareRides, type MetricDelta } from './ride-comparison';
import { RidePicker } from './ride-picker';
import type { RideDetail as RideDetailDto, RideSummary } from '../../core/api/ride.models';

@Component({
  selector: 'app-ride-detail',
  imports: [
    TranslocoPipe,
    DatePipe,
    DecimalPipe,
    RouterLink,
    MatButtonModule,
    MatIconModule,
    MatCardModule,
    SourceChips,
    Chart,
    RidePicker,
  ],
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

  readonly isAdmin = this.auth.isAdmin;

  /** Localized dataset labels for the elevation/HR/temperature graph; recomputes on language change. */
  private readonly metricLabels = computed(() => {
    this.language.current();
    return {
      elevation: this.transloco.translate('rideDetail.elevation'),
      heartRate: this.transloco.translate('rideDetail.card.heartRate'),
      temperature: this.transloco.translate('rideDetail.card.temperature'),
    };
  });

  readonly ride = signal<RideDetailDto | null>(null);

  /** The ride being compared against (full detail), and whether the picker is open. */
  readonly compareRide = signal<RideDetailDto | null>(null);
  readonly pickerOpen = signal(false);
  /** All cycling rides for the compare picker; null until first opened. */
  readonly allRides = signal<RideSummary[] | null>(null);

  /** X-axis of the elevation/HR graph: cumulative distance or elapsed time. */
  readonly metricAxis = signal<MetricAxis>('distance');

  readonly metricChart = computed(() => {
    const series = this.ride()?.metricSeries;
    return series && hasGraphableSeries(series) ? buildMetricSeriesChart(series, this.metricAxis(), this.metricLabels()) : null;
  });

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
      ? buildComparisonMetricChart(current, compare, this.metricAxis(), this.metricLabels())
      : null;
  });

  readonly hrZoneChart = computed(() => {
    const zones = this.ride()?.hrZones;
    return zones && zones.some((z) => z.minutes > 0) ? buildHrZoneChart(zones) : null;
  });

  // Elevation on the left axis, heart rate on the right, so the two share one plot.
  readonly graphOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: 'index' },
    scales: {
      elevation: { type: 'linear', position: 'left' },
      hr: { type: 'linear', position: 'right', grid: { drawOnChartArea: false } },
      // Scales the temperature line without adding a third visible axis.
      temperature: { type: 'linear', position: 'right', display: false },
    },
  };

  // Comparison overlay uses a real-value linear x-axis so rides of different length keep their ranges.
  readonly comparisonGraphOptions: ChartOptions<'line'> = {
    responsive: true,
    maintainAspectRatio: false,
    interaction: { intersect: false, mode: 'nearest' },
    scales: {
      x: { type: 'linear' },
      elevation: { type: 'linear', position: 'left' },
      hr: { type: 'linear', position: 'right', grid: { drawOnChartArea: false } },
    },
  };

  /** Exposed for the template: renders `durationMinutes` as `1h 58m`. */
  readonly formatDuration = formatDuration;

  /** Formats a comparison metric value with its unit (dash when absent). */
  displayMetric(key: string, value: number | null): string {
    if (value === null) {
      return '—';
    }
    switch (key) {
      case 'distance':
        return `${this.round(value, 1)} km`;
      case 'duration':
        return formatDuration(value);
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
