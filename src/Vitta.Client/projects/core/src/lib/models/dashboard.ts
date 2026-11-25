import { PortfolioChangeDto } from './portfolio';
import { ProspectDto, ProspectStage } from './prospect';

export interface DashboardDto {
  activeClients: number;
  totalClients: number;
  activePolicies: number;
  monthlyPremiumTotal: number;
  openProspects: number;
  followUpsDue: ProspectDto[];
  prospectsByStage: { stage: ProspectStage; count: number }[];
  policiesByInsurer: { insurerId: string; insurerName: string; count: number; monthlyPremium: number }[];
  recentExits: PortfolioChangeDto[];
  lastImportAt: string | null;
  /** Comissão do último mês carregado nas importações (nulo sem importações). */
  lastMonth: LastMonthCommissionDto | null;
}

export interface LastMonthCommissionDto {
  month: string;
  /** Nulo quando as planilhas do mês não trazem comissão nem %. */
  commission: number | null;
  premium: number;
  policies: number;
  insurers: string[];
  /** Preenchido quando o mês tem uma importação só. */
  importId: string | null;
  previousMonth: string | null;
  previousCommission: number | null;
}
