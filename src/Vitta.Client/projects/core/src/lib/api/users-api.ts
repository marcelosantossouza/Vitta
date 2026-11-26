import { HttpClient } from '@angular/common/http';
import { inject, Injectable } from '@angular/core';
import { Observable } from 'rxjs';
import { API_BASE_URL } from '../config/api-config';
import { CreateUserRequest, UpdateUserRequest, UserListItemDto } from '../models/auth';
import { IdResponse } from '../models/common';

/** Gestão de usuários (só administradores). */
@Injectable({ providedIn: 'root' })
export class UsersApi {
  private readonly http = inject(HttpClient);
  private readonly baseUrl = `${inject(API_BASE_URL)}/users`;

  list(): Observable<UserListItemDto[]> {
    return this.http.get<UserListItemDto[]>(this.baseUrl);
  }

  create(request: CreateUserRequest): Observable<IdResponse> {
    return this.http.post<IdResponse>(this.baseUrl, request);
  }

  update(id: string, request: UpdateUserRequest): Observable<void> {
    return this.http.put<void>(`${this.baseUrl}/${id}`, request);
  }

  setActive(id: string, active: boolean): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/${active ? 'activate' : 'deactivate'}`, {});
  }

  resetPassword(id: string, temporaryPassword: string): Observable<void> {
    return this.http.post<void>(`${this.baseUrl}/${id}/reset-password`, { temporaryPassword });
  }
}
