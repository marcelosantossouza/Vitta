export type PolicyStatus = 'Active' | 'Cancelled' | 'Lapsed' | 'BrokerChanged';
export type PolicyEndStatus = Exclude<PolicyStatus, 'Active'>;

export interface PolicyDto {
  id: string;
  clientId: string;
  clientName: string;
  insurerId: string;
  insurerName: string;
  policyNumber: string;
  productName: string;
  monthlyPremium: number;
  issuedOn: string;
  status: PolicyStatus;
  endedOn: string | null;
  endReason: string | null;
  /** % de comissão do corretor (12.5 = 12,5%); nulo = não informado. */
  commissionRate: number | null;
  /** Prêmio × %: quanto o corretor recebe por mês. */
  monthlyCommission: number | null;
}

/** Números da carteira cadastrada (cards de Clientes e Apólices). Valores consideram só apólices ativas. */
export interface BookSummaryDto {
  totalClients: number;
  clientsWithActivePolicies: number;
  activePolicies: number;
  endedPolicies: number;
  monthlyPremium: number;
  monthlyCommission: number;
  averageCommissionRate: number | null;
  /** Apólices ativas sem % de comissão: o total de comissão está incompleto enquanto houver. */
  activeWithoutRate: number;
}

export interface CreatePolicyRequest {
  clientId: string;
  insurerId: string;
  policyNumber: string;
  productName: string;
  monthlyPremium: number;
  issuedOn: string;
  commissionRate?: number | null;
}

export interface UpdatePolicyRequest {
  productName: string;
  monthlyPremium: number;
  commissionRate: number | null;
}

export interface EndPolicyRequest {
  status: PolicyEndStatus;
  endedOn: string;
  reason?: string | null;
}

export interface PolicyFilter {
  clientId?: string;
  insurerId?: string;
  status?: PolicyStatus | '';
  search?: string;
  page?: number;
  pageSize?: number;
}
