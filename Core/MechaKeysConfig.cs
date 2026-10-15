using System.ComponentModel;
using MechaKeys.UI;
using Terraria.ModLoader.Config;

namespace MechaKeys.Core
{
	public enum PlayMode
	{
		Everywhere,
		MenusAndTyping,
		TypingOnly
	}

	public class MechaKeysConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		// ---------------------------------------------------------------- top
		[Header("Preview")]
		[CustomModConfigItem(typeof(LogoPreviewElement))]
		[DefaultValue(false)]
		public bool LogoPreview;

		[CustomModConfigItem(typeof(PresetElement))]
		[DefaultValue(false)]
		public bool QuickPresets;

		// ---------------------------------------------------------------- general
		[Header("General")]
		[DefaultValue(true)]
		public bool Enabled = true;

		[DefaultValue(PlayMode.Everywhere)]
		[DrawTicks]
		public PlayMode PlayWhen = PlayMode.Everywhere;

		[DefaultValue(true)]
		public bool FollowGameVolume = true;

		[DefaultValue(20)]
		[Range(0, 100)]
		[Slider]
		[Increment(5)]
		public int PitchVariation = 20;

		[DefaultValue(true)]
		public bool HeavierBigKeys = true;

		[DefaultValue(false)]
		public bool StereoKeys;

		[DefaultValue(false)]
		public bool IgnoreModifiers;

		// ---------------------------------------------------------------- keyboard
		[Header("Keyboard")]
		[DefaultValue(true)]
		public bool KeyboardSounds = true;

		[DefaultValue("cherrymx_black_pbt")]
		[CustomModConfigItem(typeof(PackPickerElement))]
		public string KeyboardPack = "cherrymx_black_pbt";

		[DefaultValue(100)]
		[Range(0, 100)]
		[Slider]
		[Increment(5)]
		public int KeyboardVolume = 100;

		[DefaultValue(1)]
		[Range(1, 8)]
		[Slider]
		[Increment(1)]
		[DrawTicks]
		public int KeyboardAmplify = 1;

		[CustomModConfigItem(typeof(TestKeyboardElement))]
		[DefaultValue(false)]
		public bool TestKeyboard;

		// ---------------------------------------------------------------- mouse
		[Header("Mouse")]
		[DefaultValue(true)]
		public bool MouseSounds = true;

		[DefaultValue(false)]
		public bool MouseExtraButtons;

		[DefaultValue(false)]
		public bool MouseSameAsKeyboard;

		[DefaultValue("cherrymx_blue_abs_2")]
		[CustomModConfigItem(typeof(PackPickerElement))]
		public string MousePack = "cherrymx_blue_abs_2";

		[DefaultValue(100)]
		[Range(0, 100)]
		[Slider]
		[Increment(5)]
		public int MouseVolume = 100;

		[DefaultValue(1)]
		[Range(1, 8)]
		[Slider]
		[Increment(1)]
		[DrawTicks]
		public int MouseAmplify = 1;

		[CustomModConfigItem(typeof(TestMouseElement))]
		[DefaultValue(false)]
		public bool TestMouse;

		// ---------------------------------------------------------------- packs
		[Header("Packs")]
		[CustomModConfigItem(typeof(OpenFolderElement))]
		[DefaultValue(false)]
		public bool OpenSoundFolder;

		[CustomModConfigItem(typeof(RescanElement))]
		[DefaultValue(false)]
		public bool RescanSoundFolder;

		/// <summary>The pack the mouse actually uses (respects "same as keyboard").</summary>
		internal string EffectiveMousePack => MouseSameAsKeyboard ? KeyboardPack : MousePack;

		public override void OnChanged()
		{
			SoundPackCatalog.EnsureConfigValid(this);
		}
	}
}
