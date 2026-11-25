import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse, PagedResult } from '../models/common';
import { BookSummaryDto, CreatePolicyRequest, EndPolicyRequest, PolicyDto, PolicyFilter, UpdatePolicyRequest } from '../models/policy';

@Injectable({ providedIn: 'root' })
export class PoliciesApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/policies`;

  list(filter: PolicyFilter = {}): Observable<PagedResult<PolicyDto>> {
    let params = new HttpParams();
    for (const [key, value] of Object.entries(filter)) {
      if (value !== undefined && value !== null && value !== '') {
        params = params.set(key, String(value));
      }
    }
    return this.http.get<PagedResult<PolicyDto>>(this.baseUrl, { params });
  }

  summary(): Observable<BookSummaryDto> {
    return this.http.get<BookSummaryDto>(`${this.baseUrl}/summary`);
  }

  create(request: CreatePolicyRequest): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  update(id: string, request: UpdatePolicyRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  end(id: string, request: EndPolicyRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/end`, request);
  }

  reactivate(id: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reactivate`, {});
  }
}
