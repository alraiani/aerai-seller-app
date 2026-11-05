# Exports the production Azure SQL databases to BACPAC files in OneDrive and keeps the newest few.
# Run by the "Azure DB Backup" scheduled task; safe to run by hand at any time.
#
# Sign-in comes from the Azure CLI (az login), so no password or key is stored here. A run fails if
# that sign-in has expired or this PC's public IP is no longer in the SQL firewall rules. Each run
# appends to backup.log and rewrites last-backup-status.txt in the backup folder.

param(
    [string]$BackupRoot = 'C:\Users\alber\OneDrive\Documents Business\db-backups',
    [int]$KeepPerDatabase = 8
)

$ErrorActionPreference = 'Stop'

$databases = @(
    @{ Server = 'sql-aerai-seller-prod.database.windows.net'; Name = 'AERaiSeller' },
    @{ Server = 'sqlmulticast.database.windows.net'; Name = 'SqlMultiCast' }
)

$sqlPackage = Join-Path $env:USERPROFILE '.dotnet\tools\sqlpackage.exe'
$logFile = Join-Path $BackupRoot 'backup.log'
$statusFile = Join-Path $BackupRoot 'last-backup-status.txt'

# Files are built here and moved when finished, so OneDrive never syncs a half-written export.
$workDir = Join-Path $env:LOCALAPPDATA 'db-backup-work'

function Write-Log([string]$message) {
    $line = '{0:yyyy-MM-dd HH:mm:ss}  {1}' -f (Get-Date), $message
    Add-Content -LiteralPath $logFile -Value $line -Encoding UTF8
    Write-Output $line
}

New-Item -ItemType Directory -Force -Path $BackupRoot, $workDir | Out-Null
$results = New-Object System.Collections.ArrayList

foreach ($db in $databases) {
    $name = $db.Name
    $stamp = Get-Date -Format 'yyyy-MM-dd_HHmm'
    $fileName = "$name-$stamp.bacpac"
    $workFile = Join-Path $workDir $fileName
    $folder = Join-Path $BackupRoot $name

    try {
        New-Item -ItemType Directory -Force -Path $folder | Out-Null

        # A fresh token per database: they last about an hour.
        $token = (& az account get-access-token --resource https://database.windows.net/ --query accessToken -o tsv)
        if ($LASTEXITCODE -ne 0 -or -not $token) { throw 'Could not get an Azure token. Run: az login' }

        Write-Log "$name export started"

        # Windows PowerShell treats any stderr text as an error under 'Stop', so judge by exit code.
        $ErrorActionPreference = 'Continue'
        $output = & $sqlPackage /Action:Export "/SourceServerName:$($db.Server)" "/SourceDatabaseName:$name" "/AccessToken:$token" "/TargetFile:$workFile" 2>&1
        $exitCode = $LASTEXITCODE
        $ErrorActionPreference = 'Stop'

        if ($exitCode -ne 0) {
            $detail = ($output | Select-Object -Last 5) -join ' '
            throw "SqlPackage exit code ${exitCode}: $detail"
        }

        $size = (Get-Item -LiteralPath $workFile).Length
        if ($size -lt 1024) { throw "Export is only $size bytes" }

        Move-Item -LiteralPath $workFile -Destination (Join-Path $folder $fileName) -Force
        $sizeMb = [math]::Round($size / 1MB, 1)

        # Old exports are removed only after a new one has succeeded.
        Get-ChildItem -LiteralPath $folder -Filter "$name-*.bacpac" |
            Sort-Object LastWriteTime -Descending |
            Select-Object -Skip $KeepPerDatabase |
            Remove-Item -Force

        Write-Log "$name OK: $fileName ($sizeMb MB)"
        [void]$results.Add("OK      $name  $fileName  $sizeMb MB")
    }
    catch {
        Remove-Item -LiteralPath $workFile -Force -ErrorAction SilentlyContinue
        Write-Log "$name FAILED: $($_.Exception.Message)"
        [void]$results.Add("FAILED  $name  $($_.Exception.Message)")
    }
}

$header = 'Last run: {0:yyyy-MM-dd HH:mm}' -f (Get-Date)
Set-Content -LiteralPath $statusFile -Value (@($header) + $results) -Encoding UTF8

if ($results -match '^FAILED') { exit 1 }
exit 0
