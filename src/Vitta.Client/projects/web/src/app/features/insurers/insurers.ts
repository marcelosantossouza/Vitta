import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { InsurerDto, InsurersApi, problemMessage } from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'vt-insurers',
  imports: [ReactiveFormsModule, Icon, EmptyState],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Seguradoras</h1>
          <p class="subtitle">Companhias com as quais você trabalha.</p>
        </div>
      </div>

      <div class="layout">
        <section>
          @if (loading()) {
            <div class="grid">@for (i of [1, 2, 3]; track i) { <div class="card"><div class="skeleton" style="height: 3rem"></div></div> }</div>
          } @else if (insurers().length === 0) {
            <div class="card"><vt-empty-state icon="building" title="Nenhuma seguradora" text="Cadastre ao lado as seguradoras com que você trabalha." /></div>
          } @else {
            <div class="grid">
              @for (insurer of insurers(); track insurer.id) {
                <div class="card insurer">
                  <span class="insurer__code">{{ insurer.code.slice(0, 3) }}</span>
                  <div>
                    <strong>{{ insurer.name }}</strong>
                    <span class="muted small mono">{{ insurer.code }}</span>
                  </div>
                </div>
              }
            </div>
          }
        </section>

        <form class="card add" [formGroup]="form" (ngSubmit)="submit()" novalidate>
          <h2>Adicionar seguradora</h2>
          <label class="field">
            Código *
            <input formControlName="code" placeholder="Ex.: MAG" maxlength="20" />
            <span class="hint">Sigla curta usada nas importações.</span>
          </label>
          <label class="field">
            Nome *
            <input formControlName="name" placeholder="Ex.: MAG Seguros" maxlength="120" />
          </label>
          @if (error(); as message) {
            <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
          }
          <button class="btn btn--primary" type="submit" [disabled]="saving()"><vt-icon name="plus" [size]="18" /> Adicionar</button>
        </form>
      </div>
    </div>
  `,
  styles: `
    .layout { display: grid; gap: 1rem; grid-template-columns: minmax(0, 1fr) 320px; align-items: start; }
    .add { display: flex; flex-direction: column; gap: 1rem; position: sticky; top: 1rem; }
    .insurer { display: flex; align-items: center; gap: 0.9rem;
      div { display: flex; flex-direction: column; min-width: 0; } }
    .insurer__code {
      display: grid; place-items: center; width: 46px; height: 46px; border-radius: 12px; flex-shrink: 0;
      background: var(--vt-primary-soft); color: var(--vt-primary); font-weight: 800; font-size: 0.8rem;
    }
    @media (max-width: 900px) { .layout { grid-template-columns: 1fr; } .add { position: static; } }
  `,
})
export class Insurers {
  private readonly api = inject(InsurersApi);
  private readonly toast = inject(ToastService);

  protected readonly insurers = signal<InsurerDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    code: ['', [Validators.required, Validators.maxLength(20)]],
    name: ['', [Validators.required, Validators.maxLength(120)]],
  });

  constructor() {
    this.load();
  }

  private load(): void {
    this.api.list().subscribe({
      next: list => {
        this.insurers.set(list);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, 'Não foi possível carregar as seguradoras.');
      },
    });
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api.create({ code: v.code.trim().toUpperCase(), name: v.name.trim() }).subscribe({
      next: () => {
        this.toast.success('Seguradora adicionada.');
        this.form.reset();
        this.saving.set(false);
        this.load();
      },
      error: err => {
        this.saving.set(false);
        this.error.set(problemMessage(err, 'Erro ao adicionar seguradora.'));
      },
    });
  }
}
