#!/usr/bin/env bash

set -euo pipefail

: "${MSSQL_SA_PASSWORD:?MSSQL_SA_PASSWORD is required}"
: "${SQL_SERVER_PASSWORD:?SQL_SERVER_PASSWORD is required}"

export LC_ALL=C

if [[ ! "$SQL_SERVER_PASSWORD" =~ ^[[:graph:]]{8,128}$ ]] ||
   [[ "$SQL_SERVER_PASSWORD" == *"'"* ]]; then
    echo "Application password must contain 8-128 printable ASCII characters without spaces or single quotes." >&2
    exit 1
fi

SQLCMD="/opt/mssql-tools18/bin/sqlcmd"

export SQLCMDPASSWORD="$MSSQL_SA_PASSWORD"

echo "Creating database..."

"$SQLCMD" -S sqlserver,1433 -U sa -C -b \
    -d master \
    -i /database/scripts/001_create_database.sql

echo "Creating database schema..."

"$SQLCMD" -S sqlserver,1433 -U sa -C -b \
    -d InventoryManagement \
    -i /database/scripts/002_create_schema.sql

echo "Configuring application permissions..."

export APP_PASSWORD_HEX
APP_PASSWORD_HEX=$(printf '%s' "$SQL_SERVER_PASSWORD" |
    od -An -v -tx1 | tr -d ' \n')

"$SQLCMD" -S sqlserver,1433 -U sa -C -b \
    -d master \
    -i /database/scripts/003_create_permissions.sql

unset APP_PASSWORD_HEX

echo "Verifying application authentication..."

SQLCMDPASSWORD="$SQL_SERVER_PASSWORD" \
    "$SQLCMD" -S sqlserver,1433 -U inventory_app -C -b \
    -d InventoryManagement \
    -Q "SELECT 1;" -o /dev/null

echo "Database initialization completed successfully."