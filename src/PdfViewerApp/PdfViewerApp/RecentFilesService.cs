using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PdfViewerApp;

internal static class RecentFilesService
{
	private const int MaxRecentFiles = 8;

	private static readonly object SyncRoot = new object();

	private static readonly string RecentFilesPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "recent-files.json");

	public static IReadOnlyList<string> Load()
	{
		lock (SyncRoot)
		{
			try
			{
				string? directory = Path.GetDirectoryName(RecentFilesPath);
				if (!string.IsNullOrWhiteSpace(directory))
				{
					Directory.CreateDirectory(directory);
				}

				if (!File.Exists(RecentFilesPath))
				{
					return Array.Empty<string>();
				}

				List<string>? items = JsonSerializer.Deserialize<List<string>>(File.ReadAllText(RecentFilesPath));
				if (items == null)
				{
					return Array.Empty<string>();
				}

				return items.Where(File.Exists).Distinct(StringComparer.OrdinalIgnoreCase).Take(MaxRecentFiles).ToArray();
			}
			catch
			{
				return Array.Empty<string>();
			}
		}
	}

	public static void Record(string path)
	{
		if (string.IsNullOrWhiteSpace(path))
		{
			return;
		}

		string fullPath;
		try
		{
			fullPath = Path.GetFullPath(path);
		}
		catch
		{
			return;
		}

		if (!File.Exists(fullPath))
		{
			return;
		}

		lock (SyncRoot)
		{
			List<string> items = Load().ToList();
			items.RemoveAll(item => string.Equals(item, fullPath, StringComparison.OrdinalIgnoreCase));
			items.Insert(0, fullPath);
			items = items.Take(MaxRecentFiles).ToList();
			Save(items);
		}
	}

	private static void Save(List<string> items)
	{
		string? directory = Path.GetDirectoryName(RecentFilesPath);
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(RecentFilesPath, JsonSerializer.Serialize(items, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}
}
