using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using Terraria;
using Terraria.Localization;
using Terraria.ModLoader;

namespace MechaKeys.Core
{
	internal static class SoundPackCatalog
	{
		private static readonly Dictionary<string, List<SoundClip>> Packs = new(StringComparer.OrdinalIgnoreCase);
		private static readonly List<string> Names = new();

		internal static string PacksDirectory => Path.Combine(Main.SavePath, "MechaKeys", "SoundPacks");

		internal static IReadOnlyList<string> PackNames => Names;

		/// <summary>
		/// Pack order for a picker: keyboard pickers list switches first and mouse packs last,
		/// mouse pickers the other way round. Custom packs always come at the end.
		/// </summary>
		internal static List<string> PackNamesFor(bool mouse)
		{
			return Names
				.OrderBy(n => {
					PackInfo.Meta m = PackInfo.Get(n);
					if (m.Kind == SwitchKind.Custom)
						return 2;
					return (m.Kind == SwitchKind.Mouse) == mouse ? 0 : 1;
				})
				.ThenBy(n => PackInfo.Get(n).Order)
				.ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToList();
		}

		internal static bool Exists(string pack) => !string.IsNullOrWhiteSpace(pack) && Packs.ContainsKey(pack);

		internal static void Unload()
		{
			Packs.Clear();
			Names.Clear();
		}

		internal static void EnsureInstalled(Mod mod)
		{
			string dir = PacksDirectory;
			Directory.CreateDirectory(dir);
			WriteHowTo(dir);
			if (mod == null)
				return;

			IEnumerable<string> files;
			try {
				files = mod.GetFileNames();
			}
			catch {
				return;
			}

			foreach (string file in files) {
				string n = file.Replace('\\', '/');
				const string prefix = "Bundled/Keyboards/";
				if (!n.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
					continue;
				if (!IsAudio(n) && !n.EndsWith(".txt", StringComparison.OrdinalIgnoreCase))
					continue;
				if (n.Contains("..", StringComparison.Ordinal))
					continue;

				string rel = n.Substring(prefix.Length).Replace('/', Path.DirectorySeparatorChar);
				string dest = Path.Combine(dir, rel);
				if (File.Exists(dest))
					continue;

				try {
					string parent = Path.GetDirectoryName(dest);
					if (!string.IsNullOrEmpty(parent))
						Directory.CreateDirectory(parent);
					File.WriteAllBytes(dest, mod.GetFileBytes(file));
				}
				catch (Exception e) {
					mod.Logger.Warn("Failed to extract sound pack file " + n + ": " + e.Message);
				}
			}
		}

		internal static void Rescan()
		{
			Packs.Clear();
			Names.Clear();
			string dir = PacksDirectory;
			Directory.CreateDirectory(dir);

			try {
				LoadLooseAsSingle(dir);
				foreach (string packDir in Directory.GetDirectories(dir).OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
					LoadDirectoryPack(packDir);
				foreach (string zip in Directory.GetFiles(dir, "*.zip").OrderBy(p => Path.GetFileName(p), StringComparer.OrdinalIgnoreCase))
					LoadZipPack(zip);
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Warn("Sound pack scan failed: " + e.Message);
			}

			List<string> sorted = Names
				.OrderBy(n => PackInfo.Get(n).Order)
				.ThenBy(n => n, StringComparer.OrdinalIgnoreCase)
				.ToList();
			Names.Clear();
			Names.AddRange(sorted);

			if (ModContent.GetInstance<MechaKeysConfig>() is MechaKeysConfig cfg)
				EnsureConfigValid(cfg);
		}

		internal static void EnsureConfigValid(MechaKeysConfig cfg)
		{
			if (cfg == null || Names.Count == 0)
				return;

			if (!Exists(cfg.KeyboardPack))
				cfg.KeyboardPack = FirstExisting("cherrymx_black_pbt", "gateron_yellow") ?? PackNamesFor(false)[0];
			if (!Exists(cfg.MousePack))
				cfg.MousePack = FirstExisting("cherrymx_blue_abs_2", "mouse_classic") ?? PackNamesFor(true)[0];
		}

		private static string FirstExisting(params string[] ids) => ids.FirstOrDefault(Exists);

		internal static IReadOnlyList<SoundClip> GetClips(string pack)
		{
			if (string.IsNullOrWhiteSpace(pack) || !Packs.TryGetValue(pack, out List<SoundClip> clips) || clips.Count == 0)
				return Array.Empty<SoundClip>();
			return clips;
		}

		internal static SoundClip Pick(string pack)
		{
			IReadOnlyList<SoundClip> clips = GetClips(pack);
			if (clips.Count == 0)
				return null;
			int i = Main.rand?.Next(clips.Count) ?? Random.Shared.Next(clips.Count);
			return clips[i];
		}

		internal static string DisplayName(string id)
		{
			if (string.IsNullOrWhiteSpace(id))
				return "";

			string key = "Mods.MechaKeys.Packs." + id;
			string loc = Language.GetTextValue(key);
			if (!string.IsNullOrEmpty(loc) && loc != key)
				return loc;

			return Prettify(id);
		}

		internal static void OpenFolder()
		{
			string dir = PacksDirectory;
			Directory.CreateDirectory(dir);
			WriteHowTo(dir);
			try {
				Process.Start(new ProcessStartInfo {
					FileName = dir,
					UseShellExecute = true
				});
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Warn("Could not open sound pack folder: " + e.Message);
			}
		}

		private static void LoadLooseAsSingle(string dir)
		{
			List<SoundClip> clips = Directory.GetFiles(dir)
				.Where(IsAudio)
				.OrderBy(Path.GetFileName, StringComparer.OrdinalIgnoreCase)
				.Select(path => new SoundClip { FilePath = path })
				.ToList();
			if (clips.Count > 0)
				AddPack("single", clips);
		}

		private static void LoadDirectoryPack(string packDir)
		{
			string name = Path.GetFileName(packDir);
			if (string.IsNullOrWhiteSpace(name))
				return;

			var ordered = new List<SoundClip>();
			var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
			string index = Path.Combine(packDir, "index.txt");
			if (File.Exists(index)) {
				foreach (string raw in File.ReadAllLines(index)) {
					string line = StripComment(raw);
					if (string.IsNullOrEmpty(line))
						continue;
					string path = Path.GetFullPath(Path.Combine(packDir, line.Replace('/', Path.DirectorySeparatorChar)));
					if (!path.StartsWith(Path.GetFullPath(packDir), StringComparison.OrdinalIgnoreCase))
						continue;
					if (!File.Exists(path) || !IsAudio(path) || !seen.Add(path))
						continue;
					ordered.Add(new SoundClip { FilePath = path });
				}
			}

			foreach (string path in Directory.EnumerateFiles(packDir, "*.*", SearchOption.AllDirectories)
				         .Where(IsAudio)
				         .OrderBy(p => p, StringComparer.OrdinalIgnoreCase)) {
				if (seen.Add(path))
					ordered.Add(new SoundClip { FilePath = path });
			}

			if (ordered.Count > 0)
				AddPack(name, ordered);
		}

		private static void LoadZipPack(string zipPath)
		{
			string name = Path.GetFileNameWithoutExtension(zipPath);
			if (string.IsNullOrWhiteSpace(name) || Packs.ContainsKey(name))
				return;

			try {
				using ZipArchive zip = ZipFile.OpenRead(zipPath);
				var entries = zip.Entries
					.Where(e => !string.IsNullOrEmpty(e.Name) && IsAudio(e.FullName))
					.Select(e => e.FullName.Replace('\\', '/'))
					.Where(n => !n.Contains("..", StringComparison.Ordinal))
					.Distinct(StringComparer.OrdinalIgnoreCase)
					.ToList();
				if (entries.Count == 0)
					return;

				var ordered = new List<string>();
				var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
				ZipArchiveEntry index = zip.GetEntry("index.txt");
				if (index != null) {
					using var reader = new StreamReader(index.Open());
					while (!reader.EndOfStream) {
						string line = StripComment(reader.ReadLine());
						if (string.IsNullOrEmpty(line))
							continue;
						string match = entries.FirstOrDefault(e => string.Equals(e, line.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase));
						if (match != null && seen.Add(match))
							ordered.Add(match);
					}
				}

				foreach (string entry in entries.OrderBy(e => e, StringComparer.OrdinalIgnoreCase)) {
					if (seen.Add(entry))
						ordered.Add(entry);
				}

				AddPack(name, ordered.Select(e => new SoundClip { ZipPath = zipPath, ZipEntry = e }).ToList());
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Warn("Failed to read zip sound pack " + zipPath + ": " + e.Message);
			}
		}

		private static void AddPack(string name, List<SoundClip> clips)
		{
			if (clips == null || clips.Count == 0)
				return;
			Packs[name] = clips;
			if (!Names.Exists(n => string.Equals(n, name, StringComparison.OrdinalIgnoreCase)))
				Names.Add(name);
		}

		private static string StripComment(string raw)
		{
			if (string.IsNullOrWhiteSpace(raw))
				return "";
			int hash = raw.IndexOf('#');
			if (hash >= 0)
				raw = raw.Substring(0, hash);
			return raw.Trim().Replace('\\', '/');
		}

		private static bool IsAudio(string path)
		{
			string ext = Path.GetExtension(path).ToLowerInvariant();
			return ext is ".ogg" or ".wav";
		}

		private static string Prettify(string id)
		{
			string[] parts = id.Replace('-', '_').Split('_', StringSplitOptions.RemoveEmptyEntries);
			for (int i = 0; i < parts.Length; i++) {
				string p = parts[i];
				if (p.Equals("mx", StringComparison.OrdinalIgnoreCase))
					parts[i] = "MX";
				else if (p.Equals("abs", StringComparison.OrdinalIgnoreCase) || p.Equals("pbt", StringComparison.OrdinalIgnoreCase))
					parts[i] = p.ToUpperInvariant();
				else if (p.Length > 0)
					parts[i] = char.ToUpperInvariant(p[0]) + (p.Length > 1 ? p.Substring(1) : "");
			}

			string joined = string.Join(" ", parts);
			if (joined.StartsWith("Cherrymx", StringComparison.OrdinalIgnoreCase))
				joined = "Cherry MX" + joined.Substring("Cherrymx".Length);
			return joined;
		}

		private static void WriteHowTo(string dir)
		{
			string path = Path.Combine(dir, "HOW_TO_ADD.txt");
			const string text =
@"Mecha Keys / CreamyKeys sound packs
===================================

Drop .ogg (or .wav) files in this folder to create a simple keyboard named 'single'.

Or create a folder — the folder name becomes the keyboard name — and put audio files inside.
Folders can have subfolders.

You can also drop a .zip with .ogg files inside. The zip name becomes the keyboard name.
Do not use .jar files as sound packs.

Optional: add an index.txt inside a keyboard folder (or zip) to control ordering.
Each line is a relative path to an audio file. Anything after # is a comment.
Even with index.txt, extra audio files are still included.

Open Mecha Keys settings and press Rescan sound packs after adding files.
Tip: a pack plays a random file each press, so 10+ slightly different clicks sound the most natural.

-----------------------------------

Пакеты звуков Mecha Keys
========================

Положите файлы .ogg (или .wav) прямо в эту папку — получится клавиатура с именем 'single'.

Или создайте папку (имя папки станет названием пакета) и положите звуки внутрь.
Подпапки тоже читаются.

Можно положить .zip с .ogg внутри. Имя zip-файла станет названием пакета.
Не используйте .jar как пакеты звуков.

Необязательно: index.txt внутри папки (или zip) задаёт порядок.
Каждая строка — относительный путь к файлу. Всё после # это комментарий.
Лишние звуки всё равно добавятся.

После добавления файлов откройте настройки Mecha Keys и нажмите «Пересканировать пакеты».
Совет: при каждом нажатии играет случайный файл, так что 10+ чуть разных кликов звучат естественнее всего.
";
			try {
				File.WriteAllText(path, text);
			}
			catch {
			}
		}
	}
}
