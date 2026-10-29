# Постоянные ссылки на релизы

У MinterWallet тег `v*` запускает Windows/Linux сборки и self-test, упаковку Linux и публикацию файлов с версией и SHA256SUMS.txt. README ведет на [последний релиз](https://github.com/xDilettante/MinterWallet/releases/latest), а не на прямой файл с постоянным именем. Проверены текущие [workflow](https://github.com/xDilettante/MinterWallet/blob/main/.github/workflows/release.yml) и [README](https://github.com/xDilettante/MinterWallet/blob/main/README.md) 29.10.2026.

GitHub поддерживает постоянную страницу `/releases/latest` и прямой адрес `/releases/latest/download/ИМЯ_ФАЙЛА`: [документация](https://docs.github.com/en/repositories/releasing-projects-on-github/linking-to-releases). Для прямой ссылки имя файла должно повторяться в каждом новом релизе.

## Имена DC WALLET

База после выбора и подключения репозитория: `https://github.com/maxwell2010/DCWallet/releases/latest/download/`. Репозиторий проекта - maxwell2010/DCWallet; опубликованного stable release пока нет.

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

`tools/prepare-release-assets.ps1` проверяет наличие и размер всех шести бинарных файлов, в CI также всех семи пакетов. Он создает копии с постоянными именами и проверяет SHA-256, затем формирует манифест и общий список хэшей. Файлы с версией остаются в релизе. Сборка не может тихо пропустить одну платформу.

## Канал и публикация

- Текущий workflow создает draft prerelease после всех шести сборок, тестов и нативного self-test. Автоматической публичной публикации нет.
- Draft и prerelease исключены из GitHub `latest`. Пока нет опубликованного стабильного релиза, `/releases/latest/download/...` не является работающей ссылкой для alpha.
- Для опубликованной alpha используется `/releases/tag/vВЕРСИЯ` или общая страница `/releases`. Фиксированный prerelease-канал `preview` пока не настроен; не следует обещать для него работающие постоянные прямые ссылки.
- После завершения требований готовности и нативного CI сопровождающий проверяет draft, выбирает стабильный статус и публикует его как Latest. Только после этого постоянные прямые ссылки начинают работать. Следующие Latest-релизы обязаны сохранять имена из таблицы.
- Устаревший тег не перезаписывается. Для конкретной версии используются `/releases/download/vВЕРСИЯ/ИМЯ_С_ВЕРСИЕЙ`.

Для локальной подготовки уже собранных бинарных файлов: `pwsh -File tools/prepare-release-assets.ps1 -Version 0.2.0-alpha.2`. В CI используется `-RequirePackages`. Это подготовка файлов, а не публикация на GitHub.
