export type PortfolioChangeType = 'Entered' | 'Left' | 'Changed';

export interface PortfolioImportDto {
  id: string;
  insurerId: string;
  insurerName: string;
  /** yyyy-MM-01 */
  referenceMonth: string;
  fileName: string;
  status: string;
  totalRows: number;
  entered: number;
  left: number;
  changed: number;
  isFirstImport: boolean;
  createdAt: string;
}

export interface PortfolioChangeDto {
  id: string;
  importId: string;
  referenceMonth: string;
  insurerName: string;
  type: PortfolioChangeType;
  policyNumber: string;
  holderName: string;
  holderCpf: string | null;
  detail: string | null;
  clientId: string | null;
}

export interface PortfolioImportDetailDto extends PortfolioImportDto {
  changes: PortfolioChangeDto[];
}
