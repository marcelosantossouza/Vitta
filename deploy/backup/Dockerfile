# Backup do Postgres: pg_dump toda madrugada, guarda N dias e (opcional) copia para a nuvem com rclone.
FROM postgres:17-alpine
RUN apk add --no-cache rclone tzdata
COPY backup.sh entrypoint.sh /usr/local/bin/
RUN chmod +x /usr/local/bin/backup.sh /usr/local/bin/entrypoint.sh
ENTRYPOINT ["/usr/local/bin/entrypoint.sh"]
