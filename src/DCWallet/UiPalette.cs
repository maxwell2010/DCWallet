using Avalonia.Media;

namespace DCWallet;
internal static class UiPalette
{
    public static readonly SolidColorBrush Canvas = new(), Sidebar = new(), Card = new(), Ink = new(), Muted = new(), Line = new(), Accent = new(), Selected = new(), Soft = new(), Error = new(), Primary = new(), PrimaryInk = new(), PrimaryHover = new(), PrimaryPressed = new();
    public static void Set(bool dark, bool neon = false)
    {
        if (neon)
        {
            // MintCandy console/src/styles.css: data-theme="neon".
            Canvas.Color = Color.Parse("#070711"); Sidebar.Color = Color.Parse("#0B0B1E");
            Card.Color = Color.Parse("#0B0B1E"); Ink.Color = Color.Parse("#EDF7FF");
            Muted.Color = Color.Parse("#ACACD1"); Line.Color = Color.Parse("#343667");
            Accent.Color = Color.Parse("#29DCFF"); Selected.Color = Color.Parse("#7136D8");
            Soft.Color = Color.Parse("#12112A"); Error.Color = Color.Parse("#FF9BCB");
            Primary.Color = Color.Parse("#19CCEF"); PrimaryInk.Color = Color.Parse("#06101C");
            PrimaryHover.Color = Color.Parse("#52DCF4"); PrimaryPressed.Color = Color.Parse("#0CB4D5");
            return;
        }
        Canvas.Color = Color.Parse(dark ? "#101722" : "#F4F6FA");
        Sidebar.Color = Color.Parse(dark ? "#151F2E" : "#EDF0F6");
        Card.Color = Color.Parse(dark ? "#1B2636" : "#FFFFFF");
        Ink.Color = Color.Parse(dark ? "#EBF1FB" : "#101A2B");
        Muted.Color = Color.Parse(dark ? "#A0B0C9" : "#63728B");
        Line.Color = Color.Parse(dark ? "#344359" : "#D7DFEC");
        Accent.Color = Color.Parse(dark ? "#73B7FF" : "#2563EB");
        Selected.Color = Color.Parse(dark ? "#263E60" : "#DCE7FC");
        Soft.Color = Color.Parse(dark ? "#202E42" : "#F0F3F8");
        Error.Color = Color.Parse(dark ? "#FFA0A6" : "#B52D39");
        Primary.Color = Color.Parse("#2563EB"); PrimaryInk.Color = Colors.White;
        PrimaryHover.Color = Color.Parse("#1D4ED8"); PrimaryPressed.Color = Color.Parse("#1E40AF");
    }
}
