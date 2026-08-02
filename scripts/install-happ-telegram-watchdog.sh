#!/usr/bin/env bash
# Устанавливает watchdog Happ/Telegram на Ubuntu с systemd.
# Запускайте из корня исходного репозитория: sudo scripts/install-happ-telegram-watchdog.sh

set -Eeuo pipefail
IFS=$'\n\t'

readonly SCRIPT_DIR="$(cd -- "$(dirname -- "${BASH_SOURCE[0]}")" && pwd)"
readonly LINUX_DIR="${SCRIPT_DIR}/linux"
readonly TARGET_DIR="/usr/local/libexec/sugarguard"
readonly CONFIG_DIR="/etc/sugarguard"

[[ "${EUID}" -eq 0 ]] || {
    echo "Запустите установку от root: sudo $0" >&2
    exit 1
}

for file in \
    "${LINUX_DIR}/happ-telegram-watchdog.sh" \
    "${LINUX_DIR}/happ-telegram-watchdog.env.example" \
    "${LINUX_DIR}/sugarguard-bot-happ-routing.conf" \
    "${LINUX_DIR}/sugarguard-happ-watchdog.service" \
    "${LINUX_DIR}/sugarguard-happ-watchdog.timer"; do
    [[ -f "${file}" ]] || {
        echo "Не найден обязательный файл: ${file}" >&2
        exit 1
    }
done

install -d -m 0750 "${TARGET_DIR}" "${CONFIG_DIR}" /var/lib/sugarguard-happ-watchdog
install -m 0750 "${LINUX_DIR}/happ-telegram-watchdog.sh" "${TARGET_DIR}/happ-telegram-watchdog.sh"
install -m 0644 "${LINUX_DIR}/sugarguard-happ-watchdog.service" /etc/systemd/system/sugarguard-happ-watchdog.service
install -m 0644 "${LINUX_DIR}/sugarguard-happ-watchdog.timer" /etc/systemd/system/sugarguard-happ-watchdog.timer

# Канонический сервис бота использует proxy только для Telegram. СахарGuard API
# и heartbeat должны оставаться доступны без VPN, иначе интерфейсы не узнают о
# сбое Happ. Drop-in не содержит секретов и безопасно применяется повторно.
if systemctl cat sugarguard-bot.service >/dev/null 2>&1; then
    install -d -m 0755 /etc/systemd/system/sugarguard-bot.service.d
    install -m 0644 "${LINUX_DIR}/sugarguard-bot-happ-routing.conf" \
        /etc/systemd/system/sugarguard-bot.service.d/happ-routing.conf
fi

if [[ ! -f "${CONFIG_DIR}/happ-telegram-watchdog.env" ]]; then
    install -m 0640 "${LINUX_DIR}/happ-telegram-watchdog.env.example" "${CONFIG_DIR}/happ-telegram-watchdog.env"
fi

systemctl daemon-reload

# В ранних установках встречался второй экземпляр того же long-polling бота.
# При наличии канонического сервиса он конкурирует за getUpdates и порождает
# ложные ошибки. Останавливаем только legacy-unit, не удаляя его конфигурацию:
# действие полностью обратимо через systemctl enable --now.
if systemctl cat sugarguard-bot.service >/dev/null 2>&1 \
    && systemctl cat sugarguard-telegram-bot.service >/dev/null 2>&1; then
    systemctl disable --now sugarguard-telegram-bot.service || true
fi

if systemctl is-active --quiet sugarguard-bot.service; then
    systemctl restart sugarguard-bot.service
fi

if ! grep -Eq '^HAPP_PROXY_URL=(http|https|socks5|socks5h)://(127\\.0\\.0\\.1|localhost|\\[::1\\]):[1-9][0-9]{0,4}$' "${CONFIG_DIR}/happ-telegram-watchdog.env" \
    || ! grep -Eq '^HAPP_SERVICE=[A-Za-z0-9_.@:-]+\\.service$' "${CONFIG_DIR}/happ-telegram-watchdog.env"; then
    echo "Создана конфигурация ${CONFIG_DIR}/happ-telegram-watchdog.env."
    echo "Заполните HAPP_PROXY_URL и HAPP_SERVICE фактическими значениями, затем выполните:"
    echo "  sudo systemctl enable --now sugarguard-happ-watchdog.timer"
    exit 0
fi

systemctl enable --now sugarguard-happ-watchdog.timer
systemctl start sugarguard-happ-watchdog.service || true

echo "Watchdog установлен. Проверьте:"
echo "  sudo systemctl status sugarguard-happ-watchdog.timer"
echo "  sudo journalctl -u sugarguard-happ-watchdog.service -n 50 --no-pager"
