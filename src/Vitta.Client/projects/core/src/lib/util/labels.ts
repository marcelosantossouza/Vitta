import { ImportTemplateKind } from '../models/import-template';
import { PortfolioChangeType } from '../models/portfolio';
import { PolicyStatus } from '../models/policy';
import { ProspectStage } from '../models/prospect';

export const POLICY_STATUS_LABELS: Record<PolicyStatus, string> = {
  Active: 'Ativa',
  Cancelled: 'Cancelada',
  Lapsed: 'Caducada',
  BrokerChanged: 'Troca de corretor',
};

export const PROSPECT_STAGE_LABELS: Record<ProspectStage, string> = {
  New: 'Novo',
  Contacted: 'Contatado',
  MeetingScheduled: 'Reunião',
  ProposalSent: 'Proposta',
  Won: 'Ganho',
  Lost: 'Perdido',
};

export const CHANGE_TYPE_LABELS: Record<PortfolioChangeType, string> = {
  Entered: 'Entrou',
  Left: 'Saiu',
  Changed: 'Mudou',
};

export function policyStatusLabel(status: PolicyStatus | string | null | undefined): string {
  return (status && POLICY_STATUS_LABELS[status as PolicyStatus]) || status || '';
}

export function prospectStageLabel(stage: ProspectStage | string | null | undefined): string {
  return (stage && PROSPECT_STAGE_LABELS[stage as ProspectStage]) || stage || '';
}

export function changeTypeLabel(type: PortfolioChangeType | string | null | undefined): string {
  return (type && CHANGE_TYPE_LABELS[type as PortfolioChangeType]) || type || '';
}

export const IMPORT_TEMPLATE_KIND_LABELS: Record<ImportTemplateKind, string> = {
  Portfolio: 'Carteira',
  Prospects: 'Prospecção',
};

export function importTemplateKindLabel(kind: ImportTemplateKind | string | null | undefined): string {
  return (kind && IMPORT_TEMPLATE_KIND_LABELS[kind as ImportTemplateKind]) || kind || '';
}
