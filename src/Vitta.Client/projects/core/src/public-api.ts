/*
 * @vitta/core - codigo compartilhado entre o app web e o app Ionic:
 * modelos, clientes HTTP da Vitta.API, autenticacao e utilitarios. Sem componentes visuais.
 */
export * from './lib/config/api-config';

export * from './lib/models/common';
export * from './lib/models/auth';
export * from './lib/models/client';
export * from './lib/models/insurer';
export * from './lib/models/policy';
export * from './lib/models/prospect';
export * from './lib/models/portfolio';
export * from './lib/models/dashboard';
export * from './lib/models/import-template';
export * from './lib/models/settings';
export * from './lib/models/referral';

export * from './lib/api/auth-api';
export * from './lib/api/dashboard-api';
export * from './lib/api/clients-api';
export * from './lib/api/insurers-api';
export * from './lib/api/policies-api';
export * from './lib/api/prospects-api';
export * from './lib/api/portfolio-api';
export * from './lib/api/import-templates-api';
export * from './lib/api/settings-api';
export * from './lib/api/users-api';
export * from './lib/api/referrals-api';

export * from './lib/auth/auth.service';
export * from './lib/auth/auth.interceptor';
export * from './lib/auth/auth.guards';

export * from './lib/util/labels';
export * from './lib/util/format';
