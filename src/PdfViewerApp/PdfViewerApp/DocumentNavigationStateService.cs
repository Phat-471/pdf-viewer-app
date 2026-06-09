using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;

namespace PdfViewerApp;

internal sealed class DocumentNavigationState
{
	public List<int> RecentPages { get; set; } = new List<int>();

	public List<int> BookmarkedPages { get; set; } = new List<int>();
}

internal static class DocumentNavigationStateService
{
	private static readonly object SyncRoot = new object();

	private static readonly string StatePath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "PdfPro", "document-navigation.json");

	public static DocumentNavigationState Load(string pdfPath, int maxPage)
	{
		if (maxPage <= 0)
		{
			return new DocumentNavigationState();
		}

		string key = NormalizePath(pdfPath);
		if (string.IsNullOrWhiteSpace(key))
		{
			return new DocumentNavigationState();
		}

		lock (SyncRoot)
		{
			try
			{
				Dictionary<string, DocumentNavigationState> states = LoadAll();
				if (!states.TryGetValue(key, out DocumentNavigationState state) || state == null)
				{
					return new DocumentNavigationState();
				}

				return Sanitize(state, maxPage);
			}
			catch
			{
				return new DocumentNavigationState();
			}
		}
	}

	public static void Save(string pdfPath, IEnumerable<int> recentPages, IEnumerable<int> bookmarkedPages, int maxPage)
	{
		if (maxPage <= 0)
		{
			return;
		}

		string key = NormalizePath(pdfPath);
		if (string.IsNullOrWhiteSpace(key))
		{
			return;
		}

		lock (SyncRoot)
		{
			try
			{
				Dictionary<string, DocumentNavigationState> states = LoadAll();
				states[key] = Sanitize(new DocumentNavigationState
				{
					RecentPages = recentPages?.ToList() ?? new List<int>(),
					BookmarkedPages = bookmarkedPages?.ToList() ?? new List<int>()
				}, maxPage);
				SaveAll(states);
			}
			catch
			{
			}
		}
	}

	private static Dictionary<string, DocumentNavigationState> LoadAll()
	{
		string directory = Path.GetDirectoryName(StatePath) ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		if (!File.Exists(StatePath))
		{
			return new Dictionary<string, DocumentNavigationState>(StringComparer.OrdinalIgnoreCase);
		}

		Dictionary<string, DocumentNavigationState> states = JsonSerializer.Deserialize<Dictionary<string, DocumentNavigationState>>(File.ReadAllText(StatePath));
		return states ?? new Dictionary<string, DocumentNavigationState>(StringComparer.OrdinalIgnoreCase);
	}

	private static void SaveAll(Dictionary<string, DocumentNavigationState> states)
	{
		string directory = Path.GetDirectoryName(StatePath) ?? string.Empty;
		if (!string.IsNullOrWhiteSpace(directory))
		{
			Directory.CreateDirectory(directory);
		}

		File.WriteAllText(StatePath, JsonSerializer.Serialize(states, new JsonSerializerOptions
		{
			WriteIndented = true
		}));
	}

	private static DocumentNavigationState Sanitize(DocumentNavigationState state, int maxPage)
	{
		return new DocumentNavigationState
		{
			RecentPages = state.RecentPages
				.Where(page => page >= 1 && page <= maxPage)
				.Distinct()
				.Take(8)
				.ToList(),
			BookmarkedPages = state.BookmarkedPages
				.Where(page => page >= 1 && page <= maxPage)
				.Distinct()
				.OrderBy(page => page)
				.ToList()
		};
	}

	private static string NormalizePath(string path)
	{
		try
		{
			return Path.GetFullPath(path);
		}
		catch
		{
			return string.Empty;
		}
	}
}
