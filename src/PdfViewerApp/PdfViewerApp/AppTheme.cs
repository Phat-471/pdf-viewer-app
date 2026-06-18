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

	// ─── Các thuộc tính dành riêng cho việc hiển thị badge nút theme ─────────

	/// <summary>Mã glyph Segoe MDL2 Assets đại diện cho theme.</summary>
	public string ThemeBadgeIconGlyph { get; init; } = "";

	/// <summary>Màu nền của badge chọn theme.</summary>
	public string ThemeBadgeBackground { get; init; } = "";

	/// <summary>Màu viền của badge chọn theme.</summary>
	public string ThemeBadgeBorder { get; init; } = "";

	/// <summary>Màu icon bên trong badge chọn theme.</summary>
	public string ThemeBadgeIconColor { get; init; } = "";

	/// <summary>Màu của hiệu ứng đổ bóng/phát sáng của badge.</summary>
	public string ThemeBadgeGlowColor { get; init; } = "";
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
		// ─── 1. TỐI (DARK) - Mặc định ──────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Dark,
			DisplayName           = "Tối",
			Icon                  = "🌑",
			WindowBackground      = "#0B0F19",
			TitleBarBackground    = "#0F172A",
			PanelBackground       = "#111827",
			SurfaceBackground     = "#0B1220",
			HoverBackground       = "#1E293B",
			BorderColor           = "#1E293B",
			AccentColor           = "#14B8A6",
			AccentDark            = "#0F766E",
			ForegroundPrimary     = "#F8FAFC",
			ForegroundSecondary   = "#94A3B8",
			StatusBarStart        = "#0F172A",
			StatusBarMid          = "#1E293B",
			FluentTheme           = "Dark.Blue",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE708", 
			ThemeBadgeBackground  = "#1E293B", // Chìm vào nền
			ThemeBadgeBorder      = "#14B8A6", 
			ThemeBadgeIconColor   = "#2DD4BF", // Phát sáng Teal
			ThemeBadgeGlowColor   = "#14B8A6",
		},
		// ─── 2. SÁNG (LIGHT) ───────────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Light,
			DisplayName           = "Sáng",
			Icon                  = "☀️",
			WindowBackground      = "#F8FAFC",
			TitleBarBackground    = "#E2E8F0",
			PanelBackground       = "#FFFFFF",
			SurfaceBackground     = "#F1F5F9",
			HoverBackground       = "#E2E8F0",
			BorderColor           = "#CBD5E1",
			AccentColor           = "#0F766E",
			AccentDark            = "#0D9488",
			ForegroundPrimary     = "#0F172A",
			ForegroundSecondary   = "#475569",
			StatusBarStart        = "#E2E8F0",
			StatusBarMid          = "#F1F5F9",
			FluentTheme           = "Light.Blue",
			IsLight               = true,
			ThemeBadgeIconGlyph   = "\uE706", 
			ThemeBadgeBackground  = "#F1F5F9", 
			ThemeBadgeBorder      = "#0F766E", 
			ThemeBadgeIconColor   = "#B45309", // Hổ phách đậm tương phản tốt
			ThemeBadgeGlowColor   = "#000000",
		},
		// ─── 3. TÍM (MIDNIGHT) ─────────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Midnight,
			DisplayName           = "Tím",
			Icon                  = "🌌",
			WindowBackground      = "#0D0D1A",
			TitleBarBackground    = "#12122A",
			PanelBackground       = "#161630",
			SurfaceBackground     = "#0A0A1A",
			HoverBackground       = "#1F1F45",
			BorderColor           = "#2D2B55",
			AccentColor           = "#8B5CF6",
			AccentDark            = "#7C3AED",
			ForegroundPrimary     = "#EDE9FE",
			ForegroundSecondary   = "#A78BFA",
			StatusBarStart        = "#12122A",
			StatusBarMid          = "#1F1F45",
			FluentTheme           = "Dark.Purple",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE735", 
			ThemeBadgeBackground  = "#1F1F45", 
			ThemeBadgeBorder      = "#8B5CF6", 
			ThemeBadgeIconColor   = "#A78BFA", // Lilac rực rỡ
			ThemeBadgeGlowColor   = "#8B5CF6",
		},
		// ─── 4. RỪNG XANH (FOREST) ─────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Forest,
			DisplayName           = "Rừng Xanh",
			Icon                  = "🌿",
			WindowBackground      = "#071A10",
			TitleBarBackground    = "#0A2218",
			PanelBackground       = "#0D2B1E",
			SurfaceBackground     = "#071710",
			HoverBackground       = "#143D27",
			BorderColor           = "#1A4D30",
			AccentColor           = "#22C55E",
			AccentDark            = "#16A34A",
			ForegroundPrimary     = "#DCFCE7",
			ForegroundSecondary   = "#86EFAC",
			StatusBarStart        = "#0A2218",
			StatusBarMid          = "#143D27",
			FluentTheme           = "Dark.Green",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE70E", 
			ThemeBadgeBackground  = "#143D27", 
			ThemeBadgeBorder      = "#22C55E", 
			ThemeBadgeIconColor   = "#4ADE80", // Xanh lục non
			ThemeBadgeGlowColor   = "#22C55E",
		},
		// ─── 5. HOÀNG HÔN (SUNSET) ─────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Sunset,
			DisplayName           = "Hoàng Hôn",
			Icon                  = "🌅",
			WindowBackground      = "#1A0A06",
			TitleBarBackground    = "#2A1209",
			PanelBackground       = "#2D1710",
			SurfaceBackground     = "#170A06",
			HoverBackground       = "#3D2015",
			BorderColor           = "#4A2D1E",
			AccentColor           = "#F97316",
			AccentDark            = "#EA580C",
			ForegroundPrimary     = "#FEF3C7",
			ForegroundSecondary   = "#FCA5A5",
			StatusBarStart        = "#2A1209",
			StatusBarMid          = "#3D2015",
			FluentTheme           = "Dark.Red",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE706", // Đã fix icon mặt trời
			ThemeBadgeBackground  = "#3D2015", 
			ThemeBadgeBorder      = "#F97316", 
			ThemeBadgeIconColor   = "#FB923C", // Cam chói neon
			ThemeBadgeGlowColor   = "#F97316",
		},
		// ─── 6. ĐẠI DƯƠNG (OCEAN) ──────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Ocean,
			DisplayName           = "Đại Dương",
			Icon                  = "🌊",
			WindowBackground      = "#041226",
			TitleBarBackground    = "#071A38",
			PanelBackground       = "#0A2040",
			SurfaceBackground     = "#041020",
			HoverBackground       = "#0F2E55",
			BorderColor           = "#163A65",
			AccentColor           = "#38BDF8",
			AccentDark            = "#0284C7",
			ForegroundPrimary     = "#E0F2FE",
			ForegroundSecondary   = "#7DD3FC",
			StatusBarStart        = "#071A38",
			StatusBarMid          = "#0F2E55",
			FluentTheme           = "Dark.Blue",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE9A1", 
			ThemeBadgeBackground  = "#0F2E55", 
			ThemeBadgeBorder      = "#38BDF8", 
			ThemeBadgeIconColor   = "#7DD3FC", // Xanh thiên thanh sáng
			ThemeBadgeGlowColor   = "#38BDF8",
		},
		// ─── 7. ANH ĐÀO (SAKURA) ───────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Sakura,
			DisplayName           = "Anh Đào",
			Icon                  = "🌸",
			WindowBackground      = "#FFF5F5",
			TitleBarBackground    = "#FFE3E3",
			PanelBackground       = "#FFFFFF",
			SurfaceBackground     = "#FFF0F0",
			HoverBackground       = "#FFE3E3",
			BorderColor           = "#FFD2D2",
			AccentColor           = "#FF6B6B",
			AccentDark            = "#FA5252",
			ForegroundPrimary     = "#495057",
			ForegroundSecondary   = "#868E96",
			StatusBarStart        = "#FFE3E3",
			StatusBarMid          = "#FFF0F0",
			FluentTheme           = "Light.Red",
			IsLight               = true,
			ThemeBadgeIconGlyph   = "\uE00B", 
			ThemeBadgeBackground  = "#FFF0F0", 
			ThemeBadgeBorder      = "#FF6B6B", 
			ThemeBadgeIconColor   = "#BE185D", // Đỏ mận đậm
			ThemeBadgeGlowColor   = "#000000",
		},
		// ─── 8. BẠC HÀ (MINT) ──────────────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = Mint,
			DisplayName           = "Bạc Hà",
			Icon                  = "🍃",
			WindowBackground      = "#F4FBF7",
			TitleBarBackground    = "#E6F4EA",
			PanelBackground       = "#FFFFFF",
			SurfaceBackground     = "#F0F9F4",
			HoverBackground       = "#E6F4EA",
			BorderColor           = "#CEEAD6",
			AccentColor           = "#0F9D58",
			AccentDark            = "#137333",
			ForegroundPrimary     = "#3C4043",
			ForegroundSecondary   = "#5F6368",
			StatusBarStart        = "#E6F4EA",
			StatusBarMid          = "#F0F9F4",
			FluentTheme           = "Light.Green",
			IsLight               = true,
			ThemeBadgeIconGlyph   = "\uE70E", 
			ThemeBadgeBackground  = "#F0F9F4", 
			ThemeBadgeBorder      = "#0F9D58", 
			ThemeBadgeIconColor   = "#065F46", // Xanh lục bảo sẫm
			ThemeBadgeGlowColor   = "#000000",
		},
		// ─── 9. OẢI HƯƠNG (LAVENDER) - MỚI ─────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = "Lavender",
			DisplayName           = "Oải Hương",
			Icon                  = "☂️",
			WindowBackground      = "#FAFAFF",
			TitleBarBackground    = "#F3E8FF",
			PanelBackground       = "#FFFFFF",
			SurfaceBackground     = "#FAF5FF",
			HoverBackground       = "#E9D5FF",
			BorderColor           = "#D8B4FE",
			AccentColor           = "#A855F7",
			AccentDark            = "#9333EA",
			ForegroundPrimary     = "#3B0764",
			ForegroundSecondary   = "#6B21A8",
			StatusBarStart        = "#F3E8FF",
			StatusBarMid          = "#FAF5FF",
			FluentTheme           = "Light.Purple",
			IsLight               = true,
			ThemeBadgeIconGlyph   = "\uE00B", 
			ThemeBadgeBackground  = "#FAF5FF", 
			ThemeBadgeBorder      = "#A855F7",
			ThemeBadgeIconColor   = "#7E22CE", // Tím đậm rõ nét
			ThemeBadgeGlowColor   = "#000000",
		},
		// ─── 10. CÀ PHÊ (MOCHA) - MỚI ──────────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = "Mocha",
			DisplayName           = "Cà Phê",
			Icon                  = "☕",
			WindowBackground      = "#1C1917",
			TitleBarBackground    = "#292524",
			PanelBackground       = "#44403C",
			SurfaceBackground     = "#292524",
			HoverBackground       = "#57534E",
			BorderColor           = "#57534E",
			AccentColor           = "#D97706",
			AccentDark            = "#B45309",
			ForegroundPrimary     = "#FFF7ED",
			ForegroundSecondary   = "#FDBA74",
			StatusBarStart        = "#292524",
			StatusBarMid          = "#1C1917",
			FluentTheme           = "Dark.Orange",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE735", 
			ThemeBadgeBackground  = "#292524", 
			ThemeBadgeBorder      = "#D97706",
			ThemeBadgeIconColor   = "#FBBF24", // Vàng hổ phách sáng
			ThemeBadgeGlowColor   = "#D97706",
		},
		// ─── 11. ĐỘT PHÁ (CYBERPUNK) - MỚI ─────────────────────────────────────
		new AppThemeDefinition
		{
			Name                  = "Cyberpunk",
			DisplayName           = "Đột Phá",
			Icon                  = "⚡",
			WindowBackground      = "#050505", // Đen sâu thẳm
			TitleBarBackground    = "#121212",
			PanelBackground       = "#1A1A1A",
			SurfaceBackground     = "#0F0F0F",
			HoverBackground       = "#262626",
			BorderColor           = "#333333",
			AccentColor           = "#F0168F",
			AccentDark            = "#C10B6E",
			ForegroundPrimary     = "#FFFFFF",
			ForegroundSecondary   = "#A3A3A3",
			StatusBarStart        = "#121212",
			StatusBarMid          = "#0F0F0F",
			FluentTheme           = "Dark.Magenta",
			IsLight               = false,
			ThemeBadgeIconGlyph   = "\uE706", 
			ThemeBadgeBackground  = "#121212", 
			ThemeBadgeBorder      = "#F0168F", 
			ThemeBadgeIconColor   = "#FF66C4", // Hồng Neon cực đại
			ThemeBadgeGlowColor   = "#F0168F",
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
