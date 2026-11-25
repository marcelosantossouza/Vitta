#!/bin/sh
# Agenda o backup (padrao 03:00 de Brasilia) e deixa o cron em primeiro plano.
set -eu
SCHEDULE="${BACKUP_SCHEDULE:-0 3 * * *}"
echo "$SCHEDULE /usr/local/bin/backup.sh > /proc/1/fd/1 2>&1" > /etc/crontabs/root
echo "[backup] agendado: '$SCHEDULE' (TZ=${TZ:-UTC}); destino local /backups; nuvem: ${RCLONE_REMOTE:-desligada}"
[ "${BACKUP_ON_START:-false}" = "true" ] && /usr/local/bin/backup.sh || true
exec crond -f -l 8
