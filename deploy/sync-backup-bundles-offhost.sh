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
    local status_directory status_tmp
    [ -n "${SYNC_STATUS_FILE:-}" ] || return 0
    case "$SYNC_STATUS_FILE" in
        /*) ;;
        *) printf 'SYNC_STATUS_FILE must be absolute.\n' >&2; return 1 ;;
    esac
    status_directory="$(dirname -- "$SYNC_STATUS_FILE")"
    [ -d "$status_directory" ] || { printf 'Status directory does not exist.\n' >&2; return 1; }
    status_tmp="$(mktemp "$status_directory/.sync-status.XXXXXX")" || return 1
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
    local path
    for path in "${temporary_files[@]:-}"; do
        [ -z "$path" ] || [ ! -e "$path" ] || rm -f -- "$path"
    done
}

finish() {
    local exit_code="$?"
    cleanup
    if [ "$exit_code" -eq 0 ]; then
        status_result='success'
        status_operation='completed'
    fi
    if ! write_status; then
        exit_code=1
        status_result='failure'
        status_operation='status-write'
    fi
    cleanup
    log_event 'run-finished' "$status_result" 'none' "$status_bundles_discovered"
    trap - EXIT
    exit "$exit_code"
}
trap finish EXIT

fail() {
    local operation bundle
    operation="$1"
    bundle="${2:-none}"
    status_operation="$operation"
    log_event "$operation" 'failure' "$bundle" 0 >&2
    exit 1
}

require_safe_identifier() {
    local value label
    value="$1"
    label="$2"
    case "$value" in
        ''|*[!A-Za-z0-9._-]*) printf '%s contains unsupported characters.\n' "$label" >&2; return 1 ;;
    esac
}

require_safe_windows_path() {
    local value label path_tail
    value="$1"
    label="$2"
    case "$value" in
        [A-Za-z]:/*) ;;
        *) printf '%s must be an absolute Windows path using forward slashes.\n' "$label" >&2; return 1 ;;
    esac
    case "$value" in
        *[!A-Za-z0-9._:/-]*|*..*) printf '%s contains unsupported path content.\n' "$label" >&2; return 1 ;;
    esac
    path_tail="${value:3}"
    case "$path_tail" in
        ''|*:*|*//*|*/) printf '%s contains an unsafe Windows path form.\n' "$label" >&2; return 1 ;;
    esac
}

require_local_file() {
    local value label
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
    local bundle_dir manifest names_file normalized_manifest line_number entry_count line hash remainder file_name local_entry base
    bundle_dir="$1"
    manifest="$bundle_dir/$manifest_name"
    names_file="$(mktemp)"
    normalized_manifest="$(mktemp)"
    temporary_files+=("$names_file" "$normalized_manifest")
    line_number=0
    entry_count=0

    while IFS= read -r line || [ -n "$line" ]; do
        line="${line%$'\r'}"
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
        case "$file_name" in
            [A-Za-z0-9]*) ;;
            *) printf 'Manifest filename must start with an alphanumeric character.\n' >&2; return 1 ;;
        esac
        [ -f "$bundle_dir/$file_name" ] || { printf 'Manifest file is missing.\n' >&2; return 1; }
        [ ! -L "$bundle_dir/$file_name" ] || { printf 'Manifest symlink is not allowed.\n' >&2; return 1; }
        if grep -Fqx -- "$file_name" "$names_file"; then
            printf 'Manifest contains duplicate filenames.\n' >&2
            return 1
        fi
        printf '%s\n' "$file_name" >> "$names_file"
        printf '%s\n' "$line" >> "$normalized_manifest"
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
    VALIDATED_MANIFEST_FILE="$normalized_manifest"
}

encode_powershell() {
    local script_text
    script_text="$1"
    printf '%s' "$script_text" | iconv -f UTF-8 -t UTF-16LE | base64 | tr -d '\r\n'
}

escape_powershell_literal() {
    local value
    value="$1"
    printf '%s' "$value" | sed "s/'/''/g"
}

remote_adapter() {
    "$SYNC_TEST_ADAPTER" "$@"
}

remote_powershell() {
    local action bundle_name remote_bundle names_file remote_bundle_ps remote_root_ps verifier_ps script_text encoded expected_ps expected_name
    action="$1"
    bundle_name="${2:-none}"
    remote_bundle="${3:-none}"
    names_file="${4:-}"

    if [ -n "${SYNC_TEST_ADAPTER:-}" ]; then
        remote_adapter "$action" "$bundle_name" "$remote_bundle" "$names_file"
        return
    fi

    remote_bundle_ps="$(escape_powershell_literal "$remote_bundle")"
    remote_root_ps="$(escape_powershell_literal "$SYNC_REMOTE_DIR")"
    verifier_ps="$(escape_powershell_literal "$SYNC_REMOTE_VERIFIER")"
    case "$action" in
        connectivity) script_text='exit 0' ;;
        validate-root)
            script_text="& '$verifier_ps' -DestinationRoot '$remote_root_ps' -ValidateRoot; exit \$LASTEXITCODE"
            ;;
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
        preflight)
            expected_ps=''
            while IFS= read -r expected_name; do
                [ -n "$expected_name" ] || continue
                if [ -n "$expected_ps" ]; then expected_ps="$expected_ps,"; fi
                expected_ps="$expected_ps'$expected_name'"
            done < "$names_file"
            script_text="& '$verifier_ps' -DestinationRoot '$remote_root_ps' -BundleDirectory '$remote_bundle_ps' -Preflight -ExpectedFileName @($expected_ps); exit \$LASTEXITCODE"
            ;;
        verify)
            script_text="& '$verifier_ps' -DestinationRoot '$remote_root_ps' -BundleDirectory '$remote_bundle_ps'; exit \$LASTEXITCODE"
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
    local source_file bundle_name remote_file remote_bundle
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
remote_powershell 'validate-root' || fail 'remote-root-validation' 'none'
log_event 'remote-root-validation' 'pass' 'none' 0

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
    validated_manifest="$VALIDATED_MANIFEST_FILE"
    (cd -- "$bundle_dir" && sha256sum -c -- "$validated_manifest" >/dev/null 2>&1) || fail 'local-checksum' "$bundle_name"
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
    remote_powershell 'preflight' "$bundle_name" "$remote_bundle" "$names_file" || fail 'remote-preflight' "$bundle_name"
    remote_powershell 'clear-pending' "$bundle_name" "$remote_bundle" || fail 'remote-clear-pending' "$bundle_name"

    status_operation='copy'
    copy_remote "$validated_manifest" "$bundle_name" "$manifest_name" "$remote_bundle" || fail 'scp-manifest' "$bundle_name"
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
