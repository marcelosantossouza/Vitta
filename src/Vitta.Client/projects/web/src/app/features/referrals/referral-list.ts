import { ChangeDetectionStrategy, Component, inject, signal } from '@angular/core';
import { Router, RouterLink } from '@angular/router';
import { problemMessage, ReferralGroupDto, ReferralsApi } from '@vitta/core';
import { Icon } from '../../shared/icon';
import { BrlPipe, PercentPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ReferralForm } from './referral-form';

/** Indicações: grupos de segurados que vieram por uma pessoa, empresa, sindicato ou código. */
@Component({
  selector: 'vt-referral-list',
  imports: [RouterLink, Icon, BrlPipe, PercentPipe, ReferralForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="page">
      <div class="page-header">
        <div>
          <h1>Indicações</h1>
          <p class="subtitle">Carimbe os segurados com quem indicou (pessoa, empresa, sindicato ou um código) e acompanhe o resultado
            de cada grupo mês a mês. O relatório de cada grupo pode ser salvo em PDF.</p>
        </div>
        <div class="actions">
          <button class="btn btn--primary" type="button" (click)="formOpen.set(true)"><vt-icon name="plus" [size]="18" /> Nova indicação</button>
        </div>
      </div>

      @if (groups() === null) {
        <div class="grid">@for (i of [1, 2, 3]; track i) { <div class="card"><div class="skeleton" style="height: 6rem"></div></div> }</div>
      } @else if (groups()!.length === 0) {
        <div class="card empty">
          <vt-icon name="link" [size]="28" />
          <h2>Nenhuma indicação ainda</h2>
          <p class="muted">Crie um grupo para quem indica clientes (ex.: "Sindicato dos Professores" ou o código "JOAO-01") e depois
            adicione os segurados que vieram por ele.</p>
          <button class="btn btn--primary" type="button" (click)="formOpen.set(true)"><vt-icon name="plus" [size]="18" /> Nova indicação</button>
        </div>
      } @else {
        <div class="grid">
          @for (g of groups(); track g.id) {
            <a class="card group" [routerLink]="['/indicacoes', g.id]">
              <div class="group__head">
                <strong>{{ g.name }}</strong>
                @if (g.code) { <span class="badge">{{ g.code }}</span> }
              </div>
              <span class="muted small">{{ g.activeMembers }} ativo(s) de {{ g.members }} segurado(s)</span>
              <dl>
                <div><dt>Prêmio mensal</dt><dd>{{ g.monthlyPremium | brl }}</dd></div>
                <div><dt>Comissão (estimada)</dt><dd>{{ g.monthlyCommission === null ? '—' : (g.monthlyCommission | brl) }}</dd></div>
                @if (g.sharePercent !== null) {
                  <div><dt>{{ g.shareLabel }} ({{ g.sharePercent | pct }})</dt><dd>{{ g.monthlyShare === null ? '—' : (g.monthlyShare | brl) }}</dd></div>
                }
              </dl>
            </a>
          }
        </div>
      }
    </div>

    @if (formOpen()) {
      <vt-referral-form (closed)="formOpen.set(false)" (saved)="created($event)" />
    }
  `,
  styles: `
    .grid { display: grid; gap: 1rem; grid-template-columns: repeat(auto-fill, minmax(min(100%, 280px), 1fr)); }
    .group { display: flex; flex-direction: column; gap: 0.4rem; text-decoration: none; color: inherit; transition: box-shadow 0.15s;
      &:hover { box-shadow: var(--vt-shadow-lg); } }
    .group__head { display: flex; align-items: center; justify-content: space-between; gap: 0.5rem; strong { font-size: 1.05rem; } }
    dl { margin: 0.4rem 0 0; display: grid; gap: 0.3rem;
      div { display: flex; justify-content: space-between; gap: 0.5rem; font-size: 0.875rem; }
      dt { color: var(--vt-muted); } dd { margin: 0; font-weight: 650; font-variant-numeric: tabular-nums; } }
    .empty { display: flex; flex-direction: column; align-items: center; gap: 0.6rem; text-align: center; padding: 2.5rem 1rem;
      color: var(--vt-muted); h2 { color: var(--vt-text); margin: 0; } p { max-width: 520px; margin: 0; } }
  `,
})
export class ReferralList {
  private readonly api = inject(ReferralsApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);

  protected readonly groups = signal<ReferralGroupDto[] | null>(null);
  protected readonly formOpen = signal(false);

  constructor() {
    this.api.list().subscribe({
      next: list => this.groups.set(list),
      error: err => {
        this.groups.set([]);
        this.toast.error(err, problemMessage(err, 'Não foi possível carregar as indicações.'));
      },
    });
  }

  protected created(id: string): void {
    this.formOpen.set(false);
    void this.router.navigate(['/indicacoes', id], { queryParams: { adicionar: 1 } });
  }
}
