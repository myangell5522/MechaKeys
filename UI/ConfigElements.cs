using System;
using System.Collections.Generic;
using MechaKeys.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria;
using Terraria.GameContent;
using Terraria.Localization;
using Terraria.ModLoader;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace MechaKeys.UI
{
	internal static class ConfigDraw
	{
		internal static readonly Vector2 TextScale = new(0.8f);

		internal static string L(string key) => Language.GetTextValue("Mods.MechaKeys.UI." + key);

		internal static Vector2 Measure(string text) =>
			ChatManager.GetStringSize(FontAssets.ItemStack.Value, text, TextScale);

		internal static void Text(SpriteBatch spriteBatch, string text, Vector2 pos, Color color, float maxWidth = -1f)
		{
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, text, pos, color, 0f, Vector2.Zero, TextScale, maxWidth, 2f);
		}

		internal static void ValueOnRight(SpriteBatch spriteBatch, CalculatedStyle dims, string text)
		{
			if (string.IsNullOrEmpty(text))
				return;
			Vector2 size = Measure(text);
			Text(spriteBatch, text, new Vector2(dims.X + dims.Width - size.X - 10f, dims.Y + 8f), Color.White, dims.Width);
		}

		/// <summary>Small rounded "chip" button.</summary>
		internal static void Chip(SpriteBatch spriteBatch, Rectangle r, Color fill, bool hover, bool active)
		{
			Color c = active ? Color.Lerp(fill, Color.White, 0.25f) : fill * (hover ? 1f : 0.75f);
			Utils.DrawInvBG(spriteBatch, r, c);
			if (active || hover)
				Utils.DrawInvBG(spriteBatch, r, Color.White * (active ? 0.18f : 0.08f));
		}

		internal static void PlayUiTick()
		{
			// Every config button plays a real click from the current pack, so the generic
			// mouse click for this press is suppressed to avoid a double sound.
			ClickAudio.SuppressMouse();
		}
	}

	// ------------------------------------------------------------------ pack picker
	/// <summary>
	/// "◀  [Clicky]  Kailh Box Jade   7/31  ▶" — click the arrows (or left/right click anywhere)
	/// to step through packs. Every change previews the new pack immediately.
	/// </summary>
	public class PackPickerElement : ConfigElement<string>
	{
		private Rectangle _prev;
		private Rectangle _next;

		private bool IsMouseField => MemberInfo != null && MemberInfo.Name.Contains("Mouse", StringComparison.Ordinal);

		private bool Linked => IsMouseField && Item is MechaKeysConfig cfg && cfg.MouseSameAsKeyboard;

		public override void OnBind()
		{
			base.OnBind();
			Height.Set(34f, 0f);
			Func<string> baseTooltip = TooltipFunction;
			TooltipFunction = () => {
				string tip = baseTooltip?.Invoke() ?? "";
				string pack = Value;
				PackInfo.Meta meta = PackInfo.Get(pack);
				string kind = $"[c/{PackInfo.KindColor(meta.Kind).Hex3()}:{PackInfo.KindName(meta.Kind)}]";
				string desc = PackInfo.Description(pack);
				string head = SoundPackCatalog.DisplayName(pack) + " — " + kind;
				if (!string.IsNullOrEmpty(desc))
					head += "\n" + desc;
				if (Linked)
					head += "\n[c/FFC060:" + ConfigDraw.L("MouseLinked") + "]";
				return head + (string.IsNullOrEmpty(tip) ? "" : "\n\n" + tip);
			};
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle dims = GetDimensions();
			string pack = Linked && Item is MechaKeysConfig c ? c.KeyboardPack : Value;
			List<string> names = SoundPackCatalog.PackNamesFor(IsMouseField);
			int index = names.FindIndex(n => string.Equals(n, pack, StringComparison.OrdinalIgnoreCase));

			PackInfo.Meta meta = PackInfo.Get(pack);
			string kind = $"[c/{PackInfo.KindColor(meta.Kind).Hex3()}:{PackInfo.KindName(meta.Kind)}]";
			string name = SoundPackCatalog.DisplayName(pack);
			string count = names.Count > 0 && index >= 0 ? $"  [c/8C8CA0:{index + 1}/{names.Count}]" : "";
			string middle = Linked ? $"[c/A0A0B4:{ConfigDraw.L("SameAsKeyboard")}]" : $"{kind}  {name}{count}";

			const int arrowW = 22;
			float y = dims.Y + 8f;
			float right = dims.X + dims.Width - 8f;
			_next = new Rectangle((int)(right - arrowW), (int)dims.Y + 5, arrowW, (int)dims.Height - 10);
			Vector2 midSize = ConfigDraw.Measure(middle);
			float midX = _next.X - 6f - midSize.X;
			_prev = new Rectangle((int)(midX - 6f - arrowW), (int)dims.Y + 5, arrowW, (int)dims.Height - 10);

			Point mouse = Main.MouseScreen.ToPoint();
			Color accent = meta.Accent;
			if (!Linked) {
				ConfigDraw.Chip(spriteBatch, _prev, accent * 0.55f, _prev.Contains(mouse), false);
				ConfigDraw.Chip(spriteBatch, _next, accent * 0.55f, _next.Contains(mouse), false);
				ConfigDraw.Text(spriteBatch, "<", new Vector2(_prev.X + 7f, y), Color.White);
				ConfigDraw.Text(spriteBatch, ">", new Vector2(_next.X + 8f, y), Color.White);
			}

			ConfigDraw.Text(spriteBatch, middle, new Vector2(midX, y), Linked ? Color.Gray : Color.White);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (Linked)
				return;
			Cycle(_prev.Contains(evt.MousePosition.ToPoint()) ? -1 : 1);
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			if (Linked)
				return;
			Cycle(-1);
		}

		private void Cycle(int dir)
		{
			List<string> names = SoundPackCatalog.PackNamesFor(IsMouseField);
			if (names.Count == 0)
				return;

			int i = names.FindIndex(n => string.Equals(n, Value, StringComparison.OrdinalIgnoreCase));
			if (i < 0)
				i = dir > 0 ? -1 : 0;
			i = ((i + dir) % names.Count + names.Count) % names.Count;
			Value = names[i];

			if (Item is MechaKeysConfig cfg) {
				ConfigDraw.PlayUiTick();
				if (IsMouseField)
					ClickAudio.PreviewMouse(cfg, Value);
				else
					ClickAudio.PreviewKeyboard(cfg, Value);
				LogoAnim.Pulse(Value);
			}
		}
	}

	// ------------------------------------------------------------------ presets
	/// <summary>One-click sound profiles. Sets keyboard + mouse packs (and volume for "Quiet").</summary>
	public class PresetElement : ConfigElement<bool>
	{
		private sealed record Preset(string Key, string Keyboard, string Mouse, int? Volume);

		private static readonly Preset[] Presets = {
			new("Clicky", "kailh_box_jade", "mouse_classic", null),
			new("Tactile", "holy_panda", "mouse_classic", null),
			new("Linear", "gateron_yellow", "mouse_optical", null),
			new("Thocky", "oil_king", "mouse_silent", null),
			new("Retro", "buckling_spring", "mouse_classic", null),
			new("Quiet", "silent_linear", "mouse_silent", 70),
		};

		private readonly Rectangle[] _chips = new Rectangle[Presets.Length];
		private int _hovered = -1;

		public override void OnBind()
		{
			base.OnBind();
			Height.Set(66f, 0f);
			Func<string> baseTooltip = TooltipFunction;
			TooltipFunction = () => _hovered >= 0
				? Language.GetTextValue("Mods.MechaKeys.Presets." + Presets[_hovered].Key + ".Tooltip")
				: baseTooltip?.Invoke();
		}

		private string ActiveKey()
		{
			if (Item is not MechaKeysConfig cfg)
				return null;
			foreach (Preset p in Presets) {
				if (string.Equals(cfg.KeyboardPack, p.Keyboard, StringComparison.OrdinalIgnoreCase)
				    && string.Equals(cfg.EffectiveMousePack, p.Mouse, StringComparison.OrdinalIgnoreCase))
					return p.Key;
			}

			return null;
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle dims = GetDimensions();
			const float gap = 6f;
			float x = dims.X + 8f;
			float w = (dims.Width - 16f - gap * (Presets.Length - 1)) / Presets.Length;
			float top = dims.Y + 32f;
			Point mouse = Main.MouseScreen.ToPoint();
			string active = ActiveKey();
			_hovered = -1;

			for (int i = 0; i < Presets.Length; i++) {
				Preset p = Presets[i];
				var r = new Rectangle((int)(x + i * (w + gap)), (int)top, (int)w, 26);
				_chips[i] = r;
				bool missing = !SoundPackCatalog.Exists(p.Keyboard);
				Color col = missing ? new Color(70, 70, 80) : PackInfo.Get(p.Keyboard).Accent;
				ConfigDraw.Chip(spriteBatch, r, col * 0.6f, r.Contains(mouse), p.Key == active);
				string label = Language.GetTextValue("Mods.MechaKeys.Presets." + p.Key + ".Label");
				Vector2 size = ConfigDraw.Measure(label);
				float scale = Math.Min(1f, (r.Width - 8f) / Math.Max(1f, size.X));
				ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, label,
					new Vector2(r.X + (r.Width - size.X * scale) * 0.5f, r.Y + 5f), missing ? Color.Gray : Color.White,
					0f, Vector2.Zero, ConfigDraw.TextScale * scale, -1f, 2f);

				if (r.Contains(mouse))
					_hovered = i;
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (Item is not MechaKeysConfig cfg)
				return;

			Point p = evt.MousePosition.ToPoint();
			for (int i = 0; i < Presets.Length; i++) {
				if (!_chips[i].Contains(p))
					continue;
				Preset preset = Presets[i];
				if (!SoundPackCatalog.Exists(preset.Keyboard))
					return;

				cfg.KeyboardPack = preset.Keyboard;
				if (SoundPackCatalog.Exists(preset.Mouse))
					cfg.MousePack = preset.Mouse;
				cfg.MouseSameAsKeyboard = false;
				cfg.KeyboardSounds = true;
				if (preset.Volume is int v) {
					cfg.KeyboardVolume = v;
					cfg.MouseVolume = v;
				}

				// Writing our own (unused) value marks the config as changed so "Save" lights up.
				Value = !Value;
				Value = false;

				ConfigDraw.PlayUiTick();
				ClickAudio.PreviewKeyboard(cfg);
				LogoAnim.Pulse(cfg.KeyboardPack);
				return;
			}
		}
	}

	// ------------------------------------------------------------------ action buttons
	public class TestKeyboardElement : ActionConfigElement
	{
		protected override void Run()
		{
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.PreviewKeyboard(cfg);
			LogoAnim.Pulse(cfg.KeyboardPack);
		}
	}

	public class TestMouseElement : ActionConfigElement
	{
		protected override void Run()
		{
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.PreviewMouse(cfg);
			LogoAnim.Pulse(cfg.EffectiveMousePack);
		}
	}

	public class OpenFolderElement : ActionConfigElement
	{
		protected override void Run()
		{
			SoundPackCatalog.EnsureInstalled(ModContent.GetInstance<MechaKeys>());
			SoundPackCatalog.OpenFolder();
		}
	}

	public class RescanElement : ActionConfigElement
	{
		protected override void Run()
		{
			SoundPackCatalog.EnsureInstalled(ModContent.GetInstance<MechaKeys>());
			SoundPackCatalog.Rescan();
			ClickAudio.ClearCache();
			ClickAudio.Warmup();
			if (Item is MechaKeysConfig cfg)
				SoundPackCatalog.EnsureConfigValid(cfg);
			LogoAnim.Pulse(false);
		}

		protected override string Status() =>
			Language.GetTextValue("Mods.MechaKeys.UI.PackCount", SoundPackCatalog.PackNames.Count);
	}

	public abstract class ActionConfigElement : ConfigElement<bool>
	{
		public override void OnBind()
		{
			base.OnBind();
			Height.Set(32f, 0f);
		}

		/// <summary>Optional grey text shown left of the play button.</summary>
		protected virtual string Status() => null;

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle dims = GetDimensions();
			float right = dims.X + dims.Width - 12f;
			if (PlayTexture?.Value is Texture2D tex) {
				var pos = new Vector2(right - tex.Width, dims.Y + (dims.Height - tex.Height) * 0.5f);
				spriteBatch.Draw(tex, pos, IsMouseHovering ? Color.White : Color.White * 0.8f);
				right = pos.X - 8f;
			}

			string status = Status();
			if (!string.IsNullOrEmpty(status)) {
				Vector2 size = ConfigDraw.Measure(status);
				ConfigDraw.Text(spriteBatch, status, new Vector2(right - size.X, dims.Y + 8f), new Color(150, 150, 170));
			}
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			ConfigDraw.PlayUiTick();
			Run();
		}

		protected abstract void Run();
	}

	// ------------------------------------------------------------------ logo
	public class LogoPreviewElement : ConfigElement<bool>
	{
		public override void OnBind()
		{
			base.OnBind();
			DrawLabel = false;
			Height.Set(100f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle dims = GetDimensions();
			const int size = 80;
			var dest = new Rectangle((int)(dims.X + 14f), (int)(dims.Y + (dims.Height - size) * 0.5f), size, size);
			LogoAnim.Draw(spriteBatch, dest);

			if (Item is not MechaKeysConfig cfg)
				return;

			// a compact summary next to the logo
			float x = dest.Right + 16f;
			float y = dims.Y + 14f;
			string kb = SoundPackCatalog.DisplayName(cfg.KeyboardPack);
			string ms = SoundPackCatalog.DisplayName(cfg.EffectiveMousePack);
			Color kbc = PackInfo.Get(cfg.KeyboardPack).Accent;
			Color msc = PackInfo.Get(cfg.EffectiveMousePack).Accent;
			string state = cfg.Enabled ? "" : $"  [c/FF6060:{ConfigDraw.L("Off")}]";
			float maxW = dims.X + dims.Width - x - 10f;
			ConfigDraw.Text(spriteBatch, "Mecha Keys" + state, new Vector2(x, y), Color.White, maxW);
			ConfigDraw.Text(spriteBatch, $"{ConfigDraw.L("Keyboard")}: [c/{kbc.Hex3()}:{kb}]", new Vector2(x, y + 24f), Color.White, maxW);
			ConfigDraw.Text(spriteBatch, $"{ConfigDraw.L("Mouse")}: [c/{msc.Hex3()}:{ms}]", new Vector2(x, y + 46f), Color.White, maxW);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (Item is not MechaKeysConfig cfg)
				return;
			ConfigDraw.PlayUiTick();
			ClickAudio.PreviewKeyboard(cfg);
			LogoAnim.Pulse(cfg.KeyboardPack);
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			if (Item is not MechaKeysConfig cfg)
				return;
			ConfigDraw.PlayUiTick();
			ClickAudio.PreviewMouse(cfg);
			LogoAnim.Pulse(cfg.EffectiveMousePack);
		}
	}
}
