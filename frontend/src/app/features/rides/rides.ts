import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-rides',
  imports: [TranslocoPipe],
  template: `
    <section class="page">
      <h1>{{ 'rides.title' | transloco }}</h1>
      <p>{{ 'rides.placeholder' | transloco }}</p>
    </section>
  `,
})
export class Rides {}
