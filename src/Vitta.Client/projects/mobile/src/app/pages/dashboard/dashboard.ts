import { Component, computed, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonButton, IonButtons, IonCard, IonCardContent, IonCol, IonContent, IonGrid, IonHeader, IonIcon, IonItem, IonLabel,
  IonList, IonListHeader, IonNote, IonRefresher, IonRefresherContent, IonRow, IonSkeletonText, IonText, IonTitle, IonToolbar,
} from '@ionic/angular';
import { AuthService, DashboardApi, DashboardDto, todayIso } from '@vitta/core';
import { addIcons } from 'ionicons';
import { alertCircleOutline, logOutOutline } from 'ionicons/icons';
import { Feedback } from '../../shared/feedback';
import { BrlPipe, IsoDatePipe, MonthPipe, StagePipe } from '../../shared/pipes';

@Component({
  selector: 'vt-dashboard-page',
  imports: [
    RouterLink, IonHeader, IonToolbar, IonTitle, IonButtons, IonButton, IonIcon, IonContent, IonGrid, IonRow, IonCol,
    IonCard, IonCardContent, IonList, IonListHeader, IonItem, IonLabel, IonNote, IonText, IonRefresher,
    IonRefresherContent, IonSkeletonText, BrlPipe, IsoDatePipe, StagePipe, MonthPipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar color="primary">
        <ion-title>Olá{{ firstName() ? ', ' + firstName() : '' }}</ion-title>
        <ion-buttons slot="end">
          <ion-button (click)="logout()" aria-label="Sair"><ion-icon slot="icon-only" name="log-out-outline" /></ion-button>
        </ion-buttons>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="load($event)"><ion-refresher-content /></ion-refresher>

      @if (data(); as d) {
        <ion-grid>
          <ion-row>
            <ion-col size="6"><ion-card class="kpi highlight"><ion-card-content>
              <span>Prêmio mensal</span><strong>{{ d.monthlyPremiumTotal | brl }}</strong>
            </ion-card-content></ion-card></ion-col>
            <ion-col size="6"><ion-card class="kpi"><ion-card-content>
              <span>Clientes ativos</span><strong>{{ d.activeClients }}</strong><small>de {{ d.totalClients }}</small>
            </ion-card-content></ion-card></ion-col>
            <ion-col size="6"><ion-card class="kpi"><ion-card-content>
              <span>Apólices ativas</span><strong>{{ d.activePolicies }}</strong>
            </ion-card-content></ion-card></ion-col>
            <ion-col size="6"><ion-card class="kpi" routerLink="/tabs/prospeccao"><ion-card-content>
              <span>Prospects abertos</span><strong>{{ d.openProspects }}</strong>
            </ion-card-content></ion-card></ion-col>
          </ion-row>
        </ion-grid>

        <ion-list inset>
          <ion-list-header><ion-label>Follow-ups para hoje</ion-label></ion-list-header>
          @for (p of d.followUpsDue; track p.id) {
            <ion-item routerLink="/tabs/prospeccao" detail>
              <ion-label>
                <h3>{{ p.name }}</h3>
                <p>{{ p.stage | stage }}{{ p.phone ? ' · ' + p.phone : '' }}</p>
              </ion-label>
              <ion-note slot="end" [color]="p.nextFollowUpOn && p.nextFollowUpOn < today ? 'danger' : 'warning'">
                {{ p.nextFollowUpOn && p.nextFollowUpOn < today ? (p.nextFollowUpOn | isoDate) : 'Hoje' }}
              </ion-note>
            </ion-item>
          } @empty {
            <ion-item><ion-label class="ion-text-wrap" color="medium">Nenhum contato pendente. 🎉</ion-label></ion-item>
          }
        </ion-list>

        <ion-list inset>
          <ion-list-header><ion-label>Saídas recentes da carteira</ion-label></ion-list-header>
          @for (c of d.recentExits; track c.id) {
            <ion-item [routerLink]="c.clientId ? ['/tabs/clientes', c.clientId] : null" [detail]="!!c.clientId">
              <ion-icon slot="start" name="alert-circle-outline" color="danger" />
              <ion-label>
                <h3>{{ c.holderName }}</h3>
                <p>{{ c.insurerName }} · apólice {{ c.policyNumber }} · {{ c.referenceMonth | month }}</p>
              </ion-label>
            </ion-item>
          } @empty {
            <ion-item><ion-label class="ion-text-wrap" color="medium">Nenhuma saída detectada nas importações.</ion-label></ion-item>
          }
        </ion-list>
      } @else if (failed()) {
        <ion-text color="danger"><p class="ion-padding">Não foi possível carregar o painel. Puxe para tentar de novo.</p></ion-text>
      } @else {
        <ion-grid><ion-row>
          @for (i of [1, 2, 3, 4]; track i) {
            <ion-col size="6"><ion-card><ion-card-content><ion-skeleton-text [animated]="true" style="height: 48px" /></ion-card-content></ion-card></ion-col>
          }
        </ion-row></ion-grid>
      }
    </ion-content>
  `,
  styles: `
    .kpi { margin: 0; height: 100%; }
    .kpi ion-card-content { display: flex; flex-direction: column; gap: 0.15rem; }
    .kpi span { font-size: 0.75rem; color: var(--ion-color-medium); font-weight: 600; }
    .kpi strong { font-size: 1.35rem; font-weight: 750; color: var(--ion-text-color, #16211e); }
    .kpi small { font-size: 0.75rem; color: var(--ion-color-medium); }
    .highlight { --background: linear-gradient(135deg, #0f6e5c, #0b4f42); }
    .highlight span, .highlight strong { color: #fff !important; }
  `,
})
export class DashboardPage {
  private readonly api = inject(DashboardApi);
  private readonly auth = inject(AuthService);
  private readonly feedback = inject(Feedback);

  protected readonly today = todayIso();
  protected readonly data = signal<DashboardDto | null>(null);
  protected readonly failed = signal(false);
  protected readonly firstName = computed(() => (this.auth.currentUser()?.name ?? '').split(' ')[0]);

  constructor() {
    addIcons({ logOutOutline, alertCircleOutline });
    this.load();
  }

  protected load(event?: CustomEvent): void {
    this.api.get().subscribe({
      next: d => {
        this.data.set(d);
        this.failed.set(false);
        (event?.target as HTMLIonRefresherElement | undefined)?.complete();
      },
      error: err => {
        this.failed.set(true);
        void this.feedback.error(err, 'Não foi possível carregar o painel.');
        (event?.target as HTMLIonRefresherElement | undefined)?.complete();
      },
    });
  }

  protected logout(): void {
    this.auth.logout();
  }
}
