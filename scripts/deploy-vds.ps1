param(
    [string]$Server = "195.209.215.61",
    [string]$User = "ubuntuuser",
    [string]$KeyPath = "$env:USERPROFILE\.ssh\sugarguard_vds_ed25519",
    [switch]$SkipBuild,
    [switch]$ApiOnly,
    [switch]$WebOnly
)

$ErrorActionPreference = "Stop"

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$publishRoot = Join-Path $repoRoot "artifacts\publish-linux"
$packageRoot = Join-Path $repoRoot "artifacts\packages"
$timestamp = Get-Date -Format "yyyyMMdd-HHmmss"

$deployApi = -not $WebOnly
$deployWeb = -not $ApiOnly

if ($ApiOnly -and $WebOnly) {
    throw "ApiOnly и WebOnly нельзя использовать одновременно."
}

if (-not (Test-Path $KeyPath)) {
    throw "SSH-ключ не найден: $KeyPath"
}

New-Item -ItemType Directory -Force -Path $publishRoot, $packageRoot | Out-Null

function Invoke-Step {
    param(
        [string]$Title,
        [scriptblock]$Action
    )

    Write-Host "`n==> $Title" -ForegroundColor Cyan
    & $Action
}

function Publish-Project {
    param(
        [string]$Project,
        [string]$Output
    )

    if (Test-Path $Output) {
        Remove-Item -LiteralPath $Output -Recurse -Force
    }

    dotnet publish $Project `
        -c Release `
        -r linux-x64 `
        --self-contained true `
        -p:UseAppHost=true `
        -o $Output `
        -v minimal

    if ($LASTEXITCODE -ne 0) {
        throw "dotnet publish failed for $Project"
    }
}

function New-Package {
    param(
        [string]$Source,
        [string]$Destination
    )

    if (Test-Path $Destination) {
        Remove-Item -LiteralPath $Destination -Force
    }

    tar.exe -czf $Destination -C $Source .

    if ($LASTEXITCODE -ne 0) {
        throw "tar packaging failed for $Source"
    }
}

$apiPublish = Join-Path $publishRoot "SugarGuard.API"
$webPublish = Join-Path $publishRoot "SugarGuard.Web"
$apiPackage = Join-Path $packageRoot "SugarGuard.API-$timestamp.tar.gz"
$webPackage = Join-Path $packageRoot "SugarGuard.Web-$timestamp.tar.gz"

if (-not $SkipBuild) {
    if ($deployApi) {
        Invoke-Step "Publish API for linux-x64" {
            Publish-Project `
                -Project (Join-Path $repoRoot "SugarGuard.API\SugarGuard.API.csproj") `
                -Output $apiPublish
        }
    }

    if ($deployWeb) {
        Invoke-Step "Publish Web for linux-x64" {
            Publish-Project `
                -Project (Join-Path $repoRoot "SugarGuard.Web\SugarGuard.Web.csproj") `
                -Output $webPublish
        }
    }
}

if ($deployApi) {
    Invoke-Step "Package API" {
        New-Package -Source $apiPublish -Destination $apiPackage
    }
}

if ($deployWeb) {
    Invoke-Step "Package Web" {
        New-Package -Source $webPublish -Destination $webPackage
    }
}

$sshArgs = @(
    "-i", $KeyPath,
    "-o", "BatchMode=yes",
    "-o", "IdentitiesOnly=yes",
    "-o", "StrictHostKeyChecking=yes"
)

if ($deployApi) {
    Invoke-Step "Upload API package" {
        scp @sshArgs $apiPackage "${User}@${Server}:/tmp/sugarguard-api-$timestamp.tar.gz"
        if ($LASTEXITCODE -ne 0) {
            throw "API package upload failed"
        }
    }
}

if ($deployWeb) {
    Invoke-Step "Upload Web package" {
        scp @sshArgs $webPackage "${User}@${Server}:/tmp/sugarguard-web-$timestamp.tar.gz"
        if ($LASTEXITCODE -ne 0) {
            throw "Web package upload failed"
        }
    }
}

$remoteScript = @"
set -euo pipefail
umask 077

timestamp="$timestamp"
deploy_api="$($deployApi.ToString().ToLowerInvariant())"
deploy_web="$($deployWeb.ToString().ToLowerInvariant())"

backup_dir="/opt/sugarguard/backups"
sudo install -d -o root -g root -m 0700 "`$backup_dir"
sudo install -d -o sugarguard -g sugarguard -m 0750 \
    /var/lib/sugarguard/uploads/profiles \
    /var/lib/sugarguard/uploads/doctor-verification \
    /var/lib/sugarguard/uploads/article-images

api_package="/tmp/sugarguard-api-`$timestamp.tar.gz"
web_package="/tmp/sugarguard-web-`$timestamp.tar.gz"

# Пакеты удаляются даже при неуспешном развёртывании, чтобы /tmp не рос от старых архивов.
cleanup_deploy_files() {
    sudo rm -f "`$api_package" "`$web_package"
}
trap cleanup_deploy_files EXIT

# Для отката сохраняются три последние резервные копии каждого приложения.
prune_backups() {
    local name="`$1"
    local -a stale_backups=()

    mapfile -t stale_backups < <(
        sudo find "`$backup_dir" -maxdepth 1 -type f -name "`$name-*.tar.gz" -printf '%T@ %p\n' |
            sort -nr |
            tail -n +4 |
            cut -d' ' -f2-
    )

    for stale_backup in "`${stale_backups[@]}"; do
        [ -n "`$stale_backup" ] && sudo rm -f "`$stale_backup"
    done
}

deploy_one() {
    local service="`$1"
    local target="`$2"
    local package="`$3"
    local executable="`$4"
    local name="`$5"

    echo "Stopping `$service"
    sudo systemctl stop "`$service"

    if sudo test -d "`$target"; then
        echo "Backing up `$target"
        local target_name
        target_name="`$(basename "`$target")"

        # Пользовательские вложения и APK сохраняются отдельно при развёртывании.
        # Не добавляем их в каждый архив: они не относятся к коду и сильно замедляют бэкап.
        sudo tar -C "`$(dirname "`$target")" \
            --exclude="`$target_name/wwwroot/uploads" \
            --exclude="`$target_name/wwwroot/downloads" \
            -czf "`$backup_dir/`$name-`$timestamp.tar.gz" "`$target_name"
        sudo chmod 0600 "`$backup_dir/`$name-`$timestamp.tar.gz"
    fi

    echo "Extracting `$package"
    sudo rm -rf "`$target.new" "`$target.old"
    sudo mkdir -p "`$target.new"
    sudo tar -xzf "`$package" -C "`$target.new"

    # Runtime/user-facing static data must survive binary deployments.
    if sudo test -d "`$target/wwwroot/uploads"; then
        sudo mkdir -p "`$target.new/wwwroot"
        sudo rm -rf "`$target.new/wwwroot/uploads"
        sudo cp -a "`$target/wwwroot/uploads" "`$target.new/wwwroot/uploads"
    fi

    # Published APK files are uploaded outside the application package and must
    # remain available after Web redeploys.
    if sudo test -d "`$target/wwwroot/downloads"; then
        sudo mkdir -p "`$target.new/wwwroot"
        sudo rm -rf "`$target.new/wwwroot/downloads"
        sudo cp -a "`$target/wwwroot/downloads" "`$target.new/wwwroot/downloads"
    fi

    sudo chown -R root:sugarguard "`$target.new"
    sudo find "`$target.new" -type d -exec chmod 0750 {} +
    sudo find "`$target.new" -type f -exec chmod 0640 {} +
    sudo chmod 0750 "`$target.new/`$executable"

    # Child photos are the only runtime-writable files left under wwwroot.
    # Other medical uploads are stored outside the release under /var/lib.
    if [ "`$name" = "api" ]; then
        sudo install -d -o sugarguard -g sugarguard -m 0750 "`$target.new/wwwroot/uploads/children"
        sudo chown -R sugarguard:sugarguard "`$target.new/wwwroot/uploads/children"
        sudo find "`$target.new/wwwroot/uploads/children" -type d -exec chmod 0750 {} +
        sudo find "`$target.new/wwwroot/uploads/children" -type f -exec chmod 0640 {} +
    fi

    if sudo test -d "`$target"; then
        sudo mv "`$target" "`$target.old"
    fi

    sudo rm -rf "`$target"
    sudo mv "`$target.new" "`$target"

    echo "Starting `$service"
    if ! sudo systemctl start "`$service"; then
        echo "`$service failed to start. Rolling back."
        sudo rm -rf "`$target"
        if sudo test -d "`$target.old"; then
            sudo mv "`$target.old" "`$target"
            sudo systemctl start "`$service" || true
        fi
        exit 1
    fi

    if ! sudo systemctl is-active --quiet "`$service"; then
        echo "`$service is not active after deployment. Rolling back."
        sudo systemctl stop "`$service" || true
        sudo rm -rf "`$target"
        if sudo test -d "`$target.old"; then
            sudo mv "`$target.old" "`$target"
            sudo systemctl start "`$service" || true
        fi
        exit 1
    fi

    sudo rm -rf "`$target.old"
    prune_backups "`$name"
}

if [ "`$deploy_api" = "true" ]; then
    deploy_one "sugarguard-api.service" "/opt/sugarguard/api" "`$api_package" "SugarGuard.API" "api"
fi

if [ "`$deploy_web" = "true" ]; then
    deploy_one "sugarguard-web.service" "/opt/sugarguard/web" "`$web_package" "SugarGuard.Web" "web"
fi

sudo systemctl --no-pager --full status sugarguard-api.service sugarguard-web.service | sed -n '1,80p'
"@

Invoke-Step "Deploy on VDS" {
    $remoteScriptPath = Join-Path ([System.IO.Path]::GetTempPath()) "sugarguard-deploy-$timestamp.sh"
    $remoteScriptTarget = "/tmp/sugarguard-deploy-$timestamp.sh"
    $utf8NoBom = New-Object System.Text.UTF8Encoding $false
    # Bash treats a trailing carriage return as part of the option name
    # (for example, `pipefail\r`). Always upload Unix line endings even when
    # this PowerShell script is checked out with CRLF on Windows.
    $remoteScriptWithUnixLineEndings = $remoteScript.Replace("`r`n", "`n")
    [System.IO.File]::WriteAllText($remoteScriptPath, $remoteScriptWithUnixLineEndings, $utf8NoBom)
    try {
        scp @sshArgs $remoteScriptPath "${User}@${Server}:$remoteScriptTarget"
        if ($LASTEXITCODE -ne 0) {
            throw "Remote deploy script upload failed"
        }

        ssh @sshArgs "$User@$Server" "bash $remoteScriptTarget; status=`$?; rm -f $remoteScriptTarget; exit `$status"
        if ($LASTEXITCODE -ne 0) {
            throw "Remote deployment failed"
        }
    }
    finally {
        Remove-Item -LiteralPath $remoteScriptPath -Force -ErrorAction SilentlyContinue
    }
}

Write-Host "`nDeploy completed." -ForegroundColor Green
