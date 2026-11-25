import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { AiConnectionTest, AiSettingsDto, AiSettingsInput, NotificationSettingsDto, NotificationSettingsInput } from '../models/settings';

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

  getNotifications(): Observable<NotificationSettingsDto> {
    return this.http.get<NotificationSettingsDto>(`${this.baseUrl}/notifications`);
  }

  saveNotifications(input: NotificationSettingsInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/notifications`, input);
  }

  /** Envia o resumo agora para a lista salva; devolve quantos destinatários. */
  sendDigestNow(): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/notifications/send-now`, {});
  }
}
