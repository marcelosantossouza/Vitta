# mobile (reservado)

Espaco reservado para o app Ionic + Capacitor. Quando for criado:

```bash
cd client
npm i @ionic/angular @capacitor/core && npm i -D @capacitor/cli
ng g application mobile --style=scss --routing --ssr=false --prefix=vt
```

O app mobile deve importar modelos e servicos de `@vitta/core` (mesma lib do `web`)
e usar `provideVittaApi('https://<url-da-api>/api')`.
