import { Component } from '@angular/core';
import { IonCard, IonCardContent, IonCardHeader, IonCardSubtitle, IonCardTitle, IonCol, IonContent, IonGrid, IonHeader, IonRow, IonTitle, IonToolbar } from '@ionic/angular';

@Component({
  selector: 'vt-dashboard-page',
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, IonGrid, IonRow, IonCol, IonCard, IonCardHeader, IonCardSubtitle, IonCardTitle, IonCardContent],
  template: `
    <ion-header><ion-toolbar color="primary"><ion-title>Vitta</ion-title></ion-toolbar></ion-header>
    <ion-content class="ion-padding">
      <ion-grid>
        <ion-row>
          @for (kpi of kpis; track kpi) {
            <ion-col size="6">
              <ion-card>
                <ion-card-header><ion-card-subtitle>{{ kpi }}</ion-card-subtitle><ion-card-title>—</ion-card-title></ion-card-header>
              </ion-card>
            </ion-col>
          }
        </ion-row>
      </ion-grid>
      <ion-card><ion-card-content>Indicadores serão preenchidos conforme as features forem implementadas.</ion-card-content></ion-card>
    </ion-content>
  `,
})
export class DashboardPage {
  protected readonly kpis = ['Clientes ativos', 'Apólices ativas', 'Saídas no mês', 'Follow-ups hoje'];
}
