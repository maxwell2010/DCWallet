# Проверка кошелька в Decimal Testnet

Дата: 29.10.2026. Сеть: 202020, RPC: https://202020.rpc.thirdweb.com.

Изолированный QA-адрес: `0xb92bd490c8e9e5d9c46d71666418d4e7ae4534e9`. Все операции прошли через DCWallet.Core.TransferSession: расчет комиссии, подпись, запись локального хэша до отправки, одна попытка broadcast и проверка receipt.

Подтверждено 12 транзакций. Комиссии только этих транзакций: **1.191619046475 tDEL**. Потолок комиссии одной операции: 0.5 tDEL; общий бюджет прогона: 5 tDEL.

| Проверка | Хэш | Комиссия tDEL |
|---|---|---|
| DEL memo self | `0xa836f75ee22aad80871d2c58fdc0c111ec62a4e1c9ad4b7e69263f1d8c568f94` | 0.0253999746 |
| QA ERC20 self one raw | `0x48e1c64ed6722d2755bd8fe7377c10a1aaf7a4686ae2cecbd53edbcee20ebfee` | 0.0570428001 |
| QA ERC1155 self id1 amount1 | `0x2fbf5d3c588d328c40ca09b7297be144f1a48a51ea355417e56c9104b3d64fbe` | 0.0619094619 |
| QA mint one raw | `0xa14ef1d4cec05464caa8ca1deaf5dd44bcfb6e774ddfb5a9504d60fc268791b3` | 0.07042135815 |
| QA burn newly minted one raw | `0x118cd64332e6af1869ac1f46d90d91a646151408d999969c81c5ba812f24fbeb` | 0.06450231645 |
| reserve buy 0.001 DEL | `0xc26788ff311fd6880029df9cf71414a3b629096b35dd930a75cbb0a26f1f5c15` | 0.079696348875 |
| reserve approve exact new amount | `0x74c24777ede27e898a2f03755ed6f563eca99c9fedb891b5eb4b15328e4ac356` | 0.0732427839 |
| reserve sell only new amount | `0x0239707695cd9256dfc07c2db48beed48df4cbcf40685da26922f5224c51e245` | 0.0858284856 |
| delegate 0.001 DEL | `0x594f1395f720b21c4e9f8ec9bffaa218055c45f43ffb523605f6ed53f56c682a` | 0.1580284134 |
| withdraw only new 0.001 DEL stake | `0x13b8970a9a7fe689cbd89ab3782c0237bf4c8a92199d154d69d52ed736a2e3ae` | 0.336274663725 |
| revoke remaining test-only reserve allowance | `0xe749aeafd9bc658ce2737e8b2e05c634ea8f2913f8cb9e118875f1a93556504b` | 0.0527856615 |
| complete newly matured test-only withdrawal | `0xd6bc857f47fcf4d627bf84fc43146a20bffef1f1b2f6e6671c76c9948c4e02cd` | 0.126486778275 |

Проверены неизменность баланса при self-transfer, mint одного raw unit и сжигание только нового unit, покупка на 0.001 tDEL и продажа только полученного количества. Неиспользованное разрешение после продажи отозвано до нуля. Новый стейк 0.001 tDEL отозван; заявка 225 дождалась срока и завершена, status=0, сумма вернулась за вычетом комиссии.

Отчет: `artifacts/testnet-wallet-journal/run-20261029T032324Z/report.json`. Проверка истории и журнала: `history-audit.json` в той же папке.

В отчетах нет seed-фразы, приватного ключа или подписанного raw transaction. Разрешены только два поля из существующего изолированного testnet-конфига; адрес сверяется с выведенным signer, chain ID проверяется до отправки. Mainnet-подпись и отправка в этом прогоне не использовались.

Охват ограничен перечисленными транзакциями. Создание NFT, NFT-стейкинг, bridge settlement, Checks create/redeem, управление валидаторами и Safe lifecycle этим прогоном не подтверждаются. Офлайн-матрица API находится в TRANSACTION-MATRIX.md. Общие движения тестового адреса вне перечисленных хэшей не включены в сумму комиссий.
