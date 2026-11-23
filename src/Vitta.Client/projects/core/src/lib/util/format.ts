import { HttpErrorResponse } from '@angular/common/http';
import { ProblemDetails } from '../models/common';

const brl = new Intl.NumberFormat('pt-BR', { style: 'currency', currency: 'BRL' });

export function formatCurrency(value: number | null | undefined): string {
  return brl.format(value ?? 0);
}

/** "52998224725" -> "529.982.247-25". Retorna o texto original se nao tiver 11 digitos. */
export function formatCpf(value: string | null | undefined): string {
  const digits = (value ?? '').replace(/\D/g, '');
  if (digits.length !== 11) {
    return value ?? '';
  }
  return `${digits.slice(0, 3)}.${digits.slice(3, 6)}.${digits.slice(6, 9)}-${digits.slice(9)}`;
}

/** Mascara progressiva de CPF para inputs. */
export function maskCpf(value: string): string {
  const d = value.replace(/\D/g, '').slice(0, 11);
  if (d.length <= 3) return d;
  if (d.length <= 6) return `${d.slice(0, 3)}.${d.slice(3)}`;
  if (d.length <= 9) return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6)}`;
  return `${d.slice(0, 3)}.${d.slice(3, 6)}.${d.slice(6, 9)}-${d.slice(9)}`;
}

/** Texto para busca: minúsculas e sem acentos ("José" -> "jose"). */
export function normalizeText(value: string | null | undefined): string {
  return (value ?? '').normalize('NFD').replace(/[\u0300-\u036f]/g, '').toLowerCase().trim();
}

/** Data local de hoje como "yyyy-MM-dd". */
export function todayIso(): string {
  const now = new Date();
  const mm = String(now.getMonth() + 1).padStart(2, '0');
  const dd = String(now.getDate()).padStart(2, '0');
  return `${now.getFullYear()}-${mm}-${dd}`;
}

/** "2026-10-01" -> "out/2026" */
export function formatMonth(isoDate: string | null | undefined): string {
  if (!isoDate) return '';
  const [y, m] = isoDate.split('-').map(Number);
  const label = new Date(y, (m || 1) - 1, 1).toLocaleDateString('pt-BR', { month: 'short', year: 'numeric' });
  return label.replace('.', '').replace(' de ', '/');
}

/** "2026-10-05" -> "05/10/2026" sem problemas de fuso. */
export function formatDate(isoDate: string | null | undefined): string {
  if (!isoDate) return '';
  const [y, m, d] = isoDate.slice(0, 10).split('-');
  return d && m && y ? `${d}/${m}/${y}` : isoDate;
}

/** Extrai mensagem amigavel de um erro HTTP (ProblemDetails). */
export function problemMessage(error: unknown, fallback = 'Algo deu errado. Tente novamente.'): string {
  if (error instanceof HttpErrorResponse) {
    if (error.status === 0) {
      return 'Não foi possível conectar à API. Verifique se ela está rodando.';
    }
    const problem = error.error as ProblemDetails | null;
    if (problem && typeof problem === 'object') {
      const fieldErrors = problem.errors ? Object.values(problem.errors).flat() : [];
      if (fieldErrors.length) return fieldErrors.join(' ');
      if (problem.detail) return problem.detail;
      if (problem.title) return problem.title;
    }
  }
  return fallback;
}
