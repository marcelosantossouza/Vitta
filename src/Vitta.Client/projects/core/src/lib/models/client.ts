import { PolicyDto } from './policy';

export interface ClientInput {
  fullName: string;
  birthDate?: string | null;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
}

export interface CreateClientRequest extends ClientInput {
  cpf: string;
}

export interface ClientListItemDto {
  id: string;
  fullName: string;
  /** Formatado 000.000.000-00 */
  cpf: string;
  phone: string | null;
  email: string | null;
  activePolicyCount: number;
  monthlyPremium: number;
  /** Comissão mensal somada das apólices ativas com %; nulo quando nenhuma tem %. */
  monthlyCommission: number | null;
  policiesWithoutRate: number;
}

export interface ClientDetailDto {
  id: string;
  fullName: string;
  cpf: string;
  birthDate: string | null;
  email: string | null;
  phone: string | null;
  notes: string | null;
  createdAt: string;
  policies: PolicyDto[];
}
