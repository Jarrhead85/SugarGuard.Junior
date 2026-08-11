param(
    [Parameter(Mandatory = $true)]
    [ValidatePattern("^\d+\.\d+(\.\d+)?$")]
    [string]$Version,
    [Parameter(Mandatory = $true)]
    [string]$ApkPath,
    [string]$Server = "195.209.215.61",
    [string]$User = "ubuntuuser",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\sugarguard_vds_ed25519"
)

$ErrorActionPreference = "Stop"

$resolvedApkPath = (Resolve-Path -LiteralPath $ApkPath).Path
if (-not (Test-Path -LiteralPath $KeyPath -PathType Leaf)) {
    throw "SSH key was not found: $KeyPath"
}

$expectedHash = (Get-FileHash -LiteralPath $resolvedApkPath -Algorithm SHA256).Hash.ToLowerInvariant()
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$remoteApkPath = "/tmp/SugarGuard.Junior-$Version.apk.upload"
$remoteScriptPath = "/tmp/install-sugarguard-apk-$timestamp.sh"
$localScriptPath = Join-Path ([System.IO.Path]::GetTempPath()) "install-sugarguard-apk-$timestamp.sh"
$sshArgs = @(
    "-i", $KeyPath,
    "-o", "BatchMode=yes",
    "-o", "IdentitiesOnly=yes",
    "-o", "StrictHostKeyChecking=yes",
    "-o", "ConnectTimeout=15"
)

$remoteScript = @"
set -euo pipefail
src='$remoteApkPath'
dst='/opt/sugarguard/web/wwwroot/downloads/SugarGuard.Junior-$Version.apk'
backup_dir='/opt/sugarguard/backups/mobile'
expected='$expectedHash'

cleanup() {
    rm -f "`$src"
}
trap cleanup EXIT

actual=`$(sha256sum "`$src" | awk '{print `$1}')
if [ "`$actual" != "`$expected" ]; then
    echo 'APK checksum mismatch' >&2
    exit 1
fi

sudo install -d -o root -g root -m 0700 "`$backup_dir"
sudo install -d -o root -g sugarguard -m 0750 "`$(dirname "`$dst")"

if sudo test -f "`$dst"; then
    backup="`$backup_dir/SugarGuard.Junior-$Version.`$(date -u +%Y%m%dT%H%M%SZ).apk"
    sudo cp "`$dst" "`$backup"
    sudo chmod 0600 "`$backup"
fi

sudo install -o root -g sugarguard -m 0640 "`$src" "`$dst.new"
sudo mv -f "`$dst.new" "`$dst"
sudo sha256sum "`$dst"
sudo stat -c '%U:%G %a %s %n' "`$dst"
"@

$utf8NoBom = New-Object System.Text.UTF8Encoding $false
$remoteScriptWithUnixLineEndings = $remoteScript.Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($localScriptPath, $remoteScriptWithUnixLineEndings, $utf8NoBom)

try {
    scp @sshArgs $resolvedApkPath "${User}@${Server}:$remoteApkPath"
    if ($LASTEXITCODE -ne 0) {
        throw "APK upload failed"
    }

    scp @sshArgs $localScriptPath "${User}@${Server}:$remoteScriptPath"
    if ($LASTEXITCODE -ne 0) {
        throw "APK installer upload failed"
    }

    ssh @sshArgs "$User@$Server" "bash '$remoteScriptPath'"
    if ($LASTEXITCODE -ne 0) {
        throw "Remote APK installation failed"
    }
}
finally {
    ssh @sshArgs "$User@$Server" "rm -f '$remoteScriptPath' '$remoteApkPath'" 2>$null
    Remove-Item -LiteralPath $localScriptPath -Force -ErrorAction SilentlyContinue
}

Write-Host "APK $Version installed successfully." -ForegroundColor Green
