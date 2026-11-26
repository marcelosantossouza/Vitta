export type ContactChannel = 'Call' | 'WhatsApp' | 'Email' | 'Visit' | 'Meeting' | 'Form' | 'Other';

export const CONTACT_CHANNEL_LABELS: Record<ContactChannel, string> = {
  Call: 'Ligação',
  WhatsApp: 'WhatsApp',
  Email: 'E-mail',
  Visit: 'Visita',
  Meeting: 'Reunião',
  Form: 'Formulário do site',
  Other: 'Outro',
};

/** Canais que a pessoa escolhe ao registrar (o "Formulário" entra sozinho pela página de captação). */
export const CONTACT_CHANNELS: readonly ContactChannel[] = ['Call', 'WhatsApp', 'Email', 'Meeting', 'Visit', 'Other'];

export interface ContactLogDto {
  id: string;
  occurredAt: string;
  channel: ContactChannel;
  summary: string;
  userName: string | null;
  userId: string | null;
}

export interface RegisterContactRequest {
  prospectId?: string | null;
  clientId?: string | null;
  channel: ContactChannel;
  summary: string;
  occurredAt?: string | null;
  /** Só prospect: agenda o próximo contato. */
  nextFollowUpOn?: string | null;
  /** Só prospect: se estiver em "Novo", passa para "Contatado". */
  advanceStage?: boolean;
}
