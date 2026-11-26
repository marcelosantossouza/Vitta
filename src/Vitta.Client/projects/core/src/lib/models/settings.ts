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

/** E-mail diário com o resumo da corretora. */
export interface NotificationSettingsDto {
  dailyEmailEnabled: boolean;
  recipients: string[];
  /** "HH:mm", horário de Brasília. */
  sendAt: string;
  lastSentOn: string | null;
  /** SMTP configurado no servidor (.env). */
  emailConfigured: boolean;
  emailFrom: string | null;
}

export interface NotificationSettingsInput {
  dailyEmailEnabled: boolean;
  recipients: string[];
  sendAt: string;
}

/** Página pública de captação (/simular). */
export interface LeadCaptureSettings {
  enabled: boolean;
  brokerName: string;
  headline: string;
  whatsApp: string | null;
}

export interface LeadPageDto extends LeadCaptureSettings {
  consentText: string;
}

export interface LeadRequest {
  name: string;
  phone: string;
  email: string | null;
  city: string | null;
  state: string | null;
  birthDate: string | null;
  interest: string | null;
  origin: string | null;
  consent: boolean;
  /** Campo invisível anti-robô: sempre vazio. */
  website: string | null;
}
