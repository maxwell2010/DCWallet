# Проверка релиза 0.2.0 - 29.10.2026

## Локальные и сетевые результаты

- 347 Core + 38 Avalonia Headless тестов прошли без ошибок и предупреждений. 23 отдельных opt-in теста не включаются в обычный CI.
- Два readonly теста повторно сверили 29 прежних testnet-транзакций и итоговое состояние через публичный RPC сети 202020. Новых транзакций при выпуске не отправлялось.
- 29 канонических квитанций, 29 записей журнала, pending 0. Для двух Safe исполнений пересчитаны hash и внутренние события успеха. Суммарная фактическая комиссия прежних транзакций - 5.743185923475 tDEL.
- NFT вернулся владельцу; заявки 230/231 завершены. Стейк и разрешение резервного токена равны нулю, прежний баланс восстановлен. Safe nonce 2, баланс Safe 0. Баланс токена чеков восстановлен.
- Аудит всех пяти проектов, включая транзитивные зависимости, не выявил известных NuGet-уязвимостей на дату проверки. Newtonsoft.Json закреплен на 13.0.3, Tmds.DBus.Protocol на 0.21.3. Снимок SDK из 43 файлов сохранил исходные SHA-256; версии зависимостей переопределены конфигурацией сборки.

## Исправления после alpha

Очищение сессионных кэшей при блокировке и смене RPC, атомарная запись журнала с flush до отправки, stable-канал updater, исправленные зависимости, CFBundleVersion macOS и метаданные Linux. Каждое изменение поведения покрыто регрессионной проверкой.

Нативная проверка итогового коммита выполняется отдельно на шести платформах. Издатель фиксирует успешный run ID и SHA в описании релиза. Этот отчет не заменяет результат Actions и не является независимым аудитом безопасности. Мост исключен; mainnet NFT-пауза принята и проверяется приложением.

## Повторно сверенные testnet-квитанции

| Хэш | Блок | Комиссия tDEL | Сценарий |
| --- | --- | --- | --- |
| `0xf627bb76aa00c9c4b8ce0fed6ed03a090626ecd8e0fd32934207eac7d6021326` | 22475619 | 0.076751113725 | checks QA token exact one raw approval |
| `0xd670470a9c382456cd9102e7b9a99ecf5a42de07bfa1ad8388615b912c649518` | 22475622 | 0.3026282688 | checks QA token create one raw allowance |
| `0x985811e009b419afd2294d6f0d5eca4388bf3d0bcd325b19c1f0a1b317fc8b0a` | 22475624 | 0.10636417935 | checks QA token redeem one raw |
| `0x2b52e1b28d36374019cc34a2eff63c0a78a261bf2e9c2e6399f2184bafb726e5` | 22475628 | 0.370474629525 | checks QA token create one raw permit |
| `0xac4d1aa0aa846de9fbc8dd9cdd044c7bedb6cc0f215349c37f95eccaf338f648` | 22475630 | 0.10634989365 | checks QA token redeem one raw |
| `0xe751f7e534c78d75373bf7e45b5c87c3a8fff66510f95e0aed8603a97d0e24c0` | 22475641 | 0.2771187705 | Safe QA signature owner deployment |
| `0x27d998037429f49d37df3487650650700b81028650b72e3ce4baa48a464c2bc0` | 22475697 | 0.188689097025 | Safe QA no-storage delegate target deployment |
| `0x25fa8cf4cfdc1d1fc58d220284c27deaadd8f2932720aabc0e5295c155b670e5` | 22475699 | 0.2976330357 | Safe create contract-owner threshold1 |
| `0x1e27e065d43beebfb646111d8feac22dc7073c2a870991e72ab4b45f9f9cbb13` | 22475701 | 0.03251663415 | Safe contract-owner fund two wei |
| `0x090226f4f91aa6c71e25869bc72847bb79443b14eee663720f2db44b8134da4d` | 22475730 | 0.473237622 | NFT721 create disposable QA collection |
| `0x92d8f49d49008b5fdacb3eedef60c8ea297781d256dfb37c2dd696c15938f7a8` | 22475780 | 0.22596882165 | NFT721 mint one new QA NFT with 0.001 tDEL reserve |
| `0x911e56a6f5dc46f3a769ab88fc3fa198f5fbe5a3f2070718eb758df67ddbabf9` | 22475783 | 0.08062134795 | NFT721 approve only new QA token |
| `0xc56bdaeb1d107e33043da4af01a2cf7e6a93417a0e5875f4f4183cb877a667f3` | 22475819 | 0.55800182295 | NFT721 delegate only new QA token |
| `0x44f1cb6bb01fbb7b03cc7d6284eb267e2244014045f153c2a0e7a849f980722f` | 22475822 | 0.3785520024 | NFT721 withdraw only new QA position |
| `0x2cd3ccced5c025d0c30297c1dfc4db1e2b71c88304d563040330d1dcd42d28e7` | 22475825 | 0.079696348875 | ERC20 staking buy only new reserve-token delta .001 tDEL |
| `0x3b8644d6ed1d08794d15961ee150c6f5615e0922f41480976db50bd51e0ab5cf` | 22475829 | 0.396738888975 | ERC20 staking delegate purchase delta by permit |
| `0xaf168b8c236bd66bc05acab02641beba84af7156dbafa23a4adee939fe711bd2` | 22475832 | 0.336288949425 | ERC20 staking withdraw only new purchase delta |
| `0xaca3ca69b48ac7f7897d845c15a9441dcc06e20c7b9da104856e0c55ef0594bb` | 22475845 | 0.079608253725 | convert buy only new QA amount of reserve A |
| `0xb21b388645c91e65be0f4106f8b15c387bf42296ad1ff9ab0fd227b7baf11e24` | 22475847 | 0.0732427839 | convert approve exact newly bought A |
| `0x44103cb4ff0f5af77bbad1c78186d2d604fc51f0f7c861a9b52bf4b5dae94036` | 22475850 | 0.2129712156 | reserve A to B actual convert exact allowance |
| `0x9333662d5cfd7812dd53d8e66d1d143db78c16e9e7cd927bf797f7bb515e728d` | 22475853 | 0.086427294525 | convert sell only newly received reserve B back to DEL |
| `0x8ea6a54caadd3d238a4bede4659d0c08a46b058a20fd13d8df86ac8654c06173` | 22475908 | 0.110760603525 | Safe validated contract-owner Call one wei |
| `0x47c0b40d8f7de58c80645e8dafb81fd1d2700458f7a339515612a03ef0605254` | 22475911 | 0.096962998275 | Safe validated contract-owner DelegateCall one wei |
| `0xf43c8c495dcc852580b7a8c758fd9595300b3a41419bb3045d47f1adb220f17c` | 22475947 | 0.079974920025 | convert buy only new QA amount of reserve A |
| `0x1e5ab5d4d792888484533c5f6f9d965ef9c2bb5944a9b72149462ea4912f1b6f` | 22475951 | 0.260874739125 | reserve A to B actual convert permit |
| `0xe554b684a16eebdc008285288790944341695d913d837bea19dfa82b91bf17be` | 22475954 | 0.086543961075 | convert sell only newly received reserve B back to DEL |
| `0x33b5a92760c2e9e4139e9e75457604fc76c1aba2eca57f02d526a253d32afcd8` | 22475958 | 0.13210224885 | ERC20 staking complete only new purchase delta |
| `0xa0c983609c653e48deb846562782603e27400ac59687b425ea12e3ab15d60da3` | 22476014 | 0.0858951522 | ERC20 staking sell only restored purchase delta |
| `0x51d928c59d18d256658e04af0d0293139786b5ab45c507166df9381679e24dc7` | 22476042 | 0.150190326 | NFT721 complete only new QA withdrawal |
