using System.IO.Compression;
using System.Text;
using DCWallet.Core;
using Xunit;
namespace DCWallet.Tests;
public sealed class PayoutTableTests
{
    const string Owner="0x1111111111111111111111111111111111111111";
    [Theory][InlineData("address;amount\n", ";", "1,000000000000000001")][InlineData("адрес\tсумма\n","\t","1.000000000000000001")][InlineData("recipient,amount\n",",","\"1,000000000000000001\"")]
    public void CsvAndTsvPreserveExactAmounts(string header,string delimiter,string amount)
    {
        var result=PayoutTable.Read(Encoding.UTF8.GetBytes(header+Owner+delimiter+amount),"rows.csv");Assert.True(result.Valid);Assert.Equal("1.000000000000000001",Assert.Single(result.Rows).Amount);
        var round=PayoutTable.Read(Encoding.UTF8.GetBytes(PayoutTable.Csv(result.Rows)),"round.csv");Assert.Equal(result.Rows,round.Rows);
    }
    [Fact] public void NoPartialSilentRowsAndSizeLimits()
    {
        var result=PayoutTable.Read(Encoding.UTF8.GetBytes(Owner+";1\ninvalid;1"),"rows.csv");Assert.False(result.Valid);Assert.Single(result.Errors);Assert.Single(result.Rows);
        Assert.False(PayoutTable.Read(new byte[PayoutTable.MaxBytes+1],"x.csv").Valid);
        Assert.False(PayoutTable.Validate(Enumerable.Repeat(new[]{Owner,"1"},501)).Valid);
        Assert.False(PayoutTable.Read(Encoding.UTF8.GetBytes(Owner+";=1+1"),"x.csv").Valid);
    }
    [Fact] public void XlsxTextExactFormulaAndNumericPrecisionRules()
    {
        Assert.Equal("1.000000000000000001",Assert.Single(PayoutTable.Read(Book("inlineStr","<is><t>1.000000000000000001</t></is>"),"x.xlsx").Rows).Amount);
        Assert.False(PayoutTable.Read(Book("n","<v>100000000000000001</v>"),"x.xlsx").Valid);
        Assert.False(PayoutTable.Read(Book("n","<f>1+1</f><v>2</v>"),"x.xlsx").Valid);
    }
    [Fact] public void ExportedXlsxRoundTripsExactText() { var rows=new[]{new PayoutRow(Owner,"1.000000000000000001")};var result=PayoutTable.Read(PayoutTable.Xlsx(rows),"round.xlsx");Assert.True(result.Valid);Assert.Equal(rows,result.Rows); }
    [Fact] public void ExportedMaximumRepeatedRowsRemainImportable() { var rows=Enumerable.Repeat(new PayoutRow(Owner,"1.000000000000000001"),500).ToArray();var bytes=PayoutTable.Xlsx(rows);Assert.True(bytes.Length<=PayoutTable.MaxBytes);var result=PayoutTable.Read(bytes,"round.xlsx");Assert.True(result.Valid);Assert.Equal(500,result.Rows.Count); }
    static byte[] Book(string type,string value)
    {
        using var buffer=new MemoryStream();using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true))
        {
            void Entry(string name,string text){using var writer=new StreamWriter(zip.CreateEntry(name).Open());writer.Write(text);}
            Entry("xl/workbook.xml","<workbook xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Payouts' r:id='r1'/></sheets></workbook>");
            Entry("xl/_rels/workbook.xml.rels","<Relationships><Relationship Id='r1' Target='worksheets/sheet2.xml'/></Relationships>");
            Entry("xl/worksheets/sheet2.xml","<worksheet><sheetData><row r='1'><c r='A1' t='inlineStr'><is><t>"+Owner+"</t></is></c><c r='B1' t='"+type+"'>"+value+"</c></row></sheetData></worksheet>");
        }return buffer.ToArray();
    }
}
