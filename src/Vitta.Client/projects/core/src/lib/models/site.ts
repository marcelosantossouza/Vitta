/** Home page pública da corretora. Nome, título e WhatsApp vêm da página de captação. */
export interface SiteInput {
  enabled: boolean;
  /** Domínio onde a home abre na raiz (ex.: corretora.usevitta.duckdns.org). */
  host: string | null;
  about: string | null;
  address: string | null;
  phone: string | null;
  email: string | null;
  hours: string | null;
  instagram: string | null;
  facebook: string | null;
  linkedIn: string | null;
  susep: string | null;
  cnpj: string | null;
  products: string[];
  accentColor: string | null;
}

export interface SiteDto extends Omit<SiteInput, 'accentColor'> {
  brokerName: string;
  headline: string;
  whatsApp: string | null;
  accentColor: string;
  /** Muda quando a imagem muda; nulo = sem imagem. */
  logoVersion: string | null;
  coverVersion: string | null;
  /** O formulário de contato só aparece com a página de captação ligada. */
  leadFormEnabled: boolean;
  consentText: string;
}

export type SiteImageKind = 'logo' | 'cover';
