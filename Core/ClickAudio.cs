using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Input;
using NVorbis;
using Terraria;
using Terraria.ModLoader;

namespace MechaKeys.Core
{
	internal static class ClickAudio
	{
		private const int MaxSeconds = 3;
		private static readonly Dictionary<string, SoundEffect> Cache = new(StringComparer.OrdinalIgnoreCase);
		private static bool _ignoreMouse;

		internal static void Unload()
		{
			ClearCache();
			_ignoreMouse = false;
		}

		internal static void SuppressMouse() => _ignoreMouse = true;

		internal static void UpdateMouseSuppression(MouseState mouse)
		{
			if (_ignoreMouse && mouse.LeftButton == ButtonState.Released && mouse.RightButton == ButtonState.Released)
				_ignoreMouse = false;
		}

		internal static bool MouseSuppressed => _ignoreMouse;

		internal static void ClearCache()
		{
			foreach (SoundEffect effect in Cache.Values) {
				try {
					effect.Dispose();
				}
				catch {
				}
			}

			Cache.Clear();
		}

		internal static void Warmup()
		{
			MechaKeysConfig cfg = ModContent.GetInstance<MechaKeysConfig>();
			if (cfg == null)
				return;

			WarmupPack(cfg.KeyboardPack, 8);
			if (!string.Equals(cfg.MousePack, cfg.KeyboardPack, StringComparison.OrdinalIgnoreCase))
				WarmupPack(cfg.MousePack, 8);
		}

		internal static void PlayKeyboard(MechaKeysConfig cfg)
		{
			if (cfg == null || !cfg.Enabled || !cfg.KeyboardSounds)
				return;
			PlayPack(cfg.KeyboardPack, cfg.KeyboardVolume / 100f, cfg.KeyboardAmplify);
		}

		internal static void PlayMouse(MechaKeysConfig cfg)
		{
			if (cfg == null || !cfg.Enabled || !cfg.MouseSounds)
				return;
			PlayPack(cfg.MousePack, cfg.MouseVolume / 100f, cfg.MouseAmplify);
		}

		internal static void PlayPack(string pack, float volume, int amplify)
		{
			SoundClip clip = SoundPackCatalog.Pick(pack);
			if (clip == null)
				return;

			SoundEffect effect = GetOrLoad(clip);
			if (effect == null)
				return;

			float vol = Math.Clamp(volume * Main.soundVolume, 0f, 1f);
			if (vol <= 0.001f)
				return;

			int layers = Math.Clamp(amplify, 1, 8);
			try {
				for (int i = 0; i < layers; i++)
					effect.Play(vol, 0f, 0f);
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Warn("Failed to play click: " + e.Message);
			}
		}

		private static void WarmupPack(string pack, int count)
		{
			IReadOnlyList<SoundClip> clips = SoundPackCatalog.GetClips(pack);
			int n = Math.Min(count, clips.Count);
			for (int i = 0; i < n; i++)
				GetOrLoad(clips[i]);
		}

		private static SoundEffect GetOrLoad(SoundClip clip)
		{
			if (clip == null)
				return null;
			string key = clip.CacheKey;
			if (string.IsNullOrEmpty(key))
				return null;
			if (Cache.TryGetValue(key, out SoundEffect cached))
				return cached;

			try {
				SoundEffect effect = Load(clip);
				if (effect != null)
					Cache[key] = effect;
				return effect;
			}
			catch (Exception e) {
				ModContent.GetInstance<MechaKeys>()?.Logger.Warn("Failed to decode " + key + ": " + e.Message);
				return null;
			}
		}

		private static SoundEffect Load(SoundClip clip)
		{
			if (clip.ZipPath != null)
				return LoadFromZip(clip);

			if (string.IsNullOrEmpty(clip.FilePath) || !File.Exists(clip.FilePath))
				return null;

			if (clip.Extension == ".wav") {
				using var fs = File.OpenRead(clip.FilePath);
				using var ms = new MemoryStream();
				fs.CopyTo(ms);
				ms.Position = 0;
				return SoundEffect.FromStream(ms);
			}

			using var ogg = File.OpenRead(clip.FilePath);
			return DecodeOgg(ogg);
		}

		private static SoundEffect LoadFromZip(SoundClip clip)
		{
			using ZipArchive zip = ZipFile.OpenRead(clip.ZipPath);
			ZipArchiveEntry entry = zip.GetEntry(clip.ZipEntry);
			if (entry == null)
				return null;

			using Stream raw = entry.Open();
			using var ms = new MemoryStream();
			raw.CopyTo(ms);
			ms.Position = 0;
			if (clip.Extension == ".wav")
				return SoundEffect.FromStream(ms);
			return DecodeOgg(ms);
		}

		private static SoundEffect DecodeOgg(Stream stream)
		{
			using var reader = new VorbisReader(stream, false);
			int channels = Math.Max(1, reader.Channels);
			int rate = reader.SampleRate;
			if (rate < 8000 || rate > 48000)
				return null;

			int outCh = channels >= 2 ? 2 : 1;
			int maxFloats = rate * channels * MaxSeconds;
			float[] buffer;
			int filled;
			if (reader.TotalSamples > 0 && reader.TotalSamples < 10_000_000) {
				int expected = (int)Math.Min(maxFloats, reader.TotalSamples * channels);
				buffer = new float[Math.Max(expected, channels)];
				filled = 0;
				while (filled < buffer.Length) {
					int n = reader.ReadSamples(buffer, filled, buffer.Length - filled);
					if (n <= 0)
						break;
					filled += n;
				}
			}
			else {
				var list = new List<float>(rate * channels / 4);
				var scratch = new float[4096];
				int n;
				while ((n = reader.ReadSamples(scratch, 0, scratch.Length)) > 0) {
					for (int i = 0; i < n; i++)
						list.Add(scratch[i]);
					if (list.Count >= maxFloats)
						break;
				}

				buffer = list.ToArray();
				filled = buffer.Length;
			}

			if (filled < channels)
				return null;

			int frames = filled / channels;
			var pcm = new short[frames * outCh];
			for (int f = 0; f < frames; f++) {
				for (int c = 0; c < outCh; c++) {
					int src = Math.Min(c, channels - 1);
					float sample = buffer[f * channels + src];
					if (sample > 1f)
						sample = 1f;
					else if (sample < -1f)
						sample = -1f;
					pcm[f * outCh + c] = (short)(sample * 32767f);
				}
			}

			byte[] bytes = new byte[pcm.Length * 2];
			Buffer.BlockCopy(pcm, 0, bytes, 0, bytes.Length);
			return new SoundEffect(bytes, rate, outCh == 1 ? AudioChannels.Mono : AudioChannels.Stereo);
		}
	}
}
