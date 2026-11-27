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

wait_for_database() {
    local database="$1"
    local max_attempts=30
    local attempt

    for ((attempt=1; attempt<=max_attempts; attempt++)); do
        if "$SQLCMD" -S sqlserver,1433 -U sa -C -b \
            -l 3 -t 3 \
            -d "$database" \
            -Q "SELECT 1;" \
            > /dev/null 2>&1; then
            echo "Database '$database' is available."
            return 0
        fi

        echo "Waiting for database '$database' ($attempt/$max_attempts)..."

        if ((attempt < max_attempts)); then
            sleep 2
        fi
    done

    echo "Database '$database' did not become available." >&2
    return 1
}

echo "Waiting for SQL Server..."

wait_for_database master

echo "Creating database..."

"$SQLCMD" -S sqlserver,1433 -U sa -C -b \
    -d master \
    -i /database/scripts/001_create_database.sql

wait_for_database InventoryManagement

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
