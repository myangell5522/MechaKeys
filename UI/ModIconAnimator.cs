using System;
using System.Collections;
using System.Diagnostics;
using System.Reflection;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.GameContent.UI.Elements;
using Terraria.ModLoader;

namespace MechaKeys.UI
{
	/// <summary>
	/// Animates Mecha Keys' own icon in tModLoader's "Mods" menu: the keycap is pressed over and
	/// over, and every press shows a different switch colour.
	/// tModLoader has no API for this, so the icon element is found by reflection. Any mismatch
	/// (e.g. after a tModLoader update renames a field) simply turns the animation off.
	/// </summary>
	internal static class ModIconAnimator
	{
		private static readonly Color[] Cycle = {
			new(63, 169, 255),  // blue
			new(255, 77, 94),   // red
			new(54, 211, 154),  // jade
			new(208, 138, 60),  // brown
			new(255, 210, 63),  // yellow
			new(255, 122, 200), // panda
			new(167, 123, 255), // silent
		};

		private const double FrameMs = 60;
		private const double RestMs = 650;

		private static readonly Stopwatch Clock = Stopwatch.StartNew();
		private static Texture2D[] _frames;
		private static bool _disabled;
		private static Type _uiModsType;
		private static FieldInfo _itemsField;
		private static PropertyInfo _modNameProp;
		private static FieldInfo _iconField;
		private static UIImage _lastIcon;
		private static int _lastIndex = -1;

		internal static void Unload()
		{
			// The frames are deliberately not disposed: the Mods menu may still be showing one of
			// them after an unload/reload, and drawing a disposed texture would crash. They are tiny
			// and get collected once the menu rebuilds its list.
			_frames = null;
			_disabled = false;
			_uiModsType = null;
			_itemsField = null;
			_modNameProp = null;
			_iconField = null;
			_lastIcon = null;
			_lastIndex = -1;
		}

		/// <summary>Called once per frame from the main thread.</summary>
		internal static void Tick()
		{
			if (_disabled || Main.dedServ || !Main.gameMenu)
				return;

			try {
				object state = Main.MenuUI?.CurrentState;
				if (state == null)
					return;

				_uiModsType ??= typeof(ModLoader).Assembly.GetType("Terraria.ModLoader.UI.UIMods");
				if (_uiModsType == null) {
					_disabled = true;
					return;
				}

				if (state.GetType() != _uiModsType) {
					_lastIcon = null;
					return;
				}

				_itemsField ??= _uiModsType.GetField("items", BindingFlags.Instance | BindingFlags.NonPublic);
				if (_itemsField?.GetValue(state) is not IList items)
					return;

				UIImage icon = null;
				for (int i = 0; i < items.Count; i++) {
					object item = items[i];
					if (item == null)
						continue;
					_modNameProp ??= item.GetType().GetProperty("ModName", BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
					if (_modNameProp?.GetValue(item) as string != nameof(MechaKeys))
						continue;
					_iconField ??= item.GetType().GetField("_modIcon", BindingFlags.Instance | BindingFlags.NonPublic);
					icon = _iconField?.GetValue(item) as UIImage;
					break;
				}

				if (icon == null)
					return;

				if (!EnsureFrames())
					return;

				int index = CurrentIndex();
				if (!ReferenceEquals(icon, _lastIcon) || index != _lastIndex) {
					icon.SetImage(_frames[index]);
					_lastIcon = icon;
					_lastIndex = index;
				}
			}
			catch (Exception e) {
				_disabled = true;
				ModContent.GetInstance<MechaKeys>()?.Logger.Info("Animated mod icon disabled: " + e.Message);
			}
		}

		private static int CurrentIndex()
		{
			double cycleMs = LogoAnim.Frames * FrameMs + RestMs;
			double t = Clock.Elapsed.TotalMilliseconds;
			int press = (int)(t / cycleMs);
			double inPress = t - press * cycleMs;
			int frame = inPress < LogoAnim.Frames * FrameMs ? (int)(inPress / FrameMs) : 0;
			int color = press % Cycle.Length;
			return color * LogoAnim.Frames + Math.Clamp(frame, 0, LogoAnim.Frames - 1);
		}

		private static bool EnsureFrames()
		{
			if (_frames != null)
				return true;

			Texture2D baseTex = ModContent.Request<Texture2D>(LogoAnim.BasePath, AssetRequestMode.ImmediateLoad).Value;
			Texture2D accTex = ModContent.Request<Texture2D>(LogoAnim.AccentPath, AssetRequestMode.ImmediateLoad).Value;
			int size = baseTex.Width;
			int frames = Math.Min(LogoAnim.Frames, baseTex.Height / size);
			if (frames < LogoAnim.Frames || accTex.Width != size || accTex.Height != baseTex.Height) {
				_disabled = true;
				return false;
			}

			var basePx = new Color[baseTex.Width * baseTex.Height];
			var accPx = new Color[accTex.Width * accTex.Height];
			baseTex.GetData(basePx);
			accTex.GetData(accPx);

			var result = new Texture2D[Cycle.Length * frames];
			var buffer = new Color[size * size];
			for (int c = 0; c < Cycle.Length; c++) {
				Color tint = Cycle[c];
				for (int f = 0; f < frames; f++) {
					int offset = f * size * size;
					for (int i = 0; i < buffer.Length; i++) {
						// textures are premultiplied: out = accent * tint + base * (1 - accent.a)
						Color b = basePx[offset + i];
						Color a = accPx[offset + i];
						float inv = 1f - a.A / 255f;
						buffer[i] = new Color(
							(byte)Math.Min(255, a.R * tint.R / 255 + b.R * inv),
							(byte)Math.Min(255, a.G * tint.G / 255 + b.G * inv),
							(byte)Math.Min(255, a.B * tint.B / 255 + b.B * inv),
							(byte)Math.Min(255, a.A + b.A * inv));
					}

					var tex = new Texture2D(Main.instance.GraphicsDevice, size, size);
					tex.SetData(buffer);
					result[c * frames + f] = tex;
				}
			}

			_frames = result;
			return true;
		}
	}
}
