using System;
using System.Collections.Generic;
using UnityEngine;

public partial class AudioManager : MonoBehaviour
{
	private bool glitchedState;
	
	private void Update()
	{
		if (this.musicAudioSource.timeSamples > 0 ||
            this.currentPlaylistSetting == AudioManager.PlaylistSettings.Loop ||
            this.musicStopped)
		{
			return;
		}

		if (this.glitchedState &&
            this.musicAudioSource.isPlaying)
		{
			this.glitchedState = false;
			return;
		}

		if (!this.glitchedState &&
            !this.musicAudioSource.isPlaying)
		{
			this.PlayNextTrack(false);
			this.glitchedState = true;
		}
	}
}