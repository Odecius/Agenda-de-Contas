#!/bin/sh

set -eu

: "${PILOT_EXPECTED_DATABASE:?PILOT_EXPECTED_DATABASE is required}"
: "${PILOT_EXPECTED_ROLE:?PILOT_EXPECTED_ROLE is required}"
: "${PILOT_ENVIRONMENT_MARKER:?PILOT_ENVIRONMENT_MARKER is required}"
: "${PILOT_VERIFY_MODE:?PILOT_VERIFY_MODE must be runtime or migrator}"

[ "$PILOT_EXPECTED_DATABASE" = 'agenda_pilot' ] || exit 1
[ "$PILOT_ENVIRONMENT_MARKER" = 'agenda-pilot-v1' ] || exit 1
case "$PILOT_VERIFY_MODE:$PILOT_EXPECTED_ROLE" in
    runtime:agenda_pilot_app|migrator:agenda_pilot_migrator) ;;
    *) exit 1 ;;
esac

database="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT current_database()')"
role="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT current_user')"
marker="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT identity FROM public.pilot_environment_marker')"

[ "$database" = "$PILOT_EXPECTED_DATABASE" ] || exit 1
[ "$role" = "$PILOT_EXPECTED_ROLE" ] || exit 1
[ "$marker" = "$PILOT_ENVIRONMENT_MARKER" ] || exit 1

attributes="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT rolsuper::int || ':' || rolcreatedb::int || ':' || rolcreaterole::int || ':' || rolreplication::int || ':' || rolbypassrls::int FROM pg_roles WHERE rolname = current_user")"
[ "$attributes" = '0:0:0:0:0' ] || exit 1

database_owner="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT pg_get_userbyid(datdba) FROM pg_database WHERE datname = current_database()")"
schema_owner="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT pg_get_userbyid(nspowner) FROM pg_namespace WHERE nspname = 'public'")"
marker_owner="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT pg_get_userbyid(relowner) FROM pg_class JOIN pg_namespace ON pg_namespace.oid = pg_class.relnamespace WHERE nspname = 'public' AND relname = 'pilot_environment_marker'")"

unexpected_databases="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT count(*) FROM pg_database WHERE datallowconn AND NOT datistemplate AND datname NOT IN ('postgres', 'agenda_pilot')")"
[ "$unexpected_databases" = '0' ] || exit 1

printf '%s\n' 'PILOT_DATABASE_IDENTITY=PASS'
printf '%s\n' 'UNEXPECTED_DATABASES=0'

if [ "$PILOT_VERIFY_MODE" = 'runtime' ]; then
    [ "$database_owner" != "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$schema_owner" != "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$marker_owner" != "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$(psql --no-psqlrc --tuples-only --no-align --command "SELECT has_schema_privilege(current_user, 'public', 'CREATE')::int")" = '0' ] || exit 1
    missing_dml="$(psql --no-psqlrc --tuples-only --no-align --command \
        "SELECT count(*) FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace WHERE n.nspname = 'public' AND c.relkind IN ('r','p') AND c.relname <> 'pilot_environment_marker' AND NOT (has_table_privilege(current_user, c.oid, 'SELECT') AND has_table_privilege(current_user, c.oid, 'INSERT') AND has_table_privilege(current_user, c.oid, 'UPDATE') AND has_table_privilege(current_user, c.oid, 'DELETE'))")"
    [ "$missing_dml" = '0' ] || exit 1
    printf '%s\n' 'PILOT_RUNTIME_ROLE=LEAST_PRIVILEGE'
    printf '%s\n' 'PILOT_RUNTIME_OWNERSHIP=NONE'
    printf '%s\n' 'PILOT_RUNTIME_SCHEMA_CREATE=DENIED'
    printf '%s\n' 'PILOT_RUNTIME_DML_GRANTS=PASS'
else
    [ "$database_owner" = "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$schema_owner" = "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$marker_owner" = "$PILOT_EXPECTED_ROLE" ] || exit 1
    [ "$(psql --no-psqlrc --tuples-only --no-align --command "SELECT has_schema_privilege(current_user, 'public', 'CREATE')::int")" = '1' ] || exit 1
    printf '%s\n' 'PILOT_MIGRATOR_ROLE=SCHEMA_OWNER'
    printf '%s\n' 'PILOT_MIGRATOR_GLOBAL_PRIVILEGES=NONE'
fi
