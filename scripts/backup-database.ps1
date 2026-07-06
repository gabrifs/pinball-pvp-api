<#
.SYNOPSIS
    Dumps the pinball-pvp-api Postgres database and mirrors backups to Google Drive via rclone.

.DESCRIPTION
    Intended to run on the host machine on a schedule (Windows Task Scheduler), not in CI.
    Finds the running `db` compose service by its `com.docker.compose.service` label (rather than
    a hardcoded container name, since the project name prefix depends on the checkout directory).

    Requires no database credentials here: pg_dump runs inside the container itself, using the
    POSTGRES_USER/POSTGRES_DB environment variables docker-compose.yml already injects into it,
    authenticating over the local Unix socket (trusted by default in the official postgres image).

    Requires rclone (https://rclone.org) installed and configured with a remote named `gdrive`
    (one-time setup: `rclone config`) before this script can run unattended.

    On any failure, attempts to email the address in the BACKUP_ALERT_TO_ADDRESS environment variable
    before re-throwing (so Task Scheduler still records a non-zero result). Also requires
    BACKUP_ALERT_SMTP_USERNAME/BACKUP_ALERT_SMTP_PASSWORD. All three are set at the machine level on
    the host, never stored in this repo - see deployment.md for one-time setup. If any are unset, a
    warning is printed and the script still fails loudly via its exit code, just without an email.

.PARAMETER BackupDir
    Local directory to store dated .sql dumps in. Point this at a different physical disk than
    the one Docker/the OS lives on if you want the local copy to survive a single-disk failure too.

.PARAMETER RcloneRemote
    rclone remote:path to mirror BackupDir to. `rclone sync` makes the remote match the local
    directory exactly (including deletions), so local retention pruning also prunes the remote copy.

.PARAMETER RetentionDays
    Local (and therefore remote, after sync) dumps older than this are deleted.
#>
param(
    [string]$BackupDir = "C:\pinball-pvp\backups",
    [string]$RcloneRemote = "gdrive:pinball-pvp-backups",
    [int]$RetentionDays = 14
)

$ErrorActionPreference = "Stop"

function Send-FailureAlert {
    param([string]$ErrorMessage)

    $smtpUser = $env:BACKUP_ALERT_SMTP_USERNAME
    $smtpPass = $env:BACKUP_ALERT_SMTP_PASSWORD
    $toAddress = $env:BACKUP_ALERT_TO_ADDRESS
    if (-not $smtpUser -or -not $smtpPass -or -not $toAddress) {
        Write-Warning "BACKUP_ALERT_SMTP_USERNAME/BACKUP_ALERT_SMTP_PASSWORD/BACKUP_ALERT_TO_ADDRESS not all set - skipping failure alert email."
        return
    }

    try {
        $securePass = ConvertTo-SecureString $smtpPass -AsPlainText -Force
        $cred = New-Object System.Management.Automation.PSCredential ($smtpUser, $securePass)

        Send-MailMessage -From $smtpUser -To $toAddress `
            -Subject "PinballPvP DB backup FAILED on $(Get-Date -Format 'yyyy-MM-dd HH:mm')" `
            -Body $ErrorMessage `
            -SmtpServer "smtp.gmail.com" -Port 587 -UseSsl `
            -Credential $cred
    } catch {
        Write-Warning "Failed to send failure alert email: $_"
    }
}

try {
    New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null

    $dbContainer = docker ps --filter "label=com.docker.compose.service=db" --format "{{.Names}}" | Select-Object -First 1
    if (-not $dbContainer) {
        throw "No running container found for the 'db' compose service - is the stack up?"
    }

    $timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
    $backupFile = Join-Path $BackupDir "pinballpvp_$timestamp.sql"
    $containerTempFile = "/tmp/pinballpvp_backup_$timestamp.sql"

    # Dump inside the container, then `docker cp` the file out directly - avoids PowerShell's
    # pipeline re-encoding external process stdout (which can corrupt line endings/add a BOM).
    # Deliberately unquoted $POSTGRES_USER/$POSTGRES_DB in the inner sh command: both are simple
    # identifiers with no spaces, and nesting quotes inside a native-command argument from PowerShell
    # is fragile (see the -N "" issue from earlier in this project's setup).
    docker exec $dbContainer sh -c "pg_dump -U `$POSTGRES_USER -d `$POSTGRES_DB -f $containerTempFile"
    docker cp "${dbContainer}:${containerTempFile}" $backupFile
    docker exec $dbContainer rm -f $containerTempFile

    Write-Host "Backup written to $backupFile"

    # Prune local dumps outside the retention window.
    Get-ChildItem -Path $BackupDir -Filter "pinballpvp_*.sql" |
        Where-Object { $_.LastWriteTime -lt (Get-Date).AddDays(-$RetentionDays) } |
        Remove-Item -Force

    # Mirror to Google Drive - `sync` (not `copy`) also deletes remote files pruned locally above,
    # so the remote copy never grows unbounded either.
    rclone sync $BackupDir $RcloneRemote --create-empty-src-dirs

    Write-Host "Synced $BackupDir to $RcloneRemote"
} catch {
    Send-FailureAlert -ErrorMessage $_.ToString()
    throw
}
