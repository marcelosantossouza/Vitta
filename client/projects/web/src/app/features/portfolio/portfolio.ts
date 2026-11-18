import { Component } from '@angular/core';

@Component({
  selector: 'vt-portfolio',
  template: `
    <h1>Carteira</h1>
    <div class="card">
      <p>Importação mensal da planilha das seguradoras (MAG e outras).</p>
      <p class="muted">
        Em breve: upload da planilha, comparação com o mês anterior e alertas de clientes que
        entraram, saíram ou mudaram (possível troca de corretor).
      </p>
    </div>
  `,
})
export class Portfolio {}
