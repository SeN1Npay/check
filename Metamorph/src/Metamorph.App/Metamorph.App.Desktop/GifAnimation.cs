using System;
using System.Collections.Generic;
using System.IO;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Metamorph.Core.Desktop;
using Metamorph.Core.Services;

namespace Metamorph.App.Desktop;

internal sealed class GifAnimation
{
	public IReadOnlyList<BitmapSource> Frames { get; }

	private double[] Ends { get; }

	public double Duration { get; }

	// Сколько кадров склеено (одинаковые подряд) и во сколько раз прорежено ради бюджета памяти.
	public int Merged { get; }

	public int Step { get; }

	public long Bytes { get; }

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

	private GifAnimation(IReadOnlyList<BitmapSource> frames, double[] ends, int merged, int step, long bytes)
	{
		Frames = frames;
		Ends = ends;
		Duration = ((ends.Length != 0) ? ends[^1] : 0.0);
		Merged = merged;
		Step = step;
		Bytes = bytes;
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

	// targetWidth/Height — пиксели самого большого монитора (0 — не уменьшать под монитор).
	public static GifAnimation? Load(string path, int targetWidth, int targetHeight, long budget = GifFramePlan.DefaultBudget)
	{
		try
		{
			using FileStream bitmapStream = File.OpenRead(path);
			GifBitmapDecoder decoder = new GifBitmapDecoder(bitmapStream, BitmapCreateOptions.PreservePixelFormat, BitmapCacheOption.OnLoad);
			int count = decoder.Frames.Count;
			if (count == 0)
			{
				return null;
			}
			int width = Query<ushort>(decoder.Metadata, "/logscrdesc/Width") is ushort w && w > 0 ? w : decoder.Frames[0].PixelWidth;
			int height = Query<ushort>(decoder.Metadata, "/logscrdesc/Height") is ushort h && h > 0 ? h : decoder.Frames[0].PixelHeight;
			double scale = GifFramePlan.Scale(width, height, targetWidth, targetHeight, count, budget);
			(int outWidth, int outHeight) = GifFramePlan.ScaledSize(width, height, scale);
			bool scaled = outWidth != width || outHeight != height;
			long frameBytes = GifFramePlan.EstimateBytes(outWidth, outHeight, 1);
			GifFrameAccumulator<BitmapSource> frames = new GifFrameAccumulator<BitmapSource>(budget);
			int[] canvas = new int[width * height];
			int[] scaledPixels = (scaled ? new int[outWidth * outHeight] : canvas);
			for (int i = 0; i < count; i++)
			{
				BitmapFrame frame = decoder.Frames[i];
				BitmapMetadata? meta = frame.Metadata as BitmapMetadata;
				int left = Query<ushort>(meta, "/imgdesc/Left").GetValueOrDefault();
				int top = Query<ushort>(meta, "/imgdesc/Top").GetValueOrDefault();
				double delay = (double)(int)Query<ushort>(meta, "/grctlext/Delay").GetValueOrDefault(10) / 100.0;
				if (delay < 0.02)
				{
					delay = 0.1;
				}
				byte disposal = Query<byte>(meta, "/grctlext/Disposal").GetValueOrDefault();
				int[]? restore = ((disposal == 3) ? ((int[])canvas.Clone()) : null);
				// Кадры GIF — дельты: композитим всегда, даже если сам кадр потом выкинем.
				FormatConvertedBitmap patch = new FormatConvertedBitmap(frame, PixelFormats.Bgra32, null, 0.0);
				int pw = patch.PixelWidth;
				int ph = patch.PixelHeight;
				int[] patchPixels = new int[pw * ph];
				patch.CopyPixels(patchPixels, pw * 4, 0);
				for (int y = 0; y < ph; y++)
				{
					int cy = top + y;
					if (cy < 0 || cy >= height)
					{
						continue;
					}
					for (int x = 0; x < pw; x++)
					{
						int cx = left + x;
						if (cx >= 0 && cx < width)
						{
							int px = patchPixels[y * pw + x];
							if ((uint)px >> 24 != 0)
							{
								canvas[cy * width + cx] = px;
							}
						}
					}
				}
				if (!frames.Wants(i))
				{
					frames.Extend(delay);
				}
				else
				{
					if (scaled)
					{
						BitmapSource full = BitmapSource.Create(width, height, 96.0, 96.0, PixelFormats.Bgra32, null, canvas, width * 4);
						TransformedBitmap small = new TransformedBitmap(full, new ScaleTransform((double)outWidth / width, (double)outHeight / height));
						if (small.PixelWidth != outWidth || small.PixelHeight != outHeight)
						{
							outWidth = small.PixelWidth;
							outHeight = small.PixelHeight;
							scaledPixels = new int[outWidth * outHeight];
							frameBytes = GifFramePlan.EstimateBytes(outWidth, outHeight, 1);
						}
						small.CopyPixels(scaledPixels, outWidth * 4, 0);
					}
					ulong hash = GifFramePlan.Hash(scaledPixels);
					if (!frames.TryMerge(hash, delay))
					{
						BitmapSource bitmap = BitmapSource.Create(outWidth, outHeight, 96.0, 96.0, PixelFormats.Bgra32, null, scaledPixels, outWidth * 4);
						bitmap.Freeze();
						frames.Add(bitmap, hash, delay, frameBytes);
					}
				}
				if (disposal == 2)
				{
					for (int y = 0; y < ph; y++)
					{
						int cy = top + y;
						if (cy >= 0 && cy < height && left < width)
						{
							int from = Math.Max(0, left);
							Array.Clear(canvas, cy * width + from, Math.Max(0, Math.Min(left + pw, width) - from));
						}
					}
				}
				else if (restore != null)
				{
					canvas = restore;
					if (!scaled)
					{
						scaledPixels = canvas;
					}
				}
			}
			if (frames.Decimations > 0)
			{
				Log.Warn($"GIF-обои: {Path.GetFileName(path)} не помещается в {budget / 1048576} МБ — оставлен каждый {frames.Step}-й кадр");
			}
			return new GifAnimation(new List<BitmapSource>(frames.Frames), GifFramePlan.Ends(frames.Delays), frames.Merged, frames.Step, frames.Bytes);
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
