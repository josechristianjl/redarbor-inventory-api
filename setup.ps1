$ErrorActionPreference = 'Stop'
$templatePath = Join-Path $PSScriptRoot '.env.example'
$envPath = Join-Path $PSScriptRoot '.env'

if (Test-Path -LiteralPath $envPath) {
    Start-Process notepad.exe -ArgumentList "`"$envPath`""
    exit 0
}

$keys = @(
    'MSSQL_SA_PASSWORD',
    'SQL_SERVER_PASSWORD',
    'KC_BOOTSTRAP_ADMIN_PASSWORD',
    'KEYCLOAK_SERVICE_CLIENT_SECRET',
    'KEYCLOAK_DEMO_USER_PASSWORD'
)

$lines = @(Get-Content -LiteralPath $templatePath)
$rng = [System.Security.Cryptography.RandomNumberGenerator]::Create()

try {
    foreach ($key in $keys) {
        $indices = @(
            for ($i = 0; $i -lt $lines.Count; $i++) {
                if ($lines[$i] -ceq "$key=") {
                    $i
                }
            }
        )
        if ($indices.Count -ne 1) {
            throw "Expected one empty placeholder for $key."
        }
        $bytes = New-Object byte[] 32
        $rng.GetBytes($bytes)
        $hex = [BitConverter]::ToString($bytes).Replace('-', '').ToLowerInvariant()
        $lines[$indices[0]] = "$key='Aa1!$hex'"
    }
}
finally {
    $rng.Dispose()
}

$content = ($lines -join [Environment]::NewLine) + [Environment]::NewLine
$data = [System.Text.Encoding]::UTF8.GetBytes($content)
$stream = [System.IO.File]::Open(
    $envPath,
    [System.IO.FileMode]::CreateNew,
    [System.IO.FileAccess]::Write,
    [System.IO.FileShare]::None
)

try {
    $stream.Write($data, 0, $data.Length)
}
finally {
    $stream.Dispose()
}

Start-Process notepad.exe -ArgumentList "`"$envPath`""
