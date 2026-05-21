using System;
using System.Collections.Generic;
using System.Windows;
using System.Windows.Controls;

namespace Aquila.Controls
{
    /// <summary>
    /// Flex-wrap panel: equal-width columns, top-aligned cards.
    /// Column count = floor((available + Gap) / (MinCardWidth + Gap)), minimum 1.
    /// Collapsed children are excluded from layout.
    /// </summary>
    public class ResponsiveCardPanel : Panel
    {
        public static readonly DependencyProperty GapProperty =
            DependencyProperty.Register(nameof(Gap), typeof(double), typeof(ResponsiveCardPanel),
                new FrameworkPropertyMetadata(8.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsArrange));

        public static readonly DependencyProperty MinCardWidthProperty =
            DependencyProperty.Register(nameof(MinCardWidth), typeof(double), typeof(ResponsiveCardPanel),
                new FrameworkPropertyMetadata(280.0,
                    FrameworkPropertyMetadataOptions.AffectsMeasure |
                    FrameworkPropertyMetadataOptions.AffectsArrange));

        public double Gap         { get => (double)GetValue(GapProperty);         set => SetValue(GapProperty, value); }
        public double MinCardWidth{ get => (double)GetValue(MinCardWidthProperty); set => SetValue(MinCardWidthProperty, value); }

        private int ColumnCount(double width) =>
            Math.Max(1, (int)((width + Gap) / (MinCardWidth + Gap)));

        protected override Size MeasureOverride(Size availableSize)
        {
            double available = double.IsInfinity(availableSize.Width) ? MinCardWidth : availableSize.Width;
            int    cols      = ColumnCount(available);
            double gap       = Gap;
            double colW      = Math.Max(0, (available - cols * gap) / cols);
            var    constraint = new Size(colW, double.PositiveInfinity);

            foreach (UIElement child in InternalChildren)
                child.Measure(constraint);

            double totalH = 0, rowMax = 0;
            int col = 0;

            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed) continue;
                if (child.DesiredSize.Height > rowMax) rowMax = child.DesiredSize.Height;
                if (++col == cols) { totalH += rowMax + gap; rowMax = 0; col = 0; }
            }

            if (col > 0) totalH += rowMax + gap;

            return new Size(available, totalH);
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            double available = finalSize.Width;
            int    cols      = ColumnCount(available);
            double gap       = Gap;
            double colW      = Math.Max(0, (available - cols * gap) / cols);

            var visible = new List<UIElement>();
            foreach (UIElement child in InternalChildren)
            {
                if (child.Visibility == Visibility.Collapsed)
                    child.Arrange(new Rect(0, 0, 0, 0));
                else
                    visible.Add(child);
            }

            // Group into rows, track tallest per row for y advancement
            var rows = new List<(int start, int count, double rowH)>();
            int i = 0;
            while (i < visible.Count)
            {
                int start = i, count = 0;
                double rowMax = 0;
                for (int c = 0; c < cols && i < visible.Count; c++, i++, count++)
                {
                    double h = visible[i].DesiredSize.Height;
                    if (h > rowMax) rowMax = h;
                }
                rows.Add((start, count, rowMax));
            }

            // Stretch: all cards in the same row share the row height (tallest wins)
            double y = 0;
            foreach (var (start, count, rowH) in rows)
            {
                double x = 0;
                for (int c = 0; c < count; c++)
                {
                    visible[start + c].Arrange(new Rect(x, y, colW, rowH));
                    x += colW + gap;
                }
                y += rowH + gap;
            }

            return finalSize;
        }
    }
}
