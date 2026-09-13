using System;
using System.Collections.Generic;
using MechaKeys.Core;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Terraria.GameContent;
using Terraria.ModLoader;
using Terraria.ModLoader.Config.UI;
using Terraria.UI;
using Terraria.UI.Chat;

namespace MechaKeys.UI
{
	internal static class ConfigDraw
	{
		internal static void ValueOnRight(SpriteBatch spriteBatch, CalculatedStyle dims, string text)
		{
			if (string.IsNullOrEmpty(text))
				return;
			Vector2 scale = new(0.8f);
			Vector2 size = FontAssets.ItemStack.Value.MeasureString(text) * scale;
			var pos = new Vector2(dims.X + dims.Width - size.X - 10f, dims.Y + 8f);
			ChatManager.DrawColorCodedStringWithShadow(spriteBatch, FontAssets.ItemStack.Value, text, pos, Color.White, 0f, Vector2.Zero, scale, dims.Width, 2f);
		}
	}

	public class PackCycleElement : ConfigElement<string>
	{
		public override void OnBind()
		{
			base.OnBind();
			Height.Set(32f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			ConfigDraw.ValueOnRight(spriteBatch, GetDimensions(), SoundPackCatalog.DisplayName(Value));
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Cycle(1);
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			Cycle(-1);
		}

		private void Cycle(int dir)
		{
			IReadOnlyList<string> names = SoundPackCatalog.PackNames;
			if (names.Count == 0)
				return;

			int i = 0;
			for (int n = 0; n < names.Count; n++) {
				if (string.Equals(names[n], Value, StringComparison.OrdinalIgnoreCase)) {
					i = n;
					break;
				}
			}

			i = (i + dir) % names.Count;
			if (i < 0)
				i += names.Count;
			Value = names[i];

			if (Item is MechaKeysConfig cfg) {
				ClickAudio.SuppressMouse();
				bool mouse = MemberInfo != null && MemberInfo.Name.Contains("Mouse", StringComparison.Ordinal);
				if (mouse)
					ClickAudio.PlayPack(Value, cfg.MouseVolume / 100f, cfg.MouseAmplify);
				else
					ClickAudio.PlayPack(Value, cfg.KeyboardVolume / 100f, cfg.KeyboardAmplify);
				LogoAnim.Pulse();
			}
		}
	}

	public class TestKeyboardElement : ActionConfigElement
	{
		protected override void Run()
		{
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.SuppressMouse();
			ClickAudio.PlayPack(cfg.KeyboardPack, cfg.KeyboardVolume / 100f, cfg.KeyboardAmplify);
			LogoAnim.Pulse();
		}
	}

	public class TestMouseElement : ActionConfigElement
	{
		protected override void Run()
		{
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.SuppressMouse();
			ClickAudio.PlayPack(cfg.MousePack, cfg.MouseVolume / 100f, cfg.MouseAmplify);
			LogoAnim.Pulse();
		}
	}

	public class OpenFolderElement : ActionConfigElement
	{
		protected override void Run()
		{
			ClickAudio.SuppressMouse();
			SoundPackCatalog.EnsureInstalled(ModContent.GetInstance<MechaKeys>());
			SoundPackCatalog.OpenFolder();
		}
	}

	public class RescanElement : ActionConfigElement
	{
		protected override void Run()
		{
			ClickAudio.SuppressMouse();
			SoundPackCatalog.EnsureInstalled(ModContent.GetInstance<MechaKeys>());
			SoundPackCatalog.Rescan();
			ClickAudio.ClearCache();
			ClickAudio.Warmup();
			LogoAnim.Pulse();
		}
	}

	public abstract class ActionConfigElement : ConfigElement<bool>
	{
		public override void OnBind()
		{
			base.OnBind();
			Height.Set(32f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			if (PlayTexture?.Value == null)
				return;
			Texture2D tex = PlayTexture.Value;
			CalculatedStyle dims = GetDimensions();
			var pos = new Vector2(dims.X + dims.Width - tex.Width - 12f, dims.Y + (dims.Height - tex.Height) * 0.5f);
			spriteBatch.Draw(tex, pos, Color.White);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			Run();
		}

		protected abstract void Run();
	}

	public class LogoPreviewElement : ConfigElement<bool>
	{
		public override void OnBind()
		{
			base.OnBind();
			DrawLabel = false;
			Height.Set(92f, 0f);
		}

		protected override void DrawSelf(SpriteBatch spriteBatch)
		{
			base.DrawSelf(spriteBatch);
			CalculatedStyle dims = GetDimensions();
			const int size = 80;
			var dest = new Rectangle((int)(dims.X + (dims.Width - size) * 0.5f), (int)(dims.Y + (dims.Height - size) * 0.5f), size, size);
			LogoAnim.Draw(spriteBatch, dest);
		}

		public override void LeftClick(UIMouseEvent evt)
		{
			base.LeftClick(evt);
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.SuppressMouse();
			ClickAudio.PlayPack(cfg.KeyboardPack, cfg.KeyboardVolume / 100f, cfg.KeyboardAmplify);
			LogoAnim.Pulse();
		}

		public override void RightClick(UIMouseEvent evt)
		{
			base.RightClick(evt);
			if (Item is not MechaKeysConfig cfg)
				return;
			ClickAudio.SuppressMouse();
			ClickAudio.PlayPack(cfg.MousePack, cfg.MouseVolume / 100f, cfg.MouseAmplify);
			LogoAnim.Pulse();
		}
	}
}
