import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse } from '../models/common';
import {
  ImportFieldDto, ImportTemplateDto, ImportTemplateInput, ImportTemplateKind, SpreadsheetAnalysisDto,
} from '../models/import-template';

@Injectable({ providedIn: 'root' })
export class ImportTemplatesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/import-templates`;

  list(kind?: ImportTemplateKind): Observable<ImportTemplateDto[]> {
    const params = kind ? new HttpParams().set('kind', kind) : undefined;
    return this.http.get<ImportTemplateDto[]>(this.baseUrl, { params });
  }

  fields(kind: ImportTemplateKind): Observable<ImportFieldDto[]> {
    return this.http.get<ImportFieldDto[]>(`${this.baseUrl}/fields`, { params: new HttpParams().set('kind', kind) });
  }

  get(id: string): Observable<ImportTemplateDto> {
    return this.http.get<ImportTemplateDto>(`${this.baseUrl}/${id}`);
  }

  /** Envia uma planilha de exemplo para a IA sugerir o mapeamento. */
  analyze(file: File, kind: ImportTemplateKind, sheetName?: string | null): Observable<SpreadsheetAnalysisDto> {
    const form = new FormData();
    form.append('file', file, file.name);
    form.append('kind', kind);
    if (sheetName) form.append('sheetName', sheetName);
    return this.http.post<SpreadsheetAnalysisDto>(`${this.baseUrl}/analyze`, form);
  }

  create(request: ImportTemplateInput): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  update(id: string, request: ImportTemplateInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
