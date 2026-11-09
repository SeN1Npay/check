namespace Metamorph.Core.Mascot;

public sealed record DrawItem(string Path, Affine Transform, string? Fill, string? Stroke = null, double StrokeWidth = 0.0, double Opacity = 1.0);
