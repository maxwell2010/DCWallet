using System.Text.RegularExpressions;

namespace DCWallet.Core;
public sealed record OperationGroup(string Code, string Title) { public override string ToString() => Title; }
public static class OperationPresentation
{
    public static string Description(Operation operation)
    {
        var name = operation.Method.Name.ToLowerInvariant();
        if (name.Contains("burn")) return "Уменьшает количество монет или уничтожает NFT. Проверьте актив и количество: отменить сжигание после исполнения нельзя.";
        if (name.Contains("approve") || name.Contains("approval")) return "Выдает или отзывает разрешение другому адресу распоряжаться выбранным активом. Проверьте адрес получателя разрешения и его объем.";
        if (name.Contains("create")) return "Создает новый объект в сети. Проверьте название, владельца и параметры выпуска; создание оплачивается отдельно.";
        if (name.Contains("delegate")) return "Передает актив в стейкинг выбранному валидатору. Hold дополнительно фиксирует срок; возврат выполняется через отзыв.";
        if (name.Contains("withdraw") || name.Contains("unbond")) return "Создает заявку на отзыв стейка. Средства возвращаются после установленного сетью срока и завершения заявки.";
        if (name.Contains("complete")) return "Завершает ранее созданную заявку. Ее срок и принадлежность вашему счету необходимо проверить в сети.";
        if (name.Contains("buy") || name.Contains("sell") || name.Contains("convert")) return "Обменивает выбранные активы. Минимум получения или максимум расхода ограничивает допустимую цену.";
        if (name.Contains("transfer") || name == "del" || name == "build") return "Передает актив получателю. Проверьте адрес, выбранный токен и точное количество перед подтверждением.";
        if (name.Contains("mint")) return "Дополнительно выпускает токены или NFT. Доступность зависит от прав владельца и ограничений контракта.";
        return "Изменяет состояние выбранного контракта. Проверьте назначение полей и свои права на эту операцию. Часть расширенных сценариев еще требует сетевой проверки.";
    }
    public static string FieldHelp(string field)
    {
        var key = field.TrimStart('$');
        if (key == "reserveValueWei") return WalletTokenCreationAdapter.ReserveHelp;
        if (key == "capRaw") return WalletTokenCreationAdapter.NonMintableCapHelp;
        if (key.EndsWith("Wei", StringComparison.OrdinalIgnoreCase)) return "Целое число в wei: 1 DEL = 1000000000000000000 wei. Например, 0.001 DEL = 1000000000000000 wei.";
        if (key.EndsWith("Raw", StringComparison.OrdinalIgnoreCase)) return "Количество в минимальных единицах токена, целым числом. При decimals=6 один токен = 1000000. Decimals нужно читать из контракта.";
        if (field.StartsWith('$')) return "Адрес служебного контракта выбранной сети. Известные системные адреса перечитываются из реестра перед подготовкой.";
        if (key is "from" or "sender") return "Текущий открытый счет. Отправитель подставляется кошельком.";
        if (key.Contains("validator", StringComparison.OrdinalIgnoreCase)) return "Адрес валидатора Decimal. Выберите его по имени в списке или вставьте полный адрес 0x…";
        if (key.Contains("timestamp", StringComparison.OrdinalIgnoreCase) || key == "deadline") return "Момент времени Unix в секундах, без миллисекунд. Hold задает срок фиксации; deadline ограничивает срок действия подписи.";
        if (key is "to" or "recipient" or "owner" or "creator" or "spender" or "operatorAddress") return "Полный EVM-адрес: 0x и 40 шестнадцатеричных символов. Для разрешения это адрес, которому передается право управления.";
        if (key.Contains("tokenId", StringComparison.OrdinalIgnoreCase)) return "Целый идентификатор NFT внутри указанной коллекции. Номер 0 также может быть допустим.";
        if (key.Contains("token", StringComparison.OrdinalIgnoreCase) || key.Contains("collection", StringComparison.OrdinalIgnoreCase)) return "Адрес контракта выбранного токена или NFT-коллекции. Символ и название сами по себе не являются адресом.";
        if (key is "data" or "r" or "s" or "signatures" or "checks") return "Двоичные данные в формате 0xHEX. Используйте данные именно этой операции и выбранной сети.";
        if (key.Contains("amount", StringComparison.OrdinalIgnoreCase) || key.Contains("value", StringComparison.OrdinalIgnoreCase)) return "Точная сумма. Для обычных DEL используйте точку в дробной части; поля Raw/Wei требуют целого числа минимальных единиц.";
        if (key == "nonce") return "Порядковый номер операции. Для Safe он читается из контракта и меняется после исполнения.";
        return "Параметр «" + key + "» выбранного действия. Перед подтверждением проверьте его значение и единицы измерения.";
    }
    public static readonly IReadOnlyList<OperationGroup> Groups = new[]
    {
        new OperationGroup("Transactions", "DEL и данные"), new("Multisend", "Несколько получателей"), new("Tokens", "Управление токенами"), new("TokenCreation", "Выпуск токенов"), new("TokenEconomy", "Покупка, продажа и обмен"), new("Nft", "NFT"), new("Staking", "Стейкинг"), new("Validators", "Валидаторы"), new("Checks", "Чеки"), new("Multisig", "Общий счет"), new("Bridge", "Мост")
    };
    private static readonly Dictionary<string, string> Words = new(StringComparer.OrdinalIgnoreCase)
    {
        ["delegate"] = "Делегировать", ["unbond"] = "Отозвать", ["withdraw"] = "Отозвать", ["transfer"] = "Передать", ["hold"] = "hold", ["reset"] = "сбросить", ["holds"] = "hold-позиции", ["create"] = "Создать", ["token"] = "токен", ["tokens"] = "токены", ["reserveless"] = "безрезервный", ["mint"] = "Выпустить", ["burn"] = "Сжечь", ["approve"] = "Разрешить", ["approval"] = "разрешение", ["all"] = "всем", ["set"] = "Установить", ["disable"] = "Отключить", ["buy"] = "Купить", ["sell"] = "Продать", ["convert"] = "Обменять", ["exact"] = "точно", ["for"] = "за", ["with"] = "с", ["by"] = "через", ["permit"] = "permit", ["add"] = "Добавить", ["reserve"] = "резерв", ["details"] = "описание", ["update"] = "Изменить", ["meta"] = "метаданные", ["validator"] = "валидатор", ["candidate"] = "кандидата", ["pause"] = "Приостановить", ["unpause"] = "Возобновить", ["remove"] = "Удалить", ["complete"] = "Завершить", ["redeem"] = "Погасить", ["native"] = "DEL", ["execution"] = "исполнение", ["stake"] = "стейк", ["checked"] = "с проверкой", ["from"] = "со счета", ["safe"] = "безопасно", ["batch"] = "пакетом", ["uri"] = "URI", ["contract"] = "контракта", ["call"] = "вызов", ["del"] = "DEL", ["eth"] = "DEL", ["erc"] = "ERC", ["drc"] = "DRC", ["build"] = "", ["draft"] = "", ["async"] = "", ["self"] = "свой"
    };
    public static string Title(Operation operation)
    {
        var special = operation.Method.Name switch
        {
            "Del" => "Перевод DEL", "BurnDel" => "Сжигание DEL", "ContractCall" => "Произвольный вызов контракта", "Build" => "Рассылка DEL нескольким получателям", "BuildExecutionDraft" => "Исполнение транзакции общего счета", "SetApprovalForAll" => "Разрешение оператору управлять NFT", _ => ""
        };
        if (special.Length > 0) return special;
        var tokens = Regex.Matches(operation.Method.Name, "[A-Z]?[a-z]+|[A-Z]+(?![a-z])|[0-9]+").Select(m => m.Value).Select(w => Words.GetValueOrDefault(w, w)).Where(w => w.Length > 0);
        var suffix = operation.Method.DeclaringType!.Name.Contains("721") ? " · ERC721" : operation.Method.DeclaringType.Name.Contains("1155") ? " · ERC1155" : operation.Method.DeclaringType.Name.Contains("Reserveless") ? " · безрезервный токен" : "";
        return string.Join(' ', tokens) + suffix;
    }
}
