import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse } from '../models/common';
import { ProspectImportResultDto } from '../models/import-template';
import { ChangeStageRequest, ConvertProspectRequest, ProspectDto, ProspectInput, ProspectStage } from '../models/prospect';

@Injectable({ providedIn: 'root' })
export class ProspectsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/prospects`;

  list(filter: { stage?: ProspectStage | ''; search?: string } = {}): Observable<ProspectDto[]> {
    let params = new HttpParams();
    if (filter.stage) params = params.set('stage', filter.stage);
    if (filter.search) params = params.set('search', filter.search);
    return this.http.get<ProspectDto[]>(this.baseUrl, { params });
  }

  create(request: ProspectInput): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  update(id: string, request: ProspectInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  changeStage(id: string, request: ChangeStageRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/stage`, request);
  }

  convert(id: string, request: ConvertProspectRequest): Observable<{ clientId: string }> {
    return this.http.post<{ clientId: string }>(`${this.baseUrl}/${id}/convert`, request);
  }

  /** Importa prospects de uma planilha usando um modelo (etapa Novo; duplicados sao pulados). */
  importFile(file: File, templateId: string): Observable<ProspectImportResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('templateId', templateId);
    return this.http.post<ProspectImportResultDto>(`${this.baseUrl}/imports`, form);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
