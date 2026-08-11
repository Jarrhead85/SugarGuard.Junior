# Аудит безопасности SugarGuard — 11.08.2026

## Модель угроз

Критичные активы: медицинские данные детей, текущая глюкоза и координаты SOS,
семейные и врачебные связи, учётные данные, refresh-токены, ключи шифрования,
резервные копии и доступ к production-серверу. Наиболее опасные сценарии:
получение данных другого ребёнка (BOLA/IDOR), подмена измерения CGM, захват
учётной записи, обращение сервера во внутреннюю сеть (SSRF), подмена production-
кода локальным пользователем и потеря данных без резервной копии.

## Результаты

| № | Проблема | Компонент | Критичность | Риск | Что было исправлено | Проверка исправления | Статус |
|---:|---|---|---|---|---|---|---|
| 1 | Права 777/666 на production-релизах | VDS | Высокий | Локальная подмена API/Web от имени сервисного пользователя | Владелец `root:sugarguard`, каталоги 0750, файлы 0640, executable 0750; deploy-скрипт нормализует права | Сервисный пользователь не может писать; API/Web активны, HTTP-проверки успешны | **Исправлено и проверено** |
| 2 | World-writable бинарник маршрутизации root-сервиса | `happd` | Высокий | Локальное повышение привилегий через замену исполняемого файла | `/opt/happ/bin/core/routing` и дерево `/opt/happ` очищены от group/world-write | Повторный поиск writable-файлов — 0; `happd` активен | **Исправлено и проверено** |
| 3 | SSH private keys и logcat с PII внутри workspace | DevSecOps | Высокий | Компрометация VDS и утечка локальных данных | Новая пара ключей, проверенный отдельный вход, старые ключи отозваны; копии и logcat перемещены в корзину; production SSH pinning | Старые fingerprints отсутствуют в `authorized_keys`; новый вход и `sudo -n` успешны; workspace чист | **Исправлено и проверено** |
| 4 | `StrictHostKeyChecking=accept-new` | Deploy | Средний | MITM при первом подключении | `StrictHostKeyChecking=yes`, `IdentitiesOnly=yes` | Deploy-ключ работает с закреплённым host key | **Исправлено и проверено** |
| 5 | Небезопасные права резервных архивов deploy | Deploy/VDS | Высокий | Чтение конфигурации и секретов локальным пользователем | Каталог backup 0700, архивы 0600, `umask 077`; bot backup исключает appsettings | Повторная проверка прав: отклонений нет | **Исправлено и проверено** |
| 6 | Race/replay refresh-токена | API/Auth | Высокий | Два успешных обмена одного refresh-токена | Optimistic concurrency version и атомарная ротация; повторное использование возвращает 401 | Конкурентные SQLite-тесты прошли | **Исправлено и проверено** |
| 7 | JWT сохранял старые роль/доступ после деактивации | API/Auth | Средний | Доступ до истечения токена после смены роли/пароля | `SecurityVersion`, проверка active/role/version в `OnTokenValidated` | Интеграционные authz-тесты прошли | **Исправлено и проверено** |
| 8 | Перерегистрация меняла пароль unverified-аккаунта | API/Auth | Высокий | Pre-account takeover после ввода жертвой кода | Повторная регистрация не меняет credentials/роль | Unit-тесты регистрации прошли | **Исправлено и проверено** |
| 9 | Account enumeration и timing | API/Auth | Низкий | Определение существующих email | Dummy password verification и одинаковый публичный ответ регистрации | Auth-тесты прошли | **Исправлено и проверено** |
| 10 | Подтверждение email без проверки proof внутри сервиса | API/Auth | Низкий | Случайный обход при новом внутреннем caller | Proof проверяется сервисом; demo bypass разрешён только Development + явный флаг | Новый negative-тест подтверждает, что аккаунт не активируется | **Исправлено и проверено** |
| 11 | ServiceAccount имел общие пользовательские/админские права | API/AuthZ | Высокий | Массовый доступ бота к детям и PHI | Удалён из generic policy/admin bypass; оставлены отдельные bot endpoints | Интеграционные role-тесты прошли | **Исправлено и проверено** |
| 12 | BOLA в idempotency измерений | API/Measurements | Высокий | Чтение записи другого ребёнка по MeasurementId | Поиск всегда scoped по `ChildId`; foreign collision отклоняется | Unit/integration-тесты прошли | **Исправлено и проверено** |
| 13 | Cross-child MeasurementId в AI workflow | API/AI | Высокий | Чтение/изменение чужого измерения | Все lookups scoped по ребёнку | AI и measurement-тесты прошли | **Исправлено и проверено** |
| 14 | Mutation-before-authorization SyncLog | API/Sync | Средний | Изменение чужого sync-конфликта до 403 | Scope доступа применяется до mutation/query | Тесты API прошли | **Исправлено и проверено** |
| 15 | Blind SSRF через Web Push endpoint/redirect | API/WebPush | Высокий | POST к loopback, metadata или внутренним сервисам | HTTPS:443 allowlist официальных push-хостов, без userinfo; redirects отключены, connect/request timeout | Negative URL-тесты и API build прошли | **Исправлено и проверено** |
| 16 | BOLA при захвате чужой push-подписки | API/WebPush | Высокий | Отключение уведомлений другого пользователя | Owner-scoped upsert/delete, конфликт владельца и лимит 10 | Repository/service тестовая сборка прошла | **Исправлено и проверено** |
| 17 | Path traversal/arbitrary delete через `PhotoUrl` | API/Children | Высокий | Удаление чужих uploads/static assets | Клиентский PhotoUrl игнорируется; строгий путь `/uploads/children/{childId}/{guid}.{ext}` и separator-aware containment | Security edge-case tests прошли | **Исправлено и проверено** |
| 18 | Небезопасные изображения ребёнка | API/Uploads | Средний | Active content/polyglot и публичная раздача | Magic signature, MIME/extension match, allowlist JPG/PNG/WebP, атомарная запись, 5 МБ, security headers до static files | Upload-тесты прошли | **Исправлено и проверено** |
| 19 | Подделка/spam медицинских уведомлений | API/Notifications | Средний | Ложный SOS/глюкоза/перекус | ChildSafetyEvent только ChildDevice/Patient, серверная авторизация связи, диапазоны DTO, 6 событий/мин | Authz и DTO-тесты прошли | **Исправлено и проверено** |
| 20 | Global AI limiter позволял DoS другим пользователям | API/AI | Средний | Один пользователь расходовал общий bucket | Partition по user/IP + системный guard | Тесты API прошли | **Исправлено и проверено** |
| 21 | CSV formula injection | Exports | Средний | Выполнение формул при открытии Excel | Neutralization `= + - @` после ведущих пробелов во всех CSV путях | Boundary-тесты экспорта прошли | **Исправлено и проверено** |
| 22 | Support/FAQ upload abuse | API/Uploads | Средний | Disk/mail DoS и произвольные вложения | Per-user rate limits, 2/5 МБ, magic allowlist JPG/PNG/WebP, canonical имя/MIME | API tests/build прошли | **Исправлено и проверено** |
| 23 | PHI/PII и push endpoint в логах | API/Mobile/Bot | Средний | Утечка глюкозы, email, имён, перекусов и идентификаторов | Удалены точные медицинские значения, email, имена, snacks, endpoints и Telegram IDs из основных flows; публичные ошибки стабилизированы | Повторный focused grep и 571 тест | **Исправлено и проверено** |
| 24 | Подмена CGM broadcast сторонним Android-приложением | Mobile/CGM | Высокий | Поддельная глюкоза влияет на SOS и совет по еде | Broadcast помечен untrusted; не входит в medical flow до локального подтверждения, имеет 15-минутный срок; UI confirm/reject | Trust policy negative tests и Android build прошли | **Исправлено и проверено** |
| 25 | Fail-open локального шифрования и смена ключа при миграции | Mobile/Crypto | Высокий | Подмена plaintext и потеря старых локальных данных | Legacy key переносится в versioned slot; invalid key не заменяется; unprefixed value расшифровывается только как legacy CBC и fail-closed | Android build и полный тестовый набор прошли | **Исправлено и проверено** |
| 26 | Уязвимые NuGet transitive dependencies | Supply chain | Высокий | Memory corruption/DoS и известные CVE | EF/.NET packages 9.0.18, SQLitePCLRaw 2.1.12, LocalNotification 12.0.2; удалена старая FsCheck-цепочка | `dotnet list ... --vulnerable --include-transitive`: 0 по 10 проектам | **Исправлено и проверено** |
| 27 | Избыточные write-права врача | API/AuthZ | Средний | Врач менял профиль/рюкзак вместо read-only | Отдельная ChildDataWrite policy без Doctor применена к Children/Backpack mutations | Role tests прошли | **Исправлено и проверено** |
| 28 | Нет регулярного backup/PITR | PostgreSQL/VDS | Высокий | Потеря всех медицинских данных при аварии/ошибке | Создана и проверена разовая audit-копия; нужен offsite backup, retention и restore drill | `archive_mode=off`, расписание не найдено | **Требует решения владельца** |
| 29 | Runtime DB role владеет БД/схемой/таблицами; RLS отсутствует | PostgreSQL | Высокий | Компрометация API даёт DDL/полный DB impact | Подготовлен план separate migration owner + runtime grants + optional RLS | Без controlled rollout менять нельзя | **Требует решения владельца** |
| 30 | APK подписан Android Debug key | Mobile release | Высокий | Слабая цепочка обновлений и риск невозможности upgrade | Подготовлен переход на offline production keystore/CI secret | Нужен v3 proof-of-rotation либо согласованная переустановка и `adb install -r` test | **Требует решения владельца** |
| 31 | Docker 29.5.3 ниже security-fixed releases | VDS | Средний | Известные уязвимости daemon; сейчас контейнеров нет | Обновление не выполнялось без окна обслуживания | Нужен controlled update ≥29.7 и rollback check | **Требует решения владельца** |
| 32 | Обновления ОС и reboot required | VDS | Средний | Patch lag/kernel не активирован | Security-кандидатов на момент проверки нет | 19 updates и reboot требуют окна | **Требует решения владельца** |
| 33 | `happd` остаётся unconfined с широкой capability set | VDS | Высокий | Root-impact при компрометации закрытого бинарника | Writable binary устранён; unit hardening не применялся | Нужны provenance пакета и staged systemd sandbox test | **Требует решения владельца** |
| 34 | .NET 9 заканчивает поддержку 10.11.2026 | Platform | Средний | Отсутствие будущих security fixes | Packages подняты до 9.0.18 | Нужна совместимая миграция на .NET 10 LTS | **Требует решения владельца** |
| 35 | SkiaSharp 2.88.9 не готов к Android 16 16KB pages | Mobile | Средний | Будущий запуск/публикация на Android 16 | Не обновлялся из-за риска UI/ABI-несовместимости | Android build выдаёт XA0141; нужен отдельный upgrade-test | **Требует решения владельца** |
| 36 | AV/CDR и lifecycle orphan uploads отсутствуют | Uploads | Средний | Сохранение вредоносного polyglot/рост диска | Magic/MIME/rate/size уже включены | Нужен выбранный AV/CDR и retention policy | **Требует решения владельца** |
| 37 | SQL injection | API/EF | — | — | Raw SQL проверен: параметры/константные migration SQL | Эксплуатационный путь не найден | **Не удалось подтвердить** |
| 38 | FAQ markdown XSS | Web | — | — | HTML encoded, локальные изображения ограничены | Unsafe render path не найден | **Ложное срабатывание** |
| 39 | CSRF API | API | — | — | Bearer API; refresh-cookie Strict и CORS constraints | State-changing cookie-auth path не найден | **Не удалось подтвердить** |

## Проверки

- `dotnet test SugarGuard.Tests/SugarGuard.Tests.csproj --no-restore`: **571/571**.
- API build: **0 warnings, 0 errors**.
- Android build: **0 errors**, два предупреждения совместимости Android 16/SkiaSharp.
- NuGet vulnerability audit всех проектов: **0 известных уязвимых пакетов**.
- EF migration SQL проверен: добавляет `security_version` и корректный filtered unique index.
- Production после инфраструктурных изменений: API live/ready 200, Web 200, APK range 206,
  protected endpoint 401 без корректного токена; systemd units активны.

## Резервные копии и откат

- Root-only bundle: `/root/security-backups/20260810T165732Z` (0700), файлы 0600,
  SHA-256 manifest, config archive, ACL/stat manifests и PostgreSQL custom dump.
- В каталоге есть состояния `authorized_keys` до/после ротации и ACL/stat/архив
  `happ` до исправления.
- Deploy backups: `/opt/sugarguard/backups` (0700), архивы 0600, retention последних трёх.
- Откат релиза выполняется deploy-скриптом атомарной заменой `.old`; при неуспешном
  запуске прежний каталог возвращается и service стартует повторно.

## Требуемые решения владельца

1. Согласовать окно production-деплоя кода безопасности (краткая остановка API/Web).
2. Выбрать RPO/RTO и offsite-хранилище PostgreSQL backup; после этого провести restore drill.
3. Согласовать production signing key и стратегию Android key rotation.
4. Согласовать окна Docker/OS/.NET/SkiaSharp обновлений.
5. Определить судьбу и происхождение `happd`, затем испытать systemd sandbox.
6. Выбрать AV/CDR и retention/quota policy для пользовательских изображений.
