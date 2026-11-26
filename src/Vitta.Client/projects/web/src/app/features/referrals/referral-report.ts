import { ChangeDetectionStrategy, Component, computed, inject, input, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { ActivatedRoute, Router, RouterLink } from '@angular/router';
import { ClientListItemDto, ClientsApi, problemMessage, ReferralReportDto, ReferralsApi, todayIso } from '@vitta/core';
import { debounceTime, Subject, switchMap } from 'rxjs';
import { Icon } from '../../shared/icon';
import { BrlPipe, IsoDatePipe, MonthPipe, PercentPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { ReferralForm } from './referral-form';

/**
 * Relatorio mensal de um grupo de indicação. "Exportar PDF" usa a impressao do navegador (Salvar como PDF);
 * o layout de impressao esconde menus e botoes. CPF e telefone nunca aparecem; valores por pessoa sao opcionais.
 */
@Component({
  selector: 'vt-referral-report',
  imports: [RouterLink, Icon, BrlPipe, IsoDatePipe, MonthPipe, PercentPipe, ReferralForm],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './referral-report.html',
  styleUrl: './referral-report.scss',
})
export class ReferralReport {
  private readonly api = inject(ReferralsApi);
  private readonly clientsApi = inject(ClientsApi);
  private readonly toast = inject(ToastService);
  private readonly router = inject(Router);
  private readonly route = inject(ActivatedRoute);

  readonly id = input.required<string>();

  protected readonly report = signal<ReferralReportDto | null>(null);
  protected readonly loading = signal(true);
  /** yyyy-MM do <input type="month">. */
  protected readonly month = signal(todayIso().slice(0, 7));
  protected readonly showValues = signal(false);
  protected readonly generatedAt = new Date().toLocaleDateString('pt-BR');
  protected readonly dialog = signal<'edit' | 'add' | null>(null);

  protected readonly maxShare = computed(() => Math.max(1, ...(this.report()?.history ?? []).map(h => h.premium)));

  // ---------- Adicionar segurados ----------
  protected readonly search = signal('');
  protected readonly candidates = signal<ClientListItemDto[] | null>(null);
  protected readonly picked = signal<Set<string>>(new Set());
  protected readonly busy = signal(false);
  private readonly search$ = new Subject<string>();

  constructor() {
    this.search$.pipe(debounceTime(250), switchMap(term => this.clientsApi.list(term, 1, 30)), takeUntilDestroyed()).subscribe({
      next: page => this.candidates.set(page.items),
      error: err => this.toast.error(err, 'Não foi possível buscar os clientes.'),
    });
    queueMicrotask(() => {
      this.load();
      if (this.route.snapshot.queryParamMap.get('adicionar')) this.openAdd();
    });
  }

  protected load(): void {
    this.loading.set(true);
    this.api.report(this.id(), `${this.month()}-01`).subscribe({
      next: r => {
        this.report.set(r);
        this.loading.set(false);
      },
      error: err => {
        this.loading.set(false);
        this.toast.error(err, problemMessage(err, 'Não foi possível carregar o relatório.'));
      },
    });
  }

  protected changeMonth(value: string): void {
    if (!value) return;
    this.month.set(value);
    this.load();
  }

  protected exportPdf(): void {
    const r = this.report();
    const previous = document.title;
    if (r) document.title = `Indicação ${r.group.code ?? r.group.name} - ${this.month()}`; // nome sugerido do arquivo
    window.print();
    document.title = previous;
  }

  protected openAdd(): void {
    this.picked.set(new Set());
    this.search.set('');
    this.candidates.set(null);
    this.dialog.set('add');
    this.search$.next('');
  }

  protected typed(term: string): void {
    this.search.set(term);
    this.search$.next(term.trim());
  }

  protected toggle(id: string, on: boolean): void {
    this.picked.update(s => {
      const next = new Set(s);
      if (on) next.add(id);
      else next.delete(id);
      return next;
    });
  }

  protected addPicked(): void {
    const ids = [...this.picked()];
    if (!ids.length) return;
    this.busy.set(true);
    this.api.assign(this.id(), ids).subscribe({
      next: n => {
        this.busy.set(false);
        this.toast.success(`${n} segurado(s) adicionado(s).`);
        this.closeDialog();
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.toast.error(err, 'Não foi possível adicionar.');
      },
    });
  }

  protected removeMember(clientId: string, name: string): void {
    if (!confirm(`Tirar ${name} desta indicação? O segurado continua na carteira.`)) return;
    this.api.assign(null, [clientId]).subscribe({
      next: () => {
        this.toast.success(`${name} saiu da indicação.`);
        this.load();
      },
      error: err => this.toast.error(err, 'Não foi possível remover.'),
    });
  }

  protected removeGroup(): void {
    const r = this.report();
    if (!r || !confirm(`Excluir a indicação "${r.group.name}"? Os segurados continuam na carteira, só perdem o carimbo.`)) return;
    this.api.delete(r.group.id).subscribe({
      next: () => {
        this.toast.success('Indicação excluída.');
        void this.router.navigate(['/indicacoes']);
      },
      error: err => this.toast.error(err, 'Não foi possível excluir.'),
    });
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    if (this.route.snapshot.queryParamMap.has('adicionar')) void this.router.navigate([], { queryParams: {}, replaceUrl: true });
  }

  protected saved(): void {
    this.dialog.set(null);
    this.load();
  }
}
