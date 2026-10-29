using System.Numerics;
using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public class CombinedNftPositionTests
{
 const string Captured="0x000000000000000000000000918e742d8dd0de7e7930fe0489cdbce1701fb29d000000000000000000000000000000000000000000000000000000000000000100000000000000000000000000000000000000000000000000000000000000010000000000000000000000000000000000000000000000000000000000000003000000000000000000000000b92bd490c8e9e5d9c46d71666418d4e7ae4534e900000000000000000000000005babc196b96c22f763f7e30a84e74665a920f980000000000000000000000000e7c2cd8dc3a8ae787a8142f45b268ec27feeb8b0000000000000000000000000000000000000000000000000de0b6b3a764000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000000001";
 [Fact]public void CapturedActivePositionSeparatesNftAmountFromDelReserve(){var p=CombinedNftDelegation.DecodePosition(Captured)!;Assert.Equal(BigInteger.One,p.Amount);Assert.Equal(BigInteger.Pow(10,18),p.ReserveAmount);Assert.Equal((byte)3,p.NftType);Assert.True(p.IsActive);Assert.Equal(BigInteger.One,p.TokenId);}
 [Fact]public void EmptyTupleMeansNoPosition()=>Assert.Null(CombinedNftDelegation.DecodePosition("0x"+new string('0',640)));
 [Fact]public void InactiveDoesNotBecomeActive(){var raw=Captured[..^64]+new string('0',64);Assert.False(CombinedNftDelegation.DecodePosition(raw)!.IsActive);}
 [Fact]public void InvalidBooleanFailsClosed()=>Assert.Throws<InvalidDataException>(()=>CombinedNftDelegation.DecodePosition(Captured[..^64]+new string('0',63)+"2"));
 [Fact]public void TruncatedTupleFailsClosed()=>Assert.Throws<InvalidDataException>(()=>CombinedNftDelegation.DecodePosition(Captured[..^64]));
 [Fact]public void InvalidAddressPaddingFailsClosed()=>Assert.Throws<InvalidDataException>(()=>CombinedNftDelegation.DecodePosition("0x1"+Captured[3..]));
}
