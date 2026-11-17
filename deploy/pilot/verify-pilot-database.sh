#!/bin/sh

set -eu

: "${PILOT_EXPECTED_DATABASE:?PILOT_EXPECTED_DATABASE is required}"
: "${PILOT_EXPECTED_ROLE:?PILOT_EXPECTED_ROLE is required}"
: "${PILOT_ENVIRONMENT_MARKER:?PILOT_ENVIRONMENT_MARKER is required}"

[ "$PILOT_EXPECTED_DATABASE" = 'agenda_pilot' ] || exit 1
[ "$PILOT_EXPECTED_ROLE" = 'agenda_pilot_app' ] || exit 1
[ "$PILOT_ENVIRONMENT_MARKER" = 'agenda-pilot-v1' ] || exit 1

database="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT current_database()')"
role="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT current_user')"
marker="$(psql --no-psqlrc --tuples-only --no-align --command 'SELECT identity FROM public.pilot_environment_marker')"

[ "$database" = "$PILOT_EXPECTED_DATABASE" ] || exit 1
[ "$role" = "$PILOT_EXPECTED_ROLE" ] || exit 1
[ "$marker" = "$PILOT_ENVIRONMENT_MARKER" ] || exit 1

attributes="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT rolsuper::int || ':' || rolcreatedb::int || ':' || rolcreaterole::int || ':' || rolreplication::int || ':' || rolbypassrls::int FROM pg_roles WHERE rolname = current_user")"
[ "$attributes" = '0:0:0:0:0' ] || exit 1

unexpected_databases="$(psql --no-psqlrc --tuples-only --no-align --command \
    "SELECT count(*) FROM pg_database WHERE datallowconn AND NOT datistemplate AND datname NOT IN ('postgres', 'agenda_pilot')")"
[ "$unexpected_databases" = '0' ] || exit 1

printf '%s\n' 'PILOT_DATABASE_IDENTITY=PASS'
printf '%s\n' 'PILOT_APPLICATION_ROLE=LEAST_PRIVILEGE'
printf '%s\n' 'UNEXPECTED_DATABASES=0'
