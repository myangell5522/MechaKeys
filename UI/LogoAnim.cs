using System;
using System.Diagnostics;
using MechaKeys.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace MechaKeys.UI
{
	/// <summary>
	/// The animated keycap logo: the key presses down, the switch stem (tinted with the colour of
	/// the active pack) disappears into the housing and sound rings burst out on bottom-out.
	/// LogoSheet.png holds the base frames, LogoAccent.png the white accent pixels that get tinted.
	/// </summary>
	internal static class LogoAnim
	{
		internal const int Frames = 10;
		internal const double FrameMs = 55;
		private const double IdleDelayMs = 3000;
		private const double IdlePeriodMs = 2800;

		internal const string BasePath = "MechaKeys/Assets/Textures/LogoSheet";
		internal const string AccentPath = "MechaKeys/Assets/Textures/LogoAccent";

		private static readonly Stopwatch Clock = Stopwatch.StartNew();
		private static Asset<Texture2D> _base;
		private static Asset<Texture2D> _accent;
		private static double _pressAt = -1e9;
		private static Color _pressColor = new(63, 169, 255);

		internal static void Unload()
		{
			_base = null;
			_accent = null;
			_pressAt = -1e9;
		}

		internal static double Now => Clock.Elapsed.TotalMilliseconds;

		internal static void Pulse(bool mouse)
		{
			MechaKeysConfig cfg = ModContent.GetInstance<MechaKeysConfig>();
			Pulse(mouse ? cfg?.EffectiveMousePack : cfg?.KeyboardPack);
		}

		internal static void Pulse(string pack)
		{
			double now = Now;
			double t = now - _pressAt;
			// Fast typing: re-trigger from the "already pressed" frame so the key does not flicker up.
			if (t >= 0 && t < Frames * FrameMs)
				_pressAt = t > 3 * FrameMs ? now - 2 * FrameMs : _pressAt;
			else
				_pressAt = now;
			_pressColor = PackInfo.Get(pack).Accent;
		}

		internal static int CurrentFrame(out Color accent)
		{
			double now = Now;
			double t = now - _pressAt;
			accent = _pressColor;
			if (t >= 0 && t < Frames * FrameMs)
				return Math.Clamp((int)(t / FrameMs), 0, Frames - 1);

			// Nothing pressed for a while: play a gentle demo press now and then.
			if (t > IdleDelayMs) {
				MechaKeysConfig cfg = ModContent.GetInstance<MechaKeysConfig>();
				accent = PackInfo.Get(cfg?.KeyboardPack).Accent;
				double c = now % IdlePeriodMs;
				if (c < Frames * FrameMs)
					return Math.Clamp((int)(c / FrameMs), 0, Frames - 1);
			}

			return 0;
		}

		internal static void Draw(SpriteBatch spriteBatch, Rectangle dest)
		{
			if (dest.Width < 8 || dest.Height < 8)
				return;

			_base ??= ModContent.Request<Texture2D>(BasePath, AssetRequestMode.ImmediateLoad);
			_accent ??= ModContent.Request<Texture2D>(AccentPath, AssetRequestMode.ImmediateLoad);
			Texture2D tex = _base?.Value;
			if (tex == null || tex.Width <= 0)
				return;

			int frames = Math.Max(1, tex.Height / tex.Width);
			int frame = Math.Clamp(CurrentFrame(out Color accent), 0, frames - 1);
			var src = new Rectangle(0, frame * tex.Width, tex.Width, tex.Width);
			spriteBatch.Draw(tex, dest, src, Color.White);

			Texture2D acc = _accent?.Value;
			if (acc != null && acc.Height == tex.Height)
				spriteBatch.Draw(acc, dest, src, accent);
		}
	}
}
