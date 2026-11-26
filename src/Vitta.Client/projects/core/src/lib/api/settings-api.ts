import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { LeadCaptureSettings, LeadPageDto, LeadRequest } from '../models/settings';
import { SiteDto, SiteImageKind, SiteInput } from '../models/site';
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

  getLeadPage(): Observable<LeadCaptureSettings> {
    return this.http.get<LeadCaptureSettings>(`${this.baseUrl}/lead-page`);
  }

  saveLeadPage(input: LeadCaptureSettings): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/lead-page`, input);
  }

  getSite(): Observable<SiteDto> {
    return this.http.get<SiteDto>(`${this.baseUrl}/site`);
  }

  saveSite(input: SiteInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/site`, input);
  }

  uploadSiteImage(kind: SiteImageKind, file: Blob): Observable<void> {
    const form = new FormData();
    form.append('file', file, `${kind}.img`);
    return this.http.put<void>(`${this.baseUrl}/site/images/${kind}`, form);
  }

  deleteSiteImage(kind: SiteImageKind): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/site/images/${kind}`);
  }
}

/** Rotas públicas (sem login): a página de captação. */
@Injectable({ providedIn: 'root' })
export class PublicApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/public`;

  leadPage(): Observable<LeadPageDto> {
    return this.http.get<LeadPageDto>(`${this.baseUrl}/lead-page`);
  }

  submitLead(request: LeadRequest): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/leads`, request);
  }

  site(): Observable<SiteDto> {
    return this.http.get<SiteDto>(`${this.baseUrl}/site`);
  }

  /** URL da logo/capa (com a versão, para o navegador não usar a antiga). */
  siteImageUrl(kind: SiteImageKind, version: string | null): string | null {
    return version ? `${this.baseUrl}/site/images/${kind}?v=${version}` : null;
  }
}
