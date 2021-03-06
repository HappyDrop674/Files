﻿using Files.Interacts;
using System;
using System.Collections.Generic;
using System.Linq;
using Windows.Foundation;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Shapes;

namespace Files.UserControls.Selection
{
    public class RectangleSelection_ListViewBase : RectangleSelection
    {
        private ListViewBase uiElement;
        private ScrollViewer scrollViewer;
        private SelectionChangedEventHandler selectionChanged;

        private Point originDragPoint;
        private Dictionary<object, System.Drawing.Rectangle> itemsPosition;

        public RectangleSelection_ListViewBase(ListViewBase uiElement, Rectangle selectionRectangle, SelectionChangedEventHandler selectionChanged = null)
        {
            this.uiElement = uiElement;
            this.selectionRectangle = selectionRectangle;
            this.selectionChanged = selectionChanged;
            itemsPosition = new Dictionary<object, System.Drawing.Rectangle>();
            InitEvents(null, null);
        }

        private void RectangleSelection_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            if (selectionState == SelectionState.Starting)
            {
                // Clear selected items once if the pointer is pressed and moved
                uiElement.SelectedItems.Clear();
                OnSelectionStarted();
                selectionState = SelectionState.Active;
            }
            var currentPoint = e.GetCurrentPoint(uiElement);
            if (currentPoint.Properties.IsLeftButtonPressed && scrollViewer != null)
            {
                var verticalOffset = scrollViewer.VerticalOffset;
                var originDragPointShifted = new Point(originDragPoint.X, originDragPoint.Y - verticalOffset); // Initial drag point relative to the topleft corner
                base.DrawRectangle(currentPoint, originDragPointShifted);
                // Selected area considering scrolled offset
                var rect = new System.Drawing.Rectangle((int)Canvas.GetLeft(selectionRectangle), (int)Math.Min(originDragPoint.Y, currentPoint.Position.Y + verticalOffset), (int)selectionRectangle.Width, (int)Math.Abs(originDragPoint.Y - (currentPoint.Position.Y + verticalOffset)));
                foreach (var item in uiElement.Items.Except(itemsPosition.Keys))
                {
                    var listViewItem = (FrameworkElement)uiElement.ContainerFromItem(item); // Get ListViewItem
                    if (listViewItem == null)
                    {
                        continue; // Element is not loaded (virtualized list)
                    }

                    var gt = listViewItem.TransformToVisual(uiElement);
                    var itemStartPoint = gt.TransformPoint(new Point(0, verticalOffset)); // Get item position relative to the top of the list (considering scrolled offset)
                    var itemRect = new System.Drawing.Rectangle((int)itemStartPoint.X, (int)itemStartPoint.Y, (int)listViewItem.ActualWidth, (int)listViewItem.ActualHeight);
                    itemsPosition[item] = itemRect;
                }
                foreach (var item in itemsPosition.ToList())
                {
                    try
                    {
                        // Update selected items
                        if (rect.IntersectsWith(item.Value))
                        {
                            // Selection rectangle intersects item, add to selected items
                            if (!uiElement.SelectedItems.Contains(item.Key))
                            {
                                uiElement.SelectedItems.Add(item.Key);
                            }
                        }
                        else
                        {
                            uiElement.SelectedItems.Remove(item.Key);
                        }
                    }
                    catch (ArgumentException)
                    {
                        // Item is not present in the ItemsSource
                        itemsPosition.Remove(item);
                    }
                }
                if (currentPoint.Position.Y > uiElement.ActualHeight - 20)
                {
                    // Scroll down the list if pointer is at the bottom
                    var scrollIncrement = Math.Min(currentPoint.Position.Y - (uiElement.ActualHeight - 20), 40);
                    scrollViewer.ChangeView(null, verticalOffset + scrollIncrement, null, false);
                }
                else if (currentPoint.Position.Y < 20)
                {
                    // Scroll up the list if pointer is at the top
                    var scrollIncrement = Math.Min(20 - currentPoint.Position.Y, 40);
                    scrollViewer.ChangeView(null, verticalOffset - scrollIncrement, null, false);
                }
            }
        }

        private void RectangleSelection_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            itemsPosition.Clear();
            originDragPoint = new Point(e.GetCurrentPoint(uiElement).Position.X, e.GetCurrentPoint(uiElement).Position.Y); // Initial drag point relative to the topleft corner
            var verticalOffset = scrollViewer?.VerticalOffset ?? 0;
            originDragPoint.Y += verticalOffset; // Initial drag point relative to the top of the list (considering scrolled offset)
            if (!e.GetCurrentPoint(uiElement).Properties.IsLeftButtonPressed)
            {
                // Trigger only on left click
                return;
            }
            uiElement.PointerMoved -= RectangleSelection_PointerMoved;
            uiElement.PointerMoved += RectangleSelection_PointerMoved;
            if (selectionChanged != null)
            {
                // Unsunscribe from SelectionChanged event for performance
                uiElement.SelectionChanged -= selectionChanged;
            }
            uiElement.CapturePointer(e.Pointer);
            selectionState = SelectionState.Starting;
        }

        private void RectangleSelection_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            Canvas.SetLeft(selectionRectangle, 0);
            Canvas.SetTop(selectionRectangle, 0);
            selectionRectangle.Width = 0;
            selectionRectangle.Height = 0;
            uiElement.PointerMoved -= RectangleSelection_PointerMoved;
            uiElement.ReleasePointerCapture(e.Pointer);
            if (selectionChanged != null)
            {
                // Restore and trigger SelectionChanged event
                uiElement.SelectionChanged -= selectionChanged;
                uiElement.SelectionChanged += selectionChanged;
                selectionChanged(sender, null);
            }
            if (selectionState == SelectionState.Active)
            {
                OnSelectionEnded();
            }
            selectionState = SelectionState.Inactive;
        }

        private void InitEvents(object sender, RoutedEventArgs e)
        {
            if (!uiElement.IsLoaded)
            {
                uiElement.Loaded += InitEvents;
            }
            else
            {
                uiElement.Loaded -= InitEvents;
                uiElement.PointerPressed += RectangleSelection_PointerPressed;
                uiElement.PointerReleased += RectangleSelection_PointerReleased;
                uiElement.PointerCaptureLost += RectangleSelection_PointerReleased;
                uiElement.PointerCanceled += RectangleSelection_PointerReleased;
                scrollViewer = Interaction.FindChild<ScrollViewer>(uiElement);
            }
        }
    }
}