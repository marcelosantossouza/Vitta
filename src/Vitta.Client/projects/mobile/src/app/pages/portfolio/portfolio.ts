import { Component } from '@angular/core';
import { IonCard, IonCardContent, IonContent, IonHeader, IonTitle, IonToolbar } from '@ionic/angular';

@Component({
  selector: 'vt-portfolio-page',
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, IonCard, IonCardContent],
  template: `
    <ion-header><ion-toolbar color="primary"><ion-title>Carteira</ion-title></ion-toolbar></ion-header>
    <ion-content class="ion-padding">
      <ion-card><ion-card-content>Alertas de clientes que entraram ou saíram da carteira (planilha mensal da MAG) — em breve.</ion-card-content></ion-card>
    </ion-content>
  `,
})
export class PortfolioPage {}
