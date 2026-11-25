# Site (Angular) + proxy da API + HTTPS automatico (Caddy). Build a partir da RAIZ do repositorio:
#   docker build -f deploy/web.Dockerfile .
FROM node:24-alpine AS build
WORKDIR /client
COPY src/Vitta.Client/package.json src/Vitta.Client/package-lock.json* ./
RUN npm ci
COPY src/Vitta.Client/ ./
RUN npx ng build web --configuration production

FROM caddy:2-alpine
COPY deploy/Caddyfile /etc/caddy/Caddyfile
COPY --from=build /client/dist/web/browser /srv
