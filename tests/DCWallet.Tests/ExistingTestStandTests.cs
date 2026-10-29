using System.Text;
using System.Text.Json;
using Decimal.Web3.Wallet;
using Xunit;
namespace DCWallet.Tests;
public sealed class ExistingStandFactAttribute:FactAttribute
{
    public ExistingStandFactAttribute() { if(Environment.GetEnvironmentVariable("DC_VALIDATE_EXISTING_STAND")!="1") Skip="Explicit isolated testnet credential validation opt-in required"; }
}
public class ExistingTestStandTests
{
    [ExistingStandFact] public void ExistingIsolatedStandRequiresValidMnemonicAndExactOwner()
    {
        if(Environment.GetEnvironmentVariable("DC_VALIDATE_EXISTING_STAND")!="1") return;
        var path=Environment.GetEnvironmentVariable("DC_EXISTING_STAND_ENV") ?? throw new InvalidOperationException("Isolated stand configuration path missing");
        string? phrase=null,expected=null;
        // Only the two isolated testnet fields are consumed. No production fallback exists.
        foreach(var line in File.ReadLines(path))
        {
            var i=line.IndexOf('='); if(i<=0) continue; var key=line[..i].Trim();
            if(key is not ("DECIMAL_TESTNET_TEST_MNEMONIC" or "DECIMAL_TESTNET_TEST_EXPECTED_ADDRESS")) continue;
            var value=line[(i+1)..].Trim(); if(value.Length>=2 && (value[0]=='"'&&value[^1]=='"'||value[0]=='\''&&value[^1]=='\'')) value=value[1..^1];
            if(key=="DECIMAL_TESTNET_TEST_MNEMONIC") phrase=value; else expected=value;
        }
        phrase=string.Join(' ',(phrase??"").Normalize(NormalizationForm.FormKD).Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries));
        var valid=DecimalWallet.IsValidMnemonic(phrase); var matches=false;
        if(valid) { using var signer=DecimalWallet.CreateTransactionSigner(phrase,202020); matches=signer.Address.Equals(expected,StringComparison.OrdinalIgnoreCase); }
        var result=new{mnemonicPresent=phrase.Length>0,mnemonicValid=valid,expectedAddress=expected,derivedAddressMatches=matches,chainId=202020,broadcast=false};
        var report=Environment.GetEnvironmentVariable("DC_EXISTING_STAND_REPORT"); if(report is not null) File.WriteAllText(report,JsonSerializer.Serialize(result));
        Assert.True(valid,"Isolated testnet mnemonic failed BIP39 validation; no signing or broadcast attempted.");
        Assert.True(matches,"Isolated testnet signer does not match configured owner; no signing or broadcast attempted.");
    }
}
