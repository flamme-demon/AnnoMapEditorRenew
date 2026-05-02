using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Markup.Xaml;
using Avalonia.Media;
using AnnoMapEditor.DataArchives.Assets.Models;
using AnnoMapEditor.MapTemplates.Enums;
using AnnoMapEditor.Utilities;

namespace AnnoMapEditor.UI.Avalonia.Windows
{
    /// <summary>
    /// One option in the picker. Either a Fixed island (concrete .a7m asset) or a Random
    /// "size bucket" (the engine will pick the actual island at game time).
    /// </summary>
    public class IslandChoice
    {
        public IslandAsset? FixedAsset { get; init; }
        public IslandSize? RandomSize { get; init; }
        public bool IsRandom => RandomSize is not null;
        public bool IsFixed => FixedAsset is not null;

        public static IslandChoice ForFixed(IslandAsset asset)
            => new() { FixedAsset = asset };
        public static IslandChoice ForRandom(IslandSize size)
            => new() { RandomSize = size };
    }

    /// <summary>
    /// Visual island picker — wrap panel of card-shaped tiles, each showing a thumbnail
    /// + name + tile size. Returns the chosen <see cref="IslandChoice"/> or null on cancel.
    /// </summary>
    public partial class IslandPickerDialog : Window
    {
        private List<IslandChoice> _all = new();
        private ItemsControl? _grid;
        private TextBox? _filterBox;
        private TextBlock? _countLabel;
        private TextBlock? _header;
        private ToggleButton? _filterAll, _filterRandom, _filterFixed;

        private enum Mode { All, Random, Fixed }
        private Mode _mode = Mode.All;

        public IslandPickerDialog()
        {
            InitializeComponent();
        }

        public IslandPickerDialog(IEnumerable<IslandChoice> choices, string headerText)
            : this()
        {
            _all = choices.Where(c => c is not null).ToList();
            if (_header != null) _header.Text = headerText;
            Refresh();
        }

        private void InitializeComponent()
        {
            AvaloniaXamlLoader.Load(this);
            _grid = this.FindControl<ItemsControl>("Grid");
            _filterBox = this.FindControl<TextBox>("FilterBox");
            _countLabel = this.FindControl<TextBlock>("CountLabel");
            _header = this.FindControl<TextBlock>("Header");
            _filterAll = this.FindControl<ToggleButton>("FilterAll");
            _filterRandom = this.FindControl<ToggleButton>("FilterRandom");
            _filterFixed = this.FindControl<ToggleButton>("FilterFixed");
        }

        private void OnFilterChanged(object? sender, TextChangedEventArgs e) => Refresh();

        // Three mutually-exclusive toggle buttons emulating a segmented control.
        // Clicking one snaps the other two off.
        private void OnFilterModeClicked(object? sender, RoutedEventArgs e)
        {
            if (sender is not ToggleButton clicked || _filterAll == null
                || _filterRandom == null || _filterFixed == null)
                return;

            // Don't let the user untoggle the active button by clicking it again — keep
            // exactly one selected at all times.
            if (clicked.IsChecked != true) { clicked.IsChecked = true; return; }

            _filterAll.IsChecked = clicked == _filterAll;
            _filterRandom.IsChecked = clicked == _filterRandom;
            _filterFixed.IsChecked = clicked == _filterFixed;

            _mode = clicked == _filterRandom ? Mode.Random
                  : clicked == _filterFixed ? Mode.Fixed
                  : Mode.All;
            Refresh();
        }

        private void Refresh()
        {
            if (_grid is null) return;
            string filter = _filterBox?.Text?.Trim() ?? "";

            var items = _all
                .Where(c => _mode switch
                {
                    Mode.Random => c.IsRandom,
                    Mode.Fixed => c.IsFixed,
                    _ => true
                })
                .Where(c => string.IsNullOrEmpty(filter) || MatchesFilter(c, filter))
                .ToList();

            _grid.ItemsSource = items.Select(BuildCard).ToList();
            if (_countLabel != null) _countLabel.Text = $"{items.Count} / {_all.Count}";
        }

        private static bool MatchesFilter(IslandChoice c, string filter)
        {
            if (c.FixedAsset is { } fa)
            {
                return (fa.DisplayName?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false)
                    || (fa.FilePath?.Contains(filter, StringComparison.OrdinalIgnoreCase) ?? false);
            }
            if (c.RandomSize is { } rs)
            {
                return ($"Random {rs.Name}").Contains(filter, StringComparison.OrdinalIgnoreCase);
            }
            return false;
        }

        // One island = one clickable Border with a thumbnail (or a placeholder for
        // Random sizes) and two text rows.
        private Control BuildCard(IslandChoice choice)
        {
            string name;
            string sizeLabel;
            Control imageControl;

            if (choice.FixedAsset is { } asset)
            {
                name = !string.IsNullOrEmpty(asset.DisplayName)
                    ? asset.DisplayName!
                    : Path.GetFileNameWithoutExtension(asset.FilePath ?? "(asset)");
                sizeLabel = $"Fixed · {asset.SizeInTiles}t";
                imageControl = new Image
                {
                    Source = asset.Thumbnail,
                    Width = 128,
                    Height = 128,
                    Stretch = Stretch.Uniform
                };
            }
            else if (choice.RandomSize is { } rs)
            {
                name = $"Random {rs.Name}";
                sizeLabel = $"Random · ≈ {rs.DefaultSizeInTiles}t";
                // Placeholder rectangle that hints at the relative size of the bucket.
                double rel = Math.Clamp(rs.DefaultSizeInTiles / 400.0, 0.3, 1.0);
                imageControl = new Border
                {
                    Width = 128,
                    Height = 128,
                    Background = Brushes.Transparent,
                    Child = new Border
                    {
                        Width = 128 * rel,
                        Height = 128 * rel,
                        HorizontalAlignment = HorizontalAlignment.Center,
                        VerticalAlignment = VerticalAlignment.Center,
                        BorderBrush = new SolidColorBrush(Color.Parse("#3FA7E6")),
                        BorderThickness = new Thickness(2),
                        Background = new SolidColorBrush(Color.FromArgb(40, 63, 167, 230)),
                        CornerRadius = new CornerRadius(4),
                        Child = new TextBlock
                        {
                            Text = "?",
                            FontSize = 36,
                            FontWeight = FontWeight.Bold,
                            Foreground = new SolidColorBrush(Color.Parse("#3FA7E6")),
                            HorizontalAlignment = HorizontalAlignment.Center,
                            VerticalAlignment = VerticalAlignment.Center
                        }
                    }
                };
            }
            else
            {
                return new TextBlock();
            }

            var nameBlock = new TextBlock
            {
                Text = name,
                FontSize = 12,
                FontWeight = FontWeight.SemiBold,
                TextAlignment = TextAlignment.Center,
                TextTrimming = TextTrimming.CharacterEllipsis,
                Width = 140
            };
            var sizeBlock = new TextBlock
            {
                Text = sizeLabel,
                FontSize = 11,
                Opacity = 0.7,
                TextAlignment = TextAlignment.Center
            };

            var stack = new StackPanel { Spacing = 4, Margin = new Thickness(8) };
            stack.Children.Add(imageControl);
            stack.Children.Add(nameBlock);
            stack.Children.Add(sizeBlock);

            var card = new Border
            {
                BorderBrush = new SolidColorBrush(Color.FromArgb(80, 255, 255, 255)),
                BorderThickness = new Thickness(1),
                CornerRadius = new CornerRadius(4),
                Margin = new Thickness(4),
                Width = 156,
                Background = new SolidColorBrush(Color.FromArgb(20, 255, 255, 255)),
                Cursor = new Cursor(StandardCursorType.Hand),
                Child = stack
            };
            ToolTip.SetTip(card, choice.FixedAsset?.FilePath
                ?? $"Random {choice.RandomSize?.Name}");
            card.PointerEntered += (_, _) => card.Background =
                new SolidColorBrush(Color.FromArgb(60, 100, 180, 255));
            card.PointerExited += (_, _) => card.Background =
                new SolidColorBrush(Color.FromArgb(20, 255, 255, 255));
            card.PointerReleased += (_, _) => Close(choice);
            return card;
        }

        private void OnCancel(object? sender, RoutedEventArgs e) => Close(null);
    }
}
