using System;
using Microsoft.Xna.Framework;

namespace Microsoft.Xna.Framework.Audio
{
	// http://msdn.microsoft.com/en-us/library/microsoft.xna.framework.audio.audiolistener.aspx
	public class AudioListener
	{
		public Vector3 Forward
		{
			get;
			set;
		}

		public Vector3 Position
		{
			get;
			set;
		}


		public Vector3 Up
		{
			get;
			set;
		}

		public Vector3 Velocity
		{
			get;
			set;
		}

		public AudioListener()
		{
			Forward = Vector3.Forward;
			Position = Vector3.Zero;
			Up = Vector3.Up;
			Velocity = Vector3.Zero;
		}
	}
}
