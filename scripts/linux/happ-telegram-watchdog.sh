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
    } > "$temporary_file"

    chown root:root "$temporary_file"
    chmod 0640 "$temporary_file"
    mv -f "$temporary_file" "$STATE_FILE"
}

probe_telegram_through_happ() {
    local response_code

    # /bot без токена отвечает HTTP 404 именно от Telegram Bot API. Это позволяет
    # подтвердить доступ к нужному сервису без хранения токена бота в watchdog.
    response_code="$(curl \
        --silent \
        --show-error \
        --output /dev/null \
        --write-out '%{http_code}' \
        --proxy "$HAPP_PROXY_URL" \
        --noproxy '' \
        --connect-timeout "$CONNECT_TIMEOUT_SECONDS" \
        --max-time "$MAX_TIME_SECONDS" \
        "$TELEGRAM_PROBE_URL" 2>/dev/null || true)"

    [[ "$response_code" == "404" ]]
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

    now_epoch="$(date +%s)"
    if (( now_epoch - last_recovery_epoch < RECOVERY_COOLDOWN_SECONDS )); then
        log "WARNING" "Восстановление пропущено: действует cooldown ${RECOVERY_COOLDOWN_SECONDS} с."
        return 1
    fi

    if [[ -z "$HAPP_SERVICE" ]]; then
        log "ERROR" "HAPP_SERVICE не настроен: проверка зафиксировала сбой, но безопасный перезапуск Happ невозможен."
        return 1
    fi

    restart_service_if_present "$HAPP_SERVICE" "Happ" || true

    if (( RESTART_SETTLE_SECONDS > 0 )); then
        sleep "$RESTART_SETTLE_SECONDS"
    fi

    # Перезапуск бота выполняется после Happ: long-polling получает новое соединение
    # даже если его HTTP-клиент уже успел зафиксировать timeout.
    restart_service_if_present "$BOT_SERVICE" "Telegram-бот" || true

    if probe_telegram_through_happ; then
        log "INFO" "Доступ к Telegram через Happ восстановлен."
        write_state 0 "healthy_after_recovery" "$now_epoch"
        return 0
    fi

    log "ERROR" "После перезапуска Happ и Telegram-бота Telegram всё ещё недоступен через local proxy."
    write_state "$FAILURE_THRESHOLD" "unhealthy_after_recovery" "$now_epoch"
    return 1
}

main() {
    command -v curl >/dev/null 2>&1 || fail "curl не установлен."
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

    local consecutive_failures last_recovery_epoch
    consecutive_failures="$(read_state_value consecutive_failures)"
    last_recovery_epoch="$(read_state_value last_recovery_epoch)"
    is_non_negative_integer "$consecutive_failures" || consecutive_failures=0
    is_non_negative_integer "$last_recovery_epoch" || last_recovery_epoch=0

    if probe_telegram_through_happ; then
        if (( consecutive_failures > 0 )); then
            log "INFO" "Доступ к Telegram через Happ восстановлен без перезапуска."
        fi
        write_state 0 "healthy" "$last_recovery_epoch"
        return 0
    fi

    consecutive_failures=$((consecutive_failures + 1))
    write_state "$consecutive_failures" "unhealthy" "$last_recovery_epoch"
    log "WARNING" "Telegram недоступен через local proxy Happ (сбой ${consecutive_failures}/${FAILURE_THRESHOLD})."

    if (( consecutive_failures < FAILURE_THRESHOLD )); then
        return 1
    fi

    perform_recovery "$last_recovery_epoch"
}

main "$@"
