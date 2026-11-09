using System;

namespace Metamorph.App.Launcher;

internal sealed record LauncherItem(string Title, string Subtitle, string Glyph, Action Execute, string? IconPath = null, nint Window = 0);
