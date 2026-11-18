export interface Client {
  id: string;
  fullName: string;
  cpf: string;
  birthDate?: string | null;
  email?: string | null;
  phone?: string | null;
  notes?: string | null;
}

export interface CreateClientRequest {
  fullName: string;
  cpf: string;
  birthDate?: string | null;
  email?: string | null;
  phone?: string | null;
}
