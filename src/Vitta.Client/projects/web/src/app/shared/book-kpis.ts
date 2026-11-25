import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { BookSummaryDto, PoliciesApi } from '@vitta/core';
import { Icon } from './icon';
import { BrlPipe, PercentPipe } from './pipes';

/**
 * Cards com os numeros da carteira cadastrada (so apolices ativas entram nos valores).
 * `view="clients"` destaca clientes e media por cliente; `view="policies"` destaca apolices e % medio.
 */
@Component({
  selector: 'vt-book-kpis',
  imports: [Icon, BrlPipe, PercentPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    @if (summary(); as s) {
      <div class="kpis">
        @if (view() === 'clients') {
          <div class="card kpi">
            <span class="kpi__icon"><vt-icon name="users" /></span>
            <div>
              <span class="kpi__label">Clientes com apólice ativa</span>
              <strong class="kpi__value">{{ s.clientsWithActivePolicies }}</strong>
              <span class="kpi__hint">de {{ s.totalClients }} cadastrados</span>
            </div>
          </div>
        } @else {
          <div class="card kpi">
            <span class="kpi__icon"><vt-icon name="policy" /></span>
            <div>
              <span class="kpi__label">Apólices ativas</span>
              <strong class="kpi__value">{{ s.activePolicies }}</strong>
              <span class="kpi__hint">{{ s.endedPolicies }} encerrada(s)</span>
            </div>
          </div>
        }
        <div class="card kpi">
          <span class="kpi__icon"><vt-icon name="money" /></span>
          <div>
            <span class="kpi__label">Prêmio mensal</span>
            <strong class="kpi__value">{{ s.monthlyPremium | brl }}</strong>
            <span class="kpi__hint">soma das apólices ativas</span>
          </div>
        </div>
        <div class="card kpi kpi--highlight">
          <span class="kpi__icon"><vt-icon name="trending-up" /></span>
          <div>
            <span class="kpi__label">Sua comissão mensal</span>
            <strong class="kpi__value">{{ s.monthlyCommission | brl }}</strong>
            <span class="kpi__hint">
              @if (s.activeWithoutRate > 0) { sem contar {{ s.activeWithoutRate }} apólice(s) sem % } @else { prêmio × % de cada apólice }
            </span>
          </div>
        </div>
        @if (view() === 'clients') {
          <div class="card kpi">
            <span class="kpi__icon"><vt-icon name="target" /></span>
            <div>
              <span class="kpi__label">Média por cliente</span>
              <strong class="kpi__value">{{ perClient().commission | brl }}</strong>
              <span class="kpi__hint">de comissão · {{ perClient().premium | brl }} de prêmio</span>
            </div>
          </div>
        } @else {
          <div class="card kpi">
            <span class="kpi__icon"><vt-icon name="target" /></span>
            <div>
              <span class="kpi__label">Comissão média</span>
              <strong class="kpi__value">{{ s.averageCommissionRate | pct }}</strong>
              <span class="kpi__hint">ponderada pelo prêmio</span>
            </div>
          </div>
        }
      </div>
    } @else if (loading()) {
      <div class="kpis">@for (i of [1, 2, 3, 4]; track i) { <div class="card"><div class="skeleton" style="height: 3.6rem"></div></div> }</div>
    }
  `,
})
export class BookKpis {
  readonly view = input<'clients' | 'policies'>('clients');

  protected readonly summary = signal<BookSummaryDto | null>(null);
  protected readonly loading = signal(true);
  protected readonly perClient = computed(() => {
    const s = this.summary();
    const n = s?.clientsWithActivePolicies ?? 0;
    return { premium: n ? s!.monthlyPremium / n : 0, commission: n ? s!.monthlyCommission / n : 0 };
  });

  private readonly api = inject(PoliciesApi);

  constructor() {
    this.reload();
  }

  /** Recarrega depois de criar/editar/encerrar apolices. */
  reload(): void {
    this.api.summary().subscribe({
      next: s => {
        this.summary.set(s);
        this.loading.set(false);
      },
      // Os cards sao complemento da tela: se falharem, a lista continua funcionando.
      error: () => this.loading.set(false),
    });
  }
}
