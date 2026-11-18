import { HttpErrorResponse } from '@angular/common/http';
import { Component, inject, signal } from '@angular/core';
import { NonNullableFormBuilder, ReactiveFormsModule, Validators } from '@angular/forms';
import { Router, RouterLink } from '@angular/router';
import { ClientsApi, ProblemDetails } from '@vitta/core';

@Component({
  selector: 'vt-client-form',
  imports: [ReactiveFormsModule, RouterLink],
  templateUrl: './client-form.html',
})
export class ClientForm {
  private readonly api = inject(ClientsApi);
  private readonly router = inject(Router);

  protected readonly saving = signal(false);
  protected readonly error = signal<string | null>(null);

  protected readonly form = inject(NonNullableFormBuilder).group({
    fullName: ['', [Validators.required, Validators.maxLength(200)]],
    cpf: ['', Validators.required],
    birthDate: [''],
    email: ['', Validators.email],
    phone: [''],
  });

  protected submit(): void {
    if (this.form.invalid) {
      this.form.markAllAsTouched();
      return;
    }

    const v = this.form.getRawValue();
    this.saving.set(true);
    this.error.set(null);
    this.api
      .create({ ...v, birthDate: v.birthDate || null, email: v.email || null, phone: v.phone || null })
      .subscribe({
        next: () => this.router.navigate(['/clientes']),
        error: (err: HttpErrorResponse) => {
          const problem = err.error as ProblemDetails | null;
          const fieldErrors = problem?.errors ? Object.values(problem.errors).flat().join(' ') : null;
          this.error.set(fieldErrors || problem?.detail || 'Erro ao salvar cliente.');
          this.saving.set(false);
        },
      });
  }
}
