/** Grupo de indicação: segurados que vieram por uma pessoa, empresa, sindicato ou código. */
export interface ReferralGroupInput {
  name: string;
  code: string | null;
  notes: string | null;
  /** Percentual sobre a comissão da corretora (10 = 10%). */
  sharePercent: number | null;
  /** Como o percentual aparece (Repasse, Bonificação, Participação...). */
  shareLabel: string | null;
}

export interface ReferralGroupDto {
  id: string;
  name: string;
  code: string | null;
  notes: string | null;
  sharePercent: number | null;
  shareLabel: string;
  members: number;
  activeMembers: number;
  monthlyPremium: number;
  monthlyCommission: number | null;
  monthlyShare: number | null;
}

export interface ReferralPolicyLineDto {
  id: string;
  insurerName: string;
  policyNumber: string;
  productName: string;
  monthlyPremium: number;
  commissionRate: number | null;
  monthlyCommission: number | null;
  issuedOn: string;
  /** Ativa, Nova (emitida no mês) ou Cancelada (encerrada no mês). */
  status: string;
  endedOn: string | null;
}

export interface ReferralMemberDto {
  clientId: string;
  name: string;
  /** Ativo, Cancelado ou Sem apólice. */
  status: string;
  since: string | null;
  monthlyPremium: number;
  monthlyCommission: number | null;
  importedCommission: number | null;
  policies: ReferralPolicyLineDto[];
}

export interface ReferralMonthDto {
  month: string;
  activeMembers: number;
  premium: number;
  commission: number | null;
  share: number | null;
}

export interface ReferralReportDto {
  group: ReferralGroupDto;
  month: string;
  activeMembers: number;
  newPolicies: number;
  cancelledPolicies: number;
  premium: number;
  estimatedCommission: number | null;
  policiesWithoutRate: number;
  importedCommission: number | null;
  /** "Planilhas" (comissão importada das seguradoras) ou "Apólices" (prêmio x %). */
  commissionBase: string;
  commission: number | null;
  share: number | null;
  members: ReferralMemberDto[];
  history: ReferralMonthDto[];
}
