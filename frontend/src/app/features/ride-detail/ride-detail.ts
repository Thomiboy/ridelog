import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-ride-detail',
  imports: [TranslocoPipe],
  template: `
    <section class="page">
      <h1>{{ 'rideDetail.title' | transloco }}</h1>
      <p>{{ 'rideDetail.placeholder' | transloco }}</p>
    </section>
  `,
})
export class RideDetail {}
