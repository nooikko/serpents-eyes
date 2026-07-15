using System.Collections.Generic;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Documents;
using Avalonia.Media;
using SerpentsEyes.Core.GameData;
using SerpentsEyes.App.ViewModels;

namespace SerpentsEyes.App.Controls;

/// <summary>
/// Renders the game's UE rich-text markup as styled inlines: damage types in their
/// thematic colors, scaling formulas as readable math, stat references in gold.
/// </summary>
public sealed class RichTextPresenter : TextBlock
{
    public static readonly StyledProperty<string?> RawTextProperty =
        AvaloniaProperty.Register<RichTextPresenter, string?>(nameof(RawText));

    /// <summary>Damage-type tag codes → thematic colors (default gold for unknown tags).</summary>
    private static readonly Dictionary<string, IBrush> TagBrushes = new(System.StringComparer.OrdinalIgnoreCase)
    {
        ["PH"] = new SolidColorBrush(Color.Parse("#C8C2B4")),  // Physical — bone
        ["BL"] = new SolidColorBrush(Color.Parse("#D4596B")),  // Sanguine — blood
        ["FI"] = new SolidColorBrush(Color.Parse("#E09A52")),  // Fire — ember
        ["LI"] = new SolidColorBrush(Color.Parse("#E0D060")),  // Lightning
        ["RO"] = new SolidColorBrush(Color.Parse("#8FB56A")),  // Rot / Blight
        ["SO"] = new SolidColorBrush(Color.Parse("#A79AE0")),  // Soul
    };

    private static readonly IBrush GoldBrush = new SolidColorBrush(Color.Parse("#C9A85C"));

    public string? RawText
    {
        get => GetValue(RawTextProperty);
        set => SetValue(RawTextProperty, value);
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == RawTextProperty)
        {
            Rebuild();
        }
    }

    private void Rebuild()
    {
        var inlines = new InlineCollection();
        if (RawText is { Length: > 0 } raw)
        {
            foreach (var segment in UeRichText.Parse(raw))
            {
                inlines.Add(ToInline(segment));
            }
        }
        Inlines = inlines;
    }

    private static Run ToInline(RichSegment segment) => segment.Kind switch
    {
        RichSegmentKind.Styled => new Run(segment.Text)
        {
            Foreground = segment.Tag is not null && TagBrushes.TryGetValue(segment.Tag, out var brush) ? brush : GoldBrush,
            FontWeight = FontWeight.SemiBold,
        },
        RichSegmentKind.Math => new Run(DetailModel.PrettyFormula(segment.Text))
        {
            Foreground = GoldBrush,
            FontWeight = FontWeight.SemiBold,
        },
        RichSegmentKind.StatIcon => new Run(segment.Text)
        {
            Foreground = GoldBrush,
            FontStyle = FontStyle.Italic,
        },
        RichSegmentKind.Input => new Run($"[{PrettyInput(segment.Text)}]")
        {
            Foreground = GoldBrush,
        },
        _ => new Run(segment.Text),
    };

    /// <summary>"IA_Ability_Primary" → "Primary".</summary>
    private static string PrettyInput(string action)
    {
        int lastUnderscore = action.LastIndexOf('_');
        return lastUnderscore >= 0 && lastUnderscore + 1 < action.Length ? action[(lastUnderscore + 1)..] : action;
    }
}
