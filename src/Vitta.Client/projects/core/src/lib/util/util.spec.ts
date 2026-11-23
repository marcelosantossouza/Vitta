import { HttpErrorResponse } from '@angular/common/http';
import { formatCpf, formatCurrency, formatDate, maskCpf, normalizeText, problemMessage } from './format';
import { changeTypeLabel, policyStatusLabel, PROSPECT_STAGE_LABELS, prospectStageLabel } from './labels';
import { PROSPECT_STAGES } from '../models/prospect';

describe('labels', () => {
  it('traduz status, etapas e tipos de mudanca', () => {
    expect(policyStatusLabel('BrokerChanged')).toBe('Troca de corretor');
    expect(prospectStageLabel('MeetingScheduled')).toBe('Reunião');
    expect(changeTypeLabel('Left')).toBe('Saiu');
  });

  it('tem rotulo para todas as etapas do funil', () => {
    for (const stage of PROSPECT_STAGES) {
      expect(PROSPECT_STAGE_LABELS[stage]).toBeTruthy();
    }
  });

  it('devolve o valor original quando desconhecido', () => {
    expect(policyStatusLabel('Foo')).toBe('Foo');
    expect(policyStatusLabel(null)).toBe('');
  });
});

describe('format', () => {
  it('formata moeda em reais', () => {
    expect(formatCurrency(1234.5).replace(/\s/g, ' ')).toBe('R$ 1.234,50');
  });

  it('formata e mascara CPF', () => {
    expect(formatCpf('52998224725')).toBe('529.982.247-25');
    expect(formatCpf('123')).toBe('123');
    expect(maskCpf('5299822')).toBe('529.982.2');
    expect(maskCpf('529982247259999')).toBe('529.982.247-25');
  });

  it('formata data ISO sem depender de fuso', () => {
    expect(formatDate('2026-01-05')).toBe('05/01/2026');
    expect(formatDate(null)).toBe('');
  });

  it('extrai mensagem de ProblemDetails', () => {
    const validation = new HttpErrorResponse({ status: 400, error: { errors: { Cpf: ['CPF invalido.'] } } });
    const conflict = new HttpErrorResponse({ status: 409, error: { title: 'Client.CpfAlreadyExists', detail: 'Ja existe.' } });
    const offline = new HttpErrorResponse({ status: 0 });
    expect(problemMessage(validation)).toBe('CPF invalido.');
    expect(problemMessage(conflict)).toBe('Ja existe.');
    expect(problemMessage(offline)).toContain('API');
  });
});

describe('normalizeText', () => {
  it('remove acentos e caixa para busca', () => {
    expect(normalizeText('  José da CONCEIÇÃO ')).toBe('jose da conceicao');
    expect(normalizeText(null)).toBe('');
  });
});
