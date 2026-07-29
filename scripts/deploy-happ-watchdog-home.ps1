param(
    [string]$Server = "192.168.0.113",
    [string]$User = "jarrhead",
    [string]$KeyPath,
    [switch]$VerifyOnly
)

$ErrorActionPreference = "Stop"

$watchdogRoot = Join-Path $PSScriptRoot "linux"
$installer = Join-Path $PSScriptRoot "install-happ-telegram-watchdog.sh"
$remoteRoot = "/tmp/sugarguard-happ-watchdog-$([Guid]::NewGuid().ToString('N'))"

foreach ($requiredFile in @(
    $installer,
    (Join-Path $watchdogRoot "happ-telegram-watchdog.sh"),
    (Join-Path $watchdogRoot "happ-telegram-watchdog.env.example"),
    (Join-Path $watchdogRoot "sugarguard-happ-watchdog.service"),
    (Join-Path $watchdogRoot "sugarguard-happ-watchdog.timer"))) {
    if (-not (Test-Path -LiteralPath $requiredFile)) {
        throw "Не найден обязательный файл watchdog: $requiredFile"
    }
}

foreach ($command in @("ssh", "scp")) {
    if (-not (Get-Command $command -ErrorAction SilentlyContinue)) {
        throw "Для установки watchdog требуется OpenSSH: команда '$command' не найдена."
    }
}

$sshArgs = @("-o", "StrictHostKeyChecking=accept-new")
if (-not [string]::IsNullOrWhiteSpace($KeyPath)) {
    if (-not (Test-Path -LiteralPath $KeyPath)) {
        throw "SSH-ключ не найден: $KeyPath"
    }

    $sshArgs += @("-i", $KeyPath, "-o", "BatchMode=yes")
}

$remoteHost = "${User}@${Server}"

if ($VerifyOnly) {
    & ssh @sshArgs -tt $remoteHost "sudo /usr/local/libexec/sugarguard/happ-telegram-watchdog.sh; status=`$?; sudo journalctl -u sugarguard-happ-watchdog.service -n 30 --no-pager; exit `$status"
    if ($LASTEXITCODE -ne 0) {
        throw "Проверка watchdog завершилась ошибкой. Проверьте /etc/sugarguard/happ-telegram-watchdog.env и журнал systemd."
    }

    Write-Host "Проверка watchdog выполнена успешно." -ForegroundColor Green
    return
}

try {
    & ssh @sshArgs -tt $remoteHost "mkdir -p '$remoteRoot/linux'"
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось создать временный каталог на домашнем сервере."
    }

    & scp @sshArgs $installer "${remoteHost}:$remoteRoot/"
    if ($LASTEXITCODE -ne 0) {
        throw "Не удалось передать установщик watchdog на домашний сервер."
    }

    foreach ($payloadFile in @(
        (Join-Path $watchdogRoot "happ-telegram-watchdog.sh"),
        (Join-Path $watchdogRoot "happ-telegram-watchdog.env.example"),
        (Join-Path $watchdogRoot "sugarguard-happ-watchdog.service"),
        (Join-Path $watchdogRoot "sugarguard-happ-watchdog.timer"))) {
        & scp @sshArgs $payloadFile "${remoteHost}:$remoteRoot/linux/"
        if ($LASTEXITCODE -ne 0) {
            throw "Не удалось передать файл watchdog на домашний сервер: $payloadFile"
        }
    }

    & ssh @sshArgs -tt $remoteHost "sudo bash '$remoteRoot/install-happ-telegram-watchdog.sh'; status=`$?; rm -rf '$remoteRoot'; exit `$status"
    if ($LASTEXITCODE -ne 0) {
        throw "Установка watchdog завершилась ошибкой."
    }
}
finally {
    & ssh @sshArgs $remoteHost "rm -rf '$remoteRoot'" 2>$null
}

Write-Host "Watchdog установлен на $Server." -ForegroundColor Green
Write-Host "Он не получает и не хранит подписку Happ, токен Telegram или пароли."
Write-Host "Перед включением заполните /etc/sugarguard/happ-telegram-watchdog.env фактическим local proxy Happ и существующим HAPP_SERVICE."
Write-Host "После настройки проверьте: .\scripts\deploy-happ-watchdog-home.ps1 -Server $Server -User $User -VerifyOnly"
