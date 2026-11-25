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
  totals: ImportTotalsDto;
}

export interface MonthTotalsDto {
  month: string;
  policies: number;
  premium: number;
  /** Nulo quando a planilha não traz comissão nem %. */
  commission: number | null;
  averageRate: number | null;
}

/** Mês importado e o anterior da mesma seguradora (nulo na primeira importação). */
export interface ImportTotalsDto {
  current: MonthTotalsDto;
  previous: MonthTotalsDto | null;
}

/** Linha da "tabela final" de uma importação: uma por apólice (coberturas da mesma proposta já unidas). */
export interface PortfolioRowDto {
  id: string;
  /** Nº da apólice ou, sem ele, "CPF 999.999.999-99 · dd/mm/aaaa". */
  displayId: string;
  policyNumber: string | null;
  holderName: string;
  /** 11 dígitos */
  holderCpf: string | null;
  /** Pode juntar coberturas: "VIDA INTEIRA + SAF FUNERAL". */
  productName: string | null;
  monthlyPremium: number | null;
  status: string | null;
  /** yyyy-MM-dd */
  startDate: string | null;
  city: string | null;
  /** yyyy-MM-01 */
  competence: string | null;
  /** Nº da parcela */
  installment: number | null;
  commissionAmount: number | null;
  /** 12 = 12% */
  commissionRate: number | null;
  /** Quantas linhas da planilha foram unidas nesta apólice (>= 1). */
  coverages: number;
  /** Linhas "Coluna: valor" separadas por \n. */
  extras: string | null;
  /** Cliente cadastrado com esse CPF. */
  clientId: string | null;
}
