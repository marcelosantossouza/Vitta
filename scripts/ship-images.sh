#!/usr/bin/env bash
# Monta as imagens do Vitta no SEU computador e envia prontas para o servidor por SSH (sem registry).
# Para servidores pequenos (1 GB), onde montar as imagens la seria lento demais.
#
# Uso:
#   SSH_HOST=ubuntu@IP SSH_KEY=~/Documents/vitta/ssh-key.key ./scripts/ship-images.sh
# Opcionais: PLATFORM (linux/amd64; use linux/arm64 para a maquina ARM A1)
set -euo pipefail

: "${SSH_HOST:?defina SSH_HOST (ex.: ubuntu@203.0.113.10)}"
: "${SSH_KEY:?defina SSH_KEY (chave privada .key)}"
PLATFORM="${PLATFORM:-linux/amd64}"
SSH_KEY="${SSH_KEY/#\~/$HOME}"
cd "$(dirname "$0")/.."

log() { echo "[$(date '+%H:%M:%S')] $*"; }

build() { # nome dockerfile contexto
  log "Montando $1 ($PLATFORM)..."
  docker buildx build --platform "$PLATFORM" -f "$2" -t "$1:latest" --load "$3"
}

build vitta-api src/Vitta.API/Dockerfile .
build vitta-web deploy/web.Dockerfile .
build vitta-ai src/Vitta.AI/Dockerfile src/Vitta.AI
build vitta-backup deploy/backup/Dockerfile deploy/backup

log "Enviando para $SSH_HOST (pode levar alguns minutos)..."
docker save vitta-api:latest vitta-web:latest vitta-ai:latest vitta-backup:latest \
  | gzip -1 \
  | ssh -i "$SSH_KEY" -o StrictHostKeyChecking=accept-new "$SSH_HOST" 'gunzip | docker load'

log "Pronto. No servidor:  cd ~/Vitta && docker compose -f docker-compose.prod.yml -f deploy/lowmem.yml up -d --no-build"
