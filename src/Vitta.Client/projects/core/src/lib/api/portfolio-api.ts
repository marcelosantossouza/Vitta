import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { PortfolioImportDetailDto, PortfolioImportDto, PortfolioRowDto } from '../models/portfolio';

export interface UploadPortfolioRequest {
  /** Opcional quando o modelo (templateId) ja tem seguradora. */
  insurerId?: string | null;
  /** Modelo de planilha; sem ele a API detecta as colunas pelos nomes. */
  templateId?: string | null;
  /** yyyy-MM */
  referenceMonth: string;
  file: File;
}

@Injectable({ providedIn: 'root' })
export class PortfolioApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/portfolio/imports`;

  list(): Observable<PortfolioImportDto[]> {
    return this.http.get<PortfolioImportDto[]>(this.baseUrl);
  }

  get(id: string): Observable<PortfolioImportDetailDto> {
    return this.http.get<PortfolioImportDetailDto>(`${this.baseUrl}/${id}`);
  }

  /** Apólices do mês (tabela final da importação), ordenadas pelo nome do segurado. */
  rows(id: string): Observable<PortfolioRowDto[]> {
    return this.http.get<PortfolioRowDto[]>(`${this.baseUrl}/${id}/rows`);
  }

  upload(request: UploadPortfolioRequest): Observable<PortfolioImportDetailDto> {
    const form = new FormData();
    if (request.insurerId) form.append('insurerId', request.insurerId);
    if (request.templateId) form.append('templateId', request.templateId);
    form.append('referenceMonth', request.referenceMonth);
    form.append('file', request.file, request.file.name);
    return this.http.post<PortfolioImportDetailDto>(this.baseUrl, form);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
