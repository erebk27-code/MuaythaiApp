using Avalonia.Controls;
using Avalonia.Interactivity;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace MuaythaiApp;

public partial class RingDistributionWindow : Window
{
    private readonly int dayNumber;
    private readonly List<RingCountInput> inputs = new();

    public event Action<IReadOnlyDictionary<string, int>>? DistributionApplied;

    public RingDistributionWindow()
    {
        InitializeComponent();
        dayNumber = 1;
        LocalizationService.LocalizeControlTree(this);
    }

    public RingDistributionWindow(
        int dayNumber,
        IReadOnlyList<string> ringNames,
        IReadOnlyDictionary<string, int> currentCounts)
    {
        InitializeComponent();
        this.dayNumber = dayNumber;
        Title = LocalizationService.T("DistributeRings");
        TitleText.Text = LocalizationService.Format("DistributeDayMatches", dayNumber);
        InfoText.Text = LocalizationService.T("RingDistributionHint");

        foreach (var ringName in ringNames.Where(x => !string.IsNullOrWhiteSpace(x)))
        {
            var currentCount = currentCounts.TryGetValue(ringName, out var count)
                ? count
                : 0;
            AddRingInput(ringName, currentCount);
        }

        LocalizationService.LocalizeControlTree(this);
    }

    private void AddRingInput(string ringName, int currentCount)
    {
        var textBox = new TextBox
        {
            Width = 90,
            Text = currentCount.ToString(CultureInfo.InvariantCulture),
            HorizontalAlignment = Avalonia.Layout.HorizontalAlignment.Right
        };

        inputs.Add(new RingCountInput(ringName, textBox));

        var row = new Grid
        {
            ColumnDefinitions = new ColumnDefinitions("*,110"),
            ColumnSpacing = 12
        };

        row.Children.Add(new TextBlock
        {
            Text = ringName,
            VerticalAlignment = Avalonia.Layout.VerticalAlignment.Center,
            FontWeight = Avalonia.Media.FontWeight.SemiBold
        });

        Grid.SetColumn(textBox, 1);
        row.Children.Add(textBox);
        RingPanel.Children.Add(row);
    }

    private void CancelClick(object? sender, RoutedEventArgs e)
    {
        Close();
    }

    private void ApplyClick(object? sender, RoutedEventArgs e)
    {
        var distribution = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

        foreach (var input in inputs)
        {
            if (!int.TryParse(input.TextBox.Text?.Trim(), NumberStyles.Integer, CultureInfo.InvariantCulture, out var count) ||
                count < 0)
            {
                InfoText.Text = LocalizationService.Format("RingCountInvalid", input.RingName);
                return;
            }

            distribution[input.RingName] = count;
        }

        DistributionApplied?.Invoke(distribution);
        Close();
    }

    private sealed record RingCountInput(string RingName, TextBox TextBox);
}
