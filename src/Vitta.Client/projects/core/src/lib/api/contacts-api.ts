import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse } from '../models/common';
import { ContactLogDto, RegisterContactRequest } from '../models/contact';

/** Histórico de contatos de prospects e clientes. */
@Injectable({ providedIn: 'root' })
export class ContactsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/contacts`;

  list(target: { prospectId?: string | null; clientId?: string | null }): Observable<ContactLogDto[]> {
    let params = new HttpParams();
    if (target.prospectId) params = params.set('prospectId', target.prospectId);
    if (target.clientId) params = params.set('clientId', target.clientId);
    return this.http.get<ContactLogDto[]>(this.baseUrl, { params });
  }

  register(request: RegisterContactRequest): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
