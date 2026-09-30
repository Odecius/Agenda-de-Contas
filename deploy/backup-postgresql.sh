#!/bin/sh

set -eu
umask 077

readonly marker_name='.agendador-backup-target'
readonly marker_value='agendador-postgresql-backup-v1'
readonly prefix='agendador-postgresql-'

started_at="$(date +%s)"
temporary_files=''

log_event() {
    printf 'timestamp=%s operation=%s result=%s duration_seconds=%s file=%s checksum_status=%s\n' \
        "$(date -u +%Y-%m-%dT%H:%M:%SZ)" "$1" "$2" "$3" "$4" "$5"
}

write_status() {
    result="$1"
    operation="$2"
    file_name="$3"
    checksum_status="$4"
    duration="$(($(date +%s) - started_at))"
    status_tmp="$local_dir/.backup-status.$$"
    temporary_files="$temporary_files $status_tmp"
    {
        printf 'timestamp=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
        printf 'operation=%s\n' "$operation"
        printf 'result=%s\n' "$result"
        printf 'duration_seconds=%s\n' "$duration"
        printf 'file=%s\n' "$file_name"
        printf 'checksum_status=%s\n' "$checksum_status"
    } > "$status_tmp"
    mv -f -- "$status_tmp" "$local_dir/backup-status.env"
    log_event "$operation" "$result" "$duration" "$file_name" "$checksum_status"
}

cleanup() {
    for path in $temporary_files; do
        [ ! -e "$path" ] || rm -f -- "$path"
    done
}

fail() {
    operation="$1"
    file_name="${2:-none}"
    cleanup
    write_status 'failure' "$operation" "$file_name" 'failed'
    exit 1
}

require_directory() {
    candidate="$1"
    purpose="$2"
    case "$candidate" in
        /*) ;;
        *) printf '%s directory must be absolute.\n' "$purpose" >&2; return 1 ;;
    esac
    case "$candidate" in
        *[[:space:]]*) printf '%s directory cannot contain whitespace.\n' "$purpose" >&2; return 1 ;;
    esac
    [ -d "$candidate" ] || { printf '%s directory does not exist.\n' "$purpose" >&2; return 1; }
    resolved="$(cd -- "$candidate" && pwd -P)"
    [ "$resolved" != '/' ] || { printf '%s directory cannot be filesystem root.\n' "$purpose" >&2; return 1; }
    [ -f "$resolved/$marker_name" ] || { printf '%s directory marker is missing.\n' "$purpose" >&2; return 1; }
    [ "$(cat "$resolved/$marker_name")" = "$marker_value" ] || { printf '%s directory marker is invalid.\n' "$purpose" >&2; return 1; }
    printf '%s\n' "$resolved"
}

apply_retention() {
    directory="$1"
    retention_days="$2"
    minimum_keep="$3"
    total="$(find "$directory" -maxdepth 1 -type f -name "${prefix}*.dump" | wc -l | tr -d ' ')"
    [ "$total" -gt "$minimum_keep" ] || return 0

    candidates="$(find "$directory" -maxdepth 1 -type f -name "${prefix}*.dump" -mtime "+$retention_days" -print | sort)"
    for candidate in $candidates; do
        [ "$total" -gt "$minimum_keep" ] || break
        base="$(basename "$candidate")"
        case "$base" in
            ${prefix}[0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]T[0-9][0-9][0-9][0-9][0-9][0-9]Z.dump) ;;
            *) continue ;;
        esac
        rm -f -- "$candidate" "$candidate.sha256" || return 1
        total=$((total - 1))
    done
}

sync_backup() {
    source_path="$1"
    base="$(basename "$source_path")"
    manifest="$source_path.sha256"
    [ -s "$source_path" ] || fail 'local-backup-empty' "$base"
    [ -s "$manifest" ] || fail 'local-manifest-missing' "$base"
    (cd "$local_dir" && sha256sum -c -- "$(basename "$manifest")" >/dev/null 2>&1) || fail 'local-checksum' "$base"
    pg_restore --list "$source_path" >/dev/null 2>&1 || fail 'local-catalogue' "$base"

    destination="$offhost_dir/$base"
    destination_manifest="$destination.sha256"
    if [ -e "$destination" ]; then
        [ -s "$destination_manifest" ] || fail 'offhost-manifest-missing' "$base"
        (cd "$offhost_dir" && sha256sum -c -- "$(basename "$destination_manifest")" >/dev/null 2>&1) || fail 'offhost-checksum' "$base"
        source_hash="$(sha256sum "$source_path" | awk '{print $1}')"
        destination_hash="$(sha256sum "$destination" | awk '{print $1}')"
        [ "$source_hash" = "$destination_hash" ] || fail 'offhost-source-mismatch' "$base"
        return 0
    fi

    destination_tmp="$offhost_dir/.$base.partial.$$"
    manifest_tmp="$offhost_dir/.$base.sha256.partial.$$"
    temporary_files="$temporary_files $destination_tmp $manifest_tmp"
    cp -p -- "$source_path" "$destination_tmp" || fail 'offhost-copy' "$base"
    source_hash="$(sha256sum "$source_path" | awk '{print $1}')"
    copied_hash="$(sha256sum "$destination_tmp" | awk '{print $1}')"
    [ "$source_hash" = "$copied_hash" ] || fail 'offhost-copy-checksum' "$base"
    printf '%s  %s\n' "$copied_hash" "$base" > "$manifest_tmp"
    mv -- "$destination_tmp" "$destination" || fail 'offhost-publish' "$base"
    mv -- "$manifest_tmp" "$destination_manifest" || fail 'offhost-manifest-publish' "$base"
    temporary_files=''
}

: "${BACKUP_DATABASE:?BACKUP_DATABASE is required}"
: "${BACKUP_LOCAL_DIR:?BACKUP_LOCAL_DIR is required}"
: "${BACKUP_OFFHOST_DIR:?BACKUP_OFFHOST_DIR is required}"
: "${BACKUP_OFFHOST_ENCRYPTION_ASSERTION:?BACKUP_OFFHOST_ENCRYPTION_ASSERTION is required}"

case "$BACKUP_DATABASE" in
    ''|*[!A-Za-z0-9_]*|[0-9]*) printf 'BACKUP_DATABASE must be a simple database identifier.\n' >&2; exit 1 ;;
esac
[ "$BACKUP_OFFHOST_ENCRYPTION_ASSERTION" = 'external-managed-encryption' ] || {
    printf 'Off-host encryption must be explicitly asserted as externally managed.\n' >&2
    exit 1
}

retention_days="${BACKUP_RETENTION_DAYS:-30}"
minimum_keep="${BACKUP_MINIMUM_KEEP:-7}"
case "$retention_days:$minimum_keep" in
    *[!0-9:]*|:*) printf 'Retention values must be non-negative integers.\n' >&2; exit 1 ;;
esac

if ! local_dir="$(require_directory "$BACKUP_LOCAL_DIR" 'Local backup')"; then
    exit 1
fi
if ! offhost_dir="$(require_directory "$BACKUP_OFFHOST_DIR" 'Off-host backup')"; then
    fail 'offhost-target-validation' 'none'
fi
[ "$local_dir" != "$offhost_dir" ] || { printf 'Local and off-host directories must differ.\n' >&2; exit 1; }

timestamp="${BACKUP_TIMESTAMP:-$(date -u +%Y%m%dT%H%M%SZ)}"
case "$timestamp" in
    [0-9][0-9][0-9][0-9][0-9][0-9][0-9][0-9]T[0-9][0-9][0-9][0-9][0-9][0-9]Z) ;;
    *) printf 'BACKUP_TIMESTAMP must use YYYYMMDDTHHMMSSZ.\n' >&2; exit 1 ;;
esac

file_name="${prefix}${timestamp}.dump"
final_path="$local_dir/$file_name"
manifest_path="$final_path.sha256"
dump_tmp="$local_dir/.$file_name.partial.$$"
manifest_tmp="$local_dir/.$file_name.sha256.partial.$$"
temporary_files="$dump_tmp $manifest_tmp"

if [ -e "$final_path" ]; then
    [ -s "$manifest_path" ] || fail 'existing-manifest-missing' "$file_name"
    (cd "$local_dir" && sha256sum -c -- "$(basename "$manifest_path")" >/dev/null 2>&1) || fail 'existing-checksum' "$file_name"
else
    pg_dump --dbname "$BACKUP_DATABASE" --format=custom --file="$dump_tmp" >/dev/null 2>&1 || fail 'pg-dump' "$file_name"
    [ -s "$dump_tmp" ] || fail 'pg-dump-empty' "$file_name"
    pg_restore --list "$dump_tmp" >/dev/null 2>&1 || fail 'pg-dump-catalogue' "$file_name"
    checksum="$(sha256sum "$dump_tmp" | awk '{print $1}')"
    printf '%s  %s\n' "$checksum" "$file_name" > "$manifest_tmp"
    mv -- "$dump_tmp" "$final_path" || fail 'local-publish' "$file_name"
    mv -- "$manifest_tmp" "$manifest_path" || fail 'local-manifest-publish' "$file_name"
    temporary_files=''
fi

for candidate in "$local_dir"/${prefix}*.dump; do
    [ -e "$candidate" ] || continue
    sync_backup "$candidate"
done

apply_retention "$offhost_dir" "$retention_days" "$minimum_keep" || fail 'offhost-retention' "$file_name"
apply_retention "$local_dir" "$retention_days" "$minimum_keep" || fail 'local-retention' "$file_name"
cleanup
write_status 'success' 'backup-and-offhost-copy' "$file_name" 'verified'
