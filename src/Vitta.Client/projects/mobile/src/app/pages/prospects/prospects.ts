import { Component, computed, inject, signal } from '@angular/core';
import {
  ActionSheetController, IonBadge, IonContent, IonHeader, IonItem, IonItemDivider, IonItemGroup, IonLabel, IonList,
  IonNote, IonRefresher, IonRefresherContent, IonSpinner, IonText, IonTitle, IonToolbar,
} from '@ionic/angular';
import { ProspectDto, ProspectsApi, ProspectStage, PROSPECT_STAGES, PROSPECT_STAGE_LABELS, todayIso } from '@vitta/core';
import { Feedback } from '../../shared/feedback';
import { IsoDatePipe } from '../../shared/pipes';

@Component({
  selector: 'vt-prospects-page',
  imports: [
    IonHeader, IonToolbar, IonTitle, IonContent, IonList, IonItemGroup, IonItemDivider, IonItem, IonLabel, IonNote,
    IonBadge, IonSpinner, IonText, IonRefresher, IonRefresherContent, IsoDatePipe,
  ],
  template: `
    <ion-header><ion-toolbar color="primary"><ion-title>Prospecção</ion-title></ion-toolbar></ion-header>
    <ion-content>
      <ion-refresher slot="fixed" (ionRefresh)="load($event)"><ion-refresher-content /></ion-refresher>
      @if (loading()) {
        <div class="ion-text-center ion-padding"><ion-spinner /></div>
      } @else {
        <ion-list>
          @for (group of groups(); track group.stage) {
            <ion-item-group>
              <ion-item-divider sticky>
                <ion-label>{{ group.label }}</ion-label>
                <ion-badge slot="end" color="medium">{{ group.items.length }}</ion-badge>
              </ion-item-divider>
              @for (p of group.items; track p.id) {
                <ion-item button (click)="changeStage(p)">
                  <ion-label>
                    <h3>{{ p.name }}</h3>
                    <p>{{ p.phone }}{{ p.phone && p.source ? ' · ' : '' }}{{ p.source }}</p>
                  </ion-label>
                  @if (p.nextFollowUpOn && group.open) {
                    <ion-note slot="end" [color]="p.nextFollowUpOn < today ? 'danger' : p.nextFollowUpOn === today ? 'warning' : 'medium'">
                      {{ p.nextFollowUpOn === today ? 'Hoje' : (p.nextFollowUpOn | isoDate) }}
                    </ion-note>
                  }
                </ion-item>
              } @empty {
                <ion-item lines="none"><ion-label color="medium"><p>Ninguém nesta etapa.</p></ion-label></ion-item>
              }
            </ion-item-group>
          }
        </ion-list>
        <ion-text color="medium"><p class="ion-padding small">Toque em um prospect para mudar a etapa.</p></ion-text>
      }
    </ion-content>
  `,
  styles: `.small { font-size: 0.8rem; text-align: center; }`,
})
export class ProspectsPage {
  private readonly api = inject(ProspectsApi);
  private readonly sheets = inject(ActionSheetController);
  private readonly feedback = inject(Feedback);

  protected readonly today = todayIso();
  protected readonly prospects = signal<ProspectDto[]>([]);
  protected readonly loading = signal(true);

  protected readonly groups = computed(() =>
    PROSPECT_STAGES.map(stage => ({
      stage,
      label: PROSPECT_STAGE_LABELS[stage],
      open: stage !== 'Won' && stage !== 'Lost',
      items: this.prospects().filter(p => p.stage === stage),
    })),
  );

  constructor() {
    this.load();
  }

  protected load(event?: CustomEvent): void {
    const done = () => (event?.target as HTMLIonRefresherElement | undefined)?.complete();
    this.api.list().subscribe({
      next: list => {
        this.prospects.set(list);
        this.loading.set(false);
        done();
      },
      error: err => {
        this.loading.set(false);
        void this.feedback.error(err, 'Não foi possível carregar a prospecção.');
        done();
      },
    });
  }

  protected async changeStage(p: ProspectDto): Promise<void> {
    const sheet = await this.sheets.create({
      header: p.name,
      subHeader: 'Mover para a etapa',
      buttons: [
        ...PROSPECT_STAGES.filter(s => s !== p.stage).map(stage => ({
          text: PROSPECT_STAGE_LABELS[stage],
          handler: () => this.move(p, stage),
        })),
        { text: 'Cancelar', role: 'cancel' },
      ],
    });
    await sheet.present();
  }

  private move(p: ProspectDto, stage: ProspectStage): void {
    this.api.changeStage(p.id, { stage, nextFollowUpOn: p.nextFollowUpOn }).subscribe({
      next: () => {
        this.prospects.update(list => list.map(x => (x.id === p.id ? { ...x, stage } : x)));
        void this.feedback.success(`${p.name} → ${PROSPECT_STAGE_LABELS[stage]}`);
      },
      error: err => void this.feedback.error(err, 'Não foi possível mover o prospect.'),
    });
  }
}
