import type { CapacitorConfig } from '@capacitor/cli';

// Configuracao do Capacitor para o app mobile. Plataformas (android/ios) serao adicionadas depois:
//   npx ng build mobile && npx cap add android && npx cap sync
const config: CapacitorConfig = {
  appId: 'br.com.vitta.app',
  appName: 'Vitta',
  webDir: 'dist/mobile/browser',
};

export default config;
