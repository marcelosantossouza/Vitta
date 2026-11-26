import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse, PagedResult } from '../models/common';
import { ProspectImportResultDto } from '../models/import-template';
import {
  ChangeStageRequest, ConvertProspectRequest, DeleteProspectImportResultDto, ProspectDto, ProspectFilter, ProspectFilterOptionsDto,
  ProspectInput, ProspectInsightsDto, ProspectSort, ProspectStage,
} from '../models/prospect';

/** Filtros como query string (?city=...&stage=New&stage=Contacted). Vazios não são enviados. */
function filterParams(filter: ProspectFilter): HttpParams {
  let params = new HttpParams();
  for (const [key, value] of Object.entries(filter)) {
    if (value === undefined || value === null || value === '' || value === 'Any') continue; // 'Any' = sem filtro (contato e tipo)
    if (Array.isArray(value)) {
      for (const item of value) params = params.append(key, String(item));
    } else {
      params = params.set(key, String(value));
    }
  }
  return params;
}

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

  /** Lista paginada com filtros e ordenação (tela de gestão). */
  search(filter: ProspectFilter, sort: ProspectSort, page: number, pageSize = 50): Observable<PagedResult<ProspectDto>> {
    const params = filterParams(filter).set('sort', sort).set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ProspectDto>>(`${this.baseUrl}/search`, { params });
  }

  /** Números do dashboard, com os mesmos filtros da lista. */
  insights(filter: ProspectFilter): Observable<ProspectInsightsDto> {
    return this.http.get<ProspectInsightsDto>(`${this.baseUrl}/insights`, { params: filterParams(filter) });
  }

  /** Cidades, UFs, origens e importações existentes (para montar os filtros e o histórico). */
  filterOptions(): Observable<ProspectFilterOptionsDto> {
    return this.http.get<ProspectFilterOptionsDto>(`${this.baseUrl}/filter-options`);
  }

  /** Todos os ids que batem com o filtro (para "selecionar todos"), até 5000. */
  ids(filter: ProspectFilter): Observable<string[]> {
    return this.http.get<string[]>(`${this.baseUrl}/ids`, { params: filterParams(filter) });
  }

  /** Exclui vários de uma vez (Administrador e Corretor). Devolve quantos saíram. */
  bulkDelete(ids: string[]): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/bulk-delete`, { ids });
  }

  /** Move vários de uma vez (e opcionalmente agenda o próximo contato). Devolve quantos mudaram. */
  bulkStage(ids: string[], stage: ProspectStage, nextFollowUpOn: string | null): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/bulk-stage`, { ids, stage, nextFollowUpOn });
  }

  /**
   * Importa prospects de uma planilha usando um modelo (etapa Novo). Repetidos são pulados ou,
   * com `updateExisting`, têm só os campos vazios completados.
   */
  importFile(file: File, templateId: string, updateExisting = false): Observable<ProspectImportResultDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('templateId', templateId);
    form.append('updateExisting', String(updateExisting));
    return this.http.post<ProspectImportResultDto>(`${this.baseUrl}/imports`, form);
  }

  /** Exclui o lote: remove quem ainda está em "Novo"; quem já foi trabalhado fica. */
  deleteImport(id: string): Observable<DeleteProspectImportResultDto> {
    return this.http.delete<DeleteProspectImportResultDto>(`${this.baseUrl}/imports/${id}`);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
