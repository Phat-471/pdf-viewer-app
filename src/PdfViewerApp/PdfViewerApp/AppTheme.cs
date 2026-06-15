using System.Collections.Generic;

namespace PdfViewerApp;

/// <summary>
/// Định nghĩa tất cả các theme màu của ứng dụng.
/// Mỗi theme gồm các token màu chính để áp dụng xuyên suốt UI.
/// </summary>
internal sealed class AppThemeDefinition
{
	/// <summary>Tên nội bộ (key) của theme, ví dụ "Dark", "Ocean".</summary>
	public string Name { get; init; } = "";

	/// <summary>Tên hiển thị cho người dùng.</summary>
	public string DisplayName { get; init; } = "";

	/// <summary>Emoji icon cho theme.</summary>
	public string Icon { get; init; } = "";

	/// <summary>Màu nền chính của cửa sổ (root background).</summary>
	public string WindowBackground { get; init; } = "";

	/// <summary>Màu nền của title bar.</summary>
	public string TitleBarBackground { get; init; } = "";

	/// <summary>Màu nền của card / panel phụ.</summary>
	public string PanelBackground { get; init; } = "";

	/// <summary>Màu nền của surface thứ 2 (item list, sidebar).</summary>
	public string SurfaceBackground { get; init; } = "";

	/// <summary>Màu nền hover / active.</summary>
	public string HoverBackground { get; init; } = "";

	/// <summary>Màu viền.</summary>
	public string BorderColor { get; init; } = "";

	/// <summary>Màu accent chính (nút bấm, highlight).</summary>
	public string AccentColor { get; init; } = "";

	/// <summary>Màu accent tối hơn (gradient start).</summary>
	public string AccentDark { get; init; } = "";

	/// <summary>Màu chữ chính.</summary>
	public string ForegroundPrimary { get; init; } = "";

	/// <summary>Màu chữ phụ (mô tả, nhãn).</summary>
	public string ForegroundSecondary { get; init; } = "";

	/// <summary>Màu nền status bar (gradient start).</summary>
	public string StatusBarStart { get; init; } = "";

	/// <summary>Màu nền status bar (gradient mid).</summary>
	public string StatusBarMid { get; init; } = "";

	/// <summary>Tên theme trong ControlzEx (MahApps Fluent): "Dark.Blue", "Light.Blue", v.v.</summary>
	public string FluentTheme { get; init; } = "Dark.Blue";

	/// <summary>Theme có nền sáng (light) không.</summary>
	public bool IsLight { get; init; } = false;
}

/// <summary>
/// Registry tập trung tất cả các theme có sẵn.
/// </summary>
internal static class AppThemeRegistry
{
	// ─── Tên các theme ───────────────────────────────────────────────────────
	public const string Dark     = "Dark";
	public const string Light    = "Light";
	public const string Midnight = "Midnight";
	public const string Forest   = "Forest";
	public const string Sunset   = "Sunset";
	public const string Ocean    = "Ocean";
	public const string Sakura   = "Sakura";
	public const string Mint     = "Mint";

	/// <summary>Tất cả theme theo thứ tự hiển thị trong UI.</summary>
	public static readonly IReadOnlyList<AppThemeDefinition> All = new List<AppThemeDefinition>
	{
		new AppThemeDefinition
		{
			Name                = Dark,
			DisplayName         = "Tối",
			Icon                = "🌑",
			WindowBackground    = "#0B0F19",
			TitleBarBackground  = "#0F172A",
			PanelBackground     = "#111827",
			SurfaceBackground   = "#0B1220",
			HoverBackground     = "#1E293B",
			BorderColor         = "#1E293B",
			AccentColor         = "#14B8A6",
			AccentDark          = "#0F766E",
			ForegroundPrimary   = "#F8FAFC",
			ForegroundSecondary = "#94A3B8",
			StatusBarStart      = "#0F172A",
			StatusBarMid        = "#1E293B",
			FluentTheme         = "Dark.Blue",
			IsLight             = false,
		},
		new AppThemeDefinition
		{
			Name                = Light,
			DisplayName         = "Sáng",
			Icon                = "☀️",
			WindowBackground    = "#F8FAFC",
			TitleBarBackground  = "#E2E8F0",
			PanelBackground     = "#FFFFFF",
			SurfaceBackground   = "#F1F5F9",
			HoverBackground     = "#E2E8F0",
			BorderColor         = "#CBD5E1",
			AccentColor         = "#0F766E",
			AccentDark          = "#0D9488",
			ForegroundPrimary   = "#0F172A",
			ForegroundSecondary = "#475569",
			StatusBarStart      = "#E2E8F0",
			StatusBarMid        = "#F1F5F9",
			FluentTheme         = "Light.Blue",
			IsLight             = true,
		},
		new AppThemeDefinition
		{
			Name                = Midnight,
			DisplayName         = "Đêm Tối",
			Icon                = "🌌",
			WindowBackground    = "#0D0D1A",
			TitleBarBackground  = "#12122A",
			PanelBackground     = "#161630",
			SurfaceBackground   = "#0A0A1A",
			HoverBackground     = "#1F1F45",
			BorderColor         = "#2D2B55",
			AccentColor         = "#8B5CF6",
			AccentDark          = "#7C3AED",
			ForegroundPrimary   = "#EDE9FE",
			ForegroundSecondary = "#A78BFA",
			StatusBarStart      = "#12122A",
			StatusBarMid        = "#1F1F45",
			FluentTheme         = "Dark.Purple",
			IsLight             = false,
		},
		new AppThemeDefinition
		{
			Name                = Forest,
			DisplayName         = "Rừng Xanh",
			Icon                = "🌿",
			WindowBackground    = "#071A10",
			TitleBarBackground  = "#0A2218",
			PanelBackground     = "#0D2B1E",
			SurfaceBackground   = "#071710",
			HoverBackground     = "#143D27",
			BorderColor         = "#1A4D30",
			AccentColor         = "#22C55E",
			AccentDark          = "#16A34A",
			ForegroundPrimary   = "#DCFCE7",
			ForegroundSecondary = "#86EFAC",
			StatusBarStart      = "#0A2218",
			StatusBarMid        = "#143D27",
			FluentTheme         = "Dark.Green",
			IsLight             = false,
		},
		new AppThemeDefinition
		{
			Name                = Sunset,
			DisplayName         = "Hoàng Hôn",
			Icon                = "🌅",
			WindowBackground    = "#1A0A06",
			TitleBarBackground  = "#2A1209",
			PanelBackground     = "#2D1710",
			SurfaceBackground   = "#170A06",
			HoverBackground     = "#3D2015",
			BorderColor         = "#4A2D1E",
			AccentColor         = "#F97316",
			AccentDark          = "#EA580C",
			ForegroundPrimary   = "#FEF3C7",
			ForegroundSecondary = "#FCA5A5",
			StatusBarStart      = "#2A1209",
			StatusBarMid        = "#3D2015",
			FluentTheme         = "Dark.Red",
			IsLight             = false,
		},
		new AppThemeDefinition
		{
			Name                = Ocean,
			DisplayName         = "Đại Dương",
			Icon                = "🌊",
			WindowBackground    = "#041226",
			TitleBarBackground  = "#071A38",
			PanelBackground     = "#0A2040",
			SurfaceBackground   = "#041020",
			HoverBackground     = "#0F2E55",
			BorderColor         = "#163A65",
			AccentColor         = "#38BDF8",
			AccentDark          = "#0284C7",
			ForegroundPrimary   = "#E0F2FE",
			ForegroundSecondary = "#7DD3FC",
			StatusBarStart      = "#071A38",
			StatusBarMid        = "#0F2E55",
			FluentTheme         = "Dark.Blue",
			IsLight             = false,
		},
		new AppThemeDefinition
		{
			Name                = Sakura,
			DisplayName         = "Anh Đào",
			Icon                = "🌸",
			WindowBackground    = "#FFF5F5",
			TitleBarBackground  = "#FFE3E3",
			PanelBackground     = "#FFFFFF",
			SurfaceBackground   = "#FFF0F0",
			HoverBackground     = "#FFE3E3",
			BorderColor         = "#FFD2D2",
			AccentColor         = "#FF6B6B",
			AccentDark          = "#FA5252",
			ForegroundPrimary   = "#495057",
			ForegroundSecondary = "#868E96",
			StatusBarStart      = "#FFE3E3",
			StatusBarMid        = "#FFF0F0",
			FluentTheme         = "Light.Red",
			IsLight             = true,
		},
		new AppThemeDefinition
		{
			Name                = Mint,
			DisplayName         = "Bạc Hà",
			Icon                = "🍃",
			WindowBackground    = "#F4FBF7",
			TitleBarBackground  = "#E6F4EA",
			PanelBackground     = "#FFFFFF",
			SurfaceBackground   = "#F0F9F4",
			HoverBackground     = "#E6F4EA",
			BorderColor         = "#CEEAD6",
			AccentColor         = "#0F9D58",
			AccentDark          = "#137333",
			ForegroundPrimary   = "#3C4043",
			ForegroundSecondary = "#5F6368",
			StatusBarStart      = "#E6F4EA",
			StatusBarMid        = "#F0F9F4",
			FluentTheme         = "Light.Green",
			IsLight             = true,
		},
	};

	/// <summary>Lấy theme theo tên, fallback về Dark nếu không tìm thấy.</summary>
	public static AppThemeDefinition Get(string? name)
	{
		if (!string.IsNullOrEmpty(name))
		{
			foreach (var t in All)
			{
				if (string.Equals(t.Name, name, System.StringComparison.OrdinalIgnoreCase))
					return t;
			}
		}
		return All[0]; // Dark
	}

	/// <summary>Tương thích ngược: chuyển bool IsDarkTheme → ThemeName.</summary>
	public static string FromLegacyBool(bool isDark) => isDark ? Dark : Light;
}
