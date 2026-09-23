# HoPiKo: music disappears on entering a level in Shuffle and PlayAll modes

**Scope:** the music player in the Windows/Steam version of HoPiKo, built with Unity Mono.

## 1. Summary

In the original, unmodified HoPiKo installation, music played in the level-selection menu under every playlist mode. On entering a level, it disappeared in “Shuffle” and “Play All” modes. Returning to the menu restored the music. Repeating a single track worked in the level.

Static analysis found a branch shared by the two affected modes: `AudioManager.Update()` calls for the next track on every frame where `AudioSource.isPlaying == false`. This check does not distinguish a finished track from one that has not started playing, and it ignores the game's own `musicStopped` flag for intentional stops.

The following results were reported by the user for the original game, before the patch.

| Selected mode | Level-selection menu | After entering a level | After returning to the menu |
|---|---|---|---|
| Repeat one track (`Loop`) | Music plays | Music plays | Not tested separately |
| Shuffle (`Shuffle`) | Music plays | Silence | Music returns |
| Play All (`PlayAll`) | Music plays | Silence | Music returns |

## 2. Working hypothesis

After switching from the menu version of a track to its gameplay version, the audio source does not immediately report that it is playing. Automatic advancement selects another track and calls `Stop()` → assign clip → `Play()`. If this sequence repeats before playback can begin, it creates a track-switching loop and silence. This branch is disabled in Loop mode.

The chosen fix allows automatic advancement only after playback has been observed, when playback was not intentionally stopped, and when the current `timeSamples` position is not positive.

## 3. Music-player architecture

The central object is `AudioManager.audioManager`, a static reference to an `AudioManager : MonoBehaviour` instance.

In `Start()`, an object is instantiated from `musicAudioSourcePrefab` and parented to the manager. Its `AudioSource` is stored in the private `musicAudioSource` field. The manager subscribes `OnRunLoaded` to `Level.level.runLoadedDelegate`.

Sound effects use a separate pool of `AudioObject` instances.

### Selected setting versus active mode

```csharp
public enum PlaylistSettings { Loop = 0, Shuffle = 1, PlayAll = 2 }
public enum TrackTypes { Main = 0, Menu = 1, World = 2 }

public PlaylistSettings currentPlaylistSetting;
public PlaylistSettings displayPlaylistSetting = PlaylistSettings.Shuffle;
```

`displayPlaylistSetting` is the user's selection in the interface. `currentPlaylistSetting` is the mode actually used in the current context. The menu is forced into Loop even if Shuffle or PlayAll is selected on screen.

Each `Track` entry has three audio-clip references:

```csharp
public AudioClip trackClip_Main;
public AudioClip trackClip_Menu;
public AudioClip trackClip_World;
public Vector2 trackUnlockedAt;
public bool trackUnlocked;
```

Thus, entering a level changes more than the mode: the source receives the value of a different field in the `Track` entry. Those fields may refer to different clips; without examining the serialized references, we cannot establish whether the actual `AudioClip` objects are the same.

### Method map for the original DLL

| Method | Purpose |
|---|---|
| `PlayNextTrack(bool)` | Select a track index and start playback |
| `Update()` | Advance automatically |
| `SwitchTrackType(TrackTypes)` | Switch between Main/Menu/World and set looping |
| `PlayTrack(int)` | Select a track by index |
| `PlayTrack(float)` | Stop, assign a clip, play |
| `StopMusic()` | Stop intentionally |
| `OnRunLoaded()` | Switch to gameplay music |

## 4. Event chain when entering a level

Confirmed call relationships:

```text
HopikoMenu.MenuTransition("RunSelect")
  -> AudioManager.SwitchTrackType(World)
     -> currentPlaylistSetting = Loop
     -> musicAudioSource.loop = true
     -> PlayTrack(float): trackClip_World

HopikoMenu.EnterLevel / EnterLevelCoroutine
  -> Level.InstantiateRun / InstantiateRunCoroutine
     -> runLoadedDelegate.Invoke()
        -> AudioManager.OnRunLoaded()
           -> SwitchTrackType(Main)
              -> currentPlaylistSetting = displayPlaylistSetting
              -> musicAudioSource.loop = (mode == Loop)
              -> PlayTrack(float): trackClip_Main

Subsequent frames:
AudioManager.Update()
  -> in Shuffle/PlayAll, if !isPlaying: PlayNextTrack(false)
```

In `Level/<InstantiateRunCoroutine>c__IteratorF::MoveNext()`, the run-loaded delegate is invoked at `IL_0781`. The code then resets the camera and reaches `WaitForSeconds(1f)`. `OnRunLoaded` does not wait for confirmation that music has begun playing.

The following abbreviated switching code is equivalent for the three declared `TrackTypes` values:

```csharp
public void SwitchTrackType(TrackTypes type)
{
    if (currentTrackType == type)
        return;

    float time = currentTrackType == TrackTypes.Main ? 0f : musicAudioSource.time;
    currentTrackType = type;
    currentPlaylistSetting = type == TrackTypes.Main
        ? displayPlaylistSetting
        : PlaylistSettings.Loop;

    musicAudioSource.loop = currentPlaylistSetting == PlaylistSettings.Loop;
    PlayTrack(time);
}

private void OnRunLoaded()
{
    SwitchTrackType(TrackTypes.Main);
    currentPlaylistSetting = displayPlaylistSetting;
}
```

Returning to the menu goes through `Level.ExitToMenu(bool)`, which calls `StopMusic()`. Menu transitions then select World or Menu again. This is consistent with the music returning in the menu.

## 5. The problematic check

### 5.1. Confirmed behavior of the original code

```csharp
private void Update()
{
    if (currentPlaylistSetting != PlaylistSettings.Loop
        && !musicAudioSource.isPlaying)
    {
        PlayNextTrack(false);
    }
}
```

In the original IL, this checks the mode, calls `AudioSource.get_isPlaying()`, and calls `PlayNextTrack(false)` unconditionally when it returns false.

`PlayNextTrack` chooses the setting as follows:

```csharp
var setting = useDisplaySettings ? displayPlaylistSetting : currentPlaylistSetting;
if (setting == PlaylistSettings.Loop && useDisplaySettings)
    setting = PlaylistSettings.PlayAll;
```

Therefore, a manual “next” command from the menu can change tracks even when repeat-one is selected. Automatic advancement from `Update` passes false.

When more than one track is unlocked, Shuffle takes the next index from a pre-shuffled array. PlayAll increments the index, wrapping at the end of the list and skipping locked tracks. Regardless of whether the index changes, the method ends by calling `PlayTrack(0f)`.

Actual playback code, abbreviated:

```csharp
private void PlayTrack(float time = 0f)
{
    musicAudioSource.Stop();
    switch (currentTrackType)
    {
        case TrackTypes.Main:
            musicAudioSource.clip = trackList[currentTrackIndex].trackClip_Main;
            musicAudioSource.time = 0f;
            break;
        case TrackTypes.Menu:
            musicAudioSource.clip = trackList[currentTrackIndex].trackClip_Menu;
            musicAudioSource.time = time;
            break;
        case TrackTypes.World:
            musicAudioSource.clip = trackList[currentTrackIndex].trackClip_World;
            musicAudioSource.time = time;
            break;
    }
    musicAudioSource.Play();
    musicStopped = false;
}
```

### 5.2. A scenario that could produce silence

| Step | Possible state | Original behavior |
|---|---|---|
| 1 | Main clip A is requested | Stop → clip=A → Play |
| 2 | On the next Update, `isPlaying == false` | Select B; Stop → Play again |
| 3 | On the following Update, B has not started either | Select C; Stop → Play again |
| 4 | This continues | Frequent restarts keep the track from becoming audible |

### 5.3. Intentional stops are ignored

```csharp
public void StopMusic()
{
    musicAudioSource.Stop();
    musicStopped = true;
}
```

`Update()` does not read `musicStopped`. With the manager active and a mode other than Loop selected, the next Update may request playback again. This inconsistency is confirmed.

### 5.4. Another possible line of investigation

Shuffle excludes index 0 from its array, initializes its position to 1, and does not check `trackUnlocked` when selecting a track. These details warrant separate investigation.

## 6. Proposed fix

The idea is to distinguish “has not started playing yet” from “played and then stopped.” A `musicPlaybackObserved` flag is introduced. It is reset whenever `PlayTrack(float)` is called and is set only when `isPlaying == true && timeSamples > 0`.

Relative to the **original**, the fix requires one state flag and changes to two methods:

1. Reset the observed-playback flag at the start of `PlayTrack(float)`. Leave the rest of the method intact.
2. In `Update()`, set the flag when `isPlaying == true` and `timeSamples > 0`; do not advance while the source is playing.
3. When the source is not playing, permit `PlayNextTrack(false)` only if the mode is not Loop, `musicStopped == false`, the flag is set, and the position is not positive.
4. Reset the flag before advancing so that the next track is also protected during startup.

The proposed logic is:

```csharp
private bool musicPlaybackObserved;

private void Update()
{
    if (musicAudioSource.isPlaying)
    {
        if (!musicPlaybackObserved && musicAudioSource.timeSamples > 0)
            musicPlaybackObserved = true;
        return;
    }

    if (currentPlaylistSetting == PlaylistSettings.Loop
        || musicStopped
        || !musicPlaybackObserved
        || musicAudioSource.timeSamples > 0)
        return;

    musicPlaybackObserved = false;
    PlayNextTrack(false);
}

private void PlayTrack(float time = 0f)
{
    musicPlaybackObserved = false;
    ...
    // Rest of the method
}
```

### Note on `|| musicAudioSource.timeSamples > 0`

An earlier version had a bug:

- In Shuffle and PlayAll, returning from Alt+Tab started a new track instead of resuming the previous one.

The likely mechanism was:

```text
Track is playing: musicPlaybackObserved = true
-> focus is lost / audio pauses temporarily
-> Update sees isPlaying == false
-> musicStopped remains false, mode != Loop
-> PlayNextTrack(false)
```

The assumption used here is that Alt+Tab preserves a positive playback position, whereas natural completion resets it to zero. This can be used in `Update()`.
