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
}
