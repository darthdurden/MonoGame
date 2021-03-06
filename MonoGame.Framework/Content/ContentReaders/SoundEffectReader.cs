// #region License
// /*
// Microsoft Public License (Ms-PL)
// MonoGame - Copyright © 2009 The MonoGame Team
// 
// All rights reserved.
// 
// This license governs use of the accompanying software. If you use the software, you accept this license. If you do not
// accept the license, do not use the software.
// 
// 1. Definitions
// The terms "reproduce," "reproduction," "derivative works," and "distribution" have the same meaning here as under 
// U.S. copyright law.
// 
// A "contribution" is the original software, or any additions or changes to the software.
// A "contributor" is any person that distributes its contribution under this license.
// "Licensed patents" are a contributor's patent claims that read directly on its contribution.
// 
// 2. Grant of Rights
// (A) Copyright Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free copyright license to reproduce its contribution, prepare derivative works of its contribution, and distribute its contribution or any derivative works that you create.
// (B) Patent Grant- Subject to the terms of this license, including the license conditions and limitations in section 3, 
// each contributor grants you a non-exclusive, worldwide, royalty-free license under its licensed patents to make, have made, use, sell, offer for sale, import, and/or otherwise dispose of its contribution in the software or derivative works of the contribution in the software.
// 
// 3. Conditions and Limitations
// (A) No Trademark License- This license does not grant you rights to use any contributors' name, logo, or trademarks.
// (B) If you bring a patent claim against any contributor over patents that you claim are infringed by the software, 
// your patent license from such contributor to the software ends automatically.
// (C) If you distribute any portion of the software, you must retain all copyright, patent, trademark, and attribution 
// notices that are present in the software.
// (D) If you distribute any portion of the software in source code form, you may do so only under this license by including 
// a complete copy of this license with your distribution. If you distribute any portion of the software in compiled or object 
// code form, you may only do so under a license that complies with this license.
// (E) The software is licensed "as-is." You bear the risk of using it. The contributors give no express warranties, guarantees
// or conditions. You may have additional consumer rights under your local laws which this license cannot change. To the extent
// permitted under your local laws, the contributors exclude the implied warranties of merchantability, fitness for a particular
// purpose and non-infringement.
// */
// #endregion License
// 

using System;
using System.IO;

using Microsoft.Xna.Framework.Audio;

#if WINRT
using SharpDX.XAudio2;
#endif

namespace Microsoft.Xna.Framework.Content
{
	internal class SoundEffectReader : ContentTypeReader<SoundEffect>
	{
#if ANDROID
		static string[] supportedExtensions = new string[] { ".wav", ".mp3", ".ogg", ".mid" };
#else
		static string[] supportedExtensions = new string[] { ".wav", ".aiff", ".ac3", ".mp3" };
#endif

		internal static string Normalize(string fileName)
		{
			return Normalize(fileName, supportedExtensions);
		}

		protected internal override SoundEffect Read(ContentReader input, SoundEffect existingInstance)
		{
			// Format block length
			uint formatLength = input.ReadUInt32();

			// Wavedata format
			ushort format = input.ReadUInt16();

			// Number of channels
			ushort channels = input.ReadUInt16();

			// Sample rate
			uint sampleRate = input.ReadUInt32();

			// Averate bytes per second, unused
			input.ReadUInt32();

			// Block alignment, needed for MSADPCM
			ushort blockAlign = input.ReadUInt16();

			// Bit depth, unused
			input.ReadUInt16();

			// cbSize, unused
			input.ReadUInt16();

			// Seek past the rest of this crap
			input.BaseStream.Seek(formatLength - 18, SeekOrigin.Current);

			// Wavedata
			byte[] data = input.ReadBytes(input.ReadInt32());

			// Loop information
			uint loopStart = input.ReadUInt32();
			uint loopLength = input.ReadUInt32();

			// Sound duration in milliseconds, unused
			input.ReadUInt32();

			return new SoundEffect(
				input.AssetName,
				data,
				sampleRate,
				channels,
				loopStart,
				loopLength,
				(uint) ((format == 2) ? (blockAlign / channels) : (ushort) 0)
			);
		}
	}
}
