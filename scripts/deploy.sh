#!/usr/bin/env bash
# Deploy do Vitta com UM comando, a partir do seu computador:
#   1. descobre a arquitetura (x86/ARM) e a memoria do servidor;
#   2. monta as imagens aqui e envia prontas por SSH (o servidor nao precisa do codigo nem de acesso ao GitHub);
#   3. copia so os arquivos de configuracao para ~/vitta no servidor;
#   4. na primeira vez, cria o .env perguntando dominio e login do administrador (senhas internas geradas sozinhas);
#   5. sobe os containers e espera o site responder.
#
# Uso:
#   SSH_HOST=ubuntu@IP SSH_KEY=~/Documents/vitta/ssh-key.key ./scripts/deploy.sh
# Opcionais: SKIP_BUILD=1 (reenvia/reinicia sem montar imagens de novo), SSH_PORT (22),
#            REMOTE_DIR (vitta, relativo a home do servidor), PROJECT (nome do projeto docker: vitta)
set -euo pipefail

: "${SSH_HOST:?defina SSH_HOST (ex.: ubuntu@203.0.113.10)}"
: "${SSH_KEY:?defina SSH_KEY (chave privada .key)}"
SSH_KEY="${SSH_KEY/#\~/$HOME}"
REMOTE_DIR="${REMOTE_DIR:-vitta}"
PROJECT="${PROJECT:-vitta}"
IMAGES=(vitta-api vitta-web vitta-ai vitta-backup)
cd "$(dirname "$0")/.."

log() { echo "[$(date '+%H:%M:%S')] $*"; }
SSH=(ssh -i "$SSH_KEY" -p "${SSH_PORT:-22}" -o StrictHostKeyChecking=accept-new -o ConnectTimeout=15 "$SSH_HOST")
remote() { "${SSH[@]}" "$@" </dev/null; }   # comandos simples: nao consomem o que voce digita
remote_in() { "${SSH[@]}" "$@"; }           # comandos que recebem dados pela entrada (imagens, .env)

# ---- 1. servidor ----
log "Conectando em $SSH_HOST..."
remote 'command -v docker >/dev/null' || { echo "Docker nao esta instalado no servidor (veja o passo de preparar o servidor)."; exit 1; }
ARCH=$(remote uname -m)
MEM_MB=$(remote "awk '/MemTotal/ {print int(\$2/1024)}' /proc/meminfo")
case "$ARCH" in
  x86_64) PLATFORM=linux/amd64 ;;
  aarch64 | arm64) PLATFORM=linux/arm64 ;;
  *) echo "Arquitetura nao suportada: $ARCH"; exit 1 ;;
esac
COMPOSE_FILES="-f docker-compose.prod.yml"
if [ "$MEM_MB" -lt 2048 ]; then COMPOSE_FILES="$COMPOSE_FILES -f deploy/lowmem.yml"; MODE="economico (${MEM_MB} MB)"; else MODE="normal (${MEM_MB} MB)"; fi
log "Servidor: $ARCH, memoria $MODE."

# ---- 2. imagens ----
if [ "${SKIP_BUILD:-0}" != "1" ]; then
  build() { log "Montando $1 ($PLATFORM)..."; docker buildx build --platform "$PLATFORM" -f "$2" -t "$1:latest" --load "$3" >/dev/null; }
  build vitta-api src/Vitta.API/Dockerfile .
  build vitta-web deploy/web.Dockerfile .
  build vitta-ai src/Vitta.AI/Dockerfile src/Vitta.AI
  build vitta-backup deploy/backup/Dockerfile deploy/backup
  log "Enviando imagens (alguns minutos na primeira vez)..."
  docker save "${IMAGES[@]/%/:latest}" | gzip -1 | remote_in 'gunzip | docker load' | sed 's/^/    /'
fi

# ---- 3. configuracao ----
log "Copiando configuracao para ~/$REMOTE_DIR..."
remote "mkdir -p $REMOTE_DIR/deploy/backup/rclone $REMOTE_DIR/backups"
COPYFILE_DISABLE=1 tar --no-mac-metadata -cz docker-compose.prod.yml deploy/lowmem.yml deploy/.env.prod.example deploy/backup/README.md \
  | remote_in "tar --warning=no-unknown-keyword -xz -C $REMOTE_DIR"

# ---- 4. .env (so na primeira vez) ----
if ! remote "test -f $REMOTE_DIR/.env"; then
  IP=$(echo "$SSH_HOST" | sed 's/.*@//')
  DEFAULT_DOMAIN="vitta.$(echo "$IP" | tr . -).sslip.io"
  echo
  echo "Primeiro deploy: vamos criar o .env do servidor."
  read -r -p "  Endereco do site [$DEFAULT_DOMAIN]: " DOMAIN; DOMAIN="${DOMAIN:-$DEFAULT_DOMAIN}"
  read -r -p "  E-mail do administrador (seu login): " ADMIN_EMAIL
  while :; do
    read -r -s -p "  Senha do administrador (min. 8 caracteres, sem aspas simples): " ADMIN_PASSWORD; echo
    if [ "${#ADMIN_PASSWORD}" -lt 8 ]; then echo "  Senha curta demais."; continue; fi
    case "$ADMIN_PASSWORD" in *"'"*) echo "  Nao use aspas simples (') na senha."; continue ;; esac
    break
  done
  PG=$(openssl rand -hex 24); JWT=$(openssl rand -hex 32)
  # Modelo sem as 5 linhas que preenchemos + as 5 linhas novas no fim. Aspas simples: o Docker nao interpreta $ nem &.
  # Tudo vai pela entrada padrao do ssh: as senhas nao aparecem na linha de comando de nenhuma das maquinas.
  {
    grep -v -E '^(VITTA_DOMAIN|POSTGRES_PASSWORD|JWT_KEY|ADMIN_EMAIL|ADMIN_PASSWORD)=' deploy/.env.prod.example
    echo
    echo "# Preenchido pelo scripts/deploy.sh no primeiro deploy"
    printf "VITTA_DOMAIN=%s\nPOSTGRES_PASSWORD=%s\nJWT_KEY=%s\nADMIN_EMAIL=%s\nADMIN_PASSWORD='%s'\n" \
      "$DOMAIN" "$PG" "$JWT" "$ADMIN_EMAIL" "$ADMIN_PASSWORD"
  } | remote_in "umask 077 && cat > $REMOTE_DIR/.env"
  log ".env criado em ~/$REMOTE_DIR/.env (e-mail diario, IA e Google Drive: edite depois com nano)."
fi

# ---- 5. subir ----
log "Subindo os containers..."
remote "cd $REMOTE_DIR && docker compose -p $PROJECT $COMPOSE_FILES up -d --no-build --remove-orphans" 2>&1 | sed 's/^/    /'
remote "docker image prune -f >/dev/null" || true

DOMAIN=$(remote "grep '^VITTA_DOMAIN=' $REMOTE_DIR/.env | cut -d= -f2")
log "Esperando https://$DOMAIN responder (o certificado HTTPS sai no primeiro acesso)..."
for _ in $(seq 1 60); do
  if curl -fsS ${CURL_INSECURE:+-k} --max-time 10 "https://$DOMAIN/health" 2>/dev/null | grep -q Healthy; then
    log "No ar: https://$DOMAIN"
    exit 0
  fi
  sleep 5
done
log "O site ainda nao respondeu. Veja os logs: ssh no servidor e 'cd ~/$REMOTE_DIR && docker compose -p $PROJECT $COMPOSE_FILES logs --tail 50'"
exit 1
