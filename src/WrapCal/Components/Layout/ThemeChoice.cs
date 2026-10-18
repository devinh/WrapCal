namespace WrapCal.Components.Layout;

/// <summary>
/// What the reader asked for, which is not the same as what gets drawn: <see cref="System"/> means
/// "whichever way the operating system is set", and only the browser can answer that.
/// </summary>
public enum ThemeChoice
{
    System,
    Light,
    Dark
}
