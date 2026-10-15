using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Terraria.Localization;

namespace MechaKeys.Core
{
	public enum SwitchKind
	{
		Linear,
		Tactile,
		Clicky,
		Silent,
		Special,
		Mouse,
		Custom
	}

	/// <summary>
	/// Static metadata for known packs: what kind of switch it is, the colour used for its
	/// stem in the animated logo, and where it sorts in the pack picker.
	/// Unknown (user-added) packs fall back to <see cref="SwitchKind.Custom"/>.
	/// </summary>
	internal static class PackInfo
	{
		internal readonly record struct Meta(SwitchKind Kind, Color Accent, int Order);

		private static readonly Dictionary<string, Meta> Known = new(StringComparer.OrdinalIgnoreCase) {
			// Linear
			["cherrymx_red_abs"] = new(SwitchKind.Linear, new Color(255, 77, 94), 100),
			["gateron_yellow"] = new(SwitchKind.Linear, new Color(255, 210, 63), 110),
			["oil_king"] = new(SwitchKind.Linear, new Color(150, 156, 176), 120),
			["cherrymx_black_abs"] = new(SwitchKind.Linear, new Color(170, 176, 196), 130),
			["cherrymx_black_pbt"] = new(SwitchKind.Linear, new Color(170, 176, 196), 131),
			["kailh_choc_red"] = new(SwitchKind.Linear, new Color(255, 110, 80), 140),

			// Tactile
			["cherrymx_brown_abs"] = new(SwitchKind.Tactile, new Color(208, 138, 60), 200),
			["cherrymx_brown_pbt"] = new(SwitchKind.Tactile, new Color(208, 138, 60), 201),
			["holy_panda"] = new(SwitchKind.Tactile, new Color(255, 122, 200), 210),

			// Clicky
			["cherrymx_blue_abs"] = new(SwitchKind.Clicky, new Color(63, 169, 255), 300),
			["cherrymx_blue_abs_2"] = new(SwitchKind.Clicky, new Color(63, 169, 255), 301),
			["kailh_box_jade"] = new(SwitchKind.Clicky, new Color(54, 211, 154), 310),
			["kailh_box_navy"] = new(SwitchKind.Clicky, new Color(90, 120, 255), 320),
			["alps_skcm_blue"] = new(SwitchKind.Clicky, new Color(94, 200, 255), 330),

			// Silent
			["silent_linear"] = new(SwitchKind.Silent, new Color(167, 123, 255), 400),
			["boba_u4"] = new(SwitchKind.Silent, new Color(190, 150, 255), 410),

			// Special / vintage
			["topre"] = new(SwitchKind.Special, new Color(230, 230, 236), 500),
			["buckling_spring"] = new(SwitchKind.Special, new Color(192, 200, 216), 510),
			["membrane"] = new(SwitchKind.Special, new Color(157, 176, 192), 520),
			["typewriter"] = new(SwitchKind.Special, new Color(232, 194, 90), 530),

			// Mouse
			["mouse_classic"] = new(SwitchKind.Mouse, new Color(120, 230, 255), 600),
			["mouse_optical"] = new(SwitchKind.Mouse, new Color(140, 255, 220), 610),
			["mouse_silent"] = new(SwitchKind.Mouse, new Color(180, 160, 255), 620),
			["hit7"] = new(SwitchKind.Mouse, new Color(255, 214, 90), 630),
			["hit8"] = new(SwitchKind.Mouse, new Color(255, 214, 90), 631),
			["hit"] = new(SwitchKind.Mouse, new Color(200, 210, 230), 640),
			["hit1"] = new(SwitchKind.Mouse, new Color(200, 210, 230), 641),
			["hit2"] = new(SwitchKind.Mouse, new Color(200, 210, 230), 642),
			["hit3"] = new(SwitchKind.Mouse, new Color(200, 210, 230), 643),
			["hit4"] = new(SwitchKind.Mouse, new Color(200, 210, 230), 644),
			["hit5"] = new(SwitchKind.Mouse, new Color(255, 90, 160), 650),
			["hit6"] = new(SwitchKind.Mouse, new Color(120, 255, 140), 651),
		};

		private static readonly Meta CustomMeta = new(SwitchKind.Custom, new Color(240, 240, 240), 900);

		internal static Meta Get(string id) =>
			!string.IsNullOrWhiteSpace(id) && Known.TryGetValue(id, out Meta m) ? m : CustomMeta;

		internal static Color KindColor(SwitchKind kind) => kind switch {
			SwitchKind.Linear => new Color(255, 120, 120),
			SwitchKind.Tactile => new Color(230, 170, 100),
			SwitchKind.Clicky => new Color(100, 190, 255),
			SwitchKind.Silent => new Color(190, 160, 255),
			SwitchKind.Special => new Color(240, 220, 140),
			SwitchKind.Mouse => new Color(130, 235, 235),
			_ => new Color(200, 200, 200)
		};

		internal static string KindName(SwitchKind kind) =>
			Language.GetTextValue("Mods.MechaKeys.Kinds." + kind);

		/// <summary>Optional one-line description from localization (Mods.MechaKeys.PackInfo.&lt;id&gt;).</summary>
		internal static string Description(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return null;
			string key = "Mods.MechaKeys.PackInfo." + id;
			string text = Language.GetTextValue(key);
			return string.IsNullOrEmpty(text) || text == key ? null : text;
		}
	}
}
