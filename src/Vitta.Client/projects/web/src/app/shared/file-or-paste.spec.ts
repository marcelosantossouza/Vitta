import { Component, signal } from '@angular/core';
import { TestBed } from '@angular/core/testing';
import { FileOrPaste, PASTED_FILE_NAME } from './file-or-paste';

@Component({
  imports: [FileOrPaste],
  template: `<vt-file-or-paste [pasteAction]="action()" (fileChange)="files.push($event)" />`,
})
class Host {
  readonly action = signal<string | null>(null);
  readonly files: (File | null)[] = [];
}

function setup(action: string | null = null) {
  const fixture = TestBed.createComponent(Host);
  fixture.componentInstance.action.set(action);
  fixture.detectChanges();
  const el = fixture.nativeElement as HTMLElement;
  const buttons = () => Array.from(el.querySelectorAll('button')) as HTMLButtonElement[];
  const toPaste = () => {
    buttons().find(b => b.textContent?.includes('Colar do site'))!.click();
    fixture.detectChanges();
  };
  const type = (text: string) => {
    const area = el.querySelector('textarea')!;
    area.value = text;
    area.dispatchEvent(new Event('input'));
    fixture.detectChanges();
  };
  return { fixture, el, buttons, toPaste, type, files: fixture.componentInstance.files };
}

describe('FileOrPaste', () => {
  it('emite o texto colado como arquivo .txt e mostra a contagem de linhas', async () => {
    const { el, toPaste, type, files } = setup();
    toPaste();
    type('Apólice\tNome\n123\tAna\n\n456\tBruno\n');

    const last = files.at(-1)!;
    expect(last).toBeInstanceOf(File);
    expect(last.name).toBe(PASTED_FILE_NAME);
    expect(last.type).toBe('text/plain');
    expect(await last.text()).toContain('456\tBruno');
    expect(el.textContent).toContain('3 linhas coladas');

    type('   ');
    expect(files.at(-1)).toBeNull();
  });

  it('com acao explicita, so emite ao clicar no botao', () => {
    const { buttons, toPaste, type, files } = setup('Analisar texto colado');
    toPaste();
    type('a\tb');
    expect(files.length).toBe(0);

    buttons().find(b => b.textContent?.includes('Analisar texto colado'))!.click();
    expect(files.length).toBe(1);
    expect(files[0]!.name).toBe(PASTED_FILE_NAME);
  });

  it('recusa extensoes nao suportadas com mensagem amigavel', () => {
    const { fixture, el, files } = setup();
    const input = el.querySelector('input[type=file]') as HTMLInputElement;
    // jsdom nao tem DataTransfer: simula a lista de arquivos escolhidos.
    Object.defineProperty(input, 'files', { configurable: true, value: [new File(['x'], 'carteira.pdf')] });
    input.dispatchEvent(new Event('change'));
    fixture.detectChanges();

    expect(files.length).toBe(0);
    expect(el.textContent).toContain('.xlsx, .csv ou .txt');
  });
});
