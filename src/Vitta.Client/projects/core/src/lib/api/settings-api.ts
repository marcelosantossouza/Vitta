import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { AiConnectionTest, AiSettingsDto, AiSettingsInput } from '../models/settings';

@Injectable({ providedIn: 'root' })
export class SettingsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/settings`;

  getAi(): Observable<AiSettingsDto> {
    return this.http.get<AiSettingsDto>(`${this.baseUrl}/ai`);
  }

  saveAi(input: AiSettingsInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/ai`, input);
  }

  testAi(input: AiSettingsInput): Observable<AiConnectionTest> {
    return this.http.post<AiConnectionTest>(`${this.baseUrl}/ai/test`, input);
  }
}
