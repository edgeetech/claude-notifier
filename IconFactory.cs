using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace ClaudeNotifier;

// Generates a tray icon at runtime: amber "spark" with surrounding particles
// on a transparent background. No external assets needed.
public static class IconFactory
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool DestroyIcon(IntPtr hIcon);

    public static Icon Create(int size = 32)
    {
        using var bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (var g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.CompositingQuality = CompositingQuality.HighQuality;
            g.Clear(Color.Transparent);

            float cx = size / 2f, cy = size / 2f;
            float scale = size / 32f;

            // Outer punchy glow halo
            using (var halo = new GraphicsPath())
            {
                halo.AddEllipse(cx - 15 * scale, cy - 15 * scale, 30 * scale, 30 * scale);
                using var pb = new PathGradientBrush(halo)
                {
                    CenterColor = Color.FromArgb(200, 0xFF, 0x6B, 0x00),
                    SurroundColors = new[] { Color.FromArgb(0, 0xFF, 0x6B, 0x00) }
                };
                g.FillPath(pb, halo);
            }

            // 4-point spark — vivid orange, larger
            DrawSpark(g, cx, cy, 13f * scale, 4.2f * scale,
                Color.FromArgb(255, 0xFF, 0xC1, 0x07),    // bright amber
                Color.FromArgb(255, 0xFF, 0x44, 0x00));   // deep orange-red

            // Diagonal smaller spark
            DrawSpark(g, cx + 8 * scale, cy - 8 * scale, 5.5f * scale, 1.8f * scale,
                Color.FromArgb(255, 0xFF, 0xEB, 0x3B),
                Color.FromArgb(255, 0xFF, 0x6F, 0x00), 45f);

            // Particles
            DrawParticle(g, cx - 10 * scale, cy + 10 * scale, 2.0f * scale, Color.FromArgb(255, 0xFF, 0xA0, 0x00));
            DrawParticle(g, cx + 11 * scale, cy + 7 * scale, 1.5f * scale, Color.FromArgb(255, 0xFF, 0xC1, 0x07));
            DrawParticle(g, cx - 12 * scale, cy - 6 * scale, 1.6f * scale, Color.FromArgb(255, 0xFF, 0xEB, 0x3B));
        }

        // Convert Bitmap → Icon. Use bitmap's GetHicon then create managed Icon copy.
        IntPtr hicon = bmp.GetHicon();
        try
        {
            using var tmp = Icon.FromHandle(hicon);
            // Clone so we can free the HICON
            return (Icon)tmp.Clone();
        }
        finally
        {
            DestroyIcon(hicon);
        }
    }

    private static void DrawSpark(Graphics g, float cx, float cy, float length, float width,
                                   Color light, Color dark, float rotateDeg = 0)
    {
        var state = g.Save();
        g.TranslateTransform(cx, cy);
        if (rotateDeg != 0) g.RotateTransform(rotateDeg);

        using var path = new GraphicsPath();
        // 4-point star: diamond made of two ellipses
        path.AddPolygon(new[]
        {
            new PointF(0, -length),
            new PointF(width, 0),
            new PointF(0, length),
            new PointF(-width, 0),
        });
        path.AddPolygon(new[]
        {
            new PointF(-length, 0),
            new PointF(0, -width),
            new PointF(length, 0),
            new PointF(0, width),
        });

        using var brush = new LinearGradientBrush(
            new PointF(-length, -length), new PointF(length, length),
            light, dark);
        g.FillPath(brush, path);

        // Dark outline for contrast on light backgrounds
        using var pen = new Pen(Color.FromArgb(180, 0x40, 0x10, 0x00), Math.Max(1f, width * 0.18f))
        { LineJoin = LineJoin.Round };
        g.DrawPath(pen, path);

        g.Restore(state);
    }

    private static void DrawParticle(Graphics g, float cx, float cy, float radius, Color color)
    {
        using var path = new GraphicsPath();
        path.AddEllipse(cx - radius, cy - radius, radius * 2, radius * 2);
        using var pgb = new PathGradientBrush(path)
        {
            CenterColor = color,
            SurroundColors = new[] { Color.FromArgb(0, color) }
        };
        g.FillEllipse(pgb, cx - radius, cy - radius, radius * 2, radius * 2);
        using var core = new SolidBrush(color);
        g.FillEllipse(core, cx - radius * 0.55f, cy - radius * 0.55f, radius * 1.1f, radius * 1.1f);
    }
}
