# Постоянные ссылки на релизы

## Стабильный канал

Основная страница - [последний стабильный релиз](https://github.com/maxwell2010/DCWallet/releases/latest). База загрузок: `https://github.com/maxwell2010/DCWallet/releases/latest/download/`. Имена повторяются в каждой стабильной версии, поэтому ссылки в README не требуют изменения номера версии.

| Платформа | Постоянное имя |
| --- | --- |
| Windows x64 | `DCWallet-win-x64.exe` |
| Windows ARM64 | `DCWallet-win-arm64.exe` |
| Linux x64 | `DCWallet-linux-x64` |
| Linux ARM64 | `DCWallet-linux-arm64` |
| Linux x64 AppImage | `DCWallet-x86_64.AppImage` |
| Debian/Ubuntu x64 | `dc-wallet_amd64.deb` |
| Debian/Ubuntu ARM64 | `dc-wallet_arm64.deb` |
| Linux x64 архив | `DCWallet-linux-x64.tar.gz` |
| Linux ARM64 архив | `DCWallet-linux-arm64.tar.gz` |
| macOS Intel приложение | `DCWallet-osx-x64.app.zip` |
| macOS Apple Silicon приложение | `DCWallet-osx-arm64.app.zip` |
| macOS Intel бинарный файл | `DCWallet-osx-x64` |
| macOS Apple Silicon бинарный файл | `DCWallet-osx-arm64` |
| Контрольные суммы | `SHA256SUMS.txt` |
| Версия и соответствие файлов | `DOWNLOADS.json` |

`tools/prepare-release-assets.ps1` требует все шесть бинарных файлов и, с `-RequirePackages`, все семь пакетов. Каждый релиз содержит 13 файлов с версией, 13 копий с постоянными именами, DOWNLOADS.json и SHA256SUMS.txt. Описание публикации содержит исходный run ID и commit SHA. SHA-256 проверяется до открытия релиза.

## Версии и совместимость alpha

- `v0.2.0` - неизменяемый публичный стабильный релиз, помечаемый Latest после успешного нативного CI. Для фиксированной версии используйте `/releases/download/v0.2.0/ИМЯ_ФАЙЛА`.
- `build/0.2.0` сохраняет тот же проверенный исходный коммит.
- `alpha` остается обновляемым prerelease-зеркалом для уже установленных alpha-клиентов. После публикации 0.2.0 зеркало содержит те же проверенные файлы и заголовок `DC WALLET 0.2.0`, чтобы старый updater предложил переход на стабильную версию.
- Постоянные ссылки `/releases/download/alpha/...` остаются рабочими. Для новых пользователей основным является стабильный канал `/releases/latest/download/...`.
- Новый клиент проверяет стабильный канал. При отсутствии новой версии или недоступности GitHub уведомлений нет.

Для локальной подготовки: `pwsh -File tools/prepare-release-assets.ps1 -Version 0.2.0 -RequirePackages`. Команда проверяет файлы, но не публикует их.

Подход MinterWallet разобран в [MINTER_RELEASE_REVIEW.md](MINTER_RELEASE_REVIEW.md). Старые отчеты alpha сохраняют объем и дату своих проверок.
