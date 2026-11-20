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
}

export interface CreatePolicyRequest {
  clientId: string;
  insurerId: string;
  policyNumber: string;
  productName: string;
  monthlyPremium: number;
  issuedOn: string;
}

export interface UpdatePolicyRequest {
  productName: string;
  monthlyPremium: number;
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
