using System.IO;

namespace Metamorph.Core.Cursors;

public static class CurWriter
{
	public static byte[] Write(CursorImage image)
	{
		int size = image.Size;
		int num = (size + 31) / 32 * 4;
		int num2 = size * size * 4;
		int num3 = num * size;
		int num4 = 40 + num2 + num3;
		using MemoryStream memoryStream = new MemoryStream(22 + num4);
		using BinaryWriter binaryWriter = new BinaryWriter(memoryStream);
		binaryWriter.Write((ushort)0);
		binaryWriter.Write((ushort)2);
		binaryWriter.Write((ushort)1);
		binaryWriter.Write((byte)((size < 256) ? ((uint)size) : 0u));
		binaryWriter.Write((byte)((size < 256) ? ((uint)size) : 0u));
		binaryWriter.Write((byte)0);
		binaryWriter.Write((byte)0);
		binaryWriter.Write((ushort)image.HotX);
		binaryWriter.Write((ushort)image.HotY);
		binaryWriter.Write((uint)num4);
		binaryWriter.Write(22u);
		binaryWriter.Write(40u);
		binaryWriter.Write(size);
		binaryWriter.Write(size * 2);
		binaryWriter.Write((ushort)1);
		binaryWriter.Write((ushort)32);
		binaryWriter.Write(0u);
		binaryWriter.Write((uint)(num2 + num3));
		binaryWriter.Write(0);
		binaryWriter.Write(0);
		binaryWriter.Write(0u);
		binaryWriter.Write(0u);
		for (int num5 = size - 1; num5 >= 0; num5--)
		{
			binaryWriter.Write(image.Bgra, num5 * size * 4, size * 4);
		}
		for (int num6 = size - 1; num6 >= 0; num6--)
		{
			byte[] array = new byte[num];
			for (int i = 0; i < size; i++)
			{
				if (image.Bgra[(num6 * size + i) * 4 + 3] == 0)
				{
					array[i / 8] |= (byte)(128 >> i % 8);
				}
			}
			binaryWriter.Write(array);
		}
		binaryWriter.Flush();
		return memoryStream.ToArray();
	}
}
