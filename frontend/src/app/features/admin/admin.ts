import { Component } from '@angular/core';
import { TranslocoPipe } from '@jsverse/transloco';

@Component({
  selector: 'app-admin',
  imports: [TranslocoPipe],
  template: `
    <section class="page">
      <h1>{{ 'admin.title' | transloco }}</h1>
      <p>{{ 'admin.placeholder' | transloco }}</p>
    </section>
  `,
})
export class Admin {}
