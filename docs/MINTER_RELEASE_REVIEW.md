# MinterWallet: архитектура релизов

Повторная проверка 29.10.2026: актуальные main/release.yml и README по-прежнему используют Windows/Linux jobs, versioned assets и страницу `/releases/latest`. Прямых файлов с постоянными именами в просмотренном workflow нет. Для DC WALLET дополнительно реализованы aliases без версии, манифест и SHA256SUMS: [RELEASE_DOWNLOADS.md](RELEASE_DOWNLOADS.md). Ниже сохранен исходный разбор зафиксированного коммита.

Изучен репозиторий https://github.com/xDilettante/MinterWallet на коммите `d52034111eb4df5d63c4b8a38befc92ad5b140ab`.

Первичные источники:

- [release.yml](https://github.com/xDilettante/MinterWallet/blob/d52034111eb4df5d63c4b8a38befc92ad5b140ab/.github/workflows/release.yml)
- [проект приложения](https://github.com/xDilettante/MinterWallet/blob/d52034111eb4df5d63c4b8a38befc92ad5b140ab/src/MinterWallet/MinterWallet.csproj)
- [Linux packaging](https://github.com/xDilettante/MinterWallet/blob/d52034111eb4df5d63c4b8a38befc92ad5b140ab/tools/package-linux.sh)
- [check.yml](https://github.com/xDilettante/MinterWallet/blob/d52034111eb4df5d63c4b8a38befc92ad5b140ab/.github/workflows/check.yml)

1. Тег `v*` фиксирует версию и запускает отдельные Windows/Linux jobs.
2. `dotnet publish -c Release -r win-x64/linux-x64 -p:PublishSingleFile=true` собирает приложение.
3. В single-file режиме проект включает SelfContained, упаковку native libraries, сжатие и trimming; символы исключаются.
4. Каждая сборка выполняет `--self-test`.
5. Linux-скрипт формирует отдельный бинарный файл, AppImage и `.deb`.
6. Общий job забирает артефакты, формирует SHA256SUMS.txt и публикует GitHub Release через gh.
7. Отдельный check workflow запускает Windows-проверки и Linux-проверки с Minter devnet и headless UI.

В DC WALLET этот подход адаптирован к Decimal SDK: собственный проект Avalonia, self-contained публикация, шесть RID и draft prerelease, SHA-256 и headless-проверки. Текущее целевое окружение - .NET 8/Avalonia 11.3.9, поскольку используемый C# SDK и установленный локальный toolchain основаны на net8.0. MinterWallet использует .NET 10/Avalonia 12.1.2.

Для каждого RID в release workflow выбран runner соответствующей архитектуры: windows-latest/windows-11-arm, ubuntu-22.04/ubuntu-22.04-arm, macos-15/macos-15-intel. Доступность labels сверена с [документацией GitHub](https://docs.github.com/en/actions/how-tos/write-workflows/choose-where-workflows-run/choose-the-runner-for-a-job). После подключения репозитория это позволит выполнить self-test каждой сборки нативно; пока это конфигурация, не результат CI.

Trimming отключен из-за reflection-каталога и ABI сериализации. Форматы Linux: `.deb`/`.tar.gz`, AppImage для x64 через upstream appimagetool 1.9.0; macOS `.app.zip`. Скрипты упаковки подготовлены, но нативно не выполнялись в Windows-среде. Подписи не добавлены. Minter API, Hub, типы транзакций, gas-token и правила моста не переносятся в Decimal.
