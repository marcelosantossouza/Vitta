import { Component } from '@angular/core';
import { IonIcon, IonLabel, IonTabBar, IonTabButton, IonTabs } from '@ionic/angular';
import { addIcons } from 'ionicons';
import { folderOpenOutline, peopleOutline, speedometerOutline, trendingUpOutline } from 'ionicons/icons';

@Component({
  selector: 'vt-tabs',
  imports: [IonTabs, IonTabBar, IonTabButton, IonIcon, IonLabel],
  template: `
    <ion-tabs>
      <ion-tab-bar slot="bottom">
        <ion-tab-button tab="painel"><ion-icon name="speedometer-outline" /><ion-label>Painel</ion-label></ion-tab-button>
        <ion-tab-button tab="clientes"><ion-icon name="people-outline" /><ion-label>Clientes</ion-label></ion-tab-button>
        <ion-tab-button tab="prospeccao"><ion-icon name="trending-up-outline" /><ion-label>Prospecção</ion-label></ion-tab-button>
        <ion-tab-button tab="carteira"><ion-icon name="folder-open-outline" /><ion-label>Carteira</ion-label></ion-tab-button>
      </ion-tab-bar>
    </ion-tabs>
  `,
})
export class Tabs {
  constructor() {
    addIcons({ speedometerOutline, peopleOutline, trendingUpOutline, folderOpenOutline });
  }
}
