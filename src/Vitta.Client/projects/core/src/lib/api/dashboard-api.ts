import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { DashboardDto } from '../models/dashboard';

@Injectable({ providedIn: 'root' })
export class DashboardApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/dashboard`;

  get(): Observable<DashboardDto> {
    return this.http.get<DashboardDto>(this.baseUrl);
  }
}
