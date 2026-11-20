import { HttpErrorResponse } from '@angular/common/http';
import { TestBed } from '@angular/core/testing';
import { ToastService } from './toast.service';

describe('ToastService', () => {
  it('mostra erros da API com a mensagem do ProblemDetails e some sozinho', () => {
    vi.useFakeTimers();
    const service = TestBed.inject(ToastService);

    service.error(new HttpErrorResponse({ status: 409, error: { detail: 'Ja existe um cliente com este CPF.' } }));
    expect(service.toasts()[0]).toMatchObject({ kind: 'error', message: 'Ja existe um cliente com este CPF.' });

    vi.advanceTimersByTime(7000);
    expect(service.toasts().length).toBe(0);
    vi.useRealTimers();
  });
});
