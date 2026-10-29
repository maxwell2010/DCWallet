using System.Text.Json;
using System.Text.Json.Nodes;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using DCWallet.Core;

namespace DCWallet;
public sealed partial class MainWindow
{
    private (Control View, Func<JsonNode?> Read) ParameterEditor(string key, JsonElement value)
    {
        // Readers outlive the temporary document that produced the form template.
        value = value.Clone();
        var help = OperationPresentation.FieldHelp(key);
        if (value.ValueKind == JsonValueKind.Object)
        {
            var objectFields = new Dictionary<string, Func<JsonNode?>>(); var fields = new StackPanel { Spacing = 10 };
            foreach (var property in value.EnumerateObject())
            {
                var editor = ParameterEditor(property.Name, property.Value); fields.Children.Add(editor.View); objectFields[property.Name] = editor.Read;
            }
            return (Card(fields, 12), () => { var result = new JsonObject(); foreach (var pair in objectFields) result[pair.Key] = pair.Value(); return result; });
        }
        if (value.ValueKind == JsonValueKind.Array)
        {
            var fields = new StackPanel { Spacing = 8 }; fields.Children.Add(Text(FieldLabel(key), 13, Muted)); var rows = new StackPanel { Spacing = 8 }; fields.Children.Add(rows); var readers = new List<Func<JsonNode?>>();
            var initial = value.EnumerateArray().Select(v => v.Clone()).ToArray(); var prototype = initial.FirstOrDefault(); if (prototype.ValueKind == JsonValueKind.Undefined) prototype = JsonSerializer.SerializeToElement("");
            void Add(JsonElement item)
            {
                var editor = ParameterEditor("Элемент", item); var row = new Grid { ColumnDefinitions = new ColumnDefinitions("*,Auto"), ColumnSpacing = 6 }; row.Children.Add(editor.View); readers.Add(editor.Read);
                var remove = MakeButton("Убрать", () => { rows.Children.Remove(row); readers.Remove(editor.Read); Invalidate(); return Task.CompletedTask; }); Grid.SetColumn(remove, 1); row.Children.Add(remove); rows.Children.Add(row); Invalidate();
            }
            foreach (var item in initial) Add(item);
            fields.Children.Add(MakeButton("Добавить", () => { Add(prototype); return Task.CompletedTask; }));
            return (fields, () => new JsonArray(readers.Select(r => r()).ToArray()));
        }
        if (value.ValueKind is JsonValueKind.True or JsonValueKind.False)
        {
            var box = new CheckBox { Content = FieldLabel(key), IsChecked = value.GetBoolean() }; ToolTip.SetTip(box, help); box.IsCheckedChanged += (_, _) => Invalidate(); return (box, () => JsonValue.Create(box.IsChecked == true));
        }
        var group = new StackPanel { Spacing = 5 }; group.Children.Add(Text(FieldLabel(key), 13, Muted));
        var input = new TextBox { Text = value.ValueKind == JsonValueKind.String ? value.GetString() : value.ValueKind == JsonValueKind.Null ? "" : value.GetRawText(), IsReadOnly = key is "from" or "sender", MinHeight = 34 };
        ToolTip.SetTip(input, help);
        input.PropertyChanged += (_, e) => { if (e.Property == TextBox.TextProperty) Invalidate(); }; group.Children.Add(input);
        if (key.EndsWith("Raw", StringComparison.OrdinalIgnoreCase) || key.EndsWith("Wei", StringComparison.OrdinalIgnoreCase)) group.Children.Add(Text(help, 12, Muted));
        if (key.Contains("validator", StringComparison.OrdinalIgnoreCase) && !key.Contains("name", StringComparison.OrdinalIgnoreCase)) group.Children.Add(AttachValidatorPicker(input));
        else if (key.TrimStart('$') is "token" or "tokenAddress" or "tokenIn" or "tokenOut") group.Children.Add(AttachTokenPicker(input));
        else if (key.TrimStart('$') is "collection" or "collectionAddress") group.Children.Add(AttachNftPicker(input));
        return (group, () => value.ValueKind == JsonValueKind.Number ? JsonNode.Parse(input.Text ?? "0") : value.ValueKind == JsonValueKind.Null && string.IsNullOrEmpty(input.Text) ? null : JsonValue.Create(input.Text ?? ""));
    }
}
