export type ProspectStage = 'New' | 'Contacted' | 'MeetingScheduled' | 'ProposalSent' | 'Won' | 'Lost';

export const PROSPECT_STAGES: readonly ProspectStage[] = [
  'New',
  'Contacted',
  'MeetingScheduled',
  'ProposalSent',
  'Won',
  'Lost',
];

/** Perfil vindo das listas: endereço e renda. Valores em reais; UF com 2 letras. */
export interface ProspectProfile {
  phone2: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  birthDate: string | null;
  occupation: string | null;
  estimatedSalary: number | null;
  discounts: number | null;
  loans: number | null;
}

export interface ProspectInput {
  name: string;
  phone?: string | null;
  email?: string | null;
  source?: string | null;
  notes?: string | null;
  nextFollowUpOn?: string | null;
  profile?: ProspectProfile | null;
}

export interface ProspectDto {
  id: string;
  name: string;
  phone: string | null;
  email: string | null;
  source: string | null;
  stage: ProspectStage;
  nextFollowUpOn: string | null;
  notes: string | null;
  convertedClientId: string | null;
  createdAt: string;
  phone2: string | null;
  address: string | null;
  city: string | null;
  state: string | null;
  zipCode: string | null;
  birthDate: string | null;
  occupation: string | null;
  estimatedSalary: number | null;
  discounts: number | null;
  loans: number | null;
  /** Importação que criou o prospect (nulo = cadastro manual). */
  importId: string | null;
}

export type FollowUpFilter = 'Any' | 'Overdue' | 'Today' | 'Next7Days' | 'None';
export type ProspectSort = 'FollowUp' | 'SalaryDesc' | 'SalaryAsc' | 'Name' | 'Newest' | 'City';

/** Filtros da lista e do dashboard (os dois usam os mesmos). */
export interface ProspectFilter {
  search?: string;
  stage?: ProspectStage[];
  city?: string;
  state?: string;
  source?: string;
  importId?: string;
  salaryMin?: number | null;
  salaryMax?: number | null;
  followUp?: FollowUpFilter;
  hasPhone?: boolean | null;
}

export interface LabelCount {
  label: string;
  count: number;
}

export interface ProspectInsightsDto {
  total: number;
  open: number;
  newLast7Days: number;
  overdue: number;
  dueToday: number;
  won: number;
  lost: number;
  conversionRate: number | null;
  averageSalary: number | null;
  withSalary: number;
  byStage: LabelCount[];
  topCities: LabelCount[];
  bySource: LabelCount[];
  salaryBands: LabelCount[];
}

export interface ProspectImportItemDto {
  id: string;
  fileName: string;
  templateName: string;
  createdAt: string;
  totalRows: number;
  created: number;
  updated: number;
  skipped: number;
  /** Prospects ainda ligados ao lote. */
  stillLinked: number;
  /** Desses, quantos ainda estão em "Novo" (saem se a importação for excluída). */
  stillNew: number;
}

export interface ProspectFilterOptionsDto {
  cities: string[];
  states: string[];
  sources: string[];
  imports: ProspectImportItemDto[];
}

export interface DeleteProspectImportResultDto {
  removed: number;
  kept: number;
}

export const FOLLOW_UP_FILTER_LABELS: Record<FollowUpFilter, string> = {
  Any: 'Qualquer data',
  Overdue: 'Atrasados',
  Today: 'Para hoje',
  Next7Days: 'Próximos 7 dias',
  None: 'Sem data agendada',
};

export const PROSPECT_SORT_LABELS: Record<ProspectSort, string> = {
  FollowUp: 'Próximo contato',
  SalaryDesc: 'Maior salário',
  SalaryAsc: 'Menor salário',
  Name: 'Nome (A-Z)',
  City: 'Cidade',
  Newest: 'Mais recentes',
};

export interface ChangeStageRequest {
  stage: ProspectStage;
  nextFollowUpOn?: string | null;
}

export interface ConvertProspectRequest {
  cpf: string;
  birthDate?: string | null;
}
