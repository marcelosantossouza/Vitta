import { ChangeDetectionStrategy, Component, inject, input, OnInit, output, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { problemMessage, ReferralGroupDto, ReferralsApi } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

/** Dialogo para criar/editar um grupo de indicação. Emite o id salvo. */
@Component({
  selector: 'vt-referral-form',
  imports: [ReactiveFormsModule, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="dialog-backdrop" (click)="closed.emit()">
      <form class="dialog" [formGroup]="form" (ngSubmit)="save()" (click)="$event.stopPropagation()" novalidate>
        <div class="dialog__header">
          <h2>{{ group() ? 'Editar indicação' : 'Nova indicação' }}</h2>
          <button class="btn btn--ghost btn--icon" type="button" (click)="closed.emit()" aria-label="Fechar"><vt-icon name="x" /></button>
        </div>
        <div class="form-grid">
          <label class="field span-all">Nome de quem indicou *
            <input formControlName="name" placeholder="Ex.: Sindicato dos Professores, Empresa X, João da Silva" />
            @if (form.controls.name.touched && form.controls.name.invalid) { <span class="field-error">Informe o nome.</span> }
          </label>
          <label class="field">Código (opcional)
            <input formControlName="code" placeholder="Ex.: SINDPROF" maxlength="30" />
            <span class="hint">Use um código se preferir não mostrar o nome no relatório.</span>
          </label>
          <label class="field">Percentual sobre a comissão
            <input type="number" min="0" max="100" step="0.01" inputmode="decimal" formControlName="sharePercent" placeholder="Ex.: 10" />
            @if (form.controls.sharePercent.invalid) { <span class="field-error">Entre 0 e 100.</span> }
          </label>
          <label class="field">Nome do percentual nos relatórios
            <input formControlName="shareLabel" placeholder="Repasse" maxlength="40" list="share-labels" />
            <datalist id="share-labels">
              @for (l of labels; track l) { <option [value]="l"></option> }
            </datalist>
          </label>
          <label class="field span-all">Anotações<textarea formControlName="notes" rows="2" placeholder="Contato, acordo combinado…"></textarea></label>
        </div>
        <p class="muted small">Pela lei, só corretor habilitado pode receber comissão de corretagem. Antes de pagar qualquer valor a quem
          indicou, confirme o formato com o contador.</p>
        @if (error(); as message) {
          <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
        }
        <div class="form-actions">
          <button class="btn" type="button" (click)="closed.emit()">Cancelar</button>
          <button class="btn btn--primary" type="submit" [disabled]="busy()">{{ busy() ? 'Salvando…' : 'Salvar' }}</button>
        </div>
      </form>
    </div>
  `,
})
export class ReferralForm implements OnInit {
  private readonly api = inject(ReferralsApi);
  private readonly toast = inject(ToastService);

  readonly group = input<ReferralGroupDto | null>(null);
  readonly closed = output<void>();
  readonly saved = output<string>();

  protected readonly labels = ['Repasse', 'Bonificação', 'Participação', 'Taxa de parceria', 'Incentivo'];
  protected readonly busy = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    name: ['', [Validators.required, Validators.maxLength(120)]],
    code: [''],
    sharePercent: [null as number | null, [Validators.min(0), Validators.max(100)]],
    shareLabel: ['Repasse'],
    notes: [''],
  });

  ngOnInit(): void {
    const g = this.group();
    if (g) {
      this.form.reset({ name: g.name, code: g.code ?? '', sharePercent: g.sharePercent, shareLabel: g.shareLabel, notes: g.notes ?? '' });
    }
  }

  protected save(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }
    const v = this.form.getRawValue();
    const percent = v.sharePercent === null || (v.sharePercent as unknown) === '' ? null : Number(v.sharePercent);
    const input = {
      name: v.name.trim(),
      code: v.code.trim() || null,
      notes: v.notes.trim() || null,
      sharePercent: percent,
      shareLabel: v.shareLabel.trim() || null,
    };
    const current = this.group();
    this.busy.set(true);
    this.error.set(null);
    const done = (id: string) => {
      this.busy.set(false);
      this.toast.success(current ? 'Indicação atualizada.' : 'Indicação criada.');
      this.saved.emit(id);
    };
    const fail = (err: unknown) => {
      this.busy.set(false);
      this.error.set(problemMessage(err, 'Não foi possível salvar.'));
    };
    if (current) this.api.update(current.id, input).subscribe({ next: () => done(current.id), error: fail });
    else this.api.create(input).subscribe({ next: r => done(r.id), error: fail });
  }
}
