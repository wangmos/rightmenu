using System.Windows;
using System.Windows.Media;

namespace RightMenuMaster.Imaging;

/// <summary>
/// 一个内置小图标的定义：名称、背景色与绘制委托（单位坐标 0..1）。
/// </summary>
public sealed record BuiltinIcon(string Name, string ColorHex, Action<DrawingContext, double, Brush> Draw);

/// <summary>
/// 代码绘制的内置小图标库。全部为扁平化风格：圆角背景 + 白色图形。
/// </summary>
public static class BuiltinIcons
{
    public static IReadOnlyList<BuiltinIcon> All { get; } = CreateAll();

    private static Point P(double x, double y, double s) => new(x * s, y * s);

    private static Pen WhitePen(double s, double w) => Round(new Pen(Brushes.White, s * w));

    private static Pen ColorPen(Brush b, double s, double w) => Round(new Pen(b, s * w));

    private static Pen Round(Pen pen)
    {
        pen.StartLineCap = PenLineCap.Round;
        pen.EndLineCap = PenLineCap.Round;
        pen.LineJoin = PenLineJoin.Round;
        return pen;
    }

    private static Geometry Polygon(double s, params (double x, double y)[] pts)
    {
        var g = new StreamGeometry();
        using (var c = g.Open())
        {
            c.BeginFigure(new Point(pts[0].x * s, pts[0].y * s), true, true);
            for (int i = 1; i < pts.Length; i++)
                c.LineTo(new Point(pts[i].x * s, pts[i].y * s), true, false);
        }
        g.Freeze();
        return g;
    }

    /// <summary>五角星。</summary>
    private static Geometry Star(double s, double cx, double cy, double rOuter, double rInner)
    {
        var pts = new (double, double)[10];
        for (int i = 0; i < 10; i++)
        {
            double r = i % 2 == 0 ? rOuter : rInner;
            double a = -Math.PI / 2 + i * Math.PI / 5;
            pts[i] = (cx + r * Math.Cos(a), cy + r * Math.Sin(a));
        }
        return Polygon(s, pts);
    }

    private static List<BuiltinIcon> CreateAll() => new()
    {
        // 1. 终端
        new BuiltinIcon("终端", "#334155", (dc, s, bg) =>
        {
            dc.DrawLine(WhitePen(s, 0.065), P(0.28, 0.33, s), P(0.46, 0.50, s));
            dc.DrawLine(WhitePen(s, 0.065), P(0.46, 0.50, s), P(0.28, 0.67, s));
            dc.DrawLine(WhitePen(s, 0.065), P(0.54, 0.68, s), P(0.74, 0.68, s));
        }),

        // 2. 文件夹
        new BuiltinIcon("文件夹", "#F59E0B", (dc, s, bg) =>
        {
            dc.DrawGeometry(Brushes.White, null, Polygon(s,
                (0.24, 0.32), (0.43, 0.32), (0.48, 0.39), (0.76, 0.39), (0.76, 0.68), (0.24, 0.68)));
        }),

        // 3. 记事本
        new BuiltinIcon("记事本", "#3B82F6", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.30 * s, 0.24 * s, 0.40 * s, 0.52 * s), 0.04 * s, 0.04 * s);
            foreach (var y in new[] { 0.38, 0.50, 0.62 })
                dc.DrawLine(ColorPen(bg, s, 0.045), P(0.37, y, s), P(0.63, y, s));
        }),

        // 4. 齿轮
        new BuiltinIcon("齿轮", "#64748B", (dc, s, bg) =>
        {
            var c = P(0.5, 0.5, s);
            for (int i = 0; i < 8; i++)
            {
                double a = i * Math.PI / 4;
                var p1 = new Point(c.X + 0.16 * s * Math.Cos(a), c.Y + 0.16 * s * Math.Sin(a));
                var p2 = new Point(c.X + 0.27 * s * Math.Cos(a), c.Y + 0.27 * s * Math.Sin(a));
                dc.DrawLine(WhitePen(s, 0.08), p1, p2);
            }
            dc.DrawEllipse(null, WhitePen(s, 0.09), c, 0.15 * s, 0.15 * s);
        }),

        // 5. 地球
        new BuiltinIcon("地球", "#0EA5E9", (dc, s, bg) =>
        {
            var c = P(0.5, 0.5, s);
            dc.DrawEllipse(null, WhitePen(s, 0.05), c, 0.24 * s, 0.24 * s);
            dc.DrawEllipse(null, WhitePen(s, 0.04), c, 0.10 * s, 0.24 * s);
            dc.DrawLine(WhitePen(s, 0.04), P(0.26, 0.5, s), P(0.74, 0.5, s));
        }),

        // 6. 盾牌
        new BuiltinIcon("盾牌", "#22C55E", (dc, s, bg) =>
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(P(0.5, 0.24, s), true, true);
                ctx.LineTo(P(0.71, 0.32, s), true, false);
                ctx.LineTo(P(0.71, 0.50, s), true, false);
                ctx.BezierTo(P(0.71, 0.64, s), P(0.62, 0.72, s), P(0.5, 0.77, s), true, false);
                ctx.BezierTo(P(0.38, 0.72, s), P(0.29, 0.64, s), P(0.29, 0.50, s), true, false);
                ctx.LineTo(P(0.29, 0.32, s), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(Brushes.White, null, g);
            var pen = ColorPen(bg, s, 0.055);
            dc.DrawLine(pen, P(0.41, 0.50, s), P(0.47, 0.57, s));
            dc.DrawLine(pen, P(0.47, 0.57, s), P(0.60, 0.42, s));
        }),

        // 7. 星星
        new BuiltinIcon("星星", "#EAB308", (dc, s, bg) =>
            dc.DrawGeometry(Brushes.White, null, Star(s, 0.5, 0.53, 0.27, 0.12))),

        // 8. 画笔
        new BuiltinIcon("画笔", "#A855F7", (dc, s, bg) =>
        {
            dc.DrawLine(WhitePen(s, 0.07), P(0.68, 0.28, s), P(0.50, 0.50, s));
            dc.DrawEllipse(Brushes.White, null, P(0.42, 0.59, s), 0.11 * s, 0.11 * s);
        }),

        // 9. 相机
        new BuiltinIcon("相机", "#14B8A6", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.42 * s, 0.27 * s, 0.16 * s, 0.10 * s), 0.03 * s, 0.03 * s);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.26 * s, 0.34 * s, 0.48 * s, 0.36 * s), 0.05 * s, 0.05 * s);
            dc.DrawEllipse(bg, null, P(0.5, 0.52, s), 0.12 * s, 0.12 * s);
            dc.DrawEllipse(Brushes.White, null, P(0.5, 0.52, s), 0.065 * s, 0.065 * s);
        }),

        // 10. 音乐
        new BuiltinIcon("音乐", "#EC4899", (dc, s, bg) =>
        {
            dc.DrawEllipse(Brushes.White, null, P(0.38, 0.66, s), 0.065 * s, 0.05 * s);
            dc.DrawEllipse(Brushes.White, null, P(0.63, 0.61, s), 0.065 * s, 0.05 * s);
            dc.DrawLine(WhitePen(s, 0.05), P(0.443, 0.66, s), P(0.443, 0.34, s));
            dc.DrawLine(WhitePen(s, 0.05), P(0.693, 0.61, s), P(0.693, 0.29, s));
            dc.DrawLine(WhitePen(s, 0.075), P(0.443, 0.345, s), P(0.693, 0.295, s));
        }),

        // 11. 视频
        new BuiltinIcon("视频", "#EF4444", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.26 * s, 0.30 * s, 0.48 * s, 0.40 * s), 0.06 * s, 0.06 * s);
            dc.DrawGeometry(bg, null, Polygon(s, (0.45, 0.40), (0.45, 0.60), (0.62, 0.50)));
        }),

        // 12. 压缩包
        new BuiltinIcon("压缩包", "#92400E", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.30 * s, 0.26 * s, 0.40 * s, 0.48 * s), 0.04 * s, 0.04 * s);
            var pen = ColorPen(bg, s, 0.05);
            pen.DashStyle = new DashStyle(new double[] { 1.1, 0.9 }, 0);
            dc.DrawLine(pen, P(0.5, 0.28, s), P(0.5, 0.62, s));
            dc.DrawEllipse(bg, null, P(0.5, 0.68, s), 0.035 * s, 0.035 * s);
        }),

        // 13. 搜索
        new BuiltinIcon("搜索", "#6366F1", (dc, s, bg) =>
        {
            dc.DrawEllipse(null, WhitePen(s, 0.065), P(0.45, 0.45, s), 0.17 * s, 0.17 * s);
            dc.DrawLine(WhitePen(s, 0.075), P(0.58, 0.58, s), P(0.71, 0.71, s));
        }),

        // 14. 锁
        new BuiltinIcon("锁", "#475569", (dc, s, bg) =>
        {
            var shackle = new StreamGeometry();
            using (var ctx = shackle.Open())
            {
                ctx.BeginFigure(P(0.39, 0.47, s), false, false);
                ctx.ArcTo(P(0.61, 0.47, s), new Size(0.11 * s, 0.11 * s), 0, false, SweepDirection.Clockwise, true, false);
            }
            shackle.Freeze();
            dc.DrawGeometry(null, WhitePen(s, 0.055), shackle);
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.33 * s, 0.46 * s, 0.34 * s, 0.27 * s), 0.04 * s, 0.04 * s);
            dc.DrawEllipse(bg, null, P(0.5, 0.56, s), 0.035 * s, 0.035 * s);
            dc.DrawRectangle(bg, null, new Rect(0.485 * s, 0.57 * s, 0.03 * s, 0.09 * s));
        }),

        // 15. 钥匙
        new BuiltinIcon("钥匙", "#D97706", (dc, s, bg) =>
        {
            dc.DrawEllipse(null, WhitePen(s, 0.06), P(0.38, 0.40, s), 0.11 * s, 0.11 * s);
            dc.DrawLine(WhitePen(s, 0.06), P(0.46, 0.48, s), P(0.68, 0.70, s));
            dc.DrawLine(WhitePen(s, 0.05), P(0.60, 0.62, s), P(0.66, 0.56, s));
            dc.DrawLine(WhitePen(s, 0.05), P(0.68, 0.70, s), P(0.74, 0.64, s));
        }),

        // 16. 火箭
        new BuiltinIcon("火箭", "#F97316", (dc, s, bg) =>
        {
            var body = new StreamGeometry();
            using (var ctx = body.Open())
            {
                ctx.BeginFigure(P(0.5, 0.22, s), true, true);
                ctx.BezierTo(P(0.62, 0.32, s), P(0.63, 0.48, s), P(0.58, 0.62, s), true, false);
                ctx.LineTo(P(0.42, 0.62, s), true, false);
                ctx.BezierTo(P(0.37, 0.48, s), P(0.38, 0.32, s), P(0.5, 0.22, s), true, false);
            }
            body.Freeze();
            dc.DrawGeometry(Brushes.White, null, body);
            dc.DrawGeometry(Brushes.White, null, Polygon(s, (0.42, 0.48), (0.30, 0.66), (0.43, 0.62)));
            dc.DrawGeometry(Brushes.White, null, Polygon(s, (0.58, 0.48), (0.70, 0.66), (0.57, 0.62)));
            dc.DrawEllipse(bg, null, P(0.5, 0.42, s), 0.045 * s, 0.045 * s);
            dc.DrawLine(WhitePen(s, 0.05), P(0.5, 0.67, s), P(0.5, 0.75, s));
        }),

        // 17. 闪电
        new BuiltinIcon("闪电", "#F59E0B", (dc, s, bg) =>
            dc.DrawGeometry(Brushes.White, null, Polygon(s,
                (0.55, 0.22), (0.34, 0.54), (0.47, 0.54), (0.43, 0.78), (0.66, 0.44), (0.52, 0.44)))),

        // 18. 计算器
        new BuiltinIcon("计算器", "#1E40AF", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.30 * s, 0.24 * s, 0.40 * s, 0.52 * s), 0.05 * s, 0.05 * s);
            dc.DrawRoundedRectangle(bg, null, new Rect(0.36 * s, 0.30 * s, 0.28 * s, 0.10 * s), 0.02 * s, 0.02 * s);
            foreach (var x in new[] { 0.39, 0.50, 0.61 })
                foreach (var y in new[] { 0.50, 0.60, 0.70 })
                    dc.DrawEllipse(bg, null, P(x, y, s), 0.028 * s, 0.028 * s);
        }),

        // 19. 时钟
        new BuiltinIcon("时钟", "#06B6D4", (dc, s, bg) =>
        {
            var c = P(0.5, 0.5, s);
            dc.DrawEllipse(null, WhitePen(s, 0.055), c, 0.24 * s, 0.24 * s);
            dc.DrawLine(WhitePen(s, 0.05), c, P(0.5, 0.36, s));
            dc.DrawLine(WhitePen(s, 0.05), c, P(0.61, 0.56, s));
        }),

        // 20. 心形
        new BuiltinIcon("心形", "#E11D48", (dc, s, bg) =>
        {
            var g = new StreamGeometry();
            using (var ctx = g.Open())
            {
                ctx.BeginFigure(P(0.5, 0.73, s), true, true);
                ctx.BezierTo(P(0.30, 0.58, s), P(0.25, 0.47, s), P(0.30, 0.38, s), true, false);
                ctx.BezierTo(P(0.34, 0.30, s), P(0.45, 0.30, s), P(0.5, 0.39, s), true, false);
                ctx.BezierTo(P(0.55, 0.30, s), P(0.66, 0.30, s), P(0.70, 0.38, s), true, false);
                ctx.BezierTo(P(0.75, 0.47, s), P(0.70, 0.58, s), P(0.5, 0.73, s), true, false);
            }
            g.Freeze();
            dc.DrawGeometry(Brushes.White, null, g);
        }),

        // 21. 代码
        new BuiltinIcon("代码", "#0F172A", (dc, s, bg) =>
        {
            dc.DrawLine(WhitePen(s, 0.06), P(0.38, 0.36, s), P(0.26, 0.50, s));
            dc.DrawLine(WhitePen(s, 0.06), P(0.26, 0.50, s), P(0.38, 0.64, s));
            dc.DrawLine(WhitePen(s, 0.06), P(0.62, 0.36, s), P(0.74, 0.50, s));
            dc.DrawLine(WhitePen(s, 0.06), P(0.74, 0.50, s), P(0.62, 0.64, s));
            dc.DrawLine(WhitePen(s, 0.05), P(0.55, 0.32, s), P(0.45, 0.68, s));
        }),

        // 22. 窗口
        new BuiltinIcon("窗口", "#2563EB", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(null, WhitePen(s, 0.05), new Rect(0.26 * s, 0.30 * s, 0.48 * s, 0.40 * s), 0.04 * s, 0.04 * s);
            dc.DrawLine(WhitePen(s, 0.045), P(0.265, 0.42, s), P(0.735, 0.42, s));
        }),

        // 23. 数据库
        new BuiltinIcon("数据库", "#6B7280", (dc, s, bg) =>
        {
            var pen = WhitePen(s, 0.045);
            dc.DrawEllipse(null, pen, P(0.5, 0.32, s), 0.20 * s, 0.07 * s);
            dc.DrawLine(pen, P(0.30, 0.32, s), P(0.30, 0.66, s));
            dc.DrawLine(pen, P(0.70, 0.32, s), P(0.70, 0.66, s));
            var bottom = new StreamGeometry();
            using (var ctx = bottom.Open())
            {
                ctx.BeginFigure(P(0.30, 0.66, s), false, false);
                ctx.ArcTo(P(0.70, 0.66, s), new Size(0.20 * s, 0.07 * s), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            bottom.Freeze();
            dc.DrawGeometry(null, pen, bottom);
            var middle = new StreamGeometry();
            using (var ctx = middle.Open())
            {
                ctx.BeginFigure(P(0.30, 0.49, s), false, false);
                ctx.ArcTo(P(0.70, 0.49, s), new Size(0.20 * s, 0.07 * s), 0, false, SweepDirection.Counterclockwise, true, false);
            }
            middle.Freeze();
            dc.DrawGeometry(null, pen, middle);
        }),

        // 24. 邮件
        new BuiltinIcon("邮件", "#DC2626", (dc, s, bg) =>
        {
            dc.DrawRoundedRectangle(Brushes.White, null, new Rect(0.26 * s, 0.32 * s, 0.48 * s, 0.36 * s), 0.04 * s, 0.04 * s);
            var pen = ColorPen(bg, s, 0.045);
            dc.DrawLine(pen, P(0.29, 0.36, s), P(0.5, 0.53, s));
            dc.DrawLine(pen, P(0.5, 0.53, s), P(0.71, 0.36, s));
        }),

        // 25. 下载
        new BuiltinIcon("下载", "#16A34A", (dc, s, bg) =>
        {
            dc.DrawLine(WhitePen(s, 0.06), P(0.5, 0.26, s), P(0.5, 0.56, s));
            dc.DrawLine(WhitePen(s, 0.06), P(0.37, 0.45, s), P(0.5, 0.58, s));
            dc.DrawLine(WhitePen(s, 0.06), P(0.63, 0.45, s), P(0.5, 0.58, s));
            var tray = new StreamGeometry();
            using (var ctx = tray.Open())
            {
                ctx.BeginFigure(P(0.30, 0.64, s), false, false);
                ctx.LineTo(P(0.30, 0.72, s), true, false);
                ctx.LineTo(P(0.70, 0.72, s), true, false);
                ctx.LineTo(P(0.70, 0.64, s), true, false);
            }
            tray.Freeze();
            dc.DrawGeometry(null, WhitePen(s, 0.055), tray);
        }),

        // 26. 设置滑块
        new BuiltinIcon("设置滑块", "#7C3AED", (dc, s, bg) =>
        {
            foreach (var y in new[] { 0.35, 0.50, 0.65 })
                dc.DrawLine(WhitePen(s, 0.045), P(0.28, y, s), P(0.72, y, s));
            foreach (var (x, y) in new[] { (0.60, 0.35), (0.40, 0.50), (0.65, 0.65) })
            {
                dc.DrawEllipse(Brushes.White, null, P(x, y, s), 0.06 * s, 0.06 * s);
                dc.DrawEllipse(bg, null, P(x, y, s), 0.022 * s, 0.022 * s);
            }
        }),
    };

    /// <summary>
    /// 把内置图标渲染为指尺寸的图片。
    /// </summary>
    public static System.Windows.Media.Imaging.BitmapSource Render(BuiltinIcon icon, int size)
    {
        var color = (Color)ColorConverter.ConvertFromString(icon.ColorHex);
        var brush = new SolidColorBrush(color);
        brush.Freeze();

        var visual = new DrawingVisual();
        using (var dc = visual.RenderOpen())
        {
            dc.DrawRoundedRectangle(brush, null, new Rect(0, 0, size, size), size * 0.18, size * 0.18);
            icon.Draw(dc, size, brush);
        }

        var rtb = new System.Windows.Media.Imaging.RenderTargetBitmap(size, size, 96, 96, PixelFormats.Pbgra32);
        rtb.Render(visual);
        rtb.Freeze();
        return rtb;
    }
}
