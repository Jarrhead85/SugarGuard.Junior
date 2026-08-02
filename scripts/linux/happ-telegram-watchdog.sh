#!/usr/bin/env bash
# Проверяет доступность Telegram исключительно через локальный proxy Happ.
#
# Скрипт не читает конфигурацию Happ, не обрабатывает подписки и не выбирает узлы.
# Он может только перезапустить уже настроенный systemd-сервис Happ, если Telegram
# перестал отвечать через proxy. Это исключает работу с секретами и не опирается на
# недокументированные интерфейсы Happ.

set -Eeuo pipefail
IFS=$'\n\t'

readonly DEFAULT_CONFIG_FILE="/etc/sugarguard/happ-telegram-watchdog.env"
readonly DEFAULT_STATE_DIR="/var/lib/sugarguard-happ-watchdog"
readonly DEFAULT_STATE_FILE="${DEFAULT_STATE_DIR}/state"
readonly DEFAULT_LOCK_FILE="/run/sugarguard-happ-watchdog.lock"
readonly TELEGRAM_PROBE_URL="https://api.telegram.org/bot"
readonly SUGAR_GUARD_HEARTBEAT_URL="https://api.sugar-guard.ru/api/bot-service/status/heartbeat"

CONFIG_FILE="${HAPP_WATCHDOG_CONFIG_FILE:-$DEFAULT_CONFIG_FILE}"
STATE_DIR="${HAPP_WATCHDOG_STATE_DIR:-$DEFAULT_STATE_DIR}"
STATE_FILE="${HAPP_WATCHDOG_STATE_FILE:-$DEFAULT_STATE_FILE}"
LOCK_FILE="${HAPP_WATCHDOG_LOCK_FILE:-$DEFAULT_LOCK_FILE}"

# Значения по умолчанию безопасны: без proxy или без сервисов перезапуск не выполняется.
HAPP_PROXY_URL="${HAPP_PROXY_URL:-}"
HAPP_SERVICE="${HAPP_SERVICE:-}"
BOT_SERVICE="${BOT_SERVICE:-sugarguard-bot.service}"
FAILURE_THRESHOLD="${FAILURE_THRESHOLD:-2}"
RECOVERY_COOLDOWN_SECONDS="${RECOVERY_COOLDOWN_SECONDS:-300}"
RESTART_SETTLE_SECONDS="${RESTART_SETTLE_SECONDS:-8}"
CONNECT_TIMEOUT_SECONDS="${CONNECT_TIMEOUT_SECONDS:-8}"
MAX_TIME_SECONDS="${MAX_TIME_SECONDS:-15}"
# BOT_SERVICE_AUTH_KEY передаётся systemd из закрытого EnvironmentFile
# основного Telegram-бота. Watchdog не дублирует и не хранит секрет.
BOT_SERVICE_AUTH_KEY="${BOT_SERVICE_AUTH_KEY:-}"
TELEGRAM_PROBE_LATENCY_MS=""

log() {
    local level="$1"
    shift
    printf '%s [%s] %s\n' "$(date --iso-8601=seconds)" "$level" "$*"
}

fail() {
    log "ERROR" "$*"
    exit 2
}

is_positive_integer() {
    [[ "$1" =~ ^[1-9][0-9]*$ ]]
}

is_non_negative_integer() {
    [[ "$1" =~ ^[0-9]+$ ]]
}

is_valid_unit_name() {
    [[ "$1" =~ ^[A-Za-z0-9_.@:-]+\.service$ ]]
}

# EnvironmentFile systemd не выполняет команды. Для запуска вручную читаем только
# ожидаемые ключи формата KEY=VALUE и не используем source/eval.
load_config_file() {
    [[ -f "$CONFIG_FILE" ]] || return 0

    local key value
    while IFS='=' read -r key value || [[ -n "$key" ]]; do
        key="${key//$'\r'/}"
        value="${value//$'\r'/}"

        [[ -z "$key" || "$key" == \#* ]] && continue

        case "$key" in
            HAPP_PROXY_URL) HAPP_PROXY_URL="$value" ;;
            HAPP_SERVICE) HAPP_SERVICE="$value" ;;
            BOT_SERVICE) BOT_SERVICE="$value" ;;
            FAILURE_THRESHOLD) FAILURE_THRESHOLD="$value" ;;
            RECOVERY_COOLDOWN_SECONDS) RECOVERY_COOLDOWN_SECONDS="$value" ;;
            RESTART_SETTLE_SECONDS) RESTART_SETTLE_SECONDS="$value" ;;
            CONNECT_TIMEOUT_SECONDS) CONNECT_TIMEOUT_SECONDS="$value" ;;
            MAX_TIME_SECONDS) MAX_TIME_SECONDS="$value" ;;
            *) log "WARNING" "Неизвестный ключ в конфигурации watchdog пропущен: $key" ;;
        esac
    done < "$CONFIG_FILE"
}

validate_config() {
    [[ -n "$HAPP_PROXY_URL" ]] || fail "HAPP_PROXY_URL не задан; проверка через прямое подключение запрещена."

    # Ограничиваем проверку loopback-адресом: watchdog предназначен только для
    # локального proxy Happ и не должен отправлять диагностику на внешний proxy.
    if [[ ! "$HAPP_PROXY_URL" =~ ^(http|https|socks5|socks5h)://(127\.0\.0\.1|localhost|\[::1\]):[1-9][0-9]{0,4}$ ]]; then
        fail "HAPP_PROXY_URL должен указывать на локальный HTTP/SOCKS proxy без пути и учётных данных."
    fi

    is_positive_integer "$FAILURE_THRESHOLD" || fail "FAILURE_THRESHOLD должен быть положительным целым числом."
    is_non_negative_integer "$RECOVERY_COOLDOWN_SECONDS" || fail "RECOVERY_COOLDOWN_SECONDS должен быть неотрицательным целым числом."
    is_non_negative_integer "$RESTART_SETTLE_SECONDS" || fail "RESTART_SETTLE_SECONDS должен быть неотрицательным целым числом."
    is_positive_integer "$CONNECT_TIMEOUT_SECONDS" || fail "CONNECT_TIMEOUT_SECONDS должен быть положительным целым числом."
    is_positive_integer "$MAX_TIME_SECONDS" || fail "MAX_TIME_SECONDS должен быть положительным целым числом."

    if [[ -n "$HAPP_SERVICE" ]] && ! is_valid_unit_name "$HAPP_SERVICE"; then
        fail "HAPP_SERVICE должен быть именем systemd unit с суффиксом .service."
    fi
    is_valid_unit_name "$BOT_SERVICE" || fail "BOT_SERVICE должен быть именем systemd unit с суффиксом .service."
}

read_state_value() {
    local requested_key="$1"

    [[ -f "$STATE_FILE" ]] || return 0

    awk -F= -v requested_key="$requested_key" '$1 == requested_key { print $2; exit }' "$STATE_FILE" 2>/dev/null || true
}

write_state() {
    local failures="$1"
    local result="$2"
    local recovery_epoch="$3"
    local latency_ms="${4:-}"
    local now_epoch
    local temporary_file

    now_epoch="$(date +%s)"
    install -d -m 0750 -o root -g root "$STATE_DIR"
    temporary_file="$(mktemp "${STATE_DIR}/.state.XXXXXX")"

    {
        printf 'consecutive_failures=%s\n' "$failures"
        printf 'last_check_epoch=%s\n' "$now_epoch"
        printf 'last_check_result=%s\n' "$result"
        printf 'last_recovery_epoch=%s\n' "$recovery_epoch"
        printf 'last_probe_latency_ms=%s\n' "$latency_ms"
    } > "$temporary_file"

    chown root:root "$temporary_file"
    chmod 0640 "$temporary_file"
    mv -f "$temporary_file" "$STATE_FILE"
}

probe_telegram_through_happ() {
    local response_code elapsed_seconds

    # /bot без токена отвечает HTTP 404 именно от Telegram Bot API. Это позволяет
    # подтвердить доступ к нужному сервису без хранения токена бота в watchdog.
    read -r response_code elapsed_seconds <<< "$(curl \
        --silent \
        --show-error \
        --output /dev/null \
        --write-out '%{http_code} %{time_total}' \
        --proxy "$HAPP_PROXY_URL" \
        --noproxy '' \
        --connect-timeout "$CONNECT_TIMEOUT_SECONDS" \
        --max-time "$MAX_TIME_SECONDS" \
        "$TELEGRAM_PROBE_URL" 2>/dev/null || true)"

    if [[ "$response_code" != "404" ]]; then
        TELEGRAM_PROBE_LATENCY_MS=""
        return 1
    fi

    TELEGRAM_PROBE_LATENCY_MS="$(LC_NUMERIC=C awk -v seconds="${elapsed_seconds:-0}" 'BEGIN { printf "%.0f", seconds * 1000 }')"
    return 0
}

# Отчёт отправляется напрямую в API, без proxy Happ. Благодаря этому
# кабинет и мобильное приложение узнают о деградации именно тогда,
# когда VPN перестал работать, а не только после его восстановления.
report_bot_status() {
    local telegram_available="$1"
    local error_message="${2:-}"
    local payload response_code header_file

    if [[ -z "$BOT_SERVICE_AUTH_KEY" ]]; then
        log "ERROR" "BOT_SERVICE_AUTH_KEY не передан watchdog: статус VPN не отправлен в SugarGuard API."
        return 1
    fi

    payload="$(python3 - "$telegram_available" "$error_message" <<'PY'
import json
import sys

print(json.dumps({
    "botName": "telegram",
    # Сам факт успешного POST ниже подтверждает доступность SugarGuard API.
    "internetAvailable": True,
    "externalApiAvailable": sys.argv[1].lower() == "true",
    "error": sys.argv[2] or None,
    "version": "happ-watchdog"
}, ensure_ascii=False, separators=(",", ":")))
PY
)"

    # Передача секретного заголовка в argv curl позволила бы увидеть его через
    # ps/proc. Временный config-файл доступен только root и удаляется сразу
    # после запроса.
    header_file="$(mktemp /run/sugarguard-happ-watchdog.headers.XXXXXX)"
    chmod 0600 "$header_file"
    {
        printf '%s\n' 'header = "Content-Type: application/json"'
        printf 'header = "X-Bot-Auth: %s"\n' "$BOT_SERVICE_AUTH_KEY"
    } > "$header_file"

    response_code="$(curl \
        --silent \
        --show-error \
        --output /dev/null \
        --write-out '%{http_code}' \
        --noproxy '*' \
        --connect-timeout "$CONNECT_TIMEOUT_SECONDS" \
        --max-time "$MAX_TIME_SECONDS" \
        --config "$header_file" \
        --data "$payload" \
        "$SUGAR_GUARD_HEARTBEAT_URL" 2>/dev/null || true)"
    rm -f "$header_file"

    if [[ "$response_code" =~ ^2[0-9]{2}$ ]]; then
        return 0
    fi

    log "ERROR" "Не удалось передать статус VPN в SugarGuard API (HTTP ${response_code:-000})."
    return 1
}

restart_service_if_present() {
    local unit_name="$1"
    local label="$2"

    if ! systemctl cat "$unit_name" >/dev/null 2>&1; then
        log "WARNING" "$label не перезапущен: unit $unit_name не найден."
        return 1
    fi

    log "WARNING" "Перезапуск: $label ($unit_name)."
    if ! systemctl restart "$unit_name"; then
        log "ERROR" "Не удалось перезапустить $label ($unit_name)."
        return 1
    fi

    return 0
}

perform_recovery() {
    local last_recovery_epoch="$1"
    local now_epoch
    local failure_message

    now_epoch="$(date +%s)"
    if (( now_epoch - last_recovery_epoch < RECOVERY_COOLDOWN_SECONDS )); then
        log "WARNING" "Восстановление пропущено: действует cooldown ${RECOVERY_COOLDOWN_SECONDS} с."
        report_bot_status false "Happ VPN недоступен: Telegram не отвечает через локальный proxy. Ведётся автоматическое восстановление." || true
        return 1
    fi

    if [[ -z "$HAPP_SERVICE" ]]; then
        log "ERROR" "HAPP_SERVICE не настроен: проверка зафиксировала сбой, но безопасный перезапуск Happ невозможен."
        write_state "$FAILURE_THRESHOLD" "unhealthy_without_recovery_service" "$last_recovery_epoch"
        report_bot_status false "Happ VPN недоступен: сервис автоматического восстановления не настроен. Ведутся работы по восстановлению Telegram-бота." || true
        return 1
    fi

    restart_service_if_present "$HAPP_SERVICE" "Happ" || true

    if (( RESTART_SETTLE_SECONDS > 0 )); then
        sleep "$RESTART_SETTLE_SECONDS"
    fi

    if probe_telegram_through_happ; then
        log "INFO" "Доступ к Telegram через Happ восстановлен."
        # Long-polling получает новое соединение только после успешного
        # восстановления proxy. Не перезапускаем бот при каждом неудачном
        # пробном восстановлении и не теряем его direct heartbeat.
        restart_service_if_present "$BOT_SERVICE" "Telegram-бот" || true
        write_state 0 "healthy_after_recovery" "$now_epoch" "$TELEGRAM_PROBE_LATENCY_MS"
        # Успешный probe подтверждает только VPN-маршрут. Полную доступность
        # бота подтверждает его собственный heartbeat после перезапуска.
        return 0
    fi

    failure_message="Happ VPN недоступен: Telegram не отвечает через локальный proxy после автоматического восстановления. Уведомления Telegram временно не доставляются; ведутся работы по восстановлению."
    log "ERROR" "$failure_message"
    write_state "$FAILURE_THRESHOLD" "unhealthy_after_recovery" "$now_epoch"
    report_bot_status false "$failure_message" || true
    return 1
}

main() {
    command -v curl >/dev/null 2>&1 || fail "curl не установлен."
    command -v python3 >/dev/null 2>&1 || fail "python3 не установлен."
    command -v systemctl >/dev/null 2>&1 || fail "systemctl не установлен."
    command -v flock >/dev/null 2>&1 || fail "flock не установлен."

    load_config_file
    validate_config

    install -d -m 0750 -o root -g root "$STATE_DIR"
    exec 9>"$LOCK_FILE"
    if ! flock -n 9; then
        log "INFO" "Предыдущая проверка ещё выполняется; параллельный запуск пропущен."
        return 0
    fi

    local consecutive_failures last_recovery_epoch failure_message
    consecutive_failures="$(read_state_value consecutive_failures)"
    last_recovery_epoch="$(read_state_value last_recovery_epoch)"
    is_non_negative_integer "$consecutive_failures" || consecutive_failures=0
    is_non_negative_integer "$last_recovery_epoch" || last_recovery_epoch=0

    if probe_telegram_through_happ; then
        if (( consecutive_failures > 0 )); then
            log "INFO" "Доступ к Telegram через Happ восстановлен без перезапуска."
        fi
        write_state 0 "healthy" "$last_recovery_epoch" "$TELEGRAM_PROBE_LATENCY_MS"
        # Не перезаписываем self-heartbeat бота: watchdog проверяет лишь маршрут,
        # а не состояние polling, авторизации и очереди доставки Telegram.
        return 0
    fi

    consecutive_failures=$((consecutive_failures + 1))
    write_state "$consecutive_failures" "unhealthy" "$last_recovery_epoch"
    failure_message="Happ VPN недоступен: Telegram не отвечает через локальный proxy (проверка ${consecutive_failures}/${FAILURE_THRESHOLD}). Выполняется автоматическое восстановление."
    log "WARNING" "$failure_message"
    report_bot_status false "$failure_message" || true

    if (( consecutive_failures < FAILURE_THRESHOLD )); then
        return 1
    fi

    perform_recovery "$last_recovery_epoch"
}

main "$@"
