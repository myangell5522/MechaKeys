using System;
using System.Linq;
using MechaKeys.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.ModLoader;

namespace MechaKeys.Core
{
	public class InputHookSystem : ModSystem
	{
		private static KeyboardState _oldKeys;
		private static MouseState _oldMouse;
		private static bool _primed;
		private static bool _pendingMouse;
		private static readonly Keys[] AllKeys = ((Keys[])Enum.GetValues(typeof(Keys)))
			.Where(k => k != Keys.None)
			.Distinct()
			.ToArray();

		public override void Unload()
		{
			_primed = false;
			_pendingMouse = false;
		}

		public override void PostUpdateInput() => Poll();

		public override void UpdateUI(GameTime gameTime) => Poll();

		public override void PostDrawInterface(SpriteBatch spriteBatch)
		{
			LogoAnim.Tick();
			FlushMouse();
		}

		private static void Poll()
		{
			if (Main.dedServ)
				return;

			if (!Main.hasFocus) {
				_primed = false;
				_pendingMouse = false;
				return;
			}

			KeyboardState keys = Keyboard.GetState();
			MouseState mouse = Mouse.GetState();
			if (!_primed) {
				_oldKeys = keys;
				_oldMouse = mouse;
				_primed = true;
				return;
			}

			MechaKeysConfig cfg = ModContent.GetInstance<MechaKeysConfig>();
			ClickAudio.UpdateMouseSuppression(mouse);

			if (cfg == null || !cfg.Enabled) {
				_pendingMouse = false;
				_oldKeys = keys;
				_oldMouse = mouse;
				return;
			}

			if (cfg.KeyboardSounds) {
				foreach (Keys key in AllKeys) {
					if (keys.IsKeyDown(key) && !_oldKeys.IsKeyDown(key)) {
						ClickAudio.PlayKeyboard(cfg);
						LogoAnim.Pulse();
					}
				}
			}

			if (cfg.MouseSounds) {
				if (JustPressed(mouse.LeftButton, _oldMouse.LeftButton) || JustPressed(mouse.RightButton, _oldMouse.RightButton))
					_pendingMouse = true;
				if (ClickAudio.MouseSuppressed)
					_pendingMouse = false;
			}
			else {
				_pendingMouse = false;
			}

			_oldKeys = keys;
			_oldMouse = mouse;
		}

		private static void FlushMouse()
		{
			if (!_pendingMouse)
				return;

			_pendingMouse = false;
			if (ClickAudio.MouseSuppressed)
				return;

			MechaKeysConfig cfg = ModContent.GetInstance<MechaKeysConfig>();
			if (cfg == null || !cfg.Enabled || !cfg.MouseSounds)
				return;

			ClickAudio.PlayMouse(cfg);
			LogoAnim.Pulse();
		}

		private static bool JustPressed(ButtonState now, ButtonState old) =>
			now == ButtonState.Pressed && old == ButtonState.Released;
	}
}
