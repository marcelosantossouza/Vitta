export type ProspectStage = 'New' | 'Contacted' | 'MeetingScheduled' | 'ProposalSent' | 'Won' | 'Lost';

export const PROSPECT_STAGES: readonly ProspectStage[] = [
  'New',
  'Contacted',
  'MeetingScheduled',
  'ProposalSent',
  'Won',
  'Lost',
];

export interface ProspectInput {
  name: string;
  phone?: string | null;
  email?: string | null;
  source?: string | null;
  notes?: string | null;
  nextFollowUpOn?: string | null;
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
}

export interface ChangeStageRequest {
  stage: ProspectStage;
  nextFollowUpOn?: string | null;
}

export interface ConvertProspectRequest {
  cpf: string;
  birthDate?: string | null;
}
