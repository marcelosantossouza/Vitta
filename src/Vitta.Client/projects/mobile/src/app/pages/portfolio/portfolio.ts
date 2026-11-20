import { Component, inject, signal } from '@angular/core';
import { RouterLink } from '@angular/router';
import {
  IonBadge, IonContent, IonHeader, IonIcon, IonItem, IonLabel, IonList, IonListHeader, IonNote, IonRefresher,
  IonRefresherContent, IonSpinner, IonText, IonTitle, IonToolbar,
} from '@ionic/angular';
import { DashboardApi, PortfolioApi, PortfolioChangeDto, PortfolioImportDto } from '@vitta/core';
import { addIcons } from 'ionicons';
import { alertCircleOutline } from 'ionicons/icons';
import { forkJoin } from 'rxjs';
import { Feedback } from '../../shared/feedback';
import { MonthPipe } from '../../shared/pipes';

@Component({
  selector: 'vt-portfolio-page',
  imports: [
    RouterLink, IonHeader, IonToolbar, IonTitle, IonContent, IonList, IonListHeader, IonItem, IonLabel, IonNote, IonBadge,
    IonIcon, IonSpinner, IonText, IonRefresher, IonRefresherContent, MonthPipe,
  ],
  template: `
    <ion-header><ion-toolbar color="primary"><ion-title>Carteira</ion-title></ion-toolbar></ion-header>
    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="load($event)"><ion-refresher-content /></ion-refresher>
      @if (loading()) {
        <div class="ion-text-center ion-padding"><ion-spinner /></div>
      } @else {
        <ion-list inset>
          <ion-list-header><ion-label>Saídas recentes</ion-label></ion-list-header>
          @for (c of exits(); track c.id) {
            <ion-item [routerLink]="c.clientId ? ['/tabs/clientes', c.clientId] : null" [detail]="!!c.clientId">
              <ion-icon slot="start" name="alert-circle-outline" color="danger" />
              <ion-label class="ion-text-wrap">
                <h3>{{ c.holderName }}</h3>
                <p>{{ c.insurerName }} · apólice {{ c.policyNumber }}{{ c.detail ? ' · ' + c.detail : '' }}</p>
              </ion-label>
              <ion-note slot="end">{{ c.referenceMonth | month }}</ion-note>
            </ion-item>
          } @empty {
            <ion-item><ion-label color="medium" class="ion-text-wrap">Nenhuma saída detectada.</ion-label></ion-item>
          }
        </ion-list>

        <ion-list inset>
          <ion-list-header><ion-label>Importações</ion-label></ion-list-header>
          @for (i of imports(); track i.id) {
            <ion-item>
              <ion-label>
                <h3>{{ i.insurerName }} · {{ i.referenceMonth | month }}</h3>
                <p>{{ i.totalRows }} apólices{{ i.isFirstImport ? ' · carteira base' : '' }}</p>
              </ion-label>
              @if (!i.isFirstImport) {
                <div slot="end" class="counts">
                  <ion-badge color="success">+{{ i.entered }}</ion-badge>
                  <ion-badge color="danger">−{{ i.left }}</ion-badge>
                </div>
              }
            </ion-item>
          } @empty {
            <ion-item><ion-label color="medium" class="ion-text-wrap">Nenhuma importação. Envie a planilha pelo computador.</ion-label></ion-item>
          }
        </ion-list>
        <ion-text color="medium"><p class="ion-padding hint">O envio da planilha mensal é feito pela versão web.</p></ion-text>
      }
    </ion-content>
  `,
  styles: `
    .counts { display: flex; gap: 0.3rem; }
    .hint { font-size: 0.8rem; text-align: center; }
  `,
})
export class PortfolioPage {
  private readonly portfolio = inject(PortfolioApi);
  private readonly dashboard = inject(DashboardApi);
  private readonly feedback = inject(Feedback);

  protected readonly imports = signal<PortfolioImportDto[]>([]);
  protected readonly exits = signal<PortfolioChangeDto[]>([]);
  protected readonly loading = signal(true);

  constructor() {
    addIcons({ alertCircleOutline });
    this.load();
  }

  protected load(event?: CustomEvent): void {
    const done = () => (event?.target as HTMLIonRefresherElement | undefined)?.complete();
    forkJoin({ imports: this.portfolio.list(), dashboard: this.dashboard.get() }).subscribe({
      next: ({ imports, dashboard }) => {
        this.imports.set(imports);
        this.exits.set(dashboard.recentExits);
        this.loading.set(false);
        done();
      },
      error: err => {
        this.loading.set(false);
        void this.feedback.error(err, 'Não foi possível carregar a carteira.');
        done();
      },
    });
  }
}
