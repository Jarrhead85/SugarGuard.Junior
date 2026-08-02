param(
    [string]$Server = "195.209.215.61",
    [string]$User = "ubuntuuser",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\sugarguard_vds_ed25519",
    [ValidatePattern("^\d+\.\d+(\.\d+)?$")]
    [string]$Version = "1.11",
    [string]$ReleaseNotes = "Ночной инсулин, надёжная доставка уведомлений Telegram и локальное время ребёнка в сообщениях."
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $KeyPath)) {
    throw "SSH-ключ не найден: $KeyPath"
}

$downloadUrl = "https://sugar-guard.ru/downloads/SugarGuard.Junior-$Version.apk"
$escapedNotes = $ReleaseNotes

$remoteScript = @"
set -euo pipefail
sudo sed -i -E 's|^MobileApp__Android__Version=.*|MobileApp__Android__Version=$Version|' /etc/sugarguard/api.env
sudo sed -i -E 's|^MobileApp__Android__DownloadUrl=.*|MobileApp__Android__DownloadUrl=$downloadUrl|' /etc/sugarguard/api.env
sudo sed -i -E 's|^MobileApp__Android__ReleaseNotes=.*|MobileApp__Android__ReleaseNotes=$escapedNotes|' /etc/sugarguard/api.env
sudo systemctl restart sugarguard-api.service
sleep 3
sudo systemctl is-active --quiet sugarguard-api.service
echo 'Mobile release metadata updated.'
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
