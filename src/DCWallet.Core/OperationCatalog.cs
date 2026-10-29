using System.Globalization;
using System.Numerics;
using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using Decimal.Web3;
using Decimal.Web3.Transactions;

namespace DCWallet.Core;

public sealed record Operation(MethodInfo Method)
{
    public string Id => Method.DeclaringType!.Name + "." + Method.Name + "(" + string.Join(",", Method.GetParameters().Select(p => p.ParameterType.Name)) + ")";
    public string Group => Method.DeclaringType!.Namespace!.Split('.').Last();
    public string Label => Method.DeclaringType!.Name + " / " + Method.Name + " (" + string.Join(", ", Method.GetParameters().Select(p => p.Name)) + ")";
    public ConstructorInfo? Constructor => Method.IsStatic ? null : Method.DeclaringType!.GetConstructors().OrderBy(c => c.GetParameters().Length).First();
    public override string ToString() => OperationPresentation.Title(this);
}

/// <summary>Only draft-returning SDK methods are callable. Signing and RPC broadcast are never reflected.</summary>
public static class OperationCatalog
{
    public static readonly JsonSerializerOptions Json = CreateOptions();
    public static IReadOnlyList<Operation> All { get; } = typeof(DecimalClient).Assembly.GetExportedTypes()
        .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.Instance | BindingFlags.DeclaredOnly))
        .Where(m => !m.IsSpecialName && !m.Name.Contains('<') && (m.ReturnType == typeof(TransactionDraft) || m.ReturnType == typeof(Task<TransactionDraft>)))
        .Select(m => new Operation(m)).OrderBy(o => o.Group).ThenBy(o => o.Label).ToArray();

    public static string Template(Operation operation, DecimalNetwork network, string from)
    {
        var args = new Dictionary<string, object?>();
        foreach (var p in operation.Constructor?.GetParameters() ?? [])
            if (!Injected(p.ParameterType)) args["$" + p.Name] = Example(p, network, from);
        foreach (var p in operation.Method.GetParameters()) args[p.Name!] = Example(p, network, from);
        return JsonSerializer.Serialize(args, Json);
    }

    public static async Task<TransactionDraft> BuildAsync(Operation op, string json, DecimalClient client, string from)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Object) throw new ArgumentException("Ожидается объект JSON.");
        object? Value(ParameterInfo p, string prefix)
        {
            if (p.ParameterType == typeof(DecimalNetwork)) return client.Network;
            if (p.ParameterType == typeof(DecimalClient)) return client;
            if (p.ParameterType == typeof(Nethereum.Web3.Web3)) return client.Web3;
            if (!doc.RootElement.TryGetProperty(prefix + p.Name, out var v))
            {
                if (p.HasDefaultValue) return p.DefaultValue;
                throw new ArgumentException("Не заполнено поле " + prefix + p.Name);
            }
            if (prefix == "" && p.Name is "from" or "sender" && p.ParameterType == typeof(string)) return from;
            return v.Deserialize(p.ParameterType, Json);
        }
        try
        {
            var instance = op.Constructor?.Invoke(op.Constructor.GetParameters().Select(p => Value(p, "$" )).ToArray());
            var value = op.Method.Invoke(instance, op.Method.GetParameters().Select(p => Value(p, "")).ToArray());
            var draft = value is Task<TransactionDraft> task ? await task : (TransactionDraft)value!;
            if (!draft.From.Equals(from, StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("Отправитель не совпадает с открытым счетом.");
            return TransactionDraft.ContractCall(draft.From, draft.To, draft.Data, draft.ValueWei);
        }
        catch (TargetInvocationException e) when (e.InnerException is not null) { throw e.InnerException; }
    }

    private static bool Injected(Type t) => t == typeof(DecimalNetwork) || t == typeof(DecimalClient) || t == typeof(Nethereum.Web3.Web3);
    private static object? Example(ParameterInfo p, DecimalNetwork n, string from)
    {
        if (p.Name is "from" or "sender") return from;
        if (p.Name is "delegationContract" or "delegationAddress") return n.DelegationContract;
        if (p.Name is "tokenCenter") return n.TokenCenterContract;
        if (p.Name is "nftCenter") return n.NftCenterContract;
        if (p.HasDefaultValue) return p.DefaultValue is DBNull ? null : p.DefaultValue;
        return Sample(p.ParameterType, 0);
    }
    private static object? Sample(Type type, int depth)
    {
        if (depth > 3) return null;
        if (Nullable.GetUnderlyingType(type) is not null) return null;
        if (type == typeof(string)) return "";
        if (type == typeof(BigInteger)) return "0";
        if (type == typeof(byte[])) return "0x";
        if (type == typeof(bool)) return false;
        if (type.IsPrimitive) return 0;
        if (type.IsEnum) return 0;
        if (type.IsArray) return new[] { Sample(type.GetElementType()!, depth + 1) };
        if (type.IsGenericType && typeof(System.Collections.IEnumerable).IsAssignableFrom(type)) return new[] { Sample(type.GetGenericArguments()[0], depth + 1) };
        return type.GetProperties().Where(p => p.CanWrite).ToDictionary(p => p.Name, p => Sample(p.PropertyType, depth + 1));
    }
    private static JsonSerializerOptions CreateOptions()
    {
        var options = new JsonSerializerOptions { WriteIndented = true, PropertyNameCaseInsensitive = true };
        options.Converters.Add(new BigIntegerConverter()); options.Converters.Add(new HexBytesConverter()); return options;
    }
}
public sealed class BigIntegerConverter : JsonConverter<BigInteger>
{
    public override BigInteger Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        if (reader.TokenType != JsonTokenType.String) throw new JsonException("Большие целые числа задаются строками.");
        return BigInteger.Parse(reader.GetString()!, CultureInfo.InvariantCulture);
    }
    public override void Write(Utf8JsonWriter writer, BigInteger value, JsonSerializerOptions options) => writer.WriteStringValue(value.ToString(CultureInfo.InvariantCulture));
}
public sealed class HexBytesConverter : JsonConverter<byte[]>
{
    public override byte[] Read(ref Utf8JsonReader reader, Type type, JsonSerializerOptions options)
    {
        var text = reader.GetString()!;
        if (!text.StartsWith("0x")) throw new JsonException("Байты задаются как 0xHEX.");
        return Convert.FromHexString(text[2..]);
    }
    public override void Write(Utf8JsonWriter writer, byte[] value, JsonSerializerOptions options) => writer.WriteStringValue("0x" + Convert.ToHexString(value).ToLowerInvariant());
}
