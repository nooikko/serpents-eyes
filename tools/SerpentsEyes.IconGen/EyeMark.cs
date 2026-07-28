using SkiaSharp;

namespace SerpentsEyes.IconGen;

/// <summary>
/// Draws the app mark: the same gold serpent eye the title bar draws, on a dark plate.
/// </summary>
/// <remarks>
/// The geometry is lifted from the <c>Path</c> in MainWindow.axaml so the icon and the title
/// bar are the same drawing. In that 20x12 canvas the eye is 18 units wide, its quadratic
/// control points sit 8.5 units above and below the centre line, and the slit pupil is 4 x 8 —
/// tall enough that it meets the outline top and bottom, which is what makes it read as a
/// serpent's eye rather than a human one. Everything below is those proportions scaled up.
///
/// Each size is drawn at its own resolution rather than downsampled from one large bitmap:
/// the mark is thin-stroked line art, and downsampling turns the 16px stroke to grey mush.
/// </remarks>
internal static class EyeMark
{
    /// <summary>Coordinate space the drawing is authored in; every size scales from here.</summary>
    private const float Design = 256f;

    private const float Center = Design / 2f;

    /// <summary>Rounded-plate inset and corner radius, in design units.</summary>
    private const float PlateInset = 4f;
    private const float PlateRadius = 54f;

    /// <summary>
    /// Below this the plate's hairline border and the glow behind the eye stop being detail and
    /// start being dirt, so they are dropped.
    /// </summary>
    private const int DetailThreshold = 32;

    /// <summary>
    /// Thinnest the eye outline is allowed to get, in device pixels. Scaled strictly, the stroke
    /// would be 0.75px at 16x16 and antialias itself into a smudge.
    /// </summary>
    private const float MinStrokePixels = 1.5f;

    // Palette, matching the resources in App.axaml.
    private static readonly SKColor PlateTop = new(0x1C, 0x2E, 0x23);
    private static readonly SKColor PlateBottom = new(0x0A, 0x11, 0x0D);
    private static readonly SKColor PlateEdge = new(0x2E, 0x40, 0x33);
    private static readonly SKColor GoldLight = new(0xF0, 0xDC, 0xA0);
    private static readonly SKColor GoldMid = new(0xC9, 0xA8, 0x5C);
    private static readonly SKColor GoldDeep = new(0x9C, 0x84, 0x49);

    public static SKBitmap Render(int size)
    {
        var bitmap = new SKBitmap(new SKImageInfo(size, size, SKColorType.Bgra8888, SKAlphaType.Premul));
        using var canvas = new SKCanvas(bitmap);
        canvas.Clear(SKColors.Transparent);
        canvas.Scale(size / Design);

        DrawPlate(canvas, size);
        DrawEye(canvas, size);

        canvas.Flush();
        return bitmap;
    }

    private static void DrawPlate(SKCanvas canvas, int size)
    {
        var plate = new SKRect(PlateInset, PlateInset, Design - PlateInset, Design - PlateInset);

        using (var body = new SKPaint { IsAntialias = true })
        {
            body.Shader = SKShader.CreateLinearGradient(
                new SKPoint(0f, 0f),
                new SKPoint(0f, Design),
                [PlateTop, PlateBottom],
                null,
                SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(plate, PlateRadius, PlateRadius, body);
        }

        if (size < DetailThreshold)
        {
            return;
        }

        // A pool of dim light behind the eye, so the plate is not a flat rectangle.
        using (var glow = new SKPaint { IsAntialias = true })
        {
            glow.Shader = SKShader.CreateRadialGradient(
                new SKPoint(Center, Center),
                Design * 0.44f,
                [GoldMid.WithAlpha(0x2A), GoldMid.WithAlpha(0x00)],
                null,
                SKShaderTileMode.Clamp);
            canvas.DrawRoundRect(plate, PlateRadius, PlateRadius, glow);
        }

        using var edge = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = 3f,
            Color = PlateEdge,
        };
        canvas.DrawRoundRect(SKRect.Inflate(plate, -1.5f, -1.5f), PlateRadius, PlateRadius, edge);
    }

    private static void DrawEye(SKCanvas canvas, int size)
    {
        // Small icons get a slightly wider eye: at 16px the plate's corner radius eats enough of
        // the square that the mark otherwise looks lost in the middle of it.
        float eyeWidth = size < DetailThreshold ? 194f : 182f;
        float unit = eyeWidth / 18f;
        float halfWidth = eyeWidth / 2f;
        float reach = 8.5f * unit;

        using var builder = new SKPathBuilder();
        builder.MoveTo(Center - halfWidth, Center);
        builder.QuadTo(Center, Center - reach, Center + halfWidth, Center);
        builder.QuadTo(Center, Center + reach, Center - halfWidth, Center);
        builder.Close();
        using SKPath outline = builder.Detach();

        // Narrower than the title bar's 4-unit pupil. At 20px wide that ellipse reads as a slit;
        // at 256 the same ratio reads as an oval, and an oval pupil is a cat, not a serpent.
        var pupil = new SKRect(
            Center - (1.7f * unit),
            Center - (4f * unit),
            Center + (1.7f * unit),
            Center + (4f * unit));

        // Gold runs bright at the top and deepens toward the bottom, the way the game's own
        // metal does. Bounded to the eye rather than the plate so the sheen lands on the mark.
        using var gold = SKShader.CreateLinearGradient(
            new SKPoint(0f, Center - reach),
            new SKPoint(0f, Center + reach),
            [GoldLight, GoldMid, GoldDeep],
            [0f, 0.45f, 1f],
            SKShaderTileMode.Clamp);

        if (size >= DetailThreshold)
        {
            using var halo = new SKPaint
            {
                IsAntialias = true,
                Style = SKPaintStyle.Stroke,
                StrokeWidth = 10f,
                Color = GoldMid.WithAlpha(0x33),
                MaskFilter = SKMaskFilter.CreateBlur(SKBlurStyle.Normal, 7f),
            };
            canvas.DrawPath(outline, halo);
        }

        using var stroke = new SKPaint
        {
            IsAntialias = true,
            Style = SKPaintStyle.Stroke,
            StrokeWidth = MathF.Max(12f, MinStrokePixels * Design / size),
            StrokeJoin = SKStrokeJoin.Round,
            Shader = gold,
        };
        canvas.DrawPath(outline, stroke);

        using var fill = new SKPaint { IsAntialias = true, Shader = gold };
        canvas.DrawOval(pupil, fill);
    }
}
