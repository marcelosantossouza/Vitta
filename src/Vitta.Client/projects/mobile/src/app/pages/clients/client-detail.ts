import { Component, inject, input, OnInit, signal } from '@angular/core';
import {
  IonBackButton, IonBadge, IonButtons, IonContent, IonHeader, IonItem, IonLabel, IonList, IonListHeader, IonNote,
  IonSkeletonText, IonText, IonTitle, IonToolbar,
} from '@ionic/angular';
import { ClientDetailDto, ClientsApi } from '@vitta/core';
import { Feedback } from '../../shared/feedback';
import { BrlPipe, IsoDatePipe, PolicyStatusPipe } from '../../shared/pipes';

@Component({
  selector: 'vt-client-detail-page',
  imports: [
    IonHeader, IonToolbar, IonButtons, IonBackButton, IonTitle, IonContent, IonList, IonListHeader, IonItem, IonLabel,
    IonNote, IonBadge, IonText, IonSkeletonText, BrlPipe, IsoDatePipe, PolicyStatusPipe,
  ],
  template: `
    <ion-header>
      <ion-toolbar color="primary">
        <ion-buttons slot="start"><ion-back-button defaultHref="/tabs/clientes" text="" /></ion-buttons>
        <ion-title>{{ client()?.fullName ?? 'Cliente' }}</ion-title>
      </ion-toolbar>
    </ion-header>
    <ion-content>
      @if (client(); as c) {
        <ion-list inset>
          <ion-item><ion-label><p>CPF</p><h3>{{ c.cpf }}</h3></ion-label></ion-item>
          <ion-item [href]="c.phone ? 'tel:' + c.phone : null" [detail]="false">
            <ion-label><p>Telefone</p><h3>{{ c.phone || '—' }}</h3></ion-label>
          </ion-item>
          <ion-item [href]="c.email ? 'mailto:' + c.email : null" [detail]="false">
            <ion-label><p>E-mail</p><h3>{{ c.email || '—' }}</h3></ion-label>
          </ion-item>
          <ion-item><ion-label><p>Nascimento</p><h3>{{ c.birthDate | isoDate }}</h3></ion-label></ion-item>
          @if (c.notes) {
            <ion-item><ion-label class="ion-text-wrap"><p>Observações</p><h3>{{ c.notes }}</h3></ion-label></ion-item>
          }
        </ion-list>

        <ion-list inset>
          <ion-list-header><ion-label>Apólices ({{ c.policies.length }})</ion-label></ion-list-header>
          @for (p of c.policies; track p.id) {
            <ion-item>
              <ion-label class="ion-text-wrap">
                <h3>{{ p.insurerName }} · {{ p.policyNumber }}</h3>
                <p>{{ p.productName }} · desde {{ p.issuedOn | isoDate }}</p>
                <ion-badge [color]="p.status === 'Active' ? 'success' : p.status === 'BrokerChanged' ? 'danger' : 'warning'">{{ p.status | policyStatus }}</ion-badge>
              </ion-label>
              <ion-note slot="end">{{ p.monthlyPremium | brl }}</ion-note>
            </ion-item>
          } @empty {
            <ion-item><ion-label color="medium">Nenhuma apólice cadastrada.</ion-label></ion-item>
          }
        </ion-list>
      } @else if (failed()) {
        <ion-text color="danger"><p class="ion-padding">Cliente não encontrado.</p></ion-text>
      } @else {
        <div class="ion-padding"><ion-skeleton-text [animated]="true" style="height: 160px" /></div>
      }
    </ion-content>
  `,
  styles: `ion-badge { margin-top: 0.35rem; }`,
})
export class ClientDetailPage implements OnInit {
  private readonly api = inject(ClientsApi);
  private readonly feedback = inject(Feedback);

  readonly id = input.required<string>();
  protected readonly client = signal<ClientDetailDto | null>(null);
  protected readonly failed = signal(false);

  ngOnInit(): void {
    this.api.get(this.id()).subscribe({
      next: c => this.client.set(c),
      error: err => {
        this.failed.set(true);
        void this.feedback.error(err, 'Cliente não encontrado.');
      },
    });
  }
}
