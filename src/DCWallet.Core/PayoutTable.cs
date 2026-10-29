using System.IO.Compression;
using System.Numerics;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
namespace DCWallet.Core;
public sealed record PayoutRow(string Address,string Amount);
public sealed record PayoutImport(IReadOnlyList<PayoutRow> Rows,IReadOnlyList<string> Errors)
{
    public bool Valid=>Errors.Count==0&&Rows.Count>0;
}
public static class PayoutTable
{
    public const int MaxBytes=256*1024,MaxRows=500;
    public static PayoutImport Read(byte[] bytes,string name)
    {
        if(bytes.Length is 0 or >MaxBytes)return new([], ["Размер файла должен быть от 1 байта до 256 КБ."]);
        try { return Validate(name.EndsWith(".xlsx",StringComparison.OrdinalIgnoreCase)?ReadXlsx(bytes):ReadDelimited(Decode(bytes))); }
        catch(Exception e) when(e is InvalidDataException or XmlException or DecoderFallbackException or ArgumentException or FormatException or IOException)
        { return new([], [e.Message]); }
    }
    static string Decode(byte[] bytes)
    {
        if(bytes.Length>1&&bytes[0]==255&&bytes[1]==254)return Encoding.Unicode.GetString(bytes);
        if(bytes.Length>1&&bytes[0]==254&&bytes[1]==255)return Encoding.BigEndianUnicode.GetString(bytes);
        try{return new UTF8Encoding(false,true).GetString(bytes);}
        catch(DecoderFallbackException){Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);return Encoding.GetEncoding(1251).GetString(bytes);}
    }
    static List<string[]> ReadDelimited(string text)
    {
        text=text.TrimStart('\uFEFF');var first=text.Split('\n')[0];var delimiter=first.Contains('\t')?'\t':first.Contains(';')?';':',';
        var rows=new List<string[]>();var cells=new List<string>();var cell=new StringBuilder();var quoted=false;
        for(var i=0;i<text.Length;i++)
        {
            var c=text[i];
            if(c=='"'){if(quoted&&i+1<text.Length&&text[i+1]=='"'){cell.Append('"');i++;}else quoted=!quoted;}
            else if(!quoted&&(c==delimiter||c=='\n'||c=='\r'))
            {
                cells.Add(cell.ToString());cell.Clear();
                if(c!=delimiter){if(c=='\r'&&i+1<text.Length&&text[i+1]=='\n')i++;rows.Add(cells.ToArray());cells.Clear();if(rows.Count>MaxRows+2)throw new InvalidDataException("Не более 500 получателей.");}
            }
            else cell.Append(c);
        }
        if(quoted)throw new InvalidDataException("Незакрытая кавычка CSV.");
        if(cell.Length>0||cells.Count>0){cells.Add(cell.ToString());rows.Add(cells.ToArray());}return rows;
    }
    static XDocument Xml(ZipArchiveEntry entry)
    {
        using var stream=entry.Open();using var reader=XmlReader.Create(stream,new XmlReaderSettings{DtdProcessing=DtdProcessing.Prohibit,XmlResolver=null,MaxCharactersInDocument=1024*1024});return XDocument.Load(reader);
    }
    static List<string[]> ReadXlsx(byte[] bytes)
    {
        using var zip=new ZipArchive(new MemoryStream(bytes),ZipArchiveMode.Read);if(zip.Entries.Count is 0 or >120)throw new InvalidDataException("Неверная структура XLSX.");
        if(zip.Entries.Sum(e=>e.Length)>2*1024*1024)throw new InvalidDataException("XLSX превышает лимит распаковки.");
        foreach(var entry in zip.Entries)
        {
            var path=entry.FullName.ToLowerInvariant();
            if(entry.Length>1024*1024||entry.CompressedLength>0&&entry.Length/entry.CompressedLength>100||path.Contains("vba")||path.Contains("activex")||path.Contains("embedding")||path.Contains("macrosheet")||path.Contains("externallink")||path.EndsWith(".bin")&&!Regex.IsMatch(path,"^xl/printersettings/printersettings[1-9][0-9]*[.]bin$"))throw new InvalidDataException("Макросы, внешние ссылки и активные вложения XLSX не допускаются.");
        }
        var shared=zip.GetEntry("xl/sharedStrings.xml");var strings=shared is null?[]:Xml(shared).Descendants().Where(e=>e.Name.LocalName=="si").Select(e=>string.Concat(e.Descendants().Where(t=>t.Name.LocalName=="t").Select(t=>t.Value))).ToArray();
        var workbook=Xml(zip.GetEntry("xl/workbook.xml")??throw new InvalidDataException("Нет workbook.xml."));var first=workbook.Descendants().FirstOrDefault(e=>e.Name.LocalName=="sheet")??throw new InvalidDataException("Нет листов XLSX.");
        var id=first.Attributes().FirstOrDefault(a=>a.Name.LocalName=="id")?.Value;
        var rels=Xml(zip.GetEntry("xl/_rels/workbook.xml.rels")??throw new InvalidDataException("Нет связей workbook."));var relation=rels.Descendants().FirstOrDefault(e=>e.Name.LocalName=="Relationship"&&(string?)e.Attribute("Id")==id);
        var target=(string?)relation?.Attribute("Target")??throw new InvalidDataException("Нет первого листа.");
        if(target.Contains("..")||target.Contains(':')||((string?)relation!.Attribute("TargetMode"))=="External")throw new InvalidDataException("Внешний лист не допускается.");
        var pathSheet=target.StartsWith('/')?target.TrimStart('/') : "xl/"+target;
        var sheet=Xml(zip.GetEntry(pathSheet)??throw new InvalidDataException("Лист XLSX не найден."));
        if(sheet.Descendants().Any(e=>e.Name.LocalName is "f" or "hyperlink"))throw new InvalidDataException("Формулы и ссылки не допускаются.");
        var result=new List<string[]>();
        foreach(var row in sheet.Descendants().Where(e=>e.Name.LocalName=="row"))
        {
            var values=new string[2];foreach(var cell in row.Elements().Where(e=>e.Name.LocalName=="c"))
            {
                var reference=(string?)cell.Attribute("r")??"";var col=Regex.Match(reference,"^[A-Z]+").Value;
                if(col is not("A" or "B"))throw new InvalidDataException("Нужны два столбца: адрес и сумма.");
                var value=cell.Elements().FirstOrDefault(e=>e.Name.LocalName=="v")?.Value??string.Concat(cell.Descendants().Where(e=>e.Name.LocalName=="t").Select(e=>e.Value));
                var type=(string?)cell.Attribute("t");
                if(type=="s"){if(!int.TryParse(value,out var index)||index<0||index>=strings.Length)throw new InvalidDataException("Неверная строка XLSX.");value=strings[index];}
                else if(type is not(null or "n" or "inlineStr" or "str"))throw new InvalidDataException("Неподдерживаемый тип ячейки.");
                if(col=="B"&&(type is null or "n")&&value.Count(char.IsAsciiDigit)>15)throw new InvalidDataException("Точную длинную сумму сохраните текстом в Excel.");
                values[col=="A"?0:1]=value;
            }
            result.Add(values);if(result.Count>MaxRows+1)throw new InvalidDataException("Не более 500 получателей.");
        }
        return result;
    }
    public static PayoutImport Validate(IEnumerable<string[]> matrix)
    {
        var source=matrix.Where(row=>row.Any(c=>!string.IsNullOrWhiteSpace(c))).ToArray();var rows=new List<PayoutRow>();var errors=new List<string>();
        if(source.Length>0&&new[]{"address","recipient","адрес","получатель"}.Contains((source[0].FirstOrDefault()??"").Trim().ToLowerInvariant()))source=source.Skip(1).ToArray();
        if(source.Length is 0 or >MaxRows)return new([], ["Нужно от 1 до 500 получателей."]);
        for(var i=0;i<source.Length;i++)
        {
            var row=source[i];if(row.Length!=2){errors.Add($"Строка {i+1}: нужны два столбца.");continue;}
            var address=(row[0]??"").Trim();var amount=(row[1]??"").Trim().Replace(',','.');
            if(!Regex.IsMatch(address,"^0x[0-9a-fA-F]{40}$")||address[2..].All(c=>c=='0')||!Regex.IsMatch(amount,"^[0-9]{1,78}(\\.[0-9]{1,36})?$")||!amount.Any(c=>c is >='1' and <='9')){errors.Add($"Строка {i+1}: неверный адрес или положительная сумма.");continue;}
            rows.Add(new(address,amount));
        }
        return new(rows,errors);
    }
    public static string Csv(IEnumerable<PayoutRow> rows)=>"address;amount\r\n"+string.Join("\r\n",rows.Select(r=>r.Address+";"+r.Amount))+"\r\n";
    public static byte[] Xlsx(IEnumerable<PayoutRow> rows)
    {
        var validated=Validate(rows.Select(r=>new[]{r.Address,r.Amount}));if(!validated.Valid)throw new InvalidDataException(string.Join("\n",validated.Errors));
        using var buffer=new MemoryStream();using(var zip=new ZipArchive(buffer,ZipArchiveMode.Create,true))
        {
            void Part(string name,string xml){using var writer=new StreamWriter(zip.CreateEntry(name,CompressionLevel.NoCompression).Open(),new UTF8Encoding(false));writer.Write(xml);}
            Part("[Content_Types].xml","<Types xmlns='http://schemas.openxmlformats.org/package/2006/content-types'><Default Extension='rels' ContentType='application/vnd.openxmlformats-package.relationships+xml'/><Default Extension='xml' ContentType='application/xml'/><Override PartName='/xl/workbook.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml'/><Override PartName='/xl/worksheets/sheet1.xml' ContentType='application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml'/></Types>");
            Part("_rels/.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/officeDocument' Target='xl/workbook.xml'/></Relationships>");
            Part("xl/workbook.xml","<workbook xmlns='http://schemas.openxmlformats.org/spreadsheetml/2006/main' xmlns:r='http://schemas.openxmlformats.org/officeDocument/2006/relationships'><sheets><sheet name='Recipients' sheetId='1' r:id='r1'/></sheets></workbook>");
            Part("xl/_rels/workbook.xml.rels","<Relationships xmlns='http://schemas.openxmlformats.org/package/2006/relationships'><Relationship Id='r1' Type='http://schemas.openxmlformats.org/officeDocument/2006/relationships/worksheet' Target='worksheets/sheet1.xml'/></Relationships>");
            XNamespace ns="http://schemas.openxmlformats.org/spreadsheetml/2006/main";var matrix=new[]{new[]{"address","amount"}}.Concat(validated.Rows.Select(r=>new[]{r.Address,r.Amount}));
            var sheet=new XElement(ns+"worksheet",new XElement(ns+"sheetData",matrix.Select((row,index)=>new XElement(ns+"row",new XAttribute("r",index+1),row.Select((value,col)=>new XElement(ns+"c",new XAttribute("r",(col==0?"A":"B")+(index+1)),new XAttribute("t","inlineStr"),new XElement(ns+"is",new XElement(ns+"t",value))))))));
            Part("xl/worksheets/sheet1.xml",sheet.ToString(SaveOptions.DisableFormatting));
        }return buffer.ToArray();
    }
}
