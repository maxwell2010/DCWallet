using System.Numerics;
namespace DCWallet.Core;
public sealed record SafeMember(string Owner, BigInteger Weight);
public static class SafeQuorum
{
    public static void Validate(IReadOnlyList<SafeMember> members, BigInteger threshold)
    {
        if (members.Count is < 1 or > 100 || members.Any(m => !System.Text.RegularExpressions.Regex.IsMatch(m.Owner, "^0x[0-9a-fA-F]{40}$") || BigInteger.Parse("0" + m.Owner[2..], System.Globalization.NumberStyles.HexNumber) <= 1 || m.Weight < 1 || m.Weight > 1000) || members.Select(m => m.Owner).Distinct(StringComparer.OrdinalIgnoreCase).Count() != members.Count || threshold < 1 || threshold > members.Aggregate(BigInteger.Zero, (n, m) => n + m.Weight)) throw new ArgumentException("Некорректные владельцы, веса или порог Decimal Safe.");
    }
    public static BigInteger SignedWeight(IReadOnlyList<SafeMember> members, BigInteger threshold, IEnumerable<string> signers)
    {
        Validate(members, threshold); var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase); var weight = BigInteger.Zero;
        foreach (var signer in signers)
        {
            if (!seen.Add(signer)) throw new ArgumentException("Повторная подпись владельца.");
            var member = members.FirstOrDefault(m => m.Owner.Equals(signer, StringComparison.OrdinalIgnoreCase)) ?? throw new ArgumentException("Подпись не-владельца Safe."); weight += member.Weight;
        }
        if (weight < threshold) throw new ArgumentException($"Недостаточный вес подписей: {weight} из {threshold}."); return weight;
    }
}
