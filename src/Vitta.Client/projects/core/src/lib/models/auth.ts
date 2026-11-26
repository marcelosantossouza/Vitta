/** Perfis de acesso (iguais aos da API). */
export type UserRole = 'Admin' | 'Broker' | 'Assistant';

export const USER_ROLE_LABELS: Record<UserRole, string> = {
  Admin: 'Administrador',
  Broker: 'Corretor',
  Assistant: 'Assistente',
};

export const USER_ROLE_HINTS: Record<UserRole, string> = {
  Admin: 'Tudo, inclusive usuários e Configurações.',
  Broker: 'Dia a dia completo: clientes, apólices, importações e prospecção (inclusive excluir).',
  Assistant: 'Vê e cadastra; não exclui nem acessa Configurações.',
};

export interface UserDto {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  /** Senha provisória: precisa trocar antes de usar o sistema. */
  mustChangePassword: boolean;
}

export interface LoginRequest {
  email: string;
  password: string;
}

export interface LoginResponse {
  accessToken: string;
  expiresAt: string;
  user: UserDto;
}

export interface ChangePasswordRequest {
  currentPassword: string;
  newPassword: string;
}

/** Linha da tela de usuários (só administradores). */
export interface UserListItemDto {
  id: string;
  name: string;
  email: string;
  role: UserRole;
  isActive: boolean;
  mustChangePassword: boolean;
  lastLoginAt: string | null;
  /** O próprio usuário logado (não pode mudar o próprio perfil nem se desativar). */
  isMe: boolean;
}

export interface CreateUserRequest {
  name: string;
  email: string;
  role: UserRole;
  temporaryPassword: string;
}

export interface UpdateUserRequest {
  name: string;
  role: UserRole;
}

export const MIN_PASSWORD_LENGTH = 8;
