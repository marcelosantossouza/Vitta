import { Component, inject, signal } from '@angular/core';
import { takeUntilDestroyed } from '@angular/core/rxjs-interop';
import { RouterLink } from '@angular/router';
import {
  IonBadge, IonContent, IonHeader, IonItem, IonLabel, IonList, IonRefresher, IonRefresherContent, IonSearchbar,
  IonSpinner, IonText, IonTitle, IonToolbar,
} from '@ionic/angular';
import { ClientListItemDto, ClientsApi } from '@vitta/core';
import { debounceTime, distinctUntilChanged, Subject, switchMap } from 'rxjs';
import { BrlPipe } from '../../shared/pipes';

@Component({
  selector: 'vt-clients-page',
  imports: [
    RouterLink, IonHeader, IonToolbar, IonTitle, IonContent, IonSearchbar, IonList, IonItem, IonLabel, IonBadge,
    IonSpinner, IonText, IonRefresher, IonRefresherContent, BrlPipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar color="primary"><ion-title>Clientes</ion-title></ion-toolbar>
      <ion-toolbar><ion-searchbar placeholder="Nome ou CPF" (ionInput)="search($any($event).detail.value ?? '')" /></ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="refresh($event)"><ion-refresher-content /></ion-refresher>
      @if (loading()) {
        <div class="ion-text-center ion-padding"><ion-spinner /></div>
      } @else if (error()) {
        <ion-text color="danger"><p class="ion-padding">{{ error() }}</p></ion-text>
      } @else {
        <ion-list>
          @for (client of clients(); track client.id) {
            <ion-item [routerLink]="['/tabs/clientes', client.id]" detail>
              <ion-label>
                <h2>{{ client.fullName }}</h2>
                <p>{{ client.cpf }}{{ client.phone ? ' · ' + client.phone : '' }}</p>
              </ion-label>
              <ion-badge slot="end" [color]="client.activePolicyCount > 0 ? 'primary' : 'medium'">
                {{ client.activePolicyCount }} · {{ client.monthlyPremium | brl }}
              </ion-badge>
            </ion-item>
          } @empty {
            <ion-item><ion-label class="ion-text-wrap" color="medium">Nenhum cliente encontrado.</ion-label></ion-item>
          }
        </ion-list>
      }
    </ion-content>
  `,
})
export class ClientsPage {
  private readonly api = inject(ClientsApi);
  private readonly term$ = new Subject<string>();
  private lastTerm = '';

  protected readonly clients = signal<ClientListItemDto[]>([]);
  protected readonly loading = signal(true);
  protected readonly error = signal<string | null>(null);

  constructor() {
    this.term$
      .pipe(debounceTime(300), distinctUntilChanged(), switchMap(term => this.api.list(term, 1, 50)), takeUntilDestroyed())
      .subscribe({
        next: r => {
          this.clients.set(r.items);
          this.loading.set(false);
          this.error.set(null);
        },
        error: () => {
          this.loading.set(false);
          this.error.set('Não foi possível carregar os clientes.');
        },
      });
    this.term$.next('');
  }

  protected search(term: string): void {
    this.lastTerm = term;
    this.term$.next(term);
  }

  protected refresh(event: CustomEvent): void {
    const done = () => (event.target as HTMLIonRefresherElement).complete();
    this.api.list(this.lastTerm, 1, 50).subscribe({ next: r => this.clients.set(r.items), complete: done, error: done });
  }
}
