param(
    [string]$Server = "195.209.215.61",
    [string]$User = "ubuntuuser",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\sugarguard_vds_ed25519",
    [switch]$SkipBuild
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishRoot = Join-Path $repoRoot "artifacts\publish-linux\SugarGuard.Bot"
$packageRoot = Join-Path $repoRoot "artifacts\packages"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"
$packagePath = Join-Path $packageRoot "SugarGuard.Bot-$timestamp.tar.gz"
$remotePackage = "/tmp/sugarguard-bot-$timestamp.tar.gz"
$remoteScript = "/tmp/sugarguard-deploy-bot-$timestamp.sh"

if (-not (Test-Path $KeyPath)) {
    throw "SSH-ключ не найден: $KeyPath"
}

New-Item -ItemType Directory -Force -Path $publishRoot, $packageRoot | Out-Null

if (-not $SkipBuild) {
    if (Test-Path $publishRoot) {
        Remove-Item -LiteralPath $publishRoot -Recurse -Force
    }

    dotnet publish (Join-Path $repoRoot "SugarGuard.Bot\SugarGuard.Bot.csproj") `
        -c Release `
        -r linux-x64 `
        --self-contained true `
        -p:UseAppHost=true `
        -o $publishRoot `
        -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось собрать Telegram-бота."
    }
}

tar.exe -czf $packagePath -C $publishRoot .
if ($LASTEXITCODE -ne 0) {
    throw "Не удалось упаковать Telegram-бота."
}

$sshArgs = @(
    "-i", $KeyPath,
    "-o", "BatchMode=yes",
    "-o", "IdentitiesOnly=yes",
    "-o", "StrictHostKeyChecking=yes"
)

scp @sshArgs $packagePath "${User}@${Server}:$remotePackage"
if ($LASTEXITCODE -ne 0) {
    throw "Не удалось передать пакет Telegram-бота на сервер."
}

$deployScriptContent = @"
set -euo pipefail
umask 077

target="/opt/sugarguard/bot"
backup_dir="/opt/sugarguard/backups"
package="$remotePackage"
timestamp="$timestamp"
config_backup="/tmp/sugarguard-bot-appsettings-`$timestamp.json"

sudo install -d -o root -g root -m 0700 "`$backup_dir"
sudo systemctl stop sugarguard-bot.service

if [ -d "`$target" ]; then
    sudo tar -C "`$(dirname "`$target")" \
        --exclude="`$(basename "`$target")/appsettings.json" \
        -czf "`$backup_dir/bot-`$timestamp.tar.gz" "`$(basename "`$target")"
    sudo chmod 0600 "`$backup_dir/bot-`$timestamp.tar.gz"
    if [ -f "`$target/appsettings.json" ]; then
        sudo install -o root -g root -m 0600 "`$target/appsettings.json" "`$config_backup"
    fi
fi

sudo rm -rf "`${target}.new" "`${target}.old"
sudo mkdir -p "`${target}.new"
sudo tar -xzf "`$package" -C "`${target}.new"
sudo chown -R root:sugarguard "`${target}.new"
sudo find "`${target}.new" -type d -exec chmod 0750 {} +
sudo find "`${target}.new" -type f -exec chmod 0640 {} +
sudo chmod 0750 "`${target}.new/SugarGuard.Bot"

if [ -d "`$target" ]; then
    sudo mv "`$target" "`${target}.old"
fi

sudo mv "`${target}.new" "`$target"

if [ -f "`$config_backup" ]; then
    sudo mv "`$config_backup" "`$target/appsettings.json"
    sudo chown root:sugarguard "`$target/appsettings.json"
    sudo chmod 0640 "`$target/appsettings.json"
fi

if ! sudo systemctl start sugarguard-bot.service || ! sudo systemctl is-active --quiet sugarguard-bot.service; then
    sudo systemctl stop sugarguard-bot.service || true
    sudo rm -rf "`$target"
    if [ -d "`${target}.old" ]; then
        sudo mv "`${target}.old" "`$target"
        sudo systemctl start sugarguard-bot.service || true
    fi
    exit 1
fi

sudo rm -rf "`${target}.old"
sudo rm -f "`$package"
sudo rm -f "`$config_backup"
sudo systemctl --no-pager --full status sugarguard-bot.service | sed -n '1,45p'
"@

$localScript = Join-Path $env:TEMP "sugarguard-deploy-bot-$timestamp.sh"
$deployScriptWithUnixLineEndings = $deployScriptContent.Replace("`r`n", "`n")
[System.IO.File]::WriteAllText($localScript, $deployScriptWithUnixLineEndings, [System.Text.UTF8Encoding]::new($false))

try {
    scp @sshArgs $localScript "${User}@${Server}:$remoteScript"
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось передать сценарий развёртывания Telegram-бота."
    }

    ssh @sshArgs "$User@$Server" "bash $remoteScript; status=`$?; rm -f $remoteScript; exit `$status"
    if ($LASTEXITCODE -ne 0) {
        throw "Развёртывание Telegram-бота завершилось ошибкой."
    }
}
finally {
    Remove-Item -LiteralPath $localScript -Force -ErrorAction SilentlyContinue
}

Write-Host "Telegram-бот успешно развёрнут." -ForegroundColor Green
