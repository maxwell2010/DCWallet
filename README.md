# DC WALLET

[![Native checks](https://github.com/maxwell2010/DCWallet/actions/workflows/check.yml/badge.svg)](https://github.com/maxwell2010/DCWallet/actions/workflows/check.yml)

Нативные сборки и результаты проверок: [GitHub Actions](https://github.com/maxwell2010/DCWallet/actions/workflows/check.yml). Проект публикуется как alpha. Мост исключен; mainnet NFT-делегирование сохраняет проверку паузы контракта.

## Скачать DC WALLET

Канал [alpha](https://github.com/maxwell2010/DCWallet/releases/tag/alpha) содержит проверенные сборки. Прямые ссылки сохраняются при обновлении версии; файлы появляются после успешного завершения проверки и публикации alpha.

| Платформа | Скачать |
| --- | --- |
| Windows x64 | [Приложение .exe](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-win-x64.exe) |
| Windows ARM64 | [Приложение .exe](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-win-arm64.exe) |
| Linux x64 | [AppImage](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-x86_64.AppImage), [.deb](https://github.com/maxwell2010/DCWallet/releases/download/alpha/dc-wallet_amd64.deb), [архив .tar.gz](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-linux-x64.tar.gz) |
| Linux ARM64 | [.deb](https://github.com/maxwell2010/DCWallet/releases/download/alpha/dc-wallet_arm64.deb), [архив .tar.gz](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-linux-arm64.tar.gz) |
| macOS Intel | [Приложение .app.zip](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-osx-x64.app.zip) |
| macOS Apple Silicon | [Приложение .app.zip](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DCWallet-osx-arm64.app.zip) |

[Контрольные суммы SHA-256](https://github.com/maxwell2010/DCWallet/releases/download/alpha/SHA256SUMS.txt) и [манифест файлов и версии](https://github.com/maxwell2010/DCWallet/releases/download/alpha/DOWNLOADS.json). Сборки пока без сертификатов подписи; macOS без notarization. Alpha не прошла независимый аудит безопасности.

Нативный desktop-кошелек Decimal на C#, Avalonia и SDK из чата C SDK. Графика и геометрия иконок взяты из существующего DC WALLET. Отдельный проект не изменяет мобильный кошелек и исходный SDK.

Текущий результат - **alpha**. В приложении каталог содержит 118 методов подготовки транзакций текущего C# SDK, включая перегрузки. Неподдерживаемый mint резервного токена скрыт из выбора. Наличие метода не означает проверку соответствующего контракта или полный сценарий работы. Подробности: [возможности](docs/CAPABILITIES.md), [проверки SDK](docs/SDK_VALIDATION.md), [пробелы SDK](docs/SDK_COMPATIBILITY_BACKLOG.md).

## Запуск и сборка

Требуется .NET SDK 8 для сборки. Готовые self-contained файлы содержат .NET runtime.

```powershell
dotnet run --project src/DCWallet
dotnet test DCWallet.sln -c Release
./tools/publish.ps1 -Runtime win-x64 -Version 0.2.0-alpha.3
```

Проверьте `dotnet --info`: для сборки нужен .NET SDK8. Готовые self-contained файлы не требуют установки SDK.

Сборки сохраняются в `artifacts/releases`. Команда `DCWallet --self-test` выполняет локальную проверку вывода адреса, точности сумм и наличия каталога SDK. Она не отправляет RPC-запросы и не заменяет тестирование приложения.

## Возможности alpha

- Пошаговое создание 12 или 24 слов BIP39, необязательная проверка записи, восстановление seed-фразы и зашифрованной копии, импорт приватного ключа и независимые профили.
- Три темы: светлая, темная и «Криптопанк» на основе Neon из MintCandy. Есть режим «Как в системе» с отслеживанием настроек ОС и сохранение выбора. Нативные карточки и навигация по предоставленным референсам MinterWallet с графикой DC WALLET.
- AES-256-GCM, PBKDF2-SHA256 с 600000 итераций, случайные соль и nonce. Существующее хранилище не перезаписывается.
- Десять адресов seed-профиля `m/44'/60'/0'/0/index`, один адрес приватного ключа, mainnet/testnet, автоблокировка через 5 минут.
- Автозагрузка DEL, ERC20, ERC721/1155 и стейков, RPC-проверка балансов и владения на закрепленном блоке, обновление раз в минуту. Поиск NFT и страницы по 48 карточек для больших коллекций.
- DEL с комментарием, ERC20 с чтением decimals из контракта, точные суммы через BigInteger. Рассылка DEL/ERC20, импорт/экспорт CSV и XLSX с точными суммами, точные разрешения и EIP-2612 permit.
- Получение: адрес, копирование, QR.
- Режим просмотра адреса без ключа, названия счетов, адресная книга, смена пароля с проверкой нового зашифрованного файла.
- Покупка/продажа и обмен токен → токен с on-chain котировкой, проскальзыванием; делегирование DEL/ERC20/NFT с hold, управление активными стейками и завершение отзыва. Списки токенов и валидаторов используют каталоги Console MintCandy и ручной ввод.
- Decimal Safe: проверенные mainnet/testnet фабрики, взвешенные владельцы, предложения, EOA/on-chain/contract-подписи, Call/DelegateCall, проверка хэша и симуляция. Успех требует правильного события внутреннего исполнения.
- Настраиваемая автоблокировка, предел gas price и подключение RPC с проверкой chain ID; опциональное сворачивание/закрытие в трей.
- Однократная проверка новой alpha при запуске. Если версия новее, небольшое окно показывает текущую и доступную версии; кнопка «Обновить» открывает загрузку для вашей платформы. При актуальной версии или отсутствии сети уведомление не появляется. Приложение не заменяет свои файлы автоматически.
- Формы подготовки методов SDK: токены, обмен, NFT, стейкинг, валидаторы, чеки, мост, Safe execution и мультисенд. Сложные структуры и permit-подписи требуют параметров от пользователя.
- Расчет комиссии, локальная подпись, просмотр calldata, nonce и хэша, отдельное подтверждение отправки. Подготовка истекает через 2 минуты.
- Журнал хэшей до отправки. После ошибки RPC повторная отправка автоматически не выполняется.
- История mainnet по кандидатам официального индексатора с проверкой транзакции, квитанции и канонического блока через RPC. DEL и операции с контрактами показаны отдельно; базовые движения ERC20/ERC721/ERC1155 декодируются, индексаторные и RPC-проверенные записи различаются.
- Проверка заявки разморозки по номеру непосредственно в контракте.

Текущий функциональный отчет: [VALIDATION-COMPLETION.md](docs/VALIDATION-COMPLETION.md). Независимые ABI-срезы всех семейств и подтвержденные runtime-адаптеры перечислены в [CAPABILITIES.md](docs/CAPABILITIES.md).

## Релизы

Подход MinterWallet изучен по коммиту `d52034111eb4df5d63c4b8a38befc92ad5b140ab`: [разбор](docs/MINTER_RELEASE_REVIEW.md). Мы используем отдельную реализацию и C# SDK Decimal.

GitHub Actions содержит матрицу Windows/Linux/macOS, x64/ARM64. Публикация канала `alpha` получает готовые файлы из успешного прогона Native checks для указанного коммита. Она требует все шесть платформ и проверяет SHA-256 файлов. Тег `v*` по-прежнему создает отдельный **черновик prerelease**. Актуальный результат нативных проверок виден по badge и ссылке выше. Шаги описаны в [NATIVE-CI.md](docs/NATIVE-CI.md).

`PublishTrimmed=false`: в каталоге используется reflection, а Nethereum сериализует ABI-типы. Обрезка может удалить требуемые методы. macOS-пакеты пока без подписи и notarization. Мобильные сборки не реализованы в этом desktop-проекте.

В alpha добавляются копии с постоянными именами без версии, `DOWNLOADS.json` с фактической версией и `SHA256SUMS.txt`. Используется постоянный адрес `/releases/download/alpha/ИМЯ_ФАЙЛА`. Исходный коммит версии сохраняется отдельным неизменяемым тегом `build/ВЕРСИЯ`. Канал alpha является prerelease, поэтому GitHub `/releases/latest` для него не используется. Таблица имен и правила каналов: [RELEASE_DOWNLOADS.md](docs/RELEASE_DOWNLOADS.md). Объем проверок и ограничения: [PUBLICATION_READINESS.md](docs/PUBLICATION_READINESS.md).

## Данные

Файл кошелька: `LocalApplicationData/MintCandy/DCWallet/wallet.vault`. Папка journal содержит только публичные метаданные транзакций. Seed-фраза и raw signed transaction в журнал не записываются. Кнопка экспорта сохраняет зашифрованную копию, для восстановления нужен пароль.

При запуске проверка обновления запрашивает публичные сведения об alpha-релизе GitHub. Адреса кошелька, seed-фразы, ключи и балансы в этот запрос не включаются.

Signer хранит ключ в памяти открытой сессии. Dispose не гарантирует стирание всех копий в managed heap или дампах ОС. Тестовая alpha не прошла независимый аудит безопасности.

Исходники SDK зафиксированы в `vendor/Decimal.Web3`, SHA-256 каждого файла приведен в `vendor/SOURCE.json`. Это снимок локальных изменений, а не опубликованный NuGet-релиз. Исходный проект SDK не модифицирован.
