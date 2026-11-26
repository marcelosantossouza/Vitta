import { HttpClient, HttpParams } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { IdResponse } from '../models/common';
import { ReferralGroupDto, ReferralGroupInput, ReferralReportDto } from '../models/referral';

/** Grupos de indicação (só administradores). */
@Injectable({ providedIn: 'root' })
export class ReferralsApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/referrals`;

  list(): Observable<ReferralGroupDto[]> {
    return this.http.get<ReferralGroupDto[]>(this.baseUrl);
  }

  /** `month` = qualquer dia do mês (yyyy-MM-dd); vazio = mês atual. */
  report(id: string, month?: string): Observable<ReferralReportDto> {
    const params = month ? new HttpParams().set('month', month) : undefined;
    return this.http.get<ReferralReportDto>(`${this.baseUrl}/${id}/report`, { params });
  }

  create(input: ReferralGroupInput): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, input);
  }

  update(id: string, input: ReferralGroupInput): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, input);
  }

  delete(id: string): Observable<void> {
    return this.http.delete<void>(`${this.baseUrl}/${id}`);
  }

  /** Carimba segurados com o grupo (ou tira o carimbo com `groupId` nulo). Devolve quantos mudaram. */
  assign(groupId: string | null, clientIds: string[]): Observable<number> {
    return this.http.post<number>(`${this.baseUrl}/assign`, { groupId, clientIds });
  }
}
