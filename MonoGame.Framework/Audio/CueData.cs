using System;
using System.Collections.Generic;
using System.IO;

namespace Microsoft.Xna.Framework.Audio
{
	internal class CueData
	{
		public enum MaxInstanceBehavior : byte
		{
			Fail,
			Queue,
			ReplaceOldest,
			ReplaceQuietest,
			ReplaceLowestPriority
		}

		public XACTSound[] Sounds
		{
			get;
			private set;
		}

		public ushort Category
		{
			get;
			private set;
		}

		public float[,] Probabilities
		{
			get;
			private set;
		}

		public bool IsUserControlled
		{
			get;
			private set;
		}

		public string UserControlVariable
		{
			get;
			private set;
		}

		public byte InstanceLimit
		{
			get;
			private set;
		}

		public MaxInstanceBehavior MaxCueBehavior
		{
			get;
			private set;
		}

		public CueData(XACTSound sound)
		{
			Sounds = new XACTSound[1];
			Probabilities = new float[1, 2];

			Sounds[0] = sound;
			Category = sound.Category;
			Probabilities[0, 0] = 1.0f;
			Probabilities[0, 1] = 0.0f;
			IsUserControlled = false;

			// Assume we can have max instances, for now.
			InstanceLimit = 255;
			MaxCueBehavior = MaxInstanceBehavior.ReplaceOldest;
		}

		public CueData(
			XACTSound[] sounds,
			float[,] probabilities,
			string controlVariable
		) {
			Sounds = sounds;
			Category = Sounds[0].Category; // FIXME: Assumption!
			Probabilities = probabilities;
			IsUserControlled = !String.IsNullOrEmpty(controlVariable);
			UserControlVariable = controlVariable;

			// Assume we can have max instances, for now.
			InstanceLimit = 255;
			MaxCueBehavior = MaxInstanceBehavior.ReplaceOldest;
		}

		public void SetLimit(byte instanceLimit, byte behavior)
		{
			InstanceLimit = instanceLimit;
			MaxCueBehavior = (MaxInstanceBehavior) (behavior >> 3);
		}
	}

	internal class XACTSound
	{
		private XACTClip[] INTERNAL_clips;

		public double Volume
		{
			get;
			private set;
		}

		public float Pitch
		{
			get;
			private set;
		}

		public ushort Category
		{
			get;
			private set;
		}

		public bool HasLoadedTracks
		{
			get;
			private set;
		}

		public uint[] RPCCodes
		{
			get;
			private set;
		}

		public uint[] DSPCodes
		{
			get;
			private set;
		}

		public XACTSound(ushort track, byte waveBank)
		{
			INTERNAL_clips = new XACTClip[1];
			INTERNAL_clips[0] = new XACTClip(track, waveBank);
			Category = 0;
			Volume = 0.0;
			HasLoadedTracks = false;
		}

		public XACTSound(BinaryReader reader)
		{
			// Sound Effect Flags
			byte soundFlags = reader.ReadByte();
			bool complex = (soundFlags & 0x01) != 0;

			// AudioCategory Index
			Category = reader.ReadUInt16();

			// Sound Volume
			Volume = XACTCalculator.ParseDecibel(reader.ReadByte());

			// Sound Pitch
			Pitch = (reader.ReadInt16() / 1000.0f);

			// Unknown value
			reader.ReadByte();

			// Length of Sound Entry, unused
			reader.ReadUInt16();

			// Number of Sound Clips
			if (complex)
			{
				INTERNAL_clips = new XACTClip[reader.ReadByte()];
			}
			else
			{
				// Simple Sounds always have 1 PlayWaveEvent.
				INTERNAL_clips = new XACTClip[1];
				ushort track = reader.ReadUInt16();
				byte waveBank = reader.ReadByte();
				INTERNAL_clips[0] = new XACTClip(track, waveBank);
			}

			// Parse RPC Properties
			RPCCodes = new uint[0]; // Eww... -flibit
			if ((soundFlags & 0x0E) != 0)
			{
				// RPC data length, unused
				reader.ReadUInt16();

				// Number of RPC Presets
				RPCCodes = new uint[reader.ReadByte()];

				// Obtain RPC curve codes
				for (byte i = 0; i < RPCCodes.Length; i++)
				{
					RPCCodes[i] = reader.ReadUInt32();
				}
			}

			// Parse DSP Presets
			DSPCodes = new uint[0]; // Eww... -flibit
			if ((soundFlags & 0x10) != 0)
			{
				// DSP Presets Length, unused
				reader.ReadUInt16();

				// Number of DSP Presets
				DSPCodes = new uint[reader.ReadByte()];

				// Obtain DSP Preset codes
				for (byte j = 0; j < DSPCodes.Length; j++)
				{
					DSPCodes[j] = reader.ReadUInt32();
				}
			}

			// Parse Sound Events
			if (complex)
			{
				for (int i = 0; i < INTERNAL_clips.Length; i++)
				{
					// XACT Clip volume
					double clipVolume = XACTCalculator.ParseDecibel(reader.ReadByte());

					// XACT Clip Offset in Bank
					uint offset = reader.ReadUInt32();

					// Unknown value
					reader.ReadUInt32();

					// Store this for when we're done reading the clip.
					long curPos = reader.BaseStream.Position;

					// Go to the Clip in the Bank.
					reader.BaseStream.Seek(offset, SeekOrigin.Begin);

					// Parse the Clip.
					INTERNAL_clips[i] = new XACTClip(reader, clipVolume);

					// Back to where we were...
					reader.BaseStream.Seek(curPos, SeekOrigin.Begin);
				}
			}

			HasLoadedTracks = false;
		}

		public void LoadTracks(AudioEngine audioEngine, List<string> waveBankNames)
		{
			foreach (XACTClip curClip in INTERNAL_clips)
			{
				curClip.LoadTracks(audioEngine, waveBankNames);
			}
			HasLoadedTracks = true;
		}

		public List<SoundEffectInstance> GenerateInstances(
			List<SoundEffectInstance> result,
			List<float> volumeResult
		) {
			// Get the SoundEffectInstance List
			foreach (XACTClip curClip in INTERNAL_clips)
			{
				curClip.GenerateInstances(result, Volume, Pitch);
			}

			// Store completed authored volumes
			foreach (SoundEffectInstance sfi in result)
			{
				volumeResult.Add(sfi.Volume);
			}

			return result;
		}
	}

	internal class XACTClip
	{
		private XACTEvent[] INTERNAL_events;

		private double INTERNAL_clipVolume;

		public XACTClip(ushort track, byte waveBank)
		{
			INTERNAL_clipVolume = 0.0;
			INTERNAL_events = new XACTEvent[1];
			INTERNAL_events[0] = new PlayWaveEvent(
				new ushort[] { track },
				new byte[] { waveBank },
				0,
				0,
				1.0f,
				1.0f,
				0,
				0,
				new byte[] { 0xFF }
			);
		}

		public XACTClip(BinaryReader reader, double clipVolume)
		{
			INTERNAL_clipVolume = clipVolume;

			// Number of XACT Events
			INTERNAL_events = new XACTEvent[reader.ReadByte()];

			for (int i = 0; i < INTERNAL_events.Length; i++)
			{
				// Full Event information
				uint eventInfo = reader.ReadUInt32();

				// XACT Event Type
				uint eventType = eventInfo & 0x0000001F;

				// Load the Event
				if (eventType == 1)
				{
					// Unknown values
					reader.ReadBytes(3);

					/* Event Flags
					 * 0x01 = Break Loop
					 * 0x02 = Use Speaker Position
					 * 0x04 = Use Center Speaker
					 * 0x08 = New Speaker Position On Loop
					 */
					reader.ReadByte();

					// WaveBank Track Index
					ushort track = reader.ReadUInt16();

					// WaveBank Index
					byte waveBank = reader.ReadByte();

					// Number of times to loop wave (255 is infinite)
					byte loopCount = reader.ReadByte();

					// Unknown value
					reader.ReadUInt32();

					// Finally.
					INTERNAL_events[i] = new PlayWaveEvent(
						new ushort[] { track },
						new byte[] { waveBank },
						0,
						0,
						0.0,
						0.0,
						loopCount,
						0,
						new byte[] { 0xFF }
					);
				}
				else if (eventType == 3)
				{
					// Unknown values
					reader.ReadBytes(3);

					/* Event Flags
					 * 0x01 = Break Loop
					 * 0x02 = Use Speaker Position
					 * 0x04 = Use Center Speaker
					 * 0x08 = New Speaker Position On Loop
					 */
					reader.ReadByte();

					// Unknown values
					reader.ReadBytes(5);

					// Number of WaveBank tracks
					ushort numTracks = reader.ReadUInt16();

					// Variation Playlist Type
					ushort variationType = reader.ReadUInt16();

					// Unknown values
					reader.ReadBytes(4);

					// Obtain WaveBank track information
					ushort[] tracks = new ushort[numTracks];
					byte[] waveBanks = new byte[numTracks];
					byte[] weights = new byte[numTracks];
					for (ushort j = 0; j < numTracks; j++)
					{
						tracks[j] = reader.ReadUInt16();
						waveBanks[j] = reader.ReadByte();
						byte minWeight = reader.ReadByte();
						byte maxWeight = reader.ReadByte();
						weights[j] = (byte) (maxWeight - minWeight);
					}

					// Finally.
					INTERNAL_events[i] = new PlayWaveEvent(
						tracks,
						waveBanks,
						0,
						0,
						0.0,
						0.0,
						0,
						variationType,
						weights
					);
				}
				else if (eventType == 4)
				{
					// Unknown values
					reader.ReadBytes(3);

					/* Event Flags
					 * 0x01 = Break Loop
					 * 0x02 = Use Speaker Position
					 * 0x04 = Use Center Speaker
					 * 0x08 = New Speaker Position On Loop
					 */
					reader.ReadByte();
					
					// WaveBank track
					ushort track = reader.ReadUInt16();
					
					// WaveBank index, unconfirmed
					byte waveBank = reader.ReadByte();
					
					// Loop Count, unconfirmed
					byte loopCount = reader.ReadByte();
					
					// Unknown values
					reader.ReadBytes(4);
					
					// Pitch Variation
					short minPitch = reader.ReadInt16();
					short maxPitch = reader.ReadInt16();
					
					// Volume Variation
					double minVolume = XACTCalculator.ParseDecibel(reader.ReadByte());
					double maxVolume = XACTCalculator.ParseDecibel(reader.ReadByte());

					// Unknown values
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadByte();
					
					// Finally.
					INTERNAL_events[i] = new PlayWaveEvent(
						new ushort[] { track },
						new byte[] { waveBank },
						minPitch,
						maxPitch,
						minVolume,
						maxVolume,
						loopCount,
						0,
						new byte[] { 0xFF }
					);
				}
				else if (eventType == 6)
				{
					// Unknown values
					reader.ReadBytes(3);

					/* Event Flags
					 * 0x01 = Break Loop
					 * 0x02 = Use Speaker Position
					 * 0x04 = Use Center Speaker
					 * 0x08 = New Speaker Position On Loop
					 */
					reader.ReadByte();

					// Unknown values
					reader.ReadBytes(5);

					// Pitch variation
					short minPitch = reader.ReadInt16();
					short maxPitch = reader.ReadInt16();

					// Volume variation
					double minVolume = XACTCalculator.ParseDecibel(reader.ReadByte());
					double maxVolume = XACTCalculator.ParseDecibel(reader.ReadByte());

					// Unknown values
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadSingle();
					reader.ReadByte();

					// Variation flags
					// FIXME: There's probably more to these flags...
					byte varFlags = reader.ReadByte();
					if ((varFlags & 0x20) != 0x20)
					{
						// Throw out the volume variation.
						minVolume = 0.0;
						maxVolume = 0.0;
					}
					if ((varFlags & 0x10) != 0x10)
					{
						// Throw out the pitch variation
						minPitch = 0;
						maxPitch = 0;
					}

					// Number of WaveBank tracks
					ushort numTracks = reader.ReadUInt16();

					// Variation Playlist Type
					ushort variationType = reader.ReadUInt16();

					// Unknown values
					reader.ReadBytes(4);

					// Obtain WaveBank track information
					ushort[] tracks = new ushort[numTracks];
					byte[] waveBanks = new byte[numTracks];
					byte[] weights = new byte[numTracks];
					for (ushort j = 0; j < numTracks; j++)
					{
						tracks[j] = reader.ReadUInt16();
						waveBanks[j] = reader.ReadByte();
						byte minWeight = reader.ReadByte();
						byte maxWeight = reader.ReadByte();
						weights[j] = (byte) (maxWeight - minWeight);
					}

					// Finally.
					INTERNAL_events[i] = new PlayWaveEvent(
						tracks,
						waveBanks,
						minPitch,
						maxPitch,
						minVolume,
						maxVolume,
						0,
						variationType,
						weights
					);
				}
				else if (eventType == 8)
				{
					// Unknown values
					reader.ReadBytes(5);

					// Operand Constant
					float constant = reader.ReadSingle();

					// Unknown values
					reader.ReadBytes(8);

					INTERNAL_events[i] = new SetVolumeEvent(
						constant
					);
				}
				else
				{
					// TODO: All XACT Events
					throw new Exception(
						"EVENT TYPE " + eventType + " NOT IMPLEMENTED!"
					);
				}
			}
		}

		public void LoadTracks(AudioEngine audioEngine, List<string> waveBankNames)
		{
			foreach (XACTEvent curEvent in INTERNAL_events)
			{
				if (curEvent.Type == 1)
				{
					((PlayWaveEvent) curEvent).LoadTracks(
						audioEngine,
						waveBankNames
					);
				}
			}
		}

		public void GenerateInstances(
			List<SoundEffectInstance> result,
			double soundVolume,
			float soundPitch
		) {
			List<SoundEffectInstance> wavs = new List<SoundEffectInstance>();
			float eventVolume = 1.0f;
			foreach (XACTEvent curEvent in INTERNAL_events)
			{
				if (curEvent.Type == 1)
				{
					wavs.Add(((PlayWaveEvent) curEvent).GenerateInstance(
						INTERNAL_clipVolume,
						soundVolume,
						soundPitch
					));
				}
				else if (curEvent.Type == 2)
				{
					eventVolume *= ((SetVolumeEvent) curEvent).GetVolume();
				}
			}
			foreach (SoundEffectInstance wav in wavs)
			{
				wav.Volume *= eventVolume;
			}
			result.AddRange(wavs);
		}
	}

	internal abstract class XACTEvent
	{
		public uint Type
		{
			get;
			private set;
		}

		public XACTEvent(uint type)
		{
			Type = type;
		}
	}

	internal class PlayWaveEvent : XACTEvent
	{
		private enum VariationPlaylistType : ushort
		{
			Ordered,
			OrderedFromRandom,
			Random,
			RandomNoImmediateRepeats,
			Shuffle
		}

		private ushort[] INTERNAL_tracks;
		private byte[] INTERNAL_waveBanks;

		private short INTERNAL_minPitch;
		private short INTERNAL_maxPitch;

		private double INTERNAL_minVolume;
		private double INTERNAL_maxVolume;

		private byte INTERNAL_loopCount;

		private VariationPlaylistType INTERNAL_variationType;
		private byte[] INTERNAL_weights;
		private int INTERNAL_curWave;

		private SoundEffect[] INTERNAL_waves;

		private static Random random = new Random();

		public PlayWaveEvent(
			ushort[] tracks,
			byte[] waveBanks,
			short minPitch,
			short maxPitch,
			double minVolume,
			double maxVolume,
			byte loopCount,
			ushort variationType,
			byte[] weights
		) : base(1) {
			INTERNAL_tracks = tracks;
			INTERNAL_waveBanks = waveBanks;
			INTERNAL_minPitch = minPitch;
			INTERNAL_maxPitch = maxPitch;
			INTERNAL_minVolume = minVolume;
			INTERNAL_maxVolume = maxVolume;
			INTERNAL_loopCount = loopCount;
			INTERNAL_variationType = (VariationPlaylistType) variationType;
			INTERNAL_weights = weights;
			INTERNAL_waves = new SoundEffect[tracks.Length];
			INTERNAL_curWave = -1;
		}

		public void LoadTracks(AudioEngine audioEngine, List<string> waveBankNames)
		{
			for (int i = 0; i < INTERNAL_waves.Length; i++)
			{
				INTERNAL_waves[i] = audioEngine.INTERNAL_getWaveBankTrack(
					waveBankNames[INTERNAL_waveBanks[i]],
					INTERNAL_tracks[i]
				);
			}
		}

		public SoundEffectInstance GenerateInstance(
			double clipVolume,
			double soundVolume,
			float soundPitch
		) {
			INTERNAL_getNextSound();
			SoundEffectInstance result = INTERNAL_waves[INTERNAL_curWave].CreateInstance();
			result.INTERNAL_isXACTSource = true;
			result.Volume = XACTCalculator.CalculateAmplitudeRatio(
				soundVolume + clipVolume + (
					random.NextDouble() *
					(INTERNAL_maxVolume - INTERNAL_minVolume)
				) + INTERNAL_minVolume
			);
			result.Pitch = (
				random.Next(
					INTERNAL_minPitch,
					INTERNAL_maxPitch
				) / 1000.0f
			) + soundPitch;
			// FIXME: Better looping!
			result.IsLooped = (INTERNAL_loopCount == 255);
			return result;
		}

		private void INTERNAL_getNextSound()
		{
			if (INTERNAL_variationType == VariationPlaylistType.Ordered)
			{
				INTERNAL_curWave += 1;
				if (INTERNAL_curWave >= INTERNAL_waves.Length)
				{
					INTERNAL_curWave = 0;
				}
			}
			else if (INTERNAL_variationType == VariationPlaylistType.OrderedFromRandom)
			{
				// FIXME: It seems like XACT organizes this for us?
				INTERNAL_curWave += 1;
				if (INTERNAL_curWave >= INTERNAL_waves.Length)
				{
					INTERNAL_curWave = 0;
				}
			}
			else if (INTERNAL_variationType == VariationPlaylistType.Random)
			{
				double max = 0.0;
				for (int i = 0; i < INTERNAL_weights.Length; i++)
				{
					max += INTERNAL_weights[i];
				}
				double next = random.NextDouble() * max;
				for (int i = INTERNAL_weights.Length - 1; i >= 0; i--)
				{
					if (next > max - INTERNAL_weights[i])
					{
						INTERNAL_curWave = i;
						return;
					}
					max -= INTERNAL_weights[i];
				}
			}
			else if (INTERNAL_variationType == VariationPlaylistType.RandomNoImmediateRepeats)
			{
				double max = 0.0;
				for (int i = 0; i < INTERNAL_weights.Length; i++)
				{
					if (i == INTERNAL_curWave)
					{
						continue;
					}
					max += INTERNAL_weights[i];
				}
				double next = random.NextDouble() * max;
				for (int i = INTERNAL_weights.Length - 1; i >= 0; i--)
				{
					if (i == INTERNAL_curWave)
					{
						continue;
					}
					if (next > max - INTERNAL_weights[i])
					{
						INTERNAL_curWave = i;
						return;
					}
					max -= INTERNAL_weights[i];
				}
			}
			else
			{
				throw new Exception(
					"Variation Playlist Type unhandled: " +
					INTERNAL_variationType
				);
			}
		}
	}

	internal class SetVolumeEvent : XACTEvent
	{
		private float INTERNAL_constant;

		public SetVolumeEvent(
			float constant
		) : base(2) {
			INTERNAL_constant = constant;
		}

		public float GetVolume()
		{
			// FIXME: There's probably more that this event does...
			return XACTCalculator.CalculateAmplitudeRatio(INTERNAL_constant);
		}
	}
}
