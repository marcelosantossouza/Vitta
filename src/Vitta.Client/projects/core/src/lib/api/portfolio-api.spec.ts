import { provideHttpClient } from '@angular/common/http';
import { HttpTestingController, provideHttpClientTesting } from '@angular/common/http/testing';
import { TestBed } from '@angular/core/testing';
import { PortfolioRowDto } from '../models/portfolio';
import { PortfolioApi } from './portfolio-api';

describe('PortfolioApi', () => {
  let api: PortfolioApi;
  let controller: HttpTestingController;

  beforeEach(() => {
    TestBed.configureTestingModule({ providers: [provideHttpClient(), provideHttpClientTesting()] });
    api = TestBed.inject(PortfolioApi);
    controller = TestBed.inject(HttpTestingController);
  });

  afterEach(() => controller.verify());

  it('busca as apólices do mês de uma importação', () => {
    let rows: PortfolioRowDto[] = [];
    api.rows('abc').subscribe(r => (rows = r));
    const req = controller.expectOne('/api/portfolio/imports/abc/rows');
    expect(req.request.method).toBe('GET');
    req.flush([{ id: '1', displayId: '123', holderName: 'Ana', coverages: 2 }]);
    expect(rows.length).toBe(1);
    expect(rows[0].coverages).toBe(2);
  });

  it('lista, detalha e exclui importações', () => {
    api.list().subscribe();
    api.get('abc').subscribe();
    api.delete('abc').subscribe();
    controller.expectOne(r => r.method === 'GET' && r.url === '/api/portfolio/imports').flush([]);
    controller.expectOne(r => r.method === 'GET' && r.url === '/api/portfolio/imports/abc').flush({});
    controller.expectOne(r => r.method === 'DELETE' && r.url === '/api/portfolio/imports/abc').flush(null);
  });
});
