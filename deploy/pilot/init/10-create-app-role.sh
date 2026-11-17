#!/bin/sh

set -eu
umask 077

: "${PILOT_DB_IDENTITY:?PILOT_DB_IDENTITY is required}"
: "${PILOT_MIGRATOR_PASSWORD_FILE:?PILOT_MIGRATOR_PASSWORD_FILE is required}"
: "${PILOT_APP_PASSWORD_FILE:?PILOT_APP_PASSWORD_FILE is required}"

[ "$PILOT_DB_IDENTITY" = 'agenda-pilot-v1' ] || { printf '%s\n' 'Pilot database identity is invalid.' >&2; exit 1; }
[ "$POSTGRES_DB" = 'agenda_pilot' ] || { printf '%s\n' 'Pilot database name is invalid.' >&2; exit 1; }
[ -f "$PILOT_MIGRATOR_PASSWORD_FILE" ] || { printf '%s\n' 'Pilot migration password file is missing.' >&2; exit 1; }
[ -f "$PILOT_APP_PASSWORD_FILE" ] || { printf '%s\n' 'Pilot application password file is missing.' >&2; exit 1; }

migrator_password="$(tr -d '\r\n' < "$PILOT_MIGRATOR_PASSWORD_FILE")"
app_password="$(tr -d '\r\n' < "$PILOT_APP_PASSWORD_FILE")"
[ "${#migrator_password}" -ge 32 ] || { printf '%s\n' 'Pilot migration password does not meet the minimum length.' >&2; exit 1; }
[ "${#app_password}" -ge 32 ] || { printf '%s\n' 'Pilot application password does not meet the minimum length.' >&2; exit 1; }
case "$migrator_password" in
    *[!A-Za-z0-9_-]*) printf '%s\n' 'Pilot migration password contains an unsupported character.' >&2; exit 1 ;;
esac
case "$app_password" in
    *[!A-Za-z0-9_-]*) printf '%s\n' 'Pilot application password contains an unsupported character.' >&2; exit 1 ;;
esac
[ "$migrator_password" != "$app_password" ] || { printf '%s\n' 'Pilot migration and application passwords must differ.' >&2; exit 1; }

psql --variable=ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname postgres <<SQL
CREATE ROLE agenda_pilot_migrator LOGIN PASSWORD '$migrator_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
CREATE ROLE agenda_pilot_app LOGIN PASSWORD '$app_password'
    NOSUPERUSER NOCREATEDB NOCREATEROLE NOINHERIT NOREPLICATION NOBYPASSRLS;
ALTER DATABASE agenda_pilot OWNER TO agenda_pilot_migrator;
GRANT CONNECT ON DATABASE agenda_pilot TO agenda_pilot_app;
REVOKE CONNECT ON DATABASE postgres FROM PUBLIC;
REVOKE CONNECT ON DATABASE template1 FROM PUBLIC;
SQL

psql --variable=ON_ERROR_STOP=1 --username "$POSTGRES_USER" --dbname agenda_pilot <<'SQL'
REVOKE ALL ON SCHEMA public FROM PUBLIC;
ALTER SCHEMA public OWNER TO agenda_pilot_migrator;
GRANT USAGE ON SCHEMA public TO agenda_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE agenda_pilot_migrator IN SCHEMA public
    GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO agenda_pilot_app;
ALTER DEFAULT PRIVILEGES FOR ROLE agenda_pilot_migrator IN SCHEMA public
    GRANT USAGE, SELECT ON SEQUENCES TO agenda_pilot_app;
CREATE TABLE public.pilot_environment_marker (
    identity text PRIMARY KEY,
    created_at_utc timestamp with time zone NOT NULL DEFAULT CURRENT_TIMESTAMP,
    CONSTRAINT ck_pilot_environment_marker_identity CHECK (identity = 'agenda-pilot-v1')
);
ALTER TABLE public.pilot_environment_marker OWNER TO agenda_pilot_migrator;
INSERT INTO public.pilot_environment_marker (identity) VALUES ('agenda-pilot-v1');
GRANT SELECT ON public.pilot_environment_marker TO agenda_pilot_app;
SQL

unset migrator_password
unset app_password
printf '%s\n' 'Pilot migration role, application role and environment marker created.'
