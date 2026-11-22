namespace DeviceBatteryControl.Models;

public enum TaskbarLayout { Horizontal, TwoRows }
public enum TaskbarDensity { Compact, Standard, Comfortable }
public enum TaskbarLabelPosition { Above, Below, Before, After }
public enum TaskbarIconPosition { Before, After }

public sealed record TaskbarLayoutMetrics(
    int Columns, int Rows, int CellWidth, int CellHeight, int OuterPadding,
    int ColumnGap, int RowGap, int ContentGap, int IconSize, int ValueSize, int ValueWidth,
    int LabelSize, int LabelWidth, TaskbarLabelPosition LabelPosition)
{
    public int Width => OuterPadding * 2 + Columns * CellWidth + (Columns - 1) * ColumnGap;
    public int Height => Rows * CellHeight + (Rows - 1) * RowGap;

    public static TaskbarLayoutMetrics Create(BatterySettings settings, int deviceCount)
    {
        var count = Math.Clamp(deviceCount, 1, 10);
        var stacked = settings.Layout == TaskbarLayout.TwoRows;
        var rows = stacked ? Math.Min(2, count) : 1;
        var columns = (count + rows - 1) / rows;
        var (padding, columnGap, contentGap, extra) = settings.Density switch
        {
            TaskbarDensity.Compact => (6, 6, 4, 0),
            TaskbarDensity.Comfortable => (14, 20, 8, 8),
            _ => (10, 14, 6, 4)
        };
        var iconSize = Math.Clamp(settings.IconSize, 12, stacked ? 18 : 24);
        var valueSize = Math.Clamp(settings.ValueFontSize, 10, stacked ? 16 : 18);
        var labelSize = Math.Clamp(settings.LabelFontSize, 9, 14);
        var labelWidth = Math.Clamp(settings.LabelWidth, 40, 160);
        var position = stacked && settings.LabelPosition is TaskbarLabelPosition.Above or TaskbarLabelPosition.Below
            ? TaskbarLabelPosition.Before : settings.LabelPosition;
        var valueWidth = (int)Math.Ceiling(valueSize * (settings.ShowPercentSymbol ? 2.9 : 2.3));
        var textWidth = valueWidth;
        var textHeight = valueSize + 4;
        if (settings.LabelMode != PreviewLabelMode.None)
        {
            if (position is TaskbarLabelPosition.Above or TaskbarLabelPosition.Below)
            {
                textWidth = Math.Max(textWidth, labelWidth);
                textHeight += labelSize + 2;
            }
            else
            {
                textWidth += labelWidth + contentGap;
                textHeight = Math.Max(textHeight, labelSize + 4);
            }
        }
        var width = textWidth + extra + (settings.ShowIcons ? iconSize + 4 + contentGap : 0);
        var height = Math.Max(textHeight, settings.ShowIcons ? iconSize + 4 : 0);
        return new(columns, rows, width, height, padding, columnGap, stacked ? 2 : 0,
            contentGap, iconSize, valueSize, valueWidth, labelSize, labelWidth, position);
    }
}
