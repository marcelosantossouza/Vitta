import { ChangeDetectionStrategy, Component, computed, inject, input, OnInit, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ClientsApi, maskCpf, problemMessage } from '@vitta/core';
import { Observable } from 'rxjs';
import { Icon } from '../../shared/icon';
import { ToastService } from '../../shared/toast.service';

/** Criacao (/clientes/novo) e edicao (/clientes/:id/editar). CPF so pode ser informado na criacao. */
@Component({
  selector: 'vt-client-form',
  imports: [ReactiveFormsModule, RouterLink, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  templateUrl: './client-form.html',
})
export class ClientForm implements OnInit {
  private readonly api = inject(ClientsApi);
  private readonly router = inject(Router);
  private readonly toast = inject(ToastService);

  /** Preenchido pela rota em modo edicao. */
  readonly id = input<string>();

  protected readonly isEdit = computed(() => !!this.id());
  protected readonly saving = signal(false);
  protected readonly loading = signal(false);
  protected readonly error = signal<string | null>(null);
  protected readonly cpfDisplay = signal('');

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    cpf: [''],
    birthDate: [''],
    email: ['', Validators.email],
    phone: ['', Validators.maxLength(30)],
    notes: [''],
  });

  ngOnInit(): void {
    const id = this.id();
    if (!id) {
      this.form.controls.cpf.addValidators([Validators.required, Validators.pattern(/^\d{3}\.\d{3}\.\d{3}-\d{2}$/)]);
      return;
    }

    this.loading.set(true);
    this.api.get(id).subscribe({
      next: c => {
        this.form.patchValue({
          fullName: c.fullName,
          birthDate: c.birthDate ?? '',
          email: c.email ?? '',
          phone: c.phone ?? '',
          notes: c.notes ?? '',
        });
        this.cpfDisplay.set(c.cpf);
        this.loading.set(false);
      },
      error: err => {
        this.toast.error(err, 'Cliente não encontrado.');
        void this.router.navigate(['/clientes']);
      },
    });
  }

  protected onCpfInput(event: Event): void {
    const el = event.target as HTMLInputElement;
    const masked = maskCpf(el.value);
    el.value = masked;
    this.form.controls.cpf.setValue(masked);
  }

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    const body = {
      fullName: v.fullName.trim(),
      birthDate: v.birthDate || null,
      email: v.email.trim() || null,
      phone: v.phone.trim() || null,
      notes: v.notes.trim() || null,
    };

    this.saving.set(true);
    this.error.set(null);
    const id = this.id();
    const request$: Observable<unknown> = id ? this.api.update(id, body) : this.api.create({ ...body, cpf: v.cpf });

    request$.subscribe({
      next: (result: unknown) => {
        const targetId = id ?? (result as { id: string }).id;
        this.toast.success(id ? 'Cliente atualizado.' : 'Cliente cadastrado.');
        void this.router.navigate(['/clientes', targetId]);
      },
      error: (err: unknown) => {
        this.error.set(problemMessage(err, 'Erro ao salvar cliente.'));
        this.saving.set(false);
      },
    });
  }
}
