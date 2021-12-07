﻿using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using System;
using System.Linq;
using Windows.Foundation;

namespace FluentStore.Controls
{
    public class UniformColumnPanel : Panel
    {
        double[] RowHeights;
        double ActualColumnWidth;
        double TotalColumnSpacing => (ColumnCount - 1) * ColumnSpacing;
        public int RowCount { get; private set; }
        public int ColumnCount { get; private set; }

        public double DesiredItemWidth
        {
            get => (double)GetValue(DesiredItemWidthProperty);
            set => SetValue(DesiredItemWidthProperty, value);
        }
        public static readonly DependencyProperty DesiredItemWidthProperty = DependencyProperty.Register(
            nameof(DesiredItemWidth), typeof(double), typeof(UniformColumnPanel), new PropertyMetadata(double.MaxValue));

        public double RowSpacing
        {
            get => (double)GetValue(RowProperty);
            set => SetValue(RowProperty, value);
        }
        public static readonly DependencyProperty RowProperty = DependencyProperty.Register(
            nameof(RowSpacing), typeof(double), typeof(UniformColumnPanel), new PropertyMetadata(0d));

        public double ColumnSpacing
        {
            get => (double)GetValue(ColumnSpacingProperty);
            set => SetValue(ColumnSpacingProperty, value);
        }
        public static readonly DependencyProperty ColumnSpacingProperty = DependencyProperty.Register(
            nameof(ColumnSpacing), typeof(double), typeof(UniformColumnPanel), new PropertyMetadata(0d));

        protected override Size MeasureOverride(Size availableSize)
        {
            Size returnSize = new(availableSize.Width, 0);

            // What's the maximum number of items that will fit on a row?
            if (DesiredItemWidth + ColumnSpacing >= availableSize.Width)
            {
                // Cannot meet desired width, use one column
                ColumnCount = 1;
                RowCount = Children.Count;
            }
            else
            {
                ColumnCount = Math.Min((int)(availableSize.Width / (DesiredItemWidth + ColumnSpacing)), Children.Count);
                RowCount = (int)Math.Ceiling((double)Children.Count / ColumnCount);
            }

            // Now stretch items to fill length of row
            ActualColumnWidth = (availableSize.Width - TotalColumnSpacing) / ColumnCount;

            // Loop through each Child, call Measure on each
            RowHeights = new double[RowCount];
            Size itemTargetSize = new(ActualColumnWidth, double.MaxValue);
            for (int i = 0; i < Children.Count; i++)
            {
                int row = i / ColumnCount;
                UIElement child = Children[i];

                child.Measure(itemTargetSize);
                Size childDesiredSize = child.DesiredSize;

                RowHeights[row] = Math.Max(RowHeights[row], childDesiredSize.Height) + RowSpacing;
            }

            // Handle horizontal spacing
            RowHeights[^1] -= RowSpacing;
            returnSize.Height = RowHeights.Sum();

            return returnSize;
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            for (int i = 0; i < Children.Count; i++)
            {
                int row = i / ColumnCount;
                int col = i % ColumnCount;
                UIElement child = Children[i];

                // Place child
                Point anchorPoint = new(col * (ActualColumnWidth + ColumnSpacing), RowHeights.Take(row).Sum());

                child.Arrange(new Rect(anchorPoint, child.DesiredSize));
            }
            return finalSize;
        }
    }
}
