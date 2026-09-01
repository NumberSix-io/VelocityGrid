namespace VelocityGrid.Managed;

/// <summary>Caller-selected colours from the native rendering palette.</summary>
public enum VelocityGridColor : byte
{
    None = 0, Black = 1, White = 2, DarkGray = 3, Gray = 4, LightGray = 5,
    DarkRed = 6, Red = 7, LightRed = 8, Orange = 9, Amber = 10, Yellow = 11,
    Lime = 12, DarkGreen = 13, Green = 14, LightGreen = 15, Teal = 16, Cyan = 17,
    DarkBlue = 18, Blue = 19, LightBlue = 20, Indigo = 21, Violet = 22, Purple = 23,
    Pink = 24, Brown = 25
}

/// <summary>Icons from the bounded native catalogue.</summary>
public enum VelocityGridIcon : byte
{
    None = 0, UpArrow = 1, DownArrow = 2, LeftArrow = 3, RightArrow = 4,
    UpTriangle = 5, DownTriangle = 6, Check = 7, Cross = 8, Warning = 9,
    Information = 10, Star = 11, Circle = 12, Square = 13, Diamond = 14,
    Plus = 15, Minus = 16, Play = 17, Pause = 18, Stop = 19, Clock = 20,
    Flag = 21, Heart = 22, Lightning = 23, Bell = 24, Lock = 25, Unlock = 26,
    Search = 27, Edit = 28
}

/// <summary>Compact immutable visual state stored beside a cached cell value.</summary>
public readonly record struct VelocityGridCellFormat(
    VelocityGridColor Foreground = VelocityGridColor.None,
    VelocityGridColor Background = VelocityGridColor.None,
    VelocityGridIcon Icon = VelocityGridIcon.None);
