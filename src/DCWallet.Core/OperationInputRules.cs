using System.Numerics;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace DCWallet.Core;
/// <summary>Early form checks. SDK ABI validation and RPC estimation still run afterward.</summary>
public static class OperationInputRules
{
    public static void Validate(string json)
    {
        using var document = JsonDocument.Parse(json);
        if (document.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Параметры должны быть объектом.");
        Visit(document.RootElement);
    }
    private static void Visit(JsonElement node)
    {
        if (node.ValueKind == JsonValueKind.Array) { foreach (var item in node.EnumerateArray()) Visit(item); return; }
        if (node.ValueKind != JsonValueKind.Object) return;
        foreach (var field in node.EnumerateObject())
        {
            var key = field.Name.TrimStart('$');
            if (field.Value.ValueKind == JsonValueKind.String)
            {
                var value = field.Value.GetString() ?? "";
                if (key is "from" or "sender" or "to" or "recipient" or "validator" or "newValidator" or "token" or "tokenAddress" or "tokenIn" or "tokenOut" or "collection" or "collectionAddress" or "spender" or "operatorAddress" or "delegationContract" or "delegationAddress" or "tokenCenter" or "nftCenter")
                    if (!Regex.IsMatch(value, "^0x[0-9a-fA-F]{40}$")) throw new ArgumentException($"Поле {key}: нужен EVM-адрес из 40 hex-символов после 0x.");
                if (key.EndsWith("Raw", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Wei", StringComparison.OrdinalIgnoreCase))
                    if (!Regex.IsMatch(value, "^[0-9]+$") || !BigInteger.TryParse(value, out var amount) || amount >= (BigInteger.One << 256)) throw new ArgumentException($"Поле {key}: нужно целое число от 0 до 2^256-1 без округления.");
            }
            Visit(field.Value);
        }
    }
}
