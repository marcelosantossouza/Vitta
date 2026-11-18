import { Component } from '@angular/core';
import { IonCard, IonCardContent, IonContent, IonHeader, IonTitle, IonToolbar } from '@ionic/angular';

@Component({
  selector: 'vt-prospects-page',
  imports: [IonHeader, IonToolbar, IonTitle, IonContent, IonCard, IonCardContent],
  template: `
    <ion-header><ion-toolbar color="primary"><ion-title>Prospecção</ion-title></ion-toolbar></ion-header>
    <ion-content class="ion-padding">
      <ion-card><ion-card-content>Funil de possíveis clientes com lembretes de follow-up — em breve.</ion-card-content></ion-card>
    </ion-content>
  `,
})
export class ProspectsPage {}
