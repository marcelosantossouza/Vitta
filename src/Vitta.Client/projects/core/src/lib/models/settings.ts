export type AiProviderId = 'anthropic' | 'groq';
export type AiProviderEnum = 'Anthropic' | 'Groq';
export type AiEffort = 'low' | 'medium' | 'high';

export interface AiModelOption {
  id: string;
  label: string;
  note: string;
}

export interface AiProviderInfo {
  id: AiProviderId;
  label: string;
  /** Chave deste provedor configurada no servidor de IA (.env). */
  configured: boolean;
  defaultModel: string;
  models: AiModelOption[];
}

export interface AiSettingsDto {
  /** Escolha salva; nulos = padrões do servidor de IA. */
  provider: AiProviderId | null;
  model: string | null;
  effort: AiEffort | null;
  isCustom: boolean;
  serviceOnline: boolean;
  defaultProvider: AiProviderId | null;
  defaultEffort: AiEffort | null;
  providers: AiProviderInfo[];
}

export interface AiSettingsInput {
  provider: AiProviderEnum;
  model: string;
  effort: AiEffort;
}

export interface AiConnectionTest {
  ok: boolean;
  provider: string;
  model: string;
  latencyMs: number | null;
  message: string;
}

export const AI_EFFORT_LABELS: Record<AiEffort, string> = {
  low: 'Baixo — mais rápido e barato',
  medium: 'Médio — recomendado',
  high: 'Alto — mais cuidadoso',
};

export function toAiProviderEnum(id: AiProviderId): AiProviderEnum {
  return id === 'groq' ? 'Groq' : 'Anthropic';
}
