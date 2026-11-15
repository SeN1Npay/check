using System;
using System.Collections.Generic;
using System.Linq;

namespace Metamorph.Core.Input;

public readonly record struct HotkeyGesture(uint Modifiers, uint Key)
{
	private static readonly Dictionary<string, uint> Named = new Dictionary<string, uint>(StringComparer.OrdinalIgnoreCase)
	{
		["Space"] = 32u,
		["Enter"] = 13u,
		["Return"] = 13u,
		["Tab"] = 9u,
		["Esc"] = 27u,
		["Escape"] = 27u,
		["Backspace"] = 8u,
		["Delete"] = 46u,
		["Del"] = 46u,
		["Insert"] = 45u,
		["Ins"] = 45u,
		["Home"] = 36u,
		["End"] = 35u,
		["PageUp"] = 33u,
		["PageDown"] = 34u,
		["Left"] = 37u,
		["Up"] = 38u,
		["Right"] = 39u,
		["Down"] = 40u,
		["Plus"] = 187u,
		["Minus"] = 189u,
		["Comma"] = 188u,
		["Period"] = 190u,
		["Slash"] = 191u,
		["Backtick"] = 192u,
		["Tilde"] = 192u,
		["Semicolon"] = 186u,
		["Quote"] = 222u,
		["LeftBracket"] = 219u,
		["RightBracket"] = 221u,
		["Backslash"] = 220u,
		["PrintScreen"] = 44u,
		["Pause"] = 19u
	};

	public static bool TryParse(string? text, out HotkeyGesture gesture)
	{
		gesture = default(HotkeyGesture);
		if (string.IsNullOrWhiteSpace(text))
		{
			return false;
		}
		uint num = 0u;
		uint? num2 = null;
		string[] array = text.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
		foreach (string text2 in array)
		{
			switch (text2.ToLowerInvariant())
			{
			case "ctrl":
			case "control":
				num |= 2u;
				continue;
			case "alt":
				num |= 1u;
				continue;
			case "shift":
				num |= 4u;
				continue;
			case "meta":
			case "win":
			case "super":
				num |= 8u;
				continue;
			}
			if (num2.HasValue)
			{
				return false;
			}
			num2 = KeyCode(text2);
			if (!num2.HasValue)
			{
				return false;
			}
		}
		if (!num2.HasValue)
		{
			return false;
		}
		int num3;
		if (num2.HasValue)
		{
			uint valueOrDefault = num2.GetValueOrDefault();
			if (valueOrDefault >= 112)
			{
				num3 = ((valueOrDefault <= 135) ? 1 : 0);
				goto IL_01a7;
			}
		}
		num3 = 0;
		goto IL_01a7;
		IL_01a7:
		bool flag = (byte)num3 != 0;
		if (num == 0 && !flag)
		{
			return false;
		}
		gesture = new HotkeyGesture(num, num2.Value);
		return true;
	}

	private static uint? KeyCode(string name)
	{
		if (name.Length == 1)
		{
			char c = char.ToUpperInvariant(name[0]);
			bool flag;
			switch (c)
			{
			case '0':
			case '1':
			case '2':
			case '3':
			case '4':
			case '5':
			case '6':
			case '7':
			case '8':
			case '9':
			case 'A':
			case 'B':
			case 'C':
			case 'D':
			case 'E':
			case 'F':
			case 'G':
			case 'H':
			case 'I':
			case 'J':
			case 'K':
			case 'L':
			case 'M':
			case 'N':
			case 'O':
			case 'P':
			case 'Q':
			case 'R':
			case 'S':
			case 'T':
			case 'U':
			case 'V':
			case 'W':
			case 'X':
			case 'Y':
			case 'Z':
				flag = true;
				break;
			default:
				flag = false;
				break;
			}
			if (flag)
			{
				return c;
			}
			return c switch
			{
				'=' => 187u, 
				'-' => 189u, 
				',' => 188u, 
				'.' => 190u, 
				'/' => 191u, 
				'`' => 192u, 
				';' => 186u, 
				'\'' => 222u, 
				'[' => 219u, 
				']' => 221u, 
				'\\' => 220u, 
				_ => null, 
			};
		}
		char c2 = name[0];
		if ((c2 == 'F' || c2 == 'f') ? true : false)
		{
			if (int.TryParse(name.Substring(1, name.Length - 1), out var result) && result >= 1 && result <= 24)
			{
				return (uint)(112 + result - 1);
			}
		}
		if (!Named.TryGetValue(name, out var value))
		{
			return null;
		}
		return value;
	}

	public override string ToString()
	{
		List<string> list = new List<string>();
		if ((Modifiers & 2u) != 0)
		{
			list.Add("Ctrl");
		}
		if ((Modifiers & (true ? 1u : 0u)) != 0)
		{
			list.Add("Alt");
		}
		if ((Modifiers & 4u) != 0)
		{
			list.Add("Shift");
		}
		if ((Modifiers & 8u) != 0)
		{
			list.Add("Win");
		}
		list.Add(KeyName(Key));
		return string.Join("+", list);
	}

	public static string KeyName(uint vk)
	{
		if (vk >= 65)
		{
			if (vk <= 90)
			{
				goto IL_0037;
			}
		}
		else if (vk >= 48 && vk <= 57)
		{
			goto IL_0037;
		}
		bool flag = false;
		goto IL_003d;
		IL_0037:
		flag = true;
		goto IL_003d;
		IL_003d:
		if (flag)
		{
			return ((char)vk).ToString();
		}
		if (vk >= 112 && vk <= 135)
		{
			return "F" + (vk - 112 + 1);
		}
		return Named.FirstOrDefault<KeyValuePair<string, uint>>(delegate(KeyValuePair<string, uint> p)
		{
			bool flag2 = p.Value == vk;
			if (flag2)
			{
				bool flag3;
				switch (p.Key)
				{
				case "Return":
				case "Escape":
				case "Del":
				case "Ins":
				case "Tilde":
					flag3 = true;
					break;
				default:
					flag3 = false;
					break;
				}
				flag2 = !flag3;
			}
			return flag2;
		}).Key ?? $"0x{vk:X2}";
	}
}
