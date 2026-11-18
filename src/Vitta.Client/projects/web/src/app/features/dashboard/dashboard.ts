import { Component } from '@angular/core';

@Component({
  selector: 'vt-dashboard',
  template: `
    <h1>Painel</h1>
    <div class="grid">
      <div class="card"><div class="muted">Clientes ativos</div><strong>—</strong></div>
      <div class="card"><div class="muted">Apólices ativas</div><strong>—</strong></div>
      <div class="card"><div class="muted">Saídas no mês</div><strong>—</strong></div>
      <div class="card"><div class="muted">Follow-ups hoje</div><strong>—</strong></div>
    </div>
    <p class="muted">Indicadores serão preenchidos conforme as features forem implementadas.</p>
  `,
  styles: `strong { font-size: 1.75rem; }`,
})
export class Dashboard {}
