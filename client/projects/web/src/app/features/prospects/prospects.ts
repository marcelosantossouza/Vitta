import { Component } from '@angular/core';

@Component({
  selector: 'vt-prospects',
  template: `
    <h1>Prospecção</h1>
    <div class="card">
      <p>Funil de possíveis clientes: Novo → Contatado → Reunião → Proposta → Ganho/Perdido.</p>
      <p class="muted">Em breve: kanban, lembretes de follow-up e conversão em cliente.</p>
    </div>
  `,
})
export class Prospects {}
