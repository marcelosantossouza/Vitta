import { ChangeDetectionStrategy, Component, computed, input, model, output, signal } from '@angular/core';
import { Icon } from './icon';

/** Nome do arquivo gerado a partir do texto colado (a API trata `.txt` como texto separado por tabulacao). */
export const PASTED_FILE_NAME = 'texto-colado.txt';

const ACCEPTED = /\.(xlsx|csv|txt)$/i;

export type FileOrPasteMode = 'file' | 'paste';

/** Cria o arquivo enviado a API a partir do texto colado. */
export function pastedFile(text: string): File {
  return new File([text], PASTED_FILE_NAME, { type: 'text/plain' });
}

export function isPastedFileName(name: string | null | undefined): boolean {
  return name === PASTED_FILE_NAME;
}

/**
 * Escolha de planilha em dois modos: enviar arquivo (.xlsx/.csv/.txt) ou colar a tabela copiada do site da seguradora.
 * O texto colado vira um `File` (`texto-colado.txt`). Sem `pasteAction`, emite a cada alteracao do texto;
 * com `pasteAction`, so emite ao clicar no botao (util quando o envio dispara uma analise).
 */
@Component({
  selector: 'vt-file-or-paste',
  imports: [Icon],
  changeDetection: ChangeDetectionStrategy.OnPush,
  template: `
    <div class="fop">
      <div class="tabs" role="group" aria-label="Como enviar a planilha">
        <button type="button" [class.active]="mode() === 'file'" [attr.aria-pressed]="mode() === 'file'" (click)="setMode('file')">
          <vt-icon name="upload" [size]="14" /> Enviar arquivo
        </button>
        <button type="button" [class.active]="mode() === 'paste'" [attr.aria-pressed]="mode() === 'paste'" (click)="setMode('paste')">
          <vt-icon name="clipboard" [size]="14" /> Colar do site
        </button>
      </div>

      @if (mode() === 'file') {
        <label class="dropzone" [class.over]="dragOver()" [class.has-file]="picked()"
          (dragover)="$event.preventDefault(); dragOver.set(true)" (dragleave)="dragOver.set(false)" (drop)="onDrop($event)">
          <input type="file" accept=".xlsx,.csv,.txt" [disabled]="disabled()" (change)="onFile($event)" />
          @if (picked(); as f) {
            <vt-icon name="sheet" [size]="28" />
            <strong>{{ f.name }}</strong>
            <span class="muted small">{{ formatSize(f.size) }} · clique para trocar</span>
          } @else {
            <vt-icon name="upload" [size]="28" />
            <strong>{{ label() }}</strong>
            <span class="muted small">ou clique para escolher (.xlsx, .csv ou .txt)</span>
          }
        </label>
      } @else {
        <textarea class="paste" rows="8" spellcheck="false" aria-label="Texto copiado do site da seguradora"
          [value]="text()" [disabled]="disabled()" (input)="onText($any($event.target).value)"
          placeholder="No site da seguradora, selecione a tabela (de preferência com a linha de títulos), copie (Ctrl+C) e cole aqui (Ctrl+V)."></textarea>
        <div class="paste-footer">
          <span class="muted small">{{ lineLabel() }}</span>
          @if (pasteAction(); as action) {
            <button class="btn btn--primary btn--sm" type="button" [disabled]="disabled() || !lineCount()" (click)="usePaste()">
              <vt-icon name="sparkles" [size]="14" /> {{ action }}
            </button>
          }
        </div>
      }

      @if (error(); as message) {
        <div class="alert alert--danger"><vt-icon name="alert" [size]="18" /><span>{{ message }}</span></div>
      }
    </div>
  `,
  styles: `
    :host { display: block; min-width: 0; font-weight: 400; }
    .fop { display: flex; flex-direction: column; gap: 0.75rem; }
    .tabs { align-self: flex-start; max-width: 100%; }
    .tabs button { display: inline-flex; align-items: center; gap: 0.35rem; }
    .paste {
      width: 100%; min-height: 180px; font-family: ui-monospace, SFMono-Regular, Menlo, Consolas, monospace;
      font-size: 0.8rem; line-height: 1.45; white-space: pre; overflow: auto; tab-size: 4;
    }
    .paste::placeholder { white-space: normal; font-family: inherit; }
    .paste-footer { display: flex; flex-wrap: wrap; align-items: center; justify-content: space-between; gap: 0.5rem; }
  `,
})
export class FileOrPaste {
  /** Titulo da area de arrastar. */
  readonly label = input('Arraste a planilha aqui');
  /** Texto do botao que confirma o texto colado. Vazio = emite a cada alteracao. */
  readonly pasteAction = input<string | null>(null);
  readonly disabled = input(false);
  readonly mode = model<FileOrPasteMode>('file');
  readonly fileChange = output<File | null>();

  protected readonly dragOver = signal(false);
  protected readonly picked = signal<File | null>(null);
  protected readonly text = signal('');
  protected readonly error = signal<string | null>(null);

  protected readonly lineCount = computed(() => this.text().split(/\r?\n/).filter(l => l.trim()).length);
  protected readonly lineLabel = computed(() => {
    const n = this.lineCount();
    return n === 0 ? 'Nenhuma linha colada ainda.' : n === 1 ? '1 linha colada' : `${n} linhas coladas`;
  });

  protected setMode(mode: FileOrPasteMode): void {
    if (this.mode() === mode) return;
    this.mode.set(mode);
    this.error.set(null);
    // Com acao explicita (ex.: analise), nada e enviado so por trocar de modo.
    if (this.pasteAction()) return;
    // Sem acao: o que vale passa a ser o conteudo do novo modo.
    if (mode === 'file') this.fileChange.emit(this.picked());
    else this.fileChange.emit(this.lineCount() ? pastedFile(this.text()) : null);
  }

  protected onFile(event: Event): void {
    const input = event.target as HTMLInputElement;
    this.pick(input.files?.[0] ?? null);
    input.value = '';
  }

  protected onDrop(event: DragEvent): void {
    event.preventDefault();
    this.dragOver.set(false);
    if (this.disabled()) return;
    this.pick(event.dataTransfer?.files?.[0] ?? null);
  }

  private pick(file: File | null): void {
    this.error.set(null);
    if (!file) return;
    if (!ACCEPTED.test(file.name)) {
      this.error.set('Envie um arquivo .xlsx, .csv ou .txt — ou use "Colar do site" para colar a tabela.');
      return;
    }
    this.picked.set(file);
    this.fileChange.emit(file);
  }

  protected onText(value: string): void {
    this.text.set(value);
    this.error.set(null);
    if (!this.pasteAction()) this.fileChange.emit(this.lineCount() ? pastedFile(value) : null);
  }

  protected usePaste(): void {
    if (!this.lineCount()) {
      this.error.set('Cole o texto copiado do site antes de continuar.');
      return;
    }
    this.fileChange.emit(pastedFile(this.text()));
  }

  protected formatSize(bytes: number): string {
    return bytes > 1024 * 1024 ? `${(bytes / 1024 / 1024).toFixed(1)} MB` : `${Math.max(1, Math.round(bytes / 1024))} KB`;
  }
}
