﻿using Playnite.Common;
using Playnite.SDK;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace Playnite.FullscreenApp.Controls
{
    public class InternalChildrenGeneratedArgs
    {
    }

    public class FullscreenTilePanel : VirtualizingPanel, IScrollInfo
    {
        private static ILogger logger = LogManager.GetLogger();
        internal ItemsControl itemsControl => ItemsControl.GetItemsOwner(this);
        private int itemCount => itemsControl?.HasItems == true ? itemsControl.Items.Count : 0;
        private double itemWidth;
        private double itemHeight;
        private double centerMargin;
        private int computedRows;
        private int computedColumns;
        private const double marginOffset = 0.5;

        public event EventHandler<InternalChildrenGeneratedArgs> InternalChildrenGenerated;

        private void OnInternalChildrenGenerated()
        {
            InternalChildrenGenerated?.Invoke(this, new InternalChildrenGeneratedArgs());
        }

        private IRecyclingItemContainerGenerator itemContainerGenerator;
        protected IRecyclingItemContainerGenerator generator
        {
            get
            {
                if (itemContainerGenerator is null)
                {
                    /* Because of a bug in the framework the ItemContainerGenerator
                     * is null until InternalChildren accessed at least one time. */
                    var children = InternalChildren;
                    itemContainerGenerator = (IRecyclingItemContainerGenerator)base.ItemContainerGenerator;
                }
                return itemContainerGenerator;
            }
        }

        public int Columns
        {
            get
            {
                return (int)GetValue(ColumnsProperty);
            }

            set
            {
                SetValue(ColumnsProperty, value);
            }
        }

        public static DependencyProperty VerticalOffsetProperty = DependencyProperty.RegisterAttached(
            "VerticalOffset",
            typeof(double),
            typeof(FullscreenTilePanel),
            new PropertyMetadata(0.0, OnVerticalOffsetChanged));

        public static void SetVerticalOffset(FrameworkElement target, double value)
        {
            target.SetValue(VerticalOffsetProperty, value);
        }

        public static double GetVerticalOffset(FrameworkElement target)
        {
            return (double)target.GetValue(VerticalOffsetProperty);
        }

        private static void OnVerticalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FullscreenTilePanel panel)
            {
                panel.SetVerticalOffset((double)e.NewValue);
            }
        }

        public static DependencyProperty HorizontalOffsetProperty = DependencyProperty.RegisterAttached(
            "HorizontalOffset",
            typeof(double),
            typeof(FullscreenTilePanel),
            new PropertyMetadata(0.0, OnHorizontalOffsetChanged));

        public static void SetHorizontalOffset(FrameworkElement target, double value)
        {
            target.SetValue(HorizontalOffsetProperty, value);
        }

        public static double GetHorizontalOffset(FrameworkElement target)
        {
            return (double)target.GetValue(HorizontalOffsetProperty);
        }

        private static void OnHorizontalOffsetChanged(DependencyObject target, DependencyPropertyChangedEventArgs e)
        {
            if (target is FullscreenTilePanel panel)
            {
                panel.SetHorizontalOffset((double)e.NewValue);
            }
        }

        public static readonly DependencyProperty SmoothScrollEnabledProperty = DependencyProperty.RegisterAttached(
            nameof(SmoothScrollEnabled),
            typeof(bool),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(false));

        public bool SmoothScrollEnabled
        {
            get
            {
                return (bool)GetValue(SmoothScrollEnabledProperty);
            }

            set
            {
                SetValue(SmoothScrollEnabledProperty, value);
            }
        }

        public static readonly DependencyProperty ColumnsProperty = DependencyProperty.RegisterAttached(
            nameof(Columns),
            typeof(int),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(4, OnLayoutPropertyChanged));

        public AspectRatio ItemAspectRatio
        {
            get
            {
                return (AspectRatio)GetValue(ItemAspectRatioProperty);
            }

            set
            {
                SetValue(ItemAspectRatioProperty, value);
            }
        }

        public static readonly DependencyProperty ItemAspectRatioProperty = DependencyProperty.RegisterAttached(
            nameof(ItemAspectRatio),
            typeof(AspectRatio),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(new AspectRatio(92, 43), OnLayoutPropertyChanged));

        public bool UseHorizontalLayout
        {
            get
            {
                return (bool)GetValue(UseHorizontalLayoutProperty);
            }

            set
            {
                SetValue(UseHorizontalLayoutProperty, value);
            }
        }

        public static readonly DependencyProperty UseHorizontalLayoutProperty = DependencyProperty.RegisterAttached(
            nameof(UseHorizontalLayout),
            typeof(bool),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(false, OnLayoutPropertyChanged));

        public int Rows
        {
            get
            {
                return (int)GetValue(RowsProperty);
            }

            set
            {
                SetValue(RowsProperty, value);
            }
        }

        public static readonly DependencyProperty RowsProperty = DependencyProperty.RegisterAttached(
            nameof(Rows),
            typeof(int),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(4, OnLayoutPropertyChanged));

        public int ItemSpacing
        {
            get
            {
                return (int)GetValue(ItemSpacingProperty);
            }

            set
            {
                SetValue(ItemSpacingProperty, value);
            }
        }

        public static readonly DependencyProperty ItemSpacingProperty = DependencyProperty.RegisterAttached(
            nameof(ItemSpacing),
            typeof(int),
            typeof(FullscreenTilePanel),
            new FrameworkPropertyMetadata(10, OnLayoutPropertyChanged));

        // This has to be lower than key delay from ListBoxEx, because layout will get desynced otherwise.
        // TODO: fix the desync.
        private readonly TimeSpan animationLength = new TimeSpan(0, 0, 0, 0, 140);
        private readonly DoubleAnimation scrollAnimation = new DoubleAnimation();

        public FullscreenTilePanel() : base()
        {
            RenderTransform = trans;
        }

        private static void OnLayoutPropertyChanged(DependencyObject obj, DependencyPropertyChangedEventArgs e)
        {
            var panel = obj as FullscreenTilePanel;
            if (panel.itemsControl == null)
            {
                return;
            }

            panel.InvalidateMeasure();
            panel.ScrollOwner?.InvalidateScrollInfo();
        }
        protected override Size MeasureOverride(Size availableSize)
        {
            if (itemsControl == null)
            {
                return new Size(0, 0);
            }

            UpdateScrollInfo(availableSize);
            GetVisibleRange(out var firstItemIndex, out var lastItemIndex);
            if (lastItemIndex < 0)
            {
                return new Size(0, 0);
            }

            var startPos = generator.GeneratorPositionFromIndex(firstItemIndex);
            var childIndex = (startPos.Offset == 0) ? startPos.Index : startPos.Index + 1;
            var anyItemsGenerated = false;
            using (generator.StartAt(startPos, GeneratorDirection.Forward, true))
            {
                for (int itemIndex = firstItemIndex; itemIndex <= lastItemIndex; ++itemIndex, ++childIndex)
                {
                    UIElement child = generator.GenerateNext(out var newlyRealized) as UIElement;
                    if (child == null)
                    {
                        continue;
                    }

                    if (newlyRealized)
                    {
                        if (childIndex >= InternalChildren.Count)
                        {
                            AddInternalChild(child);
                        }
                        else
                        {
                            InsertInternalChild(childIndex, child);
                        }

                        generator.PrepareItemContainer(child);
                        anyItemsGenerated = true;
                    }
                    else if (!InternalChildren.Contains(child))
                    {
                        InsertInternalChild(childIndex, child);
                        ItemContainerGenerator.PrepareItemContainer(child);
                        anyItemsGenerated = true;
                    }

                    child.Measure(new Size(itemWidth, itemHeight));
                }
            }

            CleanUpItems(firstItemIndex, lastItemIndex);
            if (anyItemsGenerated)
            {
                OnInternalChildrenGenerated();
            }

            if (double.IsPositiveInfinity(availableSize.Width) || double.IsPositiveInfinity(availableSize.Height))
            {
                return GetExtent();
            }
            else
            {
                return availableSize;
            }
        }

        protected override Size ArrangeOverride(Size finalSize)
        {
            UpdateScrollInfo(finalSize);

            for (int i = 0; i < Children.Count; i++)
            {
                var child = Children[i];
                var itemIndex = generator.IndexFromGeneratorPosition(new GeneratorPosition(i, 0));
                if (itemIndex < 0)
                {
                    continue;
                }

                child.Arrange(GetItemRect(itemIndex));
            }

            return finalSize;
        }

        private Rect GetItemRect(int itemIndex)
        {
            if (UseHorizontalLayout)
            {
                if (computedRows == 0)
                    return new Rect(
                        itemWidth * 0.25,
                        centerMargin,
                        itemWidth,
                        itemHeight);

                var row = itemIndex % computedRows;
                var column = itemIndex < row ? 0 : (int)Math.Floor(itemIndex / (double)computedRows);
                return new Rect(
                    (column * itemWidth) + (itemWidth * 0.25),
                    centerMargin + (row * itemHeight),
                    itemWidth,
                    itemHeight);
            }
            else
            {
                if (computedColumns == 0)
                    return new Rect(
                        centerMargin,
                        (marginOffset / 2) * itemHeight,
                        itemWidth,
                        itemHeight);

                var column = itemIndex % computedColumns;
                var row = itemIndex < column ? 0 : (int)Math.Floor(itemIndex / (double)computedColumns);
                return new Rect(
                    centerMargin + (column * itemWidth),
                    (row * itemHeight) + ((marginOffset / 2) * itemHeight),
                    itemWidth,
                    itemHeight);
            }
        }

        private void GetVisibleRange(out int firstIndex, out int lastIndex)
        {
            if (itemCount == 0)
            {
                firstIndex = -1;
                lastIndex = -1;
                return;
            }

            if (UseHorizontalLayout)
            {
                var previousColumns = (int)Math.Ceiling((offset.X - (itemWidth * (marginOffset / 2))) / itemWidth);
                firstIndex = (int)Math.Ceiling((double)previousColumns * computedRows) - computedRows;
                if (firstIndex < 0)
                {
                    firstIndex = 0;
                }

                lastIndex = (previousColumns * computedRows) + (computedRows * (Columns + 1)) - 1;
                if (lastIndex >= itemCount)
                {
                    lastIndex = itemCount;
                }
            }
            else
            {
                var previousRows = (int)Math.Ceiling((offset.Y - (itemHeight * (marginOffset / 2))) / itemHeight);
                firstIndex = (int)Math.Ceiling((double)previousRows * computedColumns) - computedColumns;
                if (firstIndex < 0)
                {
                    firstIndex = 0;
                }

                lastIndex = (previousRows * computedColumns) + (computedColumns * (Rows + 1)) - 1;
                if (lastIndex >= itemCount)
                {
                    lastIndex = itemCount;
                }
            }
        }

        private void CleanUpItems(int firstIndex, int lastIndex)
        {
            for (int i = InternalChildren.Count - 1; i >= 0; i--)
            {
                GeneratorPosition childGeneratorPos = new GeneratorPosition(i, 0);
                int itemIndex = generator.IndexFromGeneratorPosition(childGeneratorPos);
                var child = InternalChildren[i];
                if ((itemIndex < firstIndex || itemIndex > lastIndex) && itemIndex > 0)
                {
                    generator.Recycle(childGeneratorPos, 1);
                    RemoveInternalChildRange(i, 1);
                }
                else if (child.ToString().Contains("{DisconnectedItem}"))
                {
                    try
                    {
                        generator.Remove(childGeneratorPos, 1);
                    }
                    catch (Exception e)
                    {
                        // Looks like some issue in WPF.
                        // This sometimes throws "null reference" even when the items still exists.
                        logger.Error(e, "Cleaning up DisconnectedItem failed.");
                    }

                    RemoveInternalChildRange(i, 1);
                }
            }
        }

        protected override void OnItemsChanged(object sender, ItemsChangedEventArgs args)
        {
            switch (args.Action)
            {
                case NotifyCollectionChangedAction.Remove:
                case NotifyCollectionChangedAction.Replace:
                    RemoveInternalChildRange(args.Position.Index, args.ItemUICount);
                    break;
                case NotifyCollectionChangedAction.Move:
                    RemoveInternalChildRange(args.OldPosition.Index, args.ItemUICount);
                    break;
            }
        }

        internal Size GetExtent()
        {
            if (itemCount == 0)
            {
                return new Size(0, 0);
            }

            if (UseHorizontalLayout)
            {
                if (computedRows == 0)
                    return new Size(centerMargin * 2, viewport.Height);

                var totalColumns = (int)Math.Ceiling(itemCount / (double)computedRows);
                return new Size(
                    (totalColumns * itemWidth) + (centerMargin * 2),
                    viewport.Height);
            }
            else
            {
                if (computedColumns == 0)
                    return new Size(viewport.Width, 0);

                var totalRows = (int)Math.Ceiling(itemCount / (double)computedColumns);
                return new Size(
                    viewport.Width,
                    (totalRows + marginOffset) * itemHeight);
            }
        }

        internal double GetItemHeight()
        {
            if (UseHorizontalLayout)
            {
                return ItemAspectRatio.GetHeight(GetItemWidth());
            }
            else
            {
                return viewport.Height / (Rows + marginOffset);
            }
        }

        internal double GetItemWidth()
        {
            if (UseHorizontalLayout)
            {
                return viewport.Width / (Columns + marginOffset);
            }
            else
            {
                return ItemAspectRatio.GetWidth(GetItemHeight());
            }
        }

        internal double GetCenterMargin()
        {
            if (UseHorizontalLayout)
            {
                return (viewport.Height - (computedRows * itemHeight)) / 2;
            }
            else
            {
                return (viewport.Width - (computedColumns * itemWidth)) / 2;
            }
        }

        #region IScrollInfo

        private Size viewport = new Size(0, 0);
        private Point offset = new Point(0, 0);
        private Size extent = new Size(0, 0);
        private TranslateTransform trans = new TranslateTransform();

        public bool CanVerticallyScroll { get; set; } = false;
        public bool CanHorizontallyScroll { get; set; } = false;
        public double ExtentWidth => extent.Width;
        public double ExtentHeight => extent.Height;
        public double ViewportWidth => viewport.Width;
        public double ViewportHeight => viewport.Height;
        public double HorizontalOffset => offset.X;
        public double VerticalOffset => offset.Y;
        public ScrollViewer ScrollOwner { get; set; }

        internal void UpdateScrollInfo(Size availableSize)
        {
            if (availableSize != viewport)
            {
                viewport = availableSize;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (UseHorizontalLayout)
            {
                itemHeight = GetItemHeight();
                if (itemHeight > viewport.Height)
                {
                    itemHeight = viewport.Height;
                    itemWidth = ItemAspectRatio.GetWidth(itemHeight);
                }
                else
                {
                    itemWidth = GetItemWidth();
                }

                computedRows = (int)Math.Floor(viewport.Height / itemHeight);
            }
            else
            {
                itemWidth = GetItemWidth();
                if (itemWidth > viewport.Width)
                {
                    itemWidth = viewport.Width;
                    itemHeight = ItemAspectRatio.GetHeight(itemWidth);
                }
                else
                {
                    itemHeight = GetItemHeight();
                }

                computedColumns = (int)Math.Floor(viewport.Width / itemWidth);
            }

            centerMargin = GetCenterMargin();

            var newExtent = GetExtent();
            if (extent != newExtent)
            {
                extent = newExtent;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (offset.Y > extent.Height)
            {
                offset.Y = 0;
                trans.Y = 0;
                ScrollOwner?.InvalidateScrollInfo();
            }

            if (offset.X > extent.Width)
            {
                offset.X = 0;
                trans.X = 0;
                ScrollOwner?.InvalidateScrollInfo();
            }
        }

        public void BeginAnimation(double from, double to, TimeSpan speed, DependencyProperty property)
        {
            scrollAnimation.From = from;
            scrollAnimation.To = to;
            scrollAnimation.Duration = new Duration(speed);
            BeginAnimation(property, scrollAnimation);
        }

        public void SetHorizontalOffset(double newOffset)
        {
            if (newOffset < 0 || viewport.Width >= extent.Width)
            {
                newOffset = 0;
            }
            else
            {
                if (newOffset + viewport.Width >= extent.Width)
                {
                    newOffset = extent.Width - viewport.Width;
                }
            }

            offset.X = newOffset;
            trans.X = -newOffset;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public void SetVerticalOffset(double newOffset)
        {
            if (newOffset < 0 || viewport.Height >= extent.Height)
            {
                newOffset = 0;
            }
            else
            {
                if (newOffset + viewport.Height >= extent.Height)
                {
                    newOffset = extent.Height - viewport.Height;
                }
            }

            offset.Y = newOffset;
            trans.Y = -newOffset;
            ScrollOwner?.InvalidateScrollInfo();
            InvalidateMeasure();
        }

        public void ScrollToItem(object item)
        {
            var index = itemsControl.Items.IndexOf(item);
            var itemRect = GetItemRect(index);
            if (UseHorizontalLayout)
            {
                SetHorizontalOffset(itemRect.X);
            }
            else
            {
                SetVerticalOffset(itemRect.Y);
            }
        }

        public Rect MakeVisible(Visual visual, Rect rectangle)
        {
            var index = ((ItemContainerGenerator)generator).IndexFromContainer(visual);
            if (index < 0)
            {
                return rectangle;
            }

            var itemRect = GetItemRect(index);
            if (UseHorizontalLayout)
            {
                var movedViewport = new Rect(offset.X, 0, offset.X + viewport.Width, 0);
                if (itemRect.X > movedViewport.X && itemRect.X + itemWidth < movedViewport.Width)
                {
                    return rectangle;
                }
                else if (itemRect.X > movedViewport.X && itemRect.X + itemWidth > movedViewport.Width && itemRect.X < movedViewport.Width)
                {
                    LineRight();
                    return rectangle;
                }
                else if (itemRect.X < movedViewport.X && itemRect.X + itemWidth > movedViewport.X)
                {
                    LineLeft();
                    return rectangle;
                }
            }
            else
            {
                var movedViewport = new Rect(0, offset.Y, 0, offset.Y + viewport.Height);
                if (itemRect.Y > movedViewport.Y && itemRect.Y + itemHeight < movedViewport.Height)
                {
                    return rectangle;
                }
                else if (itemRect.Y > movedViewport.Y && itemRect.Y + itemHeight > movedViewport.Height && itemRect.Y < movedViewport.Height)
                {
                    LineDown();
                    return rectangle;
                }
                else if (itemRect.Y < movedViewport.Y && itemRect.Y + itemHeight > movedViewport.Y)
                {
                    LineUp();
                    return rectangle;
                }
            }

            return rectangle;
        }

        public void LineLeft()
        {
            if (SmoothScrollEnabled)
            {
                BeginAnimation(HorizontalOffset, HorizontalOffset - itemWidth, animationLength, HorizontalOffsetProperty);
            }
            else
            {
                SetHorizontalOffset(HorizontalOffset - itemWidth);
            }
        }

        public void LineRight()
        {
            if (SmoothScrollEnabled)
            {
                BeginAnimation(HorizontalOffset, HorizontalOffset + itemWidth, animationLength, HorizontalOffsetProperty);
            }
            else
            {
                SetHorizontalOffset(HorizontalOffset + itemWidth);
            }
        }

        public void LineUp()
        {
            if (SmoothScrollEnabled)
            {
                BeginAnimation(VerticalOffset, VerticalOffset - itemHeight, animationLength, VerticalOffsetProperty);
            }
            else
            {
                SetVerticalOffset(VerticalOffset - itemHeight);
            }
        }

        public void LineDown()
        {
            if (SmoothScrollEnabled)
            {
                BeginAnimation(VerticalOffset, VerticalOffset + itemHeight, animationLength, VerticalOffsetProperty);
            }
            else
            {
                SetVerticalOffset(VerticalOffset + itemHeight);
            }
        }

        public void MouseWheelDown()
        {
        }

        public void MouseWheelLeft()
        {
        }

        public void MouseWheelRight()
        {
        }

        public void MouseWheelUp()
        {
        }

        public void PageLeft()
        {
            SetHorizontalOffset(HorizontalOffset - (itemWidth * Columns));
        }

        public void PageRight()
        {
            SetHorizontalOffset(HorizontalOffset + (itemWidth * Columns));
        }

        public void PageUp()
        {
            if (UseHorizontalLayout)
            {
                PageLeft();
            }
            else
            {
                SetVerticalOffset(VerticalOffset - (itemHeight * Rows));
            }
        }

        public void PageDown()
        {
            if (UseHorizontalLayout)
            {
                PageRight();
            }
            else
            {
                SetVerticalOffset(VerticalOffset + (itemHeight * Rows));
            }
        }

        #endregion IScrollInfo
    }
}
