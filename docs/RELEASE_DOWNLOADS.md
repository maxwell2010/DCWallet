# Постоянные ссылки на релизы

## Канал alpha

Публичный prerelease [DC WALLET alpha](https://github.com/maxwell2010/DCWallet/releases/tag/alpha) использует постоянный тег `alpha`. Адреса файлов сохраняются при обновлении версии. Публикация берет готовый артефакт `verified-platform-downloads` из успешного прогона Native checks для выбранного коммита. Пока проверка и публикация новой alpha не завершены, канал сохраняет предыдущую опубликованную сборку.

База прямых ссылок: `https://github.com/maxwell2010/DCWallet/releases/download/alpha/`.

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

`tools/prepare-release-assets.ps1` требует все шесть бинарных файлов и, с параметром `-RequirePackages`, все семь пакетов. Он создает постоянные имена и проверяет SHA-256, затем формирует манифест и общий список хэшей. В alpha входят 13 файлов с версией, 13 копий с постоянными именами, манифест и контрольные суммы. Исходный run ID и коммит приводятся в описании публикации для проверки происхождения файлов.

## Alpha, версии и GitHub latest

- Тег `alpha` является обновляемым каналом предварительных сборок. Для воспроизводимости сохраняйте номер версии, коммит, run ID и SHA-256 нужного файла. Постоянная alpha-ссылка означает актуальную alpha, а не неизменяемый архив старой версии.
- Исходный коммит каждой опубликованной версии фиксируется неизменяемым тегом `build/ВЕРСИЯ`, например `build/0.2.0-alpha.3`. Он позволяет найти проверенные исходники после обновления канала alpha.
- Теги `v*` запускают отдельный workflow, создающий draft prerelease. Само создание тега версии не публикует стабильный релиз.
- GitHub `/releases/latest` исключает draft и prerelease. Для текущей alpha используйте `/releases/tag/alpha` и `/releases/download/alpha/...`.
- После появления стабильного релиза можно использовать `/releases/latest/download/ИМЯ_ФАЙЛА`. Это отдельный канал, который должен сохранять имена из таблицы.

Для локальной подготовки файлов: `pwsh -File tools/prepare-release-assets.ps1 -Version 0.2.0-alpha.3 -RequirePackages`. Команда проверяет файлы, но сама не публикует их.

Подход MinterWallet разобран в [MINTER_RELEASE_REVIEW.md](MINTER_RELEASE_REVIEW.md). У него README ведет на страницу последнего релиза; постоянные имена прямых загрузок DC WALLET реализует отдельно.
