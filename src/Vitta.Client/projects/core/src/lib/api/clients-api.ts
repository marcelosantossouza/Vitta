import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { Client, CreateClientRequest } from '../models/client';
import { PagedResult } from '../models/common';

@Injectable({ providedIn: 'root' })
export class ClientsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/clients`;

  list(search = '', page = 1, pageSize = 20): Observable<PagedResult<Client>> {
    const params = new HttpParams().set('search', search).set('page', page).set('pageSize', pageSize);
    return this.http.get<PagedResult<Client>>(this.baseUrl, { params });
  }

  get(id: string): Observable<Client> {
    return this.http.get<Client>(`${this.baseUrl}/${id}`);
  }

  create(request: CreateClientRequest): Observable<{ id: string }> {
    return this.http.post<{ id: string }>(this.baseUrl, request);
  }
}
