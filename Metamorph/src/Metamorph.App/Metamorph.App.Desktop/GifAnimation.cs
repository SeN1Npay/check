using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class GifAnimation
{
	private const long MemoryBudget = 335544320L;

	public IReadOnlyList<BitmapSource> Frames { get; }

	private double[] Ends { get; }

	public double Duration { get; }

	public int PixelWidth
	{
		get
		{
			if (Frames.Count <= 0)
			{
				return 0;
			}
			return Frames[0].PixelWidth;
		}
	}

	public int PixelHeight
	{
		get
		{
			if (Frames.Count <= 0)
			{
				return 0;
			}
			return Frames[0].PixelHeight;
		}
	}

	private GifAnimation(IReadOnlyList<BitmapSource> frames, double[] ends)
	{
		Frames = frames;
		Ends = ends;
		Duration = ((ends.Length != 0) ? ends[^1] : 0.0);
	}

	public double DelayOf(int index)
	{
		if (Ends.Length == 0)
		{
			return 0.1;
		}
		index = Math.Clamp(index, 0, Ends.Length - 1);
		double num = Ends[index] - ((index > 0) ? Ends[index - 1] : 0.0);
		if (!(num > 0.005))
		{
			return 0.1;
		}
		return num;
	}

	public static BitmapSource? FirstFrame(string path)
	{
		try
		{
			using FileStream bitmapStream = File.OpenRead(path);
			GifBitmapDecoder gifBitmapDecoder = new GifBitmapDecoder(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			if (gifBitmapDecoder.Frames.Count == 0)
			{
				return null;
			}
			FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(gifBitmapDecoder.Frames[0], PixelFormats.Bgra32, null, 0.0);
			formatConvertedBitmap.Freeze();
			return formatConvertedBitmap;
		}
		catch (Exception ex)
		{
			Log.Warn("GIF: первый кадр " + path + ": " + ex.Message);
			return null;
		}
	}

	public BitmapSource? FrameAt(double seconds)
	{
		if (Frames.Count == 0)
		{
			return null;
		}
		if (Frames.Count == 1 || Duration <= 0.0)
		{
			return Frames[0];
		}
		double value = seconds % Duration;
		int num = Array.BinarySearch(Ends, value);
		if (num < 0)
		{
			num = ~num;
		}
		return Frames[Math.Clamp(num, 0, Frames.Count - 1)];
	}

	public static GifAnimation? Load(string path, int maxWidth)
	{
		try
		{
			using FileStream bitmapStream = File.OpenRead(path);
			GifBitmapDecoder gifBitmapDecoder = new GifBitmapDecoder(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			if (gifBitmapDecoder.Frames.Count == 0)
			{
				return null;
			}
			ushort? num = Query<ushort>(gifBitmapDecoder.Metadata, "/logscrdesc/Width");
			int num2;
			if (num.HasValue)
			{
				ushort valueOrDefault = num.GetValueOrDefault();
				if (valueOrDefault > 0)
				{
					num2 = valueOrDefault;
					goto IL_0060;
				}
			}
			num2 = gifBitmapDecoder.Frames[0].PixelWidth;
			goto IL_0060;
			IL_0060:
			int num3 = num2;
			num = Query<ushort>(gifBitmapDecoder.Metadata, "/logscrdesc/Height");
			int num4;
			if (num.HasValue)
			{
				ushort valueOrDefault2 = num.GetValueOrDefault();
				if (valueOrDefault2 > 0)
				{
					num4 = valueOrDefault2;
					goto IL_009f;
				}
			}
			num4 = gifBitmapDecoder.Frames[0].PixelHeight;
			goto IL_009f;
			IL_009f:
			int num5 = num4;
			int count = gifBitmapDecoder.Frames.Count;
			double val = Math.Min(1.0, Math.Sqrt(335544320.0 / ((double)num3 * (double)num5 * 4.0 * (double)count)));
			if (maxWidth > 0)
			{
				val = Math.Min(val, (double)maxWidth / (double)num3);
			}
			val = Math.Max(val, 0.1);
			int[] array = new int[num3 * num5];
			List<BitmapSource> list = new List<BitmapSource>(count);
			double[] array2 = new double[count];
			double num6 = 0.0;
			for (int i = 0; i < count; i++)
			{
				BitmapFrame bitmapFrame = gifBitmapDecoder.Frames[i];
				BitmapMetadata meta = bitmapFrame.Metadata as BitmapMetadata;
				int valueOrDefault3 = Query<ushort>(meta, "/imgdesc/Left").GetValueOrDefault();
				int valueOrDefault4 = Query<ushort>(meta, "/imgdesc/Top").GetValueOrDefault();
				double num7 = (double)(int)Query<ushort>(meta, "/grctlext/Delay").GetValueOrDefault(10) / 100.0;
				if (num7 < 0.02)
				{
					num7 = 0.1;
				}
				byte valueOrDefault5 = Query<byte>(meta, "/grctlext/Disposal").GetValueOrDefault();
				int[] array3 = ((valueOrDefault5 == 3) ? ((int[])array.Clone()) : null);
				FormatConvertedBitmap formatConvertedBitmap = new FormatConvertedBitmap(bitmapFrame, PixelFormats.Bgra32, null, 0.0);
				int pixelWidth = formatConvertedBitmap.PixelWidth;
				int pixelHeight = formatConvertedBitmap.PixelHeight;
				int[] array4 = new int[pixelWidth * pixelHeight];
				formatConvertedBitmap.CopyPixels(array4, pixelWidth * 4, 0);
				for (int j = 0; j < pixelHeight; j++)
				{
					int num8 = valueOrDefault4 + j;
					if (num8 < 0 || num8 >= num5)
					{
						continue;
					}
					for (int k = 0; k < pixelWidth; k++)
					{
						int num9 = valueOrDefault3 + k;
						if (num9 >= 0 && num9 < num3)
						{
							int num10 = array4[j * pixelWidth + k];
							if ((uint)num10 >> 24 != 0)
							{
								array[num8 * num3 + num9] = num10;
							}
						}
					}
				}
				BitmapSource bitmapSource = BitmapSource.Create(num3, num5, 96.0, 96.0, PixelFormats.Bgra32, null, array, num3 * 4);
				if (val < 0.999)
				{
					bitmapSource = new WriteableBitmap(new TransformedBitmap(bitmapSource, new ScaleTransform(val, val)));
				}
				bitmapSource.Freeze();
				list.Add(bitmapSource);
				num6 = (array2[i] = num6 + num7);
				if (valueOrDefault5 == 2)
				{
					for (int l = 0; l < pixelHeight; l++)
					{
						int num11 = valueOrDefault4 + l;
						if (num11 >= 0 && num11 < num5)
						{
							Array.Clear(array, num11 * num3 + Math.Max(0, valueOrDefault3), Math.Max(0, Math.Min(pixelWidth, num3 - valueOrDefault3)));
						}
					}
				}
				else if (array3 != null)
				{
					array = array3;
				}
			}
			return new GifAnimation(list, array2);
		}
		catch (Exception ex)
		{
			Log.Error("GIF-обои: " + path, ex);
			return null;
		}
	}

	private static T? Query<T>(BitmapMetadata? meta, string query) where T : struct
	{
		try
		{
			return (meta?.GetQuery(query) is T value) ? new T?(value) : null;
		}
		catch
		{
			return null;
		}
	}

	public static bool IsGif(string? path)
	{
		if (path != null)
		{
			return Path.GetExtension(path).Equals(".gif", StringComparison.OrdinalIgnoreCase);
		}
		return false;
	}
}
