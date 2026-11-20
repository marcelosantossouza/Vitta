import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { ImportTemplatesApi } from './import-templates-api';
import { PortfolioApi } from './portfolio-api';
import { ProspectsApi } from './prospects-api';

describe('ImportTemplatesApi', () => {
  let api: ImportTemplatesApi;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(ImportTemplatesApi);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('lista modelos filtrando pelo tipo (ou todos)', () => {
    api.list('Portfolio').subscribe();
    api.list().subscribe();
    const filtered = controller.expectOne(r => r.url === '/api/import-templates' && r.params.get('kind') === 'Portfolio');
    expect(filtered.request.method).toBe('GET');
    const all = controller.expectOne(r => r.url === '/api/import-templates' && !r.params.has('kind'));
    filtered.flush([]);
    all.flush([]);
  });

  it('busca os campos do tipo', () => {
    let labels: string[] = [];
    api.fields('Prospects').subscribe(list => (labels = list.map(f => f.label)));
    controller
      .expectOne('/api/import-templates/fields?kind=Prospects')
      .flush([{ field: 'name', label: 'Nome', required: true, multiple: false, description: '' }]);
    expect(labels).toEqual(['Nome']);
  });

  it('envia a planilha para análise como multipart', () => {
    const file = new File(['a;b'], 'carteira.csv', { type: 'text/csv' });
    api.analyze(file, 'Portfolio', 'Plan1').subscribe();
    const req = controller.expectOne('/api/import-templates/analyze');
    expect(req.request.method).toBe('POST');
    const body = req.request.body as FormData;
    expect(body.get('kind')).toBe('Portfolio');
    expect(body.get('sheetName')).toBe('Plan1');
    expect((body.get('file') as File).name).toBe('carteira.csv');
    req.flush({});
  });

  it('não envia sheetName quando não informado', () => {
    api.analyze(new File(['x'], 'p.xlsx'), 'Prospects').subscribe();
    const req = controller.expectOne('/api/import-templates/analyze');
    expect((req.request.body as FormData).has('sheetName')).toBe(false);
    req.flush({});
  });

  it('cria, atualiza, busca e exclui', () => {
    const input = { name: 'Carteira MAG', kind: 'Portfolio' as const, headerRow: 1, columns: [] };
    api.create(input).subscribe();
    api.update('42', input).subscribe();
    api.get('42').subscribe();
    api.delete('42').subscribe();
    const create = controller.expectOne(r => r.method === 'POST' && r.url === '/api/import-templates');
    expect(create.request.body).toEqual(input);
    create.flush({ id: '42' });
    controller.expectOne(r => r.method === 'PUT' && r.url === '/api/import-templates/42').flush(null);
    controller.expectOne(r => r.method === 'GET' && r.url === '/api/import-templates/42').flush({});
    controller.expectOne(r => r.method === 'DELETE' && r.url === '/api/import-templates/42').flush(null);
  });

  it('importa carteira com modelo (seguradora opcional) e prospects', () => {
    const file = new File(['x'], 'a.xlsx');
    TestBed.inject(PortfolioApi).upload({ referenceMonth: '2026-10', templateId: 't1', file }).subscribe();
    const upload = controller.expectOne('/api/portfolio/imports');
    const form = upload.request.body as FormData;
    expect(form.get('templateId')).toBe('t1');
    expect(form.has('insurerId')).toBe(false);
    upload.flush({});

    TestBed.inject(ProspectsApi).importFile(file, 't2').subscribe();
    const prospects = controller.expectOne('/api/prospects/imports');
    expect((prospects.request.body as FormData).get('templateId')).toBe('t2');
    prospects.flush({ totalRows: 1, created: 1, skipped: 0, messages: [] });
  });
});
