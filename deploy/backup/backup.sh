#!/bin/sh
# Um backup completo do banco. Falha alto (log) se o dump vier vazio ou corrompido.
set -eu
export PGPASSWORD="$POSTGRES_PASSWORD"
STAMP=$(date +%F-%H%M)
FILE="/backups/vitta-$STAMP.sql.gz"
KEEP="${BACKUP_KEEP_DAYS:-14}"

echo "[backup] $(date '+%F %T') iniciando -> $FILE"
pg_dump -h "${POSTGRES_HOST:-postgres}" -U "${POSTGRES_USER:-vitta}" "${POSTGRES_DB:-vitta}" --no-owner | gzip -9 > "$FILE.tmp"
gzip -t "$FILE.tmp"
[ "$(stat -c %s "$FILE.tmp")" -gt 1000 ] || { echo "[backup] ERRO: dump pequeno demais"; rm -f "$FILE.tmp"; exit 1; }
mv "$FILE.tmp" "$FILE"
echo "[backup] ok ($(du -h "$FILE" | cut -f1))"

find /backups -name 'vitta-*.sql.gz' -mtime +"$KEEP" -print -delete | sed 's/^/[backup] removido antigo: /'

if [ -n "${RCLONE_REMOTE:-}" ]; then
  if [ -f /config/rclone/rclone.conf ]; then
    rclone --config /config/rclone/rclone.conf copy "$FILE" "$RCLONE_REMOTE" && echo "[backup] copiado para $RCLONE_REMOTE"
    rclone --config /config/rclone/rclone.conf delete "$RCLONE_REMOTE" --min-age "${BACKUP_REMOTE_KEEP_DAYS:-60}d" || true
  else
    echo "[backup] AVISO: RCLONE_REMOTE definido mas /config/rclone/rclone.conf nao existe (veja deploy/backup/README.md)"
  fi
fi
