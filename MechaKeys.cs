using MechaKeys.Core;
using MechaKeys.UI;
using Terraria;
using Terraria.ModLoader;

namespace MechaKeys
{
	public class MechaKeys : Mod
	{
		public override void Load()
		{
			if (Main.dedServ)
				return;

			SoundPackCatalog.EnsureInstalled(this);
			SoundPackCatalog.Rescan();
			ClickAudio.Warmup();
		}

		public override void Unload()
		{
			ClickAudio.Unload();
			SoundPackCatalog.Unload();
			LogoAnim.Unload();
		}
	}
}
