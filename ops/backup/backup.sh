#!/bin/sh
# Nightly backup: pg_dump into /backups/db (rotated), then an additive copy of /data/storage and
# the dumps to an SFTP host (e.g. the Hostinger hosting) when SFTP_HOST is set.
# Run once and exit with:  docker compose -f docker-compose.prod.yml run --rm backup now
set -eu

BACKUP_HOUR="${BACKUP_HOUR:-3}"
KEEP_DAYS="${BACKUP_KEEP_DAYS:-14}"
INCLUDE_EVIDENCIAS="${BACKUP_INCLUDE_EVIDENCIAS:-false}"

offsite_configured() { [ -n "${SFTP_HOST:-}" ]; }

setup_offsite() {
    export RCLONE_CONFIG_OFFSITE_TYPE=sftp
    export RCLONE_CONFIG_OFFSITE_HOST="$SFTP_HOST"
    export RCLONE_CONFIG_OFFSITE_PORT="${SFTP_PORT:-22}"
    export RCLONE_CONFIG_OFFSITE_USER="$SFTP_USER"
    RCLONE_CONFIG_OFFSITE_PASS="$(rclone obscure "$SFTP_PASSWORD")"
    export RCLONE_CONFIG_OFFSITE_PASS
}

run_backup() {
    mkdir -p /backups/db
    stamp="$(date +%Y%m%d-%H%M%S)"
    dump="/backups/db/db-$stamp.sql"

    echo "[backup] $stamp: volcando Postgres"
    PGPASSWORD="$POSTGRES_PASSWORD" pg_dump -h postgres -U "$POSTGRES_USER" -d "$POSTGRES_DB" -f "$dump"
    gzip "$dump"
    find /backups/db -name 'db-*.sql.gz' -mtime +"$KEEP_DAYS" -delete

    if offsite_configured; then
        setup_offsite
        remote="offsite:${SFTP_REMOTE_DIR:-viriato-backups}"

        echo "[backup] subiendo base de datos a $remote/db"
        rclone copy /backups/db "$remote/db"
        rclone delete "$remote/db" --min-age "${KEEP_DAYS}d"

        # copy, never sync: a file deleted here is not deleted from the offsite copy.
        echo "[backup] subiendo archivos a $remote/storage (evidencias: $INCLUDE_EVIDENCIAS)"
        if [ "$INCLUDE_EVIDENCIAS" = "true" ]; then
            rclone copy /data/storage "$remote/storage"
        else
            rclone copy /data/storage "$remote/storage" --exclude "evidencias/**"
        fi
    else
        echo "[backup] SFTP_HOST vacío: copia solo local"
    fi

    echo "[backup] terminado"
}

if [ "${1:-}" = "now" ]; then
    run_backup
    exit 0
fi

while true; do
    h="$(date +%H)"; m="$(date +%M)"; s="$(date +%S)"
    elapsed=$(( ${h#0} * 3600 + ${m#0} * 60 + ${s#0} ))
    wait=$(( BACKUP_HOUR * 3600 - elapsed ))
    [ "$wait" -le 0 ] && wait=$(( wait + 86400 ))
    echo "[backup] próxima copia en ${wait}s (hora local $(date +%H:%M))"
    sleep "$wait"
    # A failed run must not kill the loop: tomorrow's run still has to happen.
    run_backup || echo "[backup] ERROR en la copia, se reintenta mañana"
done
