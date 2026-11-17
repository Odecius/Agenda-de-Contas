#!/bin/sh

set -eu
umask 077

: "${PILOT_DB_IDENTITY:?PILOT_DB_IDENTITY is required}"
: "${PILOT_APP_PASSWORD_FILE:?PILOT_APP_PASSWORD_FILE is required}"

[ "$PILOT_DB_IDENTITY" = 'agenda-pilot-v1' ] || { printf '%s\n' 'Pilot database identity is invalid.' >&2; exit 1; }
[ "$POSTGRES_DB" = 'agenda_pilot' ] || { printf '%s\n' 'Pilot database name is invalid.' >&2; exit 1; }
[ -f "$PILOT_APP_PASSWORD_FILE" ] || { printf '%s\n' 'Pilot application password file is missing.' >&2; exit 1; }

app_password="$(tr -d '\r\n' < "$PILOT_APP_PASSWORD_FILE")"
[ "${#app_password}" -ge 32 ] || { printf '%s\n' 'Pilot application password does not meet the minimum length.' >&2; exit 1; }
case "$app_password" in
    *[!A-Za-z0-9_-]*) printf '%s\n' 'Pilot application password contains an unsupported character.' >&2; exit 1 ;;
esac

psql --variable=ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<SQL
CREATE ROLE agenda_pilot_app LOGIN PASSWORD '$app_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
ALTER DATABASE agenda_pilot OWNER TO agenda_pilot_app;
REVOKE CONNECT ON DATABASE postgres FROM PUBLIC;
REVOKE CONNECT ON DATABASE template1 FROM PUBLIC;
SQL

psql --variable=ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname agenda_pilot <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO agenda_pilot_app;
CREATE TABLE public.pilot_environment_marker (
    identity text PRIMARY KEY,
    created_at_utc timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_pilot_environment_marker_identity CHECK (identity = 'agenda-pilot-v1')
);
ALTER TABLE public.pilot_environment_marker OWNER TO agenda_pilot_app;
INSERT INTO public.pilot_environment_marker (identity) VALUES ('agenda-pilot-v1');
SQL

unset app_password
printf '%s\n' 'Pilot application role and environment marker created.'
