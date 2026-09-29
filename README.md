# CassieReplacement

Custom CASSIE voice lines for SCP: Secret Laboratory.

Swap (or extend) the usual facility announcements with your own `.ogg` clips — custom words, extra voices, MTF / Chaos / SCP overrides, the works.

| | |
|---|---|
| **Version** | 1.9.0 |
| **API** | **LabAPI** (recommended) |
| **EXILED** | WIP — not for live servers yet |
| **Audio** | [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) by Axwabo |
| **Original** | [icedchai](https://github.com/icedchai) (`icedchqi`) — this is a fork/port of their plugin |

https://github.com/user-attachments/assets/c17a9dd9-2635-4f65-a49f-ed715d85db1c

> This LabAPI port used AI help — saying that upfront. It works on current LabAPI + SecretLabNAudio, but still: test on a private server first. If you code, feel free to rip out anything you don’t like.

---

## Table of contents

1. [What you need](#1-what-you-need)
2. [Install SecretLabNAudio](#2-install-secretlabnaudio)
3. [Install CassieReplacement](#3-install-cassiereplacement)
4. [Audio files](#4-audio-files)
5. [Config](#5-config)
6. [Playing custom CASSIE](#6-playing-custom-cassie)
7. [Examples](#7-examples)
8. [Remote Admin commands](#8-remote-admin-commands)
9. [Announcement overrides](#9-announcement-overrides)
10. [EXILED](#10-exiled)
11. [Credits](#11-credits)
12. [Starter config](#12-starter-config)

---

## 1. What you need

| Thing | Notes | Why |
|---|---|---|
| **LabAPI** | Whatever your server already runs | Loads the plugin |
| **[SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio/releases)** | Latest release | In-game speakers / playback |
| **0Harmony** | 2.2.2+ | Hooks the CASSIE queue |

This fork does **not** use AudioPlayerApi. Don’t install it alongside SecretLabNAudio for this plugin — you’ll just get silence and confusion.

CassieReplacement ships with NVorbis embedded, so you don’t need a separate NVorbis module **for this plugin**. If you use SecretLabNAudio’s modular install, follow *that* release’s readme for its own modules.

---

## 2. Install SecretLabNAudio

Install the audio plugin **before** CassieReplacement.

1. Download `SecretLabNAudio.zip` from the [releases page](https://github.com/Axwabo/SecretLabNAudio/releases).
2. Pick one install style:

### Option A — Single file (easiest)

1. From the zip, take `bin/SecretLabNAudio.dll`.
2. Put it in LabAPI’s **global** plugins folder:

| OS | Folder |
|---|---|
| **Windows** | `%appdata%\SCP Secret Laboratory\LabAPI\plugins\global\` |
| **Linux** | `~/.config/SCP Secret Laboratory/LabAPI/plugins/global/` |

Tip: paste `%appdata%\SCP Secret Laboratory\LabAPI\` into Explorer to jump there.

### Option B — Modular

- `SecretLabNAudio.Core.dll` (+ deps) → LabAPI **dependencies**
- Modules (NVorbis etc.) → wherever that release’s readme says

### Load order

If CassieReplacement loads first, audio can break. Rename so SecretLabNAudio sorts first, e.g.:

```text
0_SecretLabNAudio.dll
```

---

## 3. Install CassieReplacement

1. Put **`CassieReplacement.dll`** in your port’s plugin folder:

```text
LabAPI/plugins/<port>/
```

Example for port `7777`: `plugins/7777/`.

2. Make sure **`0Harmony.dll`** is in:

```text
LabAPI/dependencies/<port>/
```

If something else already dropped Harmony there, you’re fine.

3. Restart the server once so LabAPI loads the plugin and generates config.

---

## 4. Audio files

Wrong format = bells, then silence. Clips need to match this:

| Rule | Value |
|---|---|
| **Format** | `.ogg` (Vorbis) |
| **Channels** | **Mono** |
| **Sample rate** | **48000 Hz** |

Audacity / ffmpeg / whatever — export Ogg Vorbis, mono, 48 kHz.

### Naming

Filename without `.ogg` = the word you type.

| File | Word |
|---|---|
| `breach.ogg` | `breach` |
| `hasentered.ogg` | `hasentered` |
| `scp_049.ogg` | `scp_049` |

Use `_` instead of spaces in filenames.

### Where they go

Default folder (created on start if missing):

```text
LabAPI/configs/CASSIE Replacement/
```

That’s `{labapi_configs}/CASSIE Replacement` in config terms — same LabAPI configs root the plugin resolves at runtime.

Subfolders are fine; the plugin scans recursively (`alphabet/`, `numbers/`, etc.).

---

## 5. Config

Generated after the first restart. Important bits:

### `custom_cassie_prefix`

**Default:** `customcassie`

If the announcement includes this token (usually at the start), the plugin takes over and plays your clips. Without it, vanilla CASSIE runs.

```text
cassie customcassie attention breach
```

You can rename the prefix in config — just use the same word in commands.

### `base_directories`

Folders to scan for `.ogg` files.

| Field | Meaning |
|---|---|
| **`path`** | Folder to scan. `{labapi_configs}/CASSIE Replacement` for the default, or an absolute path. |
| **`prefix`** | Stuck on every clip name from that folder. `attention.ogg` + `prefix: j` → word `jattention`. Handy for a second voice pack. |
| **`bleed_time`** | Seconds trimmed off the end of each clip when timing the next word. Leave `0` unless words overlap / cut late. |
| **`should_list`** | Whether those words show in list helpers. |

### Speakers & volume

| Setting | Meaning |
|---|---|
| **`use_global_speaker`** | One speaker everyone hears (simple default). |
| **`use_spatial_speakers`** | Speakers at facility PA positions. |
| **`global_for_surface_only`** | Spatial/global tweak for Surface. Leave default unless you need it. |
| **`global_speaker_volume`** | Global speaker loudness. |
| **`global_speaker_volume_multiplier`** | Extra multiplier on global. |
| **`cassie_volume`** | Loudness of the custom words. |
| **`spatial_speaker_*`** | Distance / volume for spatial mode. |

Start with global on, spatial off. Get voice working first.

### `cassie_override_config`

Automatic lines (MTF, Chaos, SCP death) — not just RA `cassie`.

| Setting | Meaning |
|---|---|
| **`should_override_announcements`** | Replace built-in MTF/Chaos/termination templates with yours. Only enable when you have the clips. |
| **`should_override_all`** | Every CASSIE line tries custom audio. Missing clips = silence. Use carefully. |
| **`ntf_wave_announcement` / `chaos_*` / `scp_termination_*`…** | `words` = clip names, `translation` = subtitles. |

Put your prefix at the start of `words` when that line should use your pack.

---

## 6. Playing custom CASSIE

1. Drop mono 48 kHz `.ogg` files into `LabAPI/configs/CASSIE Replacement/`.
2. Restart (or re-register) so new files load.
3. In RA / console:

```text
cassie customcassie attention containment breach
```

4. Subtitles: semicolon `;`, then text:

```text
cassie customcassie attention containment breach ; Attention. Containment breach.
```

You get the normal CASSIE bells/noise, then your voice.

### Extra tokens

| Token | Effect |
|---|---|
| `prefix_j` | Next words look for `j` + name (`attention` → `jattention`) |
| `prefix_` | Clears that name prefix |
| `suffix_k` / `suffix_` | Same, glued to the **end** of names |
| `pitch_1.2` | Pitch / speed for following custom words |
| `jam_50_3` | Jam effect (delay % + repeats) |
| `yield_0.5` | Silence for 0.5s |
| `nocassie` | Skip vanilla noise / padding path |
| `noparse` | Leave the line to vanilla CASSIE |

### Two voices

`prefix_j` doesn’t invent a second pack. You need either:

- files named `jattention.ogg`, `jbreach.ogg`, …, or
- a second `base_directories` entry with `prefix: 'j'` (files stay `attention.ogg`, register as `jattention`)

One pack only → `prefix_j` and `prefix_` sound the same. That’s expected.

---

## 7. Examples

### Basic

Files: `attention.ogg`, `containment.ogg`, `breach.ogg`

```text
cassie customcassie attention containment breach
```

### With subtitles

```text
cassie customcassie attention containment breach ; Attention. Containment breach in progress.
```

### Pitch mid-line

```text
cassie customcassie pitch_0.85 scp 0 4 9 containedsuccessfully
```

### Pause

```text
cassie customcassie attention yield_1.0 breach
```

### Folder with a name prefix

```yaml
base_directories:
  - path: '{labapi_configs}/Nato'
    prefix: 'nato_'
    bleed_time: 0
```

`foxtrot.ogg` → word `nato_foxtrot`:

```text
cassie customcassie nato_foxtrot 1 8 hasentered
```

### Two voice folders

```yaml
base_directories:
  - path: '{labapi_configs}/CASSIE Replacement/voice_default'
    prefix: ''
    bleed_time: 0
  - path: '{labapi_configs}/CASSIE Replacement/voice_j'
    prefix: 'j'
    bleed_time: 0
```

```text
cassie customcassie prefix_ attention hasentered
cassie customcassie prefix_j attention hasentered
```

### Override MTF entrance

```yaml
cassie_override_config:
  should_override_announcements: true
  ntf_wave_announcement:
    words: 'customcassie mtfunit epsilon 11 designated {letter} {number} hasentered'
    translation: 'MTF Epsilon-11 {letter}-{number} has entered the facility.'
```

Only turn overrides on when those clips exist.

---

## 8. Remote Admin commands

| Command | Aliases | Use |
|---|---|---|
| `registercassie <path> [bleed] [prefix]` | `register`, `registercc` | Add a clip folder while the server is running |
| `unregistercassie` | `unregister`, `unregistercc` | Unregister clips |
| `clearcustomcassie` | `clearcc`, `clearcustom` | Stop custom playback / clear queue |
| `customcassievolume <value>` | `cassievolume`, `ccvolume` | Change custom volume on the fly |
| `listwords` | — | List / duration helper |

```text
registercassie C:/SCP/Audio/Cassie 0
clearcustomcassie
customcassievolume 1.2
```

---

## 9. Announcement overrides

Under `cassie_override_config`. Placeholders filled when the event fires:

| Keyword | Fills with |
|---|---|
| `{letter}` / `{number}` | MTF unit (e.g. FOXTROT-18) |
| `{scps}` | SCPs left (threat overview) |
| `{threatoverview}` | No SCPs / one SCP / many SCPs line |
| `{scp}` | Contained SCP |
| `{deathcause}` | How they died / got contained |
| `{team}` | Killer team callsign |
| `{scpkiller}` | If another SCP did it |

Start `words` with your prefix when that line should use your pack.

---

## 10. EXILED

There may be an `EXILED-WIP` build around. This fork is maintained for **LabAPI + SecretLabNAudio**. Use the LabAPI DLL on a real server.

---

## 11. Credits

| | |
|---|---|
| **Original** | **icedchqi** ([icedchai](https://github.com/icedchai)) |
| **This LabAPI / SecretLabNAudio port** | Community fork |
| **Audio** | [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) — Axwabo |

Thanks to icedchqi for the original CassieReplacement. Same idea, current LabAPI audio stack.

---

## 12. Starter config

After first start, replace or merge with this:

- Default audio folder
- Global speaker on
- Prefix `customcassie`
- Announcement overrides **off** until you have a full pack

```yaml
is_enabled: true
debug: false

use_global_speaker: true
use_spatial_speakers: false
global_for_surface_only: false

spatial_speaker_max_distance: 40
spatial_speaker_min_distance: 20
spatial_speaker_volume: 1

global_speaker_volume: 1.5
global_speaker_volume_multiplier: 1

custom_cassie_prefix: customcassie

base_directories:
  - path: '{labapi_configs}/CASSIE Replacement'
    prefix: ''
    bleed_time: 0
    should_list: true

# Optional second voice pack:
#  - path: '{labapi_configs}/CASSIE Replacement/voice_j'
#    prefix: 'j'
#    bleed_time: 0
#    should_list: true

words_to_basegame_override: {}
cassie_volume: 1

cassie_override_config:
  should_override_announcements: false
  should_override_all: false

  ntf_wave_announcement:
    words: 'customcassie mtfunit epsilon 11 designated {letter} {number} hasentered allremaining {threatoverview}'
    translation: 'Mobile Task Force Unit Epsilon-11 designated {letter}-{number} has entered the facility.<split>All remaining personnel are advised to proceed with standard evacuation protocols until an MTF squad reaches your destination.<split>{threatoverview}'

  threat_overview_no_scps:
    words: 'noscpsleft'
    translation: 'Substantial threat to safety remains within the facility -- exercise caution.'

  threat_overview_one_scp:
    words: 'awaitingrecontainment 1 scpsubject'
    translation: 'Awaiting recontainment of: 1 SCP subject.'

  threat_overview_scps:
    words: 'awaitingrecontainment {scps} scpsubjects'
    translation: 'Awaiting recontainment of: {scps} SCP subjects.'

  ntf_mini_announcement:
    words: 'customcassie ninetailedfox backup unit designated {letter} {number} hasentered {threatoverview}'
    translation: 'Nine-Tailed Fox Backup Unit designated {letter}-{number} has entered the facility.<split>{threatoverview}'

  chaos_wave_announcement:
    words: 'customcassie security alert . substantial chaos insurgent activity detected . security personnel proceed with standard protocols'
    translation: 'Security alert. Substantial Chaos Insurgent activity detected.<split>Security personnel, proceed with standard protocols.'

  chaos_mini_announcement:
    words: 'customcassie attention security personnel . chaosinsurgency spotted at gate a'
    translation: 'Attention security personnel. Chaos Insurgency spotted at Gate A.'

  scp_termination_announcement:
    words: 'customcassie {scp} {deathcause}'
    translation: '{scp} {deathcause}'

  scp_lookup_table:
    Scp049:
      words: 'scp 0 4 9'
      translation: 'SCP-049'
    Scp0492:
      words: 'scp 0 4 9 2'
      translation: 'SCP-049-2'
    Scp096:
      words: 'scp 0 9 6'
      translation: 'SCP-096'
    Scp079:
      words: 'scp 0 7 9'
      translation: 'SCP-079'
    Scp106:
      words: 'scp 1 0 6'
      translation: 'SCP-106'
    Scp939:
      words: 'scp 9 3 9'
      translation: 'SCP-939'
    Scp3114:
      words: 'scp 3 1 1 4'
      translation: 'SCP-3114'

  damage_type_termination_announcement_lookup_table:
    Tesla:
      words: ' successfully terminated by automatic security system'
      translation: 'successfully terminated by automatic security system.'
    Warhead:
      words: ' successfully terminated by alpha warhead'
      translation: 'successfully terminated by Alpha Warhead.'
    Decontamination:
      words: ' lost in decontamination sequence'
      translation: 'lost in decontamination sequence.'
    Player:
      words: ' containedsuccessfully {team}'
      translation: 'contained successfully {team}.'
    Unknown:
      words: ' successfully terminated . termination cause unspecified'
      translation: 'successfully terminated. Termination cause unspecified.'

  team_termination_callsign_lookup_table:
    ClassD:
      words: ' by classd personnel'
      translation: 'by Class-D personnel'
    ChaosInsurgency:
      words: ' by chaosinsurgency'
      translation: 'by Chaos Insurgency'
    Scientists:
      words: ' by science personnel'
      translation: 'by Science Personnel'
    FoundationForces:
      words: ' containmentunit {letter} {number}'
      translation: '-- Containment Unit {letter}-{number}'
    OtherAlive:
      words: ' by unknown personnel'
      translation: 'by unknown personnel'
    SCPs:
      words: ' by {scpkiller}'
      translation: 'by {scpkiller}'
```

### If it’s silent

1. SecretLabNAudio installed and loads **before** CassieReplacement (`0_SecretLabNAudio.dll`).
2. Clips are mono `.ogg` @ 48000 Hz.
3. Files are in `LabAPI/configs/CASSIE Replacement/`.
4. You used the prefix: `cassie customcassie yourword`.
5. Server restarted after adding new `.ogg` files.
