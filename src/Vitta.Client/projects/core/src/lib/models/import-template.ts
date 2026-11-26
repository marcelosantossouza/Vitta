export type ImportTemplateKind = 'Portfolio' | 'Prospects';

export const IMPORT_TEMPLATE_KINDS: readonly ImportTemplateKind[] = ['Portfolio', 'Prospects'];

/** Liga um campo do Vitta a uma coluna da planilha (texto do cabecalho). */
export interface ColumnMapping {
  field: string;
  column: string;
}

export interface ImportFieldDto {
  field: string;
  label: string;
  required: boolean;
  /** Aceita varias colunas (ex.: observacoes de prospects). */
  multiple: boolean;
  description: string;
}

export interface ImportTemplateDto {
  id: string;
  name: string;
  kind: ImportTemplateKind;
  insurerId: string | null;
  insurerName: string | null;
  sheetName: string | null;
  /** 1 = primeira linha */
  headerRow: number;
  columns: ColumnMapping[];
  createdByAi: boolean;
  aiNotes: string | null;
  createdAt: string;
  lastUsedAt: string | null;
}

export interface ImportTemplateInput {
  name: string;
  kind: ImportTemplateKind;
  insurerId?: string | null;
  sheetName?: string | null;
  headerRow: number;
  columns: ColumnMapping[];
  createdByAi?: boolean;
  aiNotes?: string | null;
}

export type AnalysisConfidence = 'high' | 'medium' | 'low';

export interface SpreadsheetAnalysisDto {
  fileName: string;
  /** Abas do .xlsx ([] para .csv) */
  sheets: string[];
  sheetName: string | null;
  /** Primeiras linhas "cruas" da aba */
  rows: string[][];
  /** Sugerida (1-based) */
  headerRow: number;
  columns: ColumnMapping[];
  suggestedName: string;
  notes: string | null;
  confidence: AnalysisConfidence;
  analyzedBy: 'ai' | 'heuristic';
}

export interface ProspectImportResultDto {
  totalRows: number;
  created: number;
  skipped: number;
  messages: string[];
  /** Já existiam e tiveram campos vazios completados (opção "atualizar quem já existe"). */
  updated: number;
  /** Lote criado (aparece no histórico e pode ser excluído). */
  importId: string | null;
}
