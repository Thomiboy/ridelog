import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-dashboard',
  imports: [TranslocoPipe],
  template: `
    <section class="page">
      <h1>{{ 'dashboard.title' | transloco }}</h1>
      <p>{{ 'dashboard.placeholder' | transloco }}</p>
    </section>
  `,
})
export class Dashboard {}
