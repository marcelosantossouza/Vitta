import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ClientDetailDto, ClientsApi, InsurerDto, InsurersApi, PoliciesApi, PolicyDto, PolicyEndStatus,
  POLICY_STATUS_LABELS, problemMessage, todayIso,
} from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BrlPipe, IsoDatePipe, PercentPipe, PolicyStatusPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';
import { Observable } from 'rxjs';

@Component({
  selector: 'vt-client-detail',
  imports: [RouterLink, ReactiveFormsModule, Icon, EmptyState, BrlPipe, IsoDatePipe, PercentPipe, PolicyStatusPipe],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './client-detail.html',
  styleUrl: './client-detail.scss',
})
export class ClientDetail implements OnInit {
  private readonly clientsApi = inject(ClientsApi);
  private readonly policiesApi = inject(PoliciesApi);
  private readonly insurersApi = inject(InsurersApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);
  private readonly fb = inject(NonNullableFormBuilder);

  readonly id = input.required<string>();

  protected readonly client = signal<ClientDetailDto | null>(null);
  protected readonly insurers = signal<InsurerDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly busy = signal(false);
  protected readonly dialog = signal<'policy' | 'end' | null>(null);
  protected readonly endingPolicy = signal<PolicyDto | null>(null);
  /** Apolice aberta para edicao no dialogo (nulo = nova apolice). */
  protected readonly editingPolicy = signal<PolicyDto | null>(null);
  protected readonly dialogError = signal<string | null>(null);

  protected readonly endStatuses: { value: PolicyEndStatus; label: string }[] = [
    { value: 'Cancelled', label: POLICY_STATUS_LABELS.Cancelled },
    { value: 'Lapsed', label: POLICY_STATUS_LABELS.Lapsed },
    { value: 'BrokerChanged', label: POLICY_STATUS_LABELS.BrokerChanged },
  ];

  protected readonly activePolicies = computed(() => this.client()?.policies.filter(p => p.status === 'Active') ?? []);
  protected readonly monthlyPremium = computed(() => this.activePolicies().reduce((sum, p) => sum + p.monthlyPremium, 0));
  protected readonly monthlyCommission = computed(() => this.activePolicies().reduce((sum, p) => sum + (p.monthlyCommission ?? 0), 0));
  protected readonly withoutRate = computed(() => this.activePolicies().filter(p => p.commissionRate == null).length);
  /** Previa da comissao no dialogo, conforme o usuario digita. */
  protected readonly previewCommission = signal<number | null>(null);

  protected readonly policyForm = this.fb.group({
    insurerId: ['', Validators.required],
    policyNumber: ['', [Validators.required, Validators.maxLength(50)]],
    productName: ['', [Validators.required, Validators.maxLength(150)]],
    monthlyPremium: [0, [Validators.required, Validators.min(0)]],
    commissionRate: [null as number | null, [Validators.min(0), Validators.max(300)]],
    issuedOn: [todayIso(), Validators.required],
  });

  constructor() {
    this.policyForm.valueChanges.subscribe(() => {
      const v = this.policyForm.getRawValue();
      const rate = v.commissionRate === null || (v.commissionRate as unknown) === '' ? null : Number(v.commissionRate);
      this.previewCommission.set(rate === null || Number.isNaN(rate) ? null : Math.round(Number(v.monthlyPremium) * rate) / 100);
    });
  }

  protected readonly endForm = this.fb.group({
    status: ['Cancelled' as PolicyEndStatus, Validators.required],
    endedOn: [todayIso(), Validators.required],
    reason: [''],
  });

  ngOnInit(): void {
    this.load();
    this.insurersApi.list().subscribe({ next: list => this.insurers.set(list) });
  }

  protected load(): void {
    this.clientsApi.get(this.id()).subscribe({
      next: c => {
        this.client.set(c);
        this.loading.set(false);
      },
      error: err => {
        this.toast.error(err, 'Cliente não encontrado.');
        void this.router.navigate(['/clientes']);
      },
    });
  }

  protected statusClass(status: string): string {
    return status === 'Active' ? 'badge--success' : status === 'BrokerChanged' ? 'badge--danger' : 'badge--warning';
  }

  protected openNewPolicy(): void {
    this.editingPolicy.set(null);
    this.policyForm.enable();
    this.policyForm.reset({
      insurerId: this.insurers()[0]?.id ?? '', policyNumber: '', productName: '', monthlyPremium: 0, commissionRate: null, issuedOn: todayIso(),
    });
    this.dialogError.set(null);
    this.dialog.set('policy');
  }

  /** Edita produto, premio e % (seguradora, numero e emissao identificam a apolice e ficam fixos). */
  protected openEditPolicy(policy: PolicyDto): void {
    this.editingPolicy.set(policy);
    this.policyForm.reset({
      insurerId: policy.insurerId, policyNumber: policy.policyNumber, productName: policy.productName,
      monthlyPremium: policy.monthlyPremium, commissionRate: policy.commissionRate, issuedOn: policy.issuedOn,
    });
    this.policyForm.controls.insurerId.disable();
    this.policyForm.controls.policyNumber.disable();
    this.policyForm.controls.issuedOn.disable();
    this.dialogError.set(null);
    this.dialog.set('policy');
  }

  protected savePolicy(): void {
    if (this.policyForm.invalid) {
      this.policyForm.markAllAsTouched();
      return;
    }
    const v = this.policyForm.getRawValue();
    const commissionRate = v.commissionRate === null || (v.commissionRate as unknown) === '' ? null : Number(v.commissionRate);
    const editing = this.editingPolicy();
    this.busy.set(true);
    const request$: Observable<unknown> = editing
      ? this.policiesApi.update(editing.id, { productName: v.productName.trim(), monthlyPremium: Number(v.monthlyPremium), commissionRate })
      : this.policiesApi.create({
          ...v, clientId: this.id(), monthlyPremium: Number(v.monthlyPremium), commissionRate,
          policyNumber: v.policyNumber.trim(), productName: v.productName.trim(),
        });
    request$.subscribe({
        next: () => {
          this.toast.success(editing ? 'Apólice atualizada.' : 'Apólice adicionada.');
          this.closeDialog();
          this.load();
        },
        error: err => {
          this.busy.set(false);
          this.dialogError.set(problemMessage(err, 'Erro ao salvar apólice.'));
        },
      });
  }

  protected openEnd(policy: PolicyDto): void {
    this.endingPolicy.set(policy);
    this.endForm.reset({ status: 'Cancelled', endedOn: todayIso(), reason: '' });
    this.dialogError.set(null);
    this.dialog.set('end');
  }

  protected confirmEnd(): void {
    const policy = this.endingPolicy();
    if (!policy || this.endForm.invalid) {
      this.endForm.markAllAsTouched();
      return;
    }
    const v = this.endForm.getRawValue();
    this.busy.set(true);
    this.policiesApi.end(policy.id, { status: v.status, endedOn: v.endedOn, reason: v.reason.trim() || null }).subscribe({
      next: () => {
        this.toast.success('Apólice encerrada.');
        this.closeDialog();
        this.load();
      },
      error: err => {
        this.busy.set(false);
        this.dialogError.set(problemMessage(err, 'Erro ao encerrar apólice.'));
      },
    });
  }

  protected reactivate(policy: PolicyDto): void {
    if (!confirm(`Reativar a apólice ${policy.policyNumber}?`)) return;
    this.policiesApi.reactivate(policy.id).subscribe({
      next: () => {
        this.toast.success('Apólice reativada.');
        this.load();
      },
      error: err => this.toast.error(err, 'Erro ao reativar apólice.'),
    });
  }

  protected deleteClient(): void {
    const c = this.client();
    if (!c || !confirm(`Excluir o cliente ${c.fullName}? Esta ação não pode ser desfeita.`)) return;
    this.clientsApi.delete(c.id).subscribe({
      next: () => {
        this.toast.success('Cliente excluído.');
        void this.router.navigate(['/clientes']);
      },
      error: err => this.toast.error(err, 'Não foi possível excluir o cliente.'),
    });
  }

  protected closeDialog(): void {
    this.dialog.set(null);
    this.endingPolicy.set(null);
    this.busy.set(false);
  }
}
