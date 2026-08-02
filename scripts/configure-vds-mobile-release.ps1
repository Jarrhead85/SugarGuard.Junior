param(
    [string]$Server = "195.209.215.61",
    [string]$User = "ubuntuuser",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\sugarguard_vds_ed25519",
    [ValidatePattern("^\d+\.\d+(\.\d+)?$")]
    [string]$Version = "1.13",
    [string]$ReleaseNotes
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ReleaseNotes)) {
    # UTF-8 Base64 сохраняет русскоязычный текст при запуске скрипта из Windows PowerShell 5.1.
    $ReleaseNotes = [System.Text.Encoding]::UTF8.GetString(
        [System.Convert]::FromBase64String("0KHRgtCw0YLRg9GBIFRlbGVncmFtLdCx0L7RgtCwINC/0YDQuCDQstC+0YHRgdGC0LDQvdC+0LLQu9C10L3QuNC4IFZQTiDQuCDQv9C+0LLRi9GI0LXQvdC90LDRjyDQvdCw0LTRkdC20L3QvtGB0YLRjCDRg9Cy0LXQtNC+0LzQu9C10L3QuNC5Lg==")
    )
}

if (-not (Test-Path $KeyPath)) {
    throw "SSH-ключ не найден: $KeyPath"
}

$downloadUrl = "https://sugar-guard.ru/downloads/SugarGuard.Junior-$Version.apk"
if ($ReleaseNotes -match "[\r\n]") {
    throw "ReleaseNotes должен быть одной строкой."
}

# Передаём текст в shell только как Base64: так символы кириллицы и специальные
# символы не могут повлиять на команды удалённого сервера.
$releaseNotesBase64 = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($ReleaseNotes))

$remoteScript = @"
set -euo pipefail
release_notes=`$(printf '%s' '$releaseNotesBase64' | base64 -d)
sudo sed -i -E 's|^MobileApp__Android__Version=.*|MobileApp__Android__Version=$Version|' /etc/sugarguard/api.env
sudo sed -i -E 's|^MobileApp__Android__DownloadUrl=.*|MobileApp__Android__DownloadUrl=$downloadUrl|' /etc/sugarguard/api.env
sudo sed -i -E '/^MobileApp__Android__ReleaseNotes=/d' /etc/sugarguard/api.env
printf 'MobileApp__Android__ReleaseNotes=%s\n' "`$release_notes" | sudo tee -a /etc/sugarguard/api.env >/dev/null
sudo systemctl restart sugarguard-api.service
for attempt in `{1..30}; do
    if curl --silent --fail --noproxy '*' http://127.0.0.1:5001/api/health/live >/dev/null; then
        echo 'Mobile release metadata updated and API is healthy.'
        exit 0
    fi
    sleep 1
done

sudo systemctl status sugarguard-api.service --no-pager
exit 1
"@

$encodedScript = [Convert]::ToBase64String([System.Text.Encoding]::UTF8.GetBytes($remoteScript))
$psi = New-Object System.Diagnostics.ProcessStartInfo
$psi.FileName = "ssh"
$psi.Arguments = "-i `"$KeyPath`" -o BatchMode=yes -o ConnectTimeout=15 $User@$Server `"tail -c +4 | base64 -d | bash`""
$psi.UseShellExecute = $false
$psi.RedirectStandardInput = $true
$psi.RedirectStandardOutput = $true
$psi.RedirectStandardError = $true

$process = New-Object System.Diagnostics.Process
$process.StartInfo = $psi
[void]$process.Start()

$payload = [System.Text.Encoding]::ASCII.GetBytes($encodedScript)
$process.StandardInput.BaseStream.Write($payload, 0, $payload.Length)
$process.StandardInput.BaseStream.Close()

$standardOutput = $process.StandardOutput.ReadToEnd()
$standardError = $process.StandardError.ReadToEnd()
$process.WaitForExit()

Write-Output $standardOutput
Write-Output $standardError

if ($process.ExitCode -ne 0) {
    throw "Не удалось обновить метаданные мобильного релиза."
}
