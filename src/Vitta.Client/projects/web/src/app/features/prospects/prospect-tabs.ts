import { ChangeDetectionStrategy, Component } from '@angular/core';
import { RouterLink, RouterLinkActive } from '@angular/router';
import { Icon } from '../../shared/icon';

/** Abas da prospeccao: funil (quadro), lista com filtros + dashboard, historico de importacoes. */
@Component({
  selector: 'vt-prospect-tabs',
  imports: [RouterLink, RouterLinkActive, Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <nav class="tabs view-tabs" aria-label="Prospecção">
      <a routerLink="/prospeccao" routerLinkActive="active" [routerLinkActiveOptions]="{ exact: true }" ariaCurrentWhenActive="page">
        <vt-icon name="target" [size]="16" /> Funil
      </a>
      <a routerLink="/prospeccao/lista" routerLinkActive="active" ariaCurrentWhenActive="page">
        <vt-icon name="search" [size]="16" /> Lista e filtros
      </a>
      <a routerLink="/prospeccao/importacoes" routerLinkActive="active" ariaCurrentWhenActive="page">
        <vt-icon name="upload" [size]="16" /> Importações
      </a>
    </nav>
  `,
  styles: `
    :host { display: block; margin-bottom: 1rem; }
    a { display: inline-flex; align-items: center; gap: 0.4rem; text-decoration: none; }
  `,
})
export class ProspectTabs {}
