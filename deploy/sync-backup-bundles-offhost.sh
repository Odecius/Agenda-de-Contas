#!/usr/bin/env bash

set -Eeuo pipefail
umask 077

readonly marker_name='BACKUP_OK'
readonly manifest_name='SHA256SUMS'
readonly pending_marker='.BACKUP_OK.pending'
readonly bundle_pattern='20??-??-??_??????'

started_at="$(date +%s)"
status_result='failure'
status_operation='initialization'
status_bundles_discovered=0
status_bundles_verified=0
status_bundles_copied=0
status_bundles_existing=0
status_bundles_partial=0
temporary_files=()

log_event() {
    printf 'event=%s result=%s bundle=%s count=%s duration_seconds=%s\n' \
        "$1" "$2" "${3:-none}" "${4:-0}" "$(($(date +%s) - started_at))"
}

write_status() {
    [ -n "${SYNC_STATUS_FILE:-}" ] || return 0
    case "$SYNC_STATUS_FILE" in
        /*) ;;
        *) printf 'SYNC_STATUS_FILE must be absolute.\n' >&2; return 1 ;;
    esac
    status_directory="$(dirname -- "$SYNC_STATUS_FILE")"
    [ -d "$status_directory" ] || { printf 'Status directory does not exist.\n' >&2; return 1; }
    status_tmp="$(mktemp "$status_directory/.sync-status.XXXXXX")"
    temporary_files+=("$status_tmp")
    {
        printf 'timestamp=%s\n' "$(date -u +%Y-%m-%dT%H:%M:%SZ)"
        printf 'result=%s\n' "$status_result"
        printf 'operation=%s\n' "$status_operation"
        printf 'bundles_discovered=%s\n' "$status_bundles_discovered"
        printf 'bundles_verified=%s\n' "$status_bundles_verified"
        printf 'bundles_copied=%s\n' "$status_bundles_copied"
        printf 'bundles_verified_existing=%s\n' "$status_bundles_existing"
        printf 'bundles_partial_recovered=%s\n' "$status_bundles_partial"
        printf 'duration_seconds=%s\n' "$(($(date +%s) - started_at))"
    } > "$status_tmp"
    chmod 600 -- "$status_tmp"
    mv -f -- "$status_tmp" "$SYNC_STATUS_FILE"
}

cleanup() {
    for path in "${temporary_files[@]:-}"; do
        [ -z "$path" ] || [ ! -e "$path" ] || rm -f -- "$path"
    done
}

finish() {
    exit_code=$?
    cleanup
    if [ "$exit_code" -eq 0 ]; then
        status_result='success'
        status_operation='completed'
    fi
    write_status || exit_code=1
    log_event 'run-finished' "$status_result" 'none' "$status_bundles_discovered"
    trap - EXIT
    exit "$exit_code"
}
trap finish EXIT

fail() {
    status_operation="$1"
    log_event "$1" 'failure' "${2:-none}" 0 >&2
    return 1
}

require_safe_identifier() {
    value="$1"
    label="$2"
    case "$value" in
        ''|*[!A-Za-z0-9._-]*) printf '%s contains unsupported characters.\n' "$label" >&2; return 1 ;;
    esac
}

require_safe_windows_path() {
    value="$1"
    label="$2"
    case "$value" in
        [A-Za-z]:/*) ;;
        *) printf '%s must be an absolute Windows path using forward slashes.\n' "$label" >&2; return 1 ;;
    esac
    case "$value" in
        *[!A-Za-z0-9._:/-]*|*..*) printf '%s contains unsupported path content.\n' "$label" >&2; return 1 ;;
    esac
}

require_local_file() {
    value="$1"
    label="$2"
    case "$value" in
        /*) ;;
        *) printf '%s must be absolute.\n' "$label" >&2; return 1 ;;
    esac
    [ -f "$value" ] || { printf '%s does not exist.\n' "$label" >&2; return 1; }
}

validate_bundle_name() {
    case "$1" in
        20[0-9][0-9]-[01][0-9]-[0-3][0-9]_[0-2][0-9][0-5][0-9][0-5][0-9]) return 0 ;;
        *) return 1 ;;
    esac
}

validate_manifest() {
    bundle_dir="$1"
    manifest="$bundle_dir/$manifest_name"
    names_file="$(mktemp)"
    temporary_files+=("$names_file")
    line_number=0
    entry_count=0

    while IFS= read -r line || [ -n "$line" ]; do
        line_number=$((line_number + 1))
        hash="${line%% *}"
        remainder="${line#"$hash"}"
        [ "${#hash}" -eq 64 ] || { printf 'Manifest hash length is invalid.\n' >&2; return 1; }
        case "$hash" in *[!0-9A-Fa-f]*) printf 'Manifest hash is invalid.\n' >&2; return 1 ;; esac
        if [[ "$remainder" == '  '* ]]; then
            file_name="${remainder:2}"
        elif [[ "$remainder" == ' *'* ]]; then
            file_name="${remainder:2}"
        else
            printf 'Manifest separator is invalid.\n' >&2
            return 1
        fi
        case "$file_name" in
            ''|.|..|*[!A-Za-z0-9._-]*|"$marker_name"|"$manifest_name")
                printf 'Manifest filename is unsafe.\n' >&2
                return 1
                ;;
        esac
        [ -f "$bundle_dir/$file_name" ] || { printf 'Manifest file is missing.\n' >&2; return 1; }
        [ ! -L "$bundle_dir/$file_name" ] || { printf 'Manifest symlink is not allowed.\n' >&2; return 1; }
        if grep -Fqx -- "$file_name" "$names_file"; then
            printf 'Manifest contains duplicate filenames.\n' >&2
            return 1
        fi
        printf '%s\n' "$file_name" >> "$names_file"
        entry_count=$((entry_count + 1))
    done < "$manifest"

    [ "$line_number" -gt 0 ] && [ "$entry_count" -gt 0 ] || {
        printf 'Manifest must contain at least one entry.\n' >&2
        return 1
    }

    while IFS= read -r -d '' local_entry; do
        base="$(basename -- "$local_entry")"
        case "$base" in
            "$marker_name"|"$manifest_name") continue ;;
        esac
        [ -f "$local_entry" ] && [ ! -L "$local_entry" ] || {
            printf 'Bundle contains unsupported entries.\n' >&2
            return 1
        }
        grep -Fqx -- "$base" "$names_file" || {
            printf 'Bundle contains an unlisted payload.\n' >&2
            return 1
        }
    done < <(find "$bundle_dir" -mindepth 1 -maxdepth 1 -print0)

    VALIDATED_NAMES_FILE="$names_file"
}

encode_powershell() {
    printf '%s' "$1" | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\r\n'
}

escape_powershell_literal() {
    printf '%s' "$1" | sed "s/'/''/g"
}

remote_adapter() {
    "$SYNC_TEST_ADAPTER" "$@"
}

remote_powershell() {
    action="$1"
    bundle_name="${2:-none}"
    remote_bundle="${3:-none}"

    if [ -n "${SYNC_TEST_ADAPTER:-}" ]; then
        remote_adapter "$action" "$bundle_name" "$remote_bundle"
        return
    fi

    remote_bundle_ps="$(escape_powershell_literal "$remote_bundle")"
    verifier_ps="$(escape_powershell_literal "$SYNC_REMOTE_VERIFIER")"
    case "$action" in
        connectivity) script_text='exit 0' ;;
        marker-exists)
            script_text="if (Test-Path -LiteralPath '$remote_bundle_ps/$marker_name' -PathType Leaf) { exit 0 } else { exit 3 }"
            ;;
        directory-exists)
            script_text="if (Test-Path -LiteralPath '$remote_bundle_ps' -PathType Container) { exit 0 } else { exit 3 }"
            ;;
        prepare)
            script_text="[void](New-Item -ItemType Directory -Force -Path '$remote_bundle_ps'); exit 0"
            ;;
        clear-pending)
            script_text="Remove-Item -LiteralPath '$remote_bundle_ps/$pending_marker' -Force -ErrorAction SilentlyContinue; exit 0"
            ;;
        verify)
            script_text="& '$verifier_ps' -BundleDirectory '$remote_bundle_ps'; exit \$LASTEXITCODE"
            ;;
        publish-marker)
            script_text="[IO.File]::Move('$remote_bundle_ps/$pending_marker', '$remote_bundle_ps/$marker_name'); exit 0"
            ;;
        *) printf 'Unsupported remote action.\n' >&2; return 1 ;;
    esac
    encoded="$(encode_powershell "$script_text")"
    "$ssh_binary" -i "$SYNC_SSH_KEY" -o BatchMode=yes -o IdentitiesOnly=yes \
        -o "UserKnownHostsFile=$SYNC_KNOWN_HOSTS" -- "$remote_target" \
        powershell.exe -NoProfile -NonInteractive -EncodedCommand "$encoded" \
        >/dev/null 2>&1
}

copy_remote() {
    source_file="$1"
    bundle_name="$2"
    remote_file="$3"
    remote_bundle="$4"
    if [ -n "${SYNC_TEST_ADAPTER:-}" ]; then
        remote_adapter 'copy' "$bundle_name" "$remote_bundle" "$source_file" "$remote_file"
        return
    fi
    "$scp_binary" -q -i "$SYNC_SSH_KEY" -o BatchMode=yes -o IdentitiesOnly=yes \
        -o "UserKnownHostsFile=$SYNC_KNOWN_HOSTS" -- "$source_file" \
        "$remote_target:$remote_bundle/$remote_file" >/dev/null 2>&1
}

: "${SYNC_SOURCE_ROOT:?SYNC_SOURCE_ROOT is required}"
: "${SYNC_REMOTE_HOST:?SYNC_REMOTE_HOST is required}"
: "${SYNC_REMOTE_USER:?SYNC_REMOTE_USER is required}"
: "${SYNC_REMOTE_DIR:?SYNC_REMOTE_DIR is required}"
: "${SYNC_REMOTE_VERIFIER:?SYNC_REMOTE_VERIFIER is required}"

case "$SYNC_SOURCE_ROOT" in /*) ;; *) printf 'SYNC_SOURCE_ROOT must be absolute.\n' >&2; exit 1 ;; esac
[ -d "$SYNC_SOURCE_ROOT" ] || { printf 'SYNC_SOURCE_ROOT does not exist.\n' >&2; exit 1; }
SYNC_SOURCE_ROOT="$(cd -- "$SYNC_SOURCE_ROOT" && pwd -P)"
[ "$SYNC_SOURCE_ROOT" != '/' ] || { printf 'SYNC_SOURCE_ROOT cannot be filesystem root.\n' >&2; exit 1; }
require_safe_identifier "$SYNC_REMOTE_HOST" 'SYNC_REMOTE_HOST'
require_safe_identifier "$SYNC_REMOTE_USER" 'SYNC_REMOTE_USER'
require_safe_windows_path "$SYNC_REMOTE_DIR" 'SYNC_REMOTE_DIR'
require_safe_windows_path "$SYNC_REMOTE_VERIFIER" 'SYNC_REMOTE_VERIFIER'

ssh_binary='ssh'
scp_binary='scp'
remote_target="$SYNC_REMOTE_USER@$SYNC_REMOTE_HOST"

if [ -n "${SYNC_TEST_ADAPTER:-}" ]; then
    [ "${SYNC_ALLOW_TEST_ADAPTER:-}" = '1' ] || { printf 'Test adapter requires explicit opt-in.\n' >&2; exit 1; }
    require_local_file "$SYNC_TEST_ADAPTER" 'SYNC_TEST_ADAPTER'
else
    : "${SYNC_SSH_KEY:?SYNC_SSH_KEY is required}"
    : "${SYNC_KNOWN_HOSTS:?SYNC_KNOWN_HOSTS is required}"
    require_local_file "$SYNC_SSH_KEY" 'SYNC_SSH_KEY'
    require_local_file "$SYNC_KNOWN_HOSTS" 'SYNC_KNOWN_HOSTS'
fi

log_event 'run-started' 'started' 'none' 0
status_operation='connectivity'
remote_powershell 'connectivity' || fail 'connectivity' 'none'
log_event 'connectivity' 'pass' 'none' 0

mapfile -d '' bundle_directories < <(find "$SYNC_SOURCE_ROOT" -mindepth 1 -maxdepth 1 -type d -name "$bundle_pattern" -print0 | sort -z)
status_bundles_discovered="${#bundle_directories[@]}"
log_event 'bundles-discovered' 'pass' 'none' "$status_bundles_discovered"

for bundle_dir in "${bundle_directories[@]}"; do
    bundle_name="$(basename -- "$bundle_dir")"
    validate_bundle_name "$bundle_name" || fail 'unsafe-bundle-name' 'none'

    if [ ! -f "$bundle_dir/$marker_name" ]; then
        log_event 'incomplete-local-marker' 'skipped' "$bundle_name" 0
        continue
    fi
    if [ ! -s "$bundle_dir/$manifest_name" ]; then
        log_event 'incomplete-local-manifest' 'skipped' "$bundle_name" 0
        continue
    fi

    status_operation='local-manifest-validation'
    validate_manifest "$bundle_dir" || fail 'local-manifest-validation' "$bundle_name"
    names_file="$VALIDATED_NAMES_FILE"
    (cd -- "$bundle_dir" && sha256sum -c -- "$manifest_name" >/dev/null 2>&1) || fail 'local-checksum' "$bundle_name"
    status_bundles_verified=$((status_bundles_verified + 1))
    log_event 'local-verified' 'pass' "$bundle_name" 1

    remote_bundle="${SYNC_REMOTE_DIR%/}/$bundle_name"
    set +e
    remote_powershell 'marker-exists' "$bundle_name" "$remote_bundle"
    marker_result=$?
    set -e
    if [ "$marker_result" -eq 0 ]; then
        status_operation='verify-existing'
        remote_powershell 'verify' "$bundle_name" "$remote_bundle" || fail 'remote-existing-checksum' "$bundle_name"
        status_bundles_existing=$((status_bundles_existing + 1))
        log_event 'verified-existing' 'pass' "$bundle_name" 1
        continue
    fi
    [ "$marker_result" -eq 3 ] || fail 'remote-marker-query' "$bundle_name"

    set +e
    remote_powershell 'directory-exists' "$bundle_name" "$remote_bundle"
    directory_result=$?
    set -e
    [ "$directory_result" -eq 0 ] || [ "$directory_result" -eq 3 ] || fail 'remote-directory-query' "$bundle_name"
    remote_adapter_enabled_marker=''
    if [ "$directory_result" -eq 0 ]; then
        remote_adapter_enabled_marker='partial'
    fi

    status_operation='remote-prepare'
    remote_powershell 'prepare' "$bundle_name" "$remote_bundle" || fail 'remote-prepare' "$bundle_name"
    remote_powershell 'clear-pending' "$bundle_name" "$remote_bundle" || fail 'remote-clear-pending' "$bundle_name"

    status_operation='copy'
    copy_remote "$bundle_dir/$manifest_name" "$bundle_name" "$manifest_name" "$remote_bundle" || fail 'scp-manifest' "$bundle_name"
    while IFS= read -r payload_name; do
        copy_remote "$bundle_dir/$payload_name" "$bundle_name" "$payload_name" "$remote_bundle" || fail 'scp-payload' "$bundle_name"
    done < "$names_file"
    status_bundles_copied=$((status_bundles_copied + 1))
    log_event 'copied' 'pass' "$bundle_name" 1

    status_operation='remote-verification'
    remote_powershell 'verify' "$bundle_name" "$remote_bundle" || fail 'remote-checksum' "$bundle_name"
    log_event 'remote-verified' 'pass' "$bundle_name" 1

    status_operation='marker-copy'
    copy_remote "$bundle_dir/$marker_name" "$bundle_name" "$pending_marker" "$remote_bundle" || fail 'marker-copy' "$bundle_name"
    status_operation='marker-publication'
    remote_powershell 'publish-marker' "$bundle_name" "$remote_bundle" || fail 'marker-publication' "$bundle_name"
    if [ "$remote_adapter_enabled_marker" = 'partial' ]; then
        status_bundles_partial=$((status_bundles_partial + 1))
        log_event 'partial-recovered' 'pass' "$bundle_name" 1
    else
        log_event 'published' 'pass' "$bundle_name" 1
    fi
done

status_operation='completed'
