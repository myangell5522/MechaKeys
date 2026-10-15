using System;
using System.Collections.Generic;
using System.Linq;
using MechaKeys.UI;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Terraria;
using Terraria.GameInput;
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

		private static readonly HashSet<Keys> Modifiers = new() {
			Keys.LeftShift, Keys.RightShift, Keys.LeftControl, Keys.RightControl,
			Keys.LeftAlt, Keys.RightAlt, Keys.LeftWindows, Keys.RightWindows
		};

		// Big keys get a slightly deeper click, like a stabilized spacebar.
		private static readonly Dictionary<Keys, float> BigKeyPitch = new() {
			[Keys.Space] = -0.22f,
			[Keys.Enter] = -0.14f,
			[Keys.Back] = -0.12f,
			[Keys.LeftShift] = -0.1f,
			[Keys.RightShift] = -0.1f,
			[Keys.Tab] = -0.06f,
			[Keys.CapsLock] = -0.08f,
		};

		// Rough horizontal position of each key on a full-size board, 0 = far left, 1 = far right.
		private static readonly Dictionary<Keys, float> KeyX = BuildKeyLayout();

		public override void Load()
		{
			if (!Main.dedServ)
				Main.OnPostDraw += OnPostDraw;
		}

		public override void Unload()
		{
			Main.OnPostDraw -= OnPostDraw;
			_primed = false;
			_pendingMouse = false;
		}

		public override void PostUpdateInput() => Poll();

		public override void UpdateUI(GameTime gameTime) => Poll();

		// Runs every frame in menus and in-game. Mouse clicks detected on earlier polls are played
		// here, after the UI has handled them, so config buttons can suppress the generic click.
		private static void OnPostDraw(GameTime gameTime)
		{
			try {
				FlushMouse();
				Poll();
				ModIconAnimator.Tick();
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Debug("Mecha Keys post-draw: " + e.Message);
			}
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

			if (cfg == null || !cfg.Enabled || !ShouldPlayNow(cfg.PlayWhen)) {
				_pendingMouse = false;
				_oldKeys = keys;
				_oldMouse = mouse;
				return;
			}

			if (cfg.KeyboardSounds) {
				foreach (Keys key in AllKeys) {
					if (!keys.IsKeyDown(key) || _oldKeys.IsKeyDown(key))
						continue;
					if (cfg.IgnoreModifiers && Modifiers.Contains(key))
						continue;

					float pitch = cfg.HeavierBigKeys && BigKeyPitch.TryGetValue(key, out float p) ? p : 0f;
					float pan = cfg.StereoKeys ? PanFor(key) : 0f;
					ClickAudio.PlayKeyboard(cfg, pitch, pan);
					LogoAnim.Pulse(false);
				}
			}

			if (cfg.MouseSounds) {
				bool pressed = JustPressed(mouse.LeftButton, _oldMouse.LeftButton) || JustPressed(mouse.RightButton, _oldMouse.RightButton);
				if (cfg.MouseExtraButtons) {
					pressed |= JustPressed(mouse.MiddleButton, _oldMouse.MiddleButton)
						|| JustPressed(mouse.XButton1, _oldMouse.XButton1)
						|| JustPressed(mouse.XButton2, _oldMouse.XButton2);
				}

				if (pressed)
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
			LogoAnim.Pulse(true);
		}

		/// <summary>Is the player typing into a text box right now (chat, sign, search, rename...)?</summary>
		internal static bool IsTyping()
		{
			try {
				return Main.drawingPlayerChat
					|| Main.editSign
					|| Main.editChest
					|| Main.CurrentInputTextTakerOverride != null
					|| PlayerInput.WritingText;
			}
			catch {
				return false;
			}
		}

		/// <summary>Is a menu or full-screen UI open (anything that is not plain gameplay)?</summary>
		internal static bool InMenus()
		{
			try {
				return Main.gameMenu
					|| Main.ingameOptionsWindow
					|| Main.playerInventory
					|| Main.inFancyUI
					|| Main.mapFullscreen
					|| Main.InGameUI?.CurrentState != null;
			}
			catch {
				return false;
			}
		}

		private static bool ShouldPlayNow(PlayMode mode) => mode switch {
			PlayMode.TypingOnly => IsTyping(),
			PlayMode.MenusAndTyping => IsTyping() || InMenus(),
			_ => true
		};

		private static float PanFor(Keys key)
		{
			if (!KeyX.TryGetValue(key, out float x))
				return 0f;
			return Math.Clamp((x - 0.5f) * 1.3f, -0.65f, 0.65f);
		}

		private static bool JustPressed(ButtonState now, ButtonState old) =>
			now == ButtonState.Pressed && old == ButtonState.Released;

		private static Dictionary<Keys, float> BuildKeyLayout()
		{
			var map = new Dictionary<Keys, float>();
			// main block spans roughly 0.0 .. 0.68 of a full-size keyboard
			void Row(float start, float step, params Keys[] row)
			{
				for (int i = 0; i < row.Length; i++)
					map[row[i]] = start + i * step;
			}

			const float S = 0.045f;
			Row(0.00f, S * 1.1f, Keys.Escape, Keys.F1, Keys.F2, Keys.F3, Keys.F4, Keys.F5, Keys.F6, Keys.F7, Keys.F8, Keys.F9, Keys.F10, Keys.F11, Keys.F12);
			Row(0.00f, S, Keys.OemTilde, Keys.D1, Keys.D2, Keys.D3, Keys.D4, Keys.D5, Keys.D6, Keys.D7, Keys.D8, Keys.D9, Keys.D0, Keys.OemMinus, Keys.OemPlus, Keys.Back);
			Row(0.01f, S, Keys.Tab, Keys.Q, Keys.W, Keys.E, Keys.R, Keys.T, Keys.Y, Keys.U, Keys.I, Keys.O, Keys.P, Keys.OemOpenBrackets, Keys.OemCloseBrackets, Keys.OemPipe);
			Row(0.015f, S, Keys.CapsLock, Keys.A, Keys.S, Keys.D, Keys.F, Keys.G, Keys.H, Keys.J, Keys.K, Keys.L, Keys.OemSemicolon, Keys.OemQuotes, Keys.Enter);
			Row(0.025f, S, Keys.LeftShift, Keys.Z, Keys.X, Keys.C, Keys.V, Keys.B, Keys.N, Keys.M, Keys.OemComma, Keys.OemPeriod, Keys.OemQuestion, Keys.RightShift);
			map[Keys.LeftControl] = 0.0f;
			map[Keys.LeftWindows] = 0.05f;
			map[Keys.LeftAlt] = 0.1f;
			map[Keys.Space] = 0.32f;
			map[Keys.RightAlt] = 0.52f;
			map[Keys.RightWindows] = 0.56f;
			map[Keys.Apps] = 0.6f;
			map[Keys.RightControl] = 0.64f;

			// navigation cluster
			foreach (Keys k in new[] { Keys.PrintScreen, Keys.Scroll, Keys.Pause, Keys.Insert, Keys.Home, Keys.PageUp, Keys.Delete, Keys.End, Keys.PageDown, Keys.Up })
				map[k] = 0.76f;
			map[Keys.Left] = 0.72f;
			map[Keys.Down] = 0.76f;
			map[Keys.Right] = 0.8f;

			// numpad
			foreach (Keys k in new[] { Keys.NumLock, Keys.NumPad7, Keys.NumPad4, Keys.NumPad1, Keys.NumPad0 })
				map[k] = 0.87f;
			foreach (Keys k in new[] { Keys.Divide, Keys.NumPad8, Keys.NumPad5, Keys.NumPad2 })
				map[k] = 0.91f;
			foreach (Keys k in new[] { Keys.Multiply, Keys.NumPad9, Keys.NumPad6, Keys.NumPad3, Keys.Decimal })
				map[k] = 0.95f;
			foreach (Keys k in new[] { Keys.Subtract, Keys.Add })
				map[k] = 1.0f;
			return map;
		}
	}
}
