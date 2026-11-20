import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import {
  ClientDetailDto, ClientsApi, InsurerDto, InsurersApi, PoliciesApi, PolicyDto, PolicyEndStatus,
  POLICY_STATUS_LABELS, problemMessage, todayIso,
} from '@vitta/core';
import { EmptyState } from '../../shared/empty-state';
import { Icon } from '../../shared/icon';
import { BrlPipe, IsoDatePipe, PolicyStatusPipe } from '../../shared/pipes';
import { ToastService } from '../../shared/toast.service';

@Component({
  selector: 'vt-client-detail',
  imports: [RouterLink, ReactiveFormsModule, Icon, EmptyState, BrlPipe, IsoDatePipe, PolicyStatusPipe],
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
  protected readonly dialogError = signal<string | null>(null);

  protected readonly endStatuses: { value: PolicyEndStatus; label: string }[] = [
    { value: 'Cancelled', label: POLICY_STATUS_LABELS.Cancelled },
    { value: 'Lapsed', label: POLICY_STATUS_LABELS.Lapsed },
    { value: 'BrokerChanged', label: POLICY_STATUS_LABELS.BrokerChanged },
  ];

  protected readonly activePolicies = computed(() => this.client()?.policies.filter(p => p.status === 'Active') ?? []);
  protected readonly monthlyPremium = computed(() => this.activePolicies().reduce((sum, p) => sum + p.monthlyPremium, 0));

  protected readonly policyForm = this.fb.group({
    insurerId: ['', Validators.required],
    policyNumber: ['', [Validators.required, Validators.maxLength(50)]],
    productName: ['', [Validators.required, Validators.maxLength(150)]],
    monthlyPremium: [0, [Validators.required, Validators.min(0)]],
    issuedOn: [todayIso(), Validators.required],
  });

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
    this.policyForm.reset({ insurerId: this.insurers()[0]?.id ?? '', policyNumber: '', productName: '', monthlyPremium: 0, issuedOn: todayIso() });
    this.dialogError.set(null);
    this.dialog.set('policy');
  }

  protected savePolicy(): void {
    if (this.policyForm.invalid) {
      this.policyForm.markAllAsTouched();
      return;
    }
    const v = this.policyForm.getRawValue();
    this.busy.set(true);
    this.policiesApi
      .create({ ...v, clientId: this.id(), monthlyPremium: Number(v.monthlyPremium), policyNumber: v.policyNumber.trim(), productName: v.productName.trim() })
      .subscribe({
        next: () => {
          this.toast.success('Apólice adicionada.');
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
