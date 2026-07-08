# SugarGuard Junior

SugarGuard Junior — система сопровождения ребёнка с сахарным диабетом. В проект входят мобильное приложение на .NET MAUI, ASP.NET Core API, Blazor web-кабинеты и Telegram-уведомления.

Продукт объединяет ребёнка, родителя, врача и администратора вокруг единой модели данных: измерения глюкозы, рюкзак с перекусами, дневник питания и инсулина, расписание приёмов пищи, напоминания, заметки врача, ачивки, экспорт и аудит.

## Состав решения

| Проект | Назначение |
|---|---|
| `SugarGuard.API` | ASP.NET Core Web API: авторизация, бизнес-сценарии, интеграции |
| `SugarGuard.Web` | Blazor Server: кабинеты родителя, врача и администратора |
| `SugarGuard.Junior` | .NET MAUI: мобильное приложение ребёнка |
| `SugarGuard.Domain` | Доменные сущности и правила |
| `SugarGuard.Application` | Контракты приложения и use cases |
| `SugarGuard.Infrastructure` | EF Core, репозитории и инфраструктурные сервисы |
| `SugarGuard.Shared` | DTO, валидация, константы и общие утилиты |
| `SugarGuard.Tests` | xUnit-тесты |

## Возможности

- Мобильное приложение ребёнка: ввод глюкозы, AI-рекомендации, рюкзак, дневник питания/инсулина, напоминания, ачивки, коды привязки родителей и врачей.
- Кабинет родителя: live-дашборд, измерения, уведомления, рюкзак, дневник питания, расписание, заметки врача, профиль ребёнка, управление доступом и экспорт.
- Кабинет врача: список пациентов, группы риска, карточки пациентов, графики глюкозы, клинические заметки и профиль врача.
- Кабинет администратора: пользователи, роли, верификация врачей, связи, аудит, журналы синхронизации/экспорта и состояние системы.
- Интеграции: SMTP email, Telegram Bot API, GigaChat, экспорт CSV/PDF.
- Безопасность: JWT + refresh-токены, RBAC, шифрование PHI-данных, HMAC для кодов подключения.

Все коды подтверждения, восстановления и привязки используют единый формат `ABCD-1234`.

## Требования

- .NET SDK 9
- PostgreSQL для production
- Android SDK и MAUI workload для сборки Android
- macOS + Xcode для сборки iOS
- SMTP-аккаунт, Telegram bot token и GigaChat credentials для интеграций

## Конфигурация

Секреты хранятся в переменных окружения, user-secrets или конфигурации сервера. Не коммитьте реальные ключи и пароли.

Основные ключи:

```text
ConnectionStrings__DefaultConnection
Jwt__Key
Jwt__Issuer
Jwt__Audience
CONNECTION_CODE_KEY
Smtp__Host
Smtp__Port
Smtp__Username
Smtp__Password
Smtp__FromEmail
Telegram__BotToken
GigaChat__AuthorizationKey
GigaChat__ClientId
GigaChat__ClientSecret
```

## Локальный запуск

Восстановить зависимости, собрать решение и запустить тесты:

```powershell
dotnet restore .\SugarGuard.Junior.slnx
dotnet build .\SugarGuard.Junior.slnx -v minimal
dotnet test .\SugarGuard.Tests\SugarGuard.Tests.csproj -v minimal
```

Запустить API:

```powershell
dotnet run --project .\SugarGuard.API\SugarGuard.API.csproj
```

Запустить Web:

```powershell
dotnet run --project .\SugarGuard.Web\SugarGuard.Web.csproj
```

Собрать Android-приложение:

```powershell
dotnet build .\SugarGuard.Junior\SugarGuard.Junior.csproj -f net9.0-android -v minimal
```

## Деплой

Для VDS используется скрипт:

```powershell
.\scripts\deploy-vds.ps1
```

API и Web разворачиваются как отдельные systemd-сервисы за HTTPS. Серверные секреты должны оставаться на сервере и не попадать в репозиторий.

## Документация

- [Описание проекта](./Описание-v3.md)
- [UI Kit](./UIKit-v2.md)

## Правила разработки

- Кабинеты родителя, врача и администратора должны использовать единый визуальный язык.
- Левая панель и верхние вкладки родителя должны совпадать по названиям и порядку.
- В production-интерфейсе используются только реальные данные, без демо-заглушек.
- Изменения доменных правил, авторизации, синхронизации и экспорта должны сопровождаться xUnit-тестами.
