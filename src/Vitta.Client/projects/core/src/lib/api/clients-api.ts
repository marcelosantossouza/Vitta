import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { ClientDetailDto, ClientInput, ClientListItemDto, CreateClientRequest } from '../models/client';
import { IdResponse, PagedResult } from '../models/common';

@Injectable({ providedIn: 'root' })
export class ClientsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/clients`;

  list(search = '', page = 1, pageSize = 20): Observable<PagedResult<ClientListItemDto>> {
    const params = new HttpParams().set('search', search).set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<ClientListItemDto>>(this.baseUrl, { params });
  }

  get(id: string): Observable<ClientDetailDto> {
    return this.http.get<ClientDetailDto>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateClientRequest): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  update(id: string, request: ClientInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }
}
