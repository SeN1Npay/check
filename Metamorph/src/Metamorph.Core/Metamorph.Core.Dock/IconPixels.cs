namespace Metamorph.Core.Dock;

public sealed record IconPixels(int Width, int Height, byte[] Bgra, bool Premultiplied)
{
	public int Stride => Width * 4;
}
