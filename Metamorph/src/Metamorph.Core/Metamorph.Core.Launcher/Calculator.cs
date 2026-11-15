using System;
using System.Globalization;
using System.Linq;

namespace Metamorph.Core.Launcher;

public static class Calculator
{
	private sealed class Parser
	{
		private readonly string _s;

		private int _i;

		public bool AtEnd => _i >= _s.Length;

		public Parser(string s)
		{
			_s = s;
		}

		public void SkipSpaces()
		{
			while (_i < _s.Length && char.IsWhiteSpace(_s[_i]))
			{
				_i++;
			}
		}

		private bool Eat(char ch)
		{
			SkipSpaces();
			if (_i < _s.Length && _s[_i] == ch)
			{
				_i++;
				return true;
			}
			return false;
		}

		public double ParseExpression()
		{
			double num = ParseTerm();
			while (true)
			{
				if (Eat('+'))
				{
					num += ParseTerm();
					continue;
				}
				if (!Eat('-'))
				{
					break;
				}
				num -= ParseTerm();
			}
			return num;
		}

		private double ParseTerm()
		{
			double num = ParseFactor();
			while (true)
			{
				if (Eat('*') || Eat('×'))
				{
					num *= ParseFactor();
					continue;
				}
				if (Eat('/') || Eat('÷'))
				{
					num /= ParseFactor();
					continue;
				}
				if (!Eat('%'))
				{
					break;
				}
				num %= ParseFactor();
			}
			return num;
		}

		private double ParseFactor()
		{
			double num = ParseUnary();
			if (!Eat('^'))
			{
				return num;
			}
			return Math.Pow(num, ParseFactor());
		}

		private double ParseUnary()
		{
			if (Eat('-'))
			{
				return 0.0 - ParseUnary();
			}
			if (Eat('+'))
			{
				return ParseUnary();
			}
			return ParsePrimary();
		}

		private double ParsePrimary()
		{
			SkipSpaces();
			if (Eat('('))
			{
				double result = ParseExpression();
				if (!Eat(')'))
				{
					throw new FormatException();
				}
				return result;
			}
			int num;
			if (_i < _s.Length && char.IsLetter(_s[_i]))
			{
				int i = _i;
				while (_i < _s.Length && char.IsLetter(_s[_i]))
				{
					_i++;
				}
				string s = _s;
				num = i;
				string text = s.Substring(num, _i - num).ToLowerInvariant();
				if (!(text == "pi"))
				{
					if (text == "e")
					{
						return Math.E;
					}
					if (!Eat('('))
					{
						throw new FormatException();
					}
					double num2 = ParseExpression();
					if (!Eat(')'))
					{
						throw new FormatException();
					}
					return text switch
					{
						"sqrt" => Math.Sqrt(num2), 
						"sin" => Math.Sin(num2), 
						"cos" => Math.Cos(num2), 
						"tan" => Math.Tan(num2), 
						"ln" => Math.Log(num2), 
						"log" => Math.Log10(num2), 
						"abs" => Math.Abs(num2), 
						"round" => Math.Round(num2), 
						"floor" => Math.Floor(num2), 
						"ceil" => Math.Ceiling(num2), 
						_ => throw new FormatException(), 
					};
				}
				return Math.PI;
			}
			int i2 = _i;
			while (true)
			{
				bool flag = _i < _s.Length;
				if (flag)
				{
					bool flag2 = char.IsDigit(_s[_i]);
					if (!flag2)
					{
						char c = _s[_i];
						bool flag3 = ((c == ',' || c == '.') ? true : false);
						flag2 = flag3;
					}
					flag = flag2;
				}
				if (!flag)
				{
					break;
				}
				_i++;
			}
			if (i2 == _i)
			{
				throw new FormatException();
			}
			string s2 = _s;
			num = i2;
			if (!double.TryParse(s2.Substring(num, _i - num).Replace(',', '.'), NumberStyles.Float, CultureInfo.InvariantCulture, out var result2))
			{
				throw new FormatException();
			}
			return result2;
		}
	}

	public static bool LooksLikeExpression(string text)
	{
		string text2 = text.Trim();
		if (text2.StartsWith('='))
		{
			return true;
		}
		if (text2.Length > 1 && text2.Any(char.IsDigit) && text2.IndexOfAny(new char[7] { '+', '-', '*', '/', '^', '%', '(' }) > 0)
		{
			return text2.All((char ch) => char.IsDigit(ch) || char.IsWhiteSpace(ch) || "+-*/^%().,".Contains(ch) || char.IsLetter(ch));
		}
		return false;
	}

	public static bool TryEvaluate(string text, out double value)
	{
		value = 0.0;
		try
		{
			Parser parser = new Parser(text.Trim().TrimStart('='));
			value = parser.ParseExpression();
			parser.SkipSpaces();
			return parser.AtEnd && !double.IsNaN(value) && !double.IsInfinity(value);
		}
		catch (FormatException)
		{
			return false;
		}
	}

	public static string Format(double value)
	{
		if (!(Math.Abs(value) >= 1000000000000000.0) && (!(Math.Abs(value) < 1E-06) || value == 0.0))
		{
			return Math.Round(value, 10).ToString("0.##########", CultureInfo.InvariantCulture);
		}
		return value.ToString("G10", CultureInfo.InvariantCulture);
	}
}
