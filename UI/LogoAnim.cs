using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using ReLogic.Content;
using Terraria;
using Terraria.ModLoader;

namespace MechaKeys.UI
{
	internal static class LogoAnim
	{
		private static Asset<Texture2D> _sheet;
		private static int _hold;

		internal static void Unload()
		{
			_sheet = null;
			_hold = 0;
		}

		internal static void Pulse() => _hold = 12;

		internal static void Tick()
		{
			if (_hold > 0)
				_hold--;
		}

		internal static float Amount
		{
			get
			{
				if (_hold > 0)
					return 1f;
				float cycle = Main.GlobalTimeWrappedHourly % 2.6f;
				if (cycle < 1.7f)
					return 0f;
				if (cycle < 1.85f)
					return (cycle - 1.7f) / 0.15f;
				if (cycle < 2.15f)
					return 1f;
				return MathHelper.Clamp(1f - (cycle - 2.15f) / 0.45f, 0f, 1f);
			}
		}

		internal static void Draw(SpriteBatch spriteBatch, Rectangle dest)
		{
			if (dest.Width < 8 || dest.Height < 8)
				return;

			_sheet ??= ModContent.Request<Texture2D>("MechaKeys/Assets/Textures/LogoSheet", AssetRequestMode.ImmediateLoad);
			Texture2D tex = _sheet?.Value;
			if (tex == null || tex.Width <= 0)
				return;

			int frames = Math.Max(1, tex.Height / tex.Width);
			int frame = (int)MathF.Round(Amount * (frames - 1));
			frame = Math.Clamp(frame, 0, frames - 1);
			var src = new Rectangle(0, frame * tex.Width, tex.Width, tex.Width);
			spriteBatch.Draw(tex, dest, src, Color.White);
		}
	}
}
