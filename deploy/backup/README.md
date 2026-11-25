# Backup do Vitta

O serviço `backup` do `docker-compose.prod.yml` faz um `pg_dump` completo todo dia (padrão 03:00, horário de Brasília),
confere se o arquivo não veio vazio/corrompido e guarda em `./backups` por `BACKUP_KEEP_DAYS` dias (padrão 14).

Ver o log: `docker compose -f docker-compose.prod.yml logs backup`
Backup na hora: `docker compose -f docker-compose.prod.yml exec backup backup.sh`

## Cópia para o Google Drive (recomendado)

Backup só dentro do servidor não protege se o servidor sumir. Para copiar cada backup para o Google Drive:

1. **No seu computador** (precisa de navegador), instale o rclone (`brew install rclone` no Mac) e rode:
   ```bash
   rclone authorize "drive"
   ```
   Faça login no Google e copie o bloco `{"access_token":...}` que aparece no terminal.
2. **No servidor**, na pasta do Vitta:
   ```bash
   mkdir -p deploy/backup/rclone
   docker compose -f docker-compose.prod.yml run --rm --entrypoint rclone backup --config /config/rclone/rclone.conf config
   ```
   `n` (novo) → nome `gdrive` → tipo `drive` → deixe client_id/secret em branco → escopo `1` (acesso total)
   → em "Use web browser to automatically authenticate?" responda `n` → cole o bloco do passo 1 → confirme.
3. No `.env`, defina `RCLONE_REMOTE=gdrive:vitta-backups` e rode `docker compose -f docker-compose.prod.yml up -d backup`.
4. Teste: `docker compose -f docker-compose.prod.yml exec backup backup.sh` → deve aparecer "copiado para gdrive:vitta-backups".

Na nuvem, backups com mais de 60 dias são apagados (`BACKUP_REMOTE_KEEP_DAYS`).

## Restaurar

```bash
# banco vazio (servidor novo): suba só o postgres, restaure e depois suba o resto
docker compose -f docker-compose.prod.yml up -d postgres
gunzip -c backups/vitta-AAAA-MM-DD-HHMM.sql.gz | docker compose -f docker-compose.prod.yml exec -T postgres psql -U vitta vitta
docker compose -f docker-compose.prod.yml up -d
```
