import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse } from '../models/common';
import { CreateInsurerRequest, InsurerDto } from '../models/insurer';

@Injectable({ providedIn: 'root' })
export class InsurersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/insurers`;

  list(): Observable<InsurerDto[]> {
    return this.http.get<InsurerDto[]>(this.baseUrl);
  }

  create(request: CreateInsurerRequest): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }
}
