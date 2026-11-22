using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Markup;
using DeviceBatteryControl.Localization;

namespace DeviceBatteryControl.Controls;

/// <summary>Where a <see cref="SettingsCard"/> sits, which sets its frame.</summary>
public enum SettingsCardPlacement
{
    /// <summary>A card of its own, with the card's fill and border.</summary>
    Card,

    /// <summary>An Expander's header: the Expander draws the card around it.</summary>
    ExpanderHeader,

    /// <summary>A row inside an Expander: no icon, text lined up under the header's, a divider above.</summary>
    ExpanderRow,
}

/// <summary>
/// One setting, laid out as Windows Settings lays them out: icon, header and
/// description on the left, the control on the right. A ToggleSwitch as the
/// control shows the plain On/Off on its left, and with
/// <see cref="OnDescription"/> and <see cref="OffDescription"/> the
/// description follows it.
/// </summary>
[ContentProperty(Name = nameof(ActionContent))]
public sealed partial class SettingsCard : UserControl
{
    public static readonly DependencyProperty GlyphProperty = DependencyProperty.Register(
        nameof(Glyph), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty HeaderProperty = DependencyProperty.Register(
        nameof(Header), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty DescriptionProperty = DependencyProperty.Register(
        nameof(Description), typeof(string), typeof(SettingsCard), new PropertyMetadata(string.Empty, OnTextChanged));

    public static readonly DependencyProperty ActionContentProperty = DependencyProperty.Register(
        nameof(ActionContent), typeof(object), typeof(SettingsCard), new PropertyMetadata(null, OnActionChanged));

    public static readonly DependencyProperty PlacementProperty = DependencyProperty.Register(
        nameof(Placement), typeof(SettingsCardPlacement), typeof(SettingsCard),
        new PropertyMetadata(SettingsCardPlacement.Card, OnPlacementChanged));

    public static readonly DependencyProperty ShowDividerProperty = DependencyProperty.Register(
        nameof(ShowDivider), typeof(bool), typeof(SettingsCard), new PropertyMetadata(true, OnPlacementChanged));

    private ToggleSwitch? _toggle;

    public SettingsCard()
    {
        InitializeComponent();
        ApplyPlacement();
    }

    public string Glyph
    {
        get => (string)GetValue(GlyphProperty);
        set => SetValue(GlyphProperty, value);
    }

    public string Header
    {
        get => (string)GetValue(HeaderProperty);
        set => SetValue(HeaderProperty, value);
    }

    public string Description
    {
        get => (string)GetValue(DescriptionProperty);
        set => SetValue(DescriptionProperty, value);
    }

    /// <summary>With <see cref="OffDescription"/>: the description while the switch is on.</summary>
    public string OnDescription
    {
        get => _onDescription;
        set
        {
            _onDescription = value;
            ShowState();
        }
    }

    /// <summary>With <see cref="OnDescription"/>: the description while the switch is off.</summary>
    public string OffDescription
    {
        get => _offDescription;
        set
        {
            _offDescription = value;
            ShowState();
        }
    }

    private string _onDescription = string.Empty;
    private string _offDescription = string.Empty;
    private string _onText = string.Empty;
    private string _offText = string.Empty;

    /// <summary>With <see cref="OffText"/>: what the switch's state reads while on,
    /// such as "Shown", in place of the plain On.</summary>
    public string OnText
    {
        get => _onText;
        set
        {
            _onText = value;
            ShowState();
        }
    }

    /// <summary>With <see cref="OnText"/>: what the switch's state reads while off.</summary>
    public string OffText
    {
        get => _offText;
        set
        {
            _offText = value;
            ShowState();
        }
    }

    public object? ActionContent
    {
        get => GetValue(ActionContentProperty);
        set => SetValue(ActionContentProperty, value);
    }

    public SettingsCardPlacement Placement
    {
        get => (SettingsCardPlacement)GetValue(PlacementProperty);
        set => SetValue(PlacementProperty, value);
    }

    /// <summary>For an <see cref="SettingsCardPlacement.ExpanderRow"/>: the line above it, off for the first row.</summary>
    public bool ShowDivider
    {
        get => (bool)GetValue(ShowDividerProperty);
        set => SetValue(ShowDividerProperty, value);
    }

    private static void OnTextChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((SettingsCard)sender).ApplyText();

    private static void OnPlacementChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((SettingsCard)sender).ApplyPlacement();

    private static void OnActionChanged(DependencyObject sender, DependencyPropertyChangedEventArgs e) =>
        ((SettingsCard)sender).ApplyAction();

    private void ApplyText()
    {
        IconElement.Glyph = Glyph;
        IconElement.Visibility = string.IsNullOrEmpty(Glyph) || Placement == SettingsCardPlacement.ExpanderRow
            ? Visibility.Collapsed
            : Visibility.Visible;
        HeaderText.Text = Header;
        ShowState();
        if (ActionContent is FrameworkElement action && !string.IsNullOrEmpty(Header))
        {
            AutomationProperties.SetName(action, Header);
        }
    }

    private void ApplyPlacement()
    {
        // A card keeps the frame the XAML gives it: its fill is a theme
        // resource, which a value set here would pin to one theme. Placement is
        // set once, from markup, so nothing needs putting back.
        switch (Placement)
        {
            case SettingsCardPlacement.Card:
                break;
            case SettingsCardPlacement.ExpanderHeader:
                // The Expander's toggle button is the card; its padding already
                // puts the icon 16 px from the edge.
                CardBorder.MinHeight = 0;
                CardBorder.Padding = new Thickness(0, 10, 0, 10);
                CardBorder.BorderThickness = new Thickness(0);
                CardBorder.Background = null;
                break;
            case SettingsCardPlacement.ExpanderRow:
                // Text under the header's text (58 px in; the grid's 16 px column
                // spacing still follows the empty icon column), the control
                // left of the chevron.
                CardBorder.MinHeight = 52;
                CardBorder.Padding = new Thickness(42, 8, 60, 8);
                CardBorder.BorderThickness = new Thickness(0, ShowDivider ? 1 : 0, 0, 0);
                CardBorder.Background = null;
                CardBorder.CornerRadius = new CornerRadius(0);
                break;
        }

        ApplyText();
    }

    private void ApplyAction()
    {
        if (_toggle is not null)
        {
            _toggle.Toggled -= OnToggled;
        }

        ActionPresenter.Content = ActionContent;
        _toggle = ActionContent as ToggleSwitch;
        if (_toggle is not null)
        {
            // The switch alone, its On/Off moved to StateText; the negative
            // margin gives back the gap its template keeps for that text.
            _toggle.OnContent = string.Empty;
            _toggle.OffContent = string.Empty;
            _toggle.MinWidth = 0;
            _toggle.Margin = new Thickness(0, 0, -12, 0);
            _toggle.Toggled += OnToggled;
        }

        StateText.Visibility = _toggle is null ? Visibility.Collapsed : Visibility.Visible;
        ApplyText();
    }

    private void OnToggled(object sender, RoutedEventArgs e) => ShowState();

    private void ShowState()
    {
        if (_toggle is not null)
        {
            StateText.Text = _toggle.IsOn
                ? _onText.Length > 0 ? _onText : BatteryLoc.Get("On")
                : _offText.Length > 0 ? _offText : BatteryLoc.Get("Off");
        }

        var description = _toggle is not null && _onDescription.Length > 0 && _offDescription.Length > 0
            ? _toggle.IsOn ? _onDescription : _offDescription
            : Description;
        DescriptionText.Text = description;
        DescriptionText.Visibility = string.IsNullOrEmpty(description) ? Visibility.Collapsed : Visibility.Visible;
    }
}
