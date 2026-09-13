using System.ComponentModel;
using MechaKeys.UI;
using Terraria.ModLoader.Config;

namespace MechaKeys.Core
{
	public class MechaKeysConfig : ModConfig
	{
		public override ConfigScope Mode => ConfigScope.ClientSide;

		[Header("Preview")]
		[CustomModConfigItem(typeof(LogoPreviewElement))]
		[DefaultValue(false)]
		public bool LogoPreview;

		[Header("General")]
		[DefaultValue(true)]
		public bool Enabled = true;

		[Header("Keyboard")]
		[DefaultValue(true)]
		public bool KeyboardSounds = true;

		[DefaultValue("cherrymx_black_pbt")]
		[CustomModConfigItem(typeof(PackCycleElement))]
		public string KeyboardPack = "cherrymx_black_pbt";

		[DefaultValue(100)]
		[Range(0, 100)]
		[Slider]
		[Increment(1)]
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

		[Header("Mouse")]
		[DefaultValue(true)]
		public bool MouseSounds = true;

		[DefaultValue("cherrymx_blue_abs_2")]
		[CustomModConfigItem(typeof(PackCycleElement))]
		public string MousePack = "cherrymx_blue_abs_2";

		[DefaultValue(100)]
		[Range(0, 100)]
		[Slider]
		[Increment(1)]
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

		[Header("Packs")]
		[CustomModConfigItem(typeof(OpenFolderElement))]
		[DefaultValue(false)]
		public bool OpenSoundFolder;

		[CustomModConfigItem(typeof(RescanElement))]
		[DefaultValue(false)]
		public bool RescanSoundFolder;

		public override void OnChanged()
		{
			SoundPackCatalog.EnsureConfigValid(this);
		}
	}
}
