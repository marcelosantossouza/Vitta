import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { Insurer } from '../models/insurer';

@Injectable({ providedIn: 'root' })
export class InsurersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/insurers`;

  list(): Observable<Insurer[]> {
    return this.http.get<Insurer[]>(this.baseUrl);
  }
}
