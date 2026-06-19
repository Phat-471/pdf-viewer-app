using System;
using System.Collections.Generic;
using System.IO;
using System.Numerics;

namespace PdfViewerApp;

internal sealed class NaturalFilePathComparer : IComparer<string>
{
	public static NaturalFilePathComparer Instance { get; } = new NaturalFilePathComparer();

	public int Compare(string? x, string? y)
	{
		if ((object)x == y)
		{
			return 0;
		}
		if (x == null)
		{
			return -1;
		}
		if (y == null)
		{
			return 1;
		}
		string? fileName = Path.GetFileName(x);
		string fileName2 = Path.GetFileName(y);
		int num = CompareNatural(fileName, fileName2);
		if (num != 0)
		{
			return num;
		}
		return StringComparer.OrdinalIgnoreCase.Compare(x, y);
	}

	private static int CompareNatural(string left, string right)
	{
		int index = 0;
		int index2 = 0;
		while (index < left.Length && index2 < right.Length)
		{
			char c = left[index];
			char c2 = right[index2];
			if (char.IsDigit(c) && char.IsDigit(c2))
			{
				BigInteger bigInteger = ReadNumber(left, ref index);
				BigInteger other = ReadNumber(right, ref index2);
				int num = bigInteger.CompareTo(other);
				if (num != 0)
				{
					return num;
				}
			}
			else
			{
				int num2 = char.ToUpperInvariant(c).CompareTo(char.ToUpperInvariant(c2));
				if (num2 != 0)
				{
					return num2;
				}
				index++;
				index2++;
			}
		}
		if (index < left.Length)
		{
			return 1;
		}
		if (index2 < right.Length)
		{
			return -1;
		}
		return StringComparer.OrdinalIgnoreCase.Compare(left, right);
	}

	private static BigInteger ReadNumber(string value, ref int index)
	{
		BigInteger bigInteger = BigInteger.Zero;
		while (index < value.Length && char.IsDigit(value[index]))
		{
			int num = value[index] - 48;
			bigInteger = bigInteger * 10 + num;
			index++;
		}
		return bigInteger;
	}
}
