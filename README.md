# CassieReplacement

Hey! So you want **custom CASSIE voices** on your SCP: Secret Laboratory server? You’re in the right place.

This plugin lets you swap (or extend) those classic facility announcements with **your own `.ogg` voice lines**. Custom words, funny voices, full MTF/Chaos/SCP containment overrides — all of that.

| | |
|---|---|
| **Version** | 1.9.0 |
| **Works with** | **LabAPI** (this is the one you want) |
| **EXILED** | Work in progress — don’t use it for a live server yet |
| **Needs audio from** | [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) by Axwabo |
| **Original author** | [icedchqi](https://github.com/icedchai) (`icedchai`) — this is a fork/port of their idea |

https://github.com/user-attachments/assets/c17a9dd9-2635-4f65-a49f-ed715d85db1c

> **Quick honesty note:** this LabAPI update/port was made with AI help. I’m not hiding that. Some people hate AI plugins — fair. There wasn’t a volunteer lining up to keep this thing alive on current LabAPI, so this exists as a working base. It **does work**, but treat it like any community plugin: test on a private server first, and if you’re a coder, feel free to rip out anything you don’t like.

---

## Table of contents

1. [What you need first](#1-what-you-need-first)
2. [Install SecretLabNAudio (do this first)](#2-install-secretlabnaudio-do-this-first)
3. [Install CassieReplacement](#3-install-cassiereplacement)
4. [Your audio files (this is the fun part)](#4-your-audio-files-this-is-the-fun-part)
5. [Config explained like you’re five](#5-config-explained-like-youre-five)
6. [How to actually play custom CASSIE](#6-how-to-actually-play-custom-cassie)
7. [Handy examples](#7-handy-examples)
8. [Remote Admin commands](#8-remote-admin-commands)
9. [Announcement overrides (MTF / Chaos / SCP)](#9-announcement-overrides-mtf--chaos--scp)
10. [EXILED note](#10-exiled-note)
11. [Credits](#11-credits)
12. [Copy-paste config (ready to use)](#12-copy-paste-config-ready-to-use)

---

## 1. What you need first

Think of this like ingredients before cooking. If one is missing, nothing plays.

| Thing | Version / notes | Why you need it |
|---|---|---|
| **LabAPI** | ~1.7.1 (or whatever your server already uses) | Loads plugins. No LabAPI = no plugin. |
| **[SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio/releases)** | Latest release | The actual “speakers” in-game. CassieReplacement tells it *what* to play. |
| **SecretLabNAudio NVorbis module** | Latest (comes with the zip / modular setup) | Lets the audio stack understand `.ogg` files. |
| **0Harmony** | 2.2.2 or newer | Quietly hooks into the game’s CASSIE queue so we can intercept announcements. |

**Do not** install the old **AudioPlayerApi** for this fork. Audio goes through SecretLabNAudio only. Mixing both is a great way to get silence and confusion.

---

## 2. Install SecretLabNAudio (do this first)

CassieReplacement is basically a DJ. SecretLabNAudio is the sound system. Install the sound system first.

1. Download `SecretLabNAudio.zip` from the [releases page](https://github.com/Axwabo/SecretLabNAudio/releases).
2. Pick **one** setup style:

### Option A — Single file (easiest, recommended if you’re new)

1. Open the zip and find `bin/SecretLabNAudio.dll`.
2. Drop that DLL into LabAPI’s **global** plugins folder (shared by every port):

| OS | Folder |
|---|---|
| **Windows** | `%appdata%\SCP Secret Laboratory\LabAPI\plugins\global\` |
| **Linux** | `~/.config/SCP Secret Laboratory/LabAPI/plugins/global/` |

Tip: paste `%appdata%\SCP Secret Laboratory\LabAPI\` into Windows Explorer to jump there fast.

### Option B — Modular setup (more control)

Use this if you like splitting Core / modules:

- Put `SecretLabNAudio.Core.dll` (and its dependencies) in LabAPI **dependencies**
- Put the **NVorbis** module (plus `NVorbis` / `NAudio.Vorbis` if the release ships them separately) where that release’s readme says modules go

### Load order tip (important!)

If CassieReplacement loads **before** SecretLabNAudio, audio can break in weird ways.

**Fix:** rename the audio plugin so it sorts first alphabetically, e.g.

```text
0_SecretLabNAudio.dll
```

That way LabAPI loads sound first, then CassieReplacement.

---

## 3. Install CassieReplacement

1. Copy **`CassieReplacement.dll`** into your port’s plugin folder:

```text
LabAPI/plugins/<your-server-port>/
```

Example: if the server runs on port `7777`, that’s `plugins/7777/`.

2. Make sure **`0Harmony.dll`** exists in:

```text
LabAPI/dependencies/<your-server-port>/
```

If another plugin already installed Harmony there, you’re fine — skip copying a second one.

3. **Restart the server once.**  
   Why? LabAPI needs a clean start to load the plugin and generate a config file the first time.

After that you should see a config folder/file for CassieReplacement under LabAPI configs for that port.

---

## 4. Your audio files (this is the fun part)

CASSIE only understands clips that match a few boring-but-important rules. Get these wrong and you’ll hear bells… then nothing.

### File rules

| Rule | Must be | Why |
|---|---|---|
| **Format** | `.ogg` (Vorbis) | That’s what this plugin loads |
| **Channels** | **Mono** (1 channel) | Stereo can sound wrong or fail oddly in SL speakers |
| **Sample rate** | **48000 Hz** | Matches the game’s audio pipeline |

You can convert with Audacity, ffmpeg, etc. Export → Ogg Vorbis, mono, 48000 Hz.

### Naming = the word CASSIE will say

The **filename** (without `.ogg`) becomes the word you type in-game.

| File on disk | Word you type |
|---|---|
| `breach.ogg` | `breach` |
| `hasentered.ogg` | `hasentered` |
| `scp_049.ogg` | `scp_049` |

Need a space in the *name*? Use an underscore `_` in the filename. CASSIE words don’t love random spaces in file names.

### Where to put the files

Drop them here:

```text
LabAPI/configs/CASSIE Replacement/
```

That folder is **created automatically** when the plugin starts (if it’s missing).

**Two different folders (easy to mix up):**

| Folder | What lives there |
|---|---|
| `LabAPI/configs/CASSIE Replacement/` | Your **audio clips** (`.ogg`) |
| `LabAPI/configs/<port>/CASSIE Replacement/` | The **YAML config** for that server port |

Clips = shared voice pack. Config = per-port settings.

Subfolders are fine — the plugin scans recursively (e.g. `alphabet/`, `numbers/`, `words/`).

---

## 5. Config explained like you’re five

After the first restart, open the generated YAML and you’ll see options. Here’s what the important ones **actually do**.

### `custom_cassie_prefix`

**Default:** `customcassie`

This is the magic word.  
If an announcement **includes this token** (usually at the start), CassieReplacement takes over and plays **your** clips instead of vanilla TTS.

```text
cassie customcassie attention breach
         ^^^^^^^^^^^^
         “hey plugin, use my voice pack”
```

Without that prefix → normal game CASSIE. With it → your voice.

You can rename it in config if you want (`mycassie`, `sam`, whatever) — just remember to type the same word in commands.

---

### `base_directories`

A **list of folders** where the plugin looks for `.ogg` files.

Each entry has:

| Setting | What it does in real life |
|---|---|
| **`path`** | Folder to scan. Use `{labapi_configs}/CASSIE Replacement` for the default auto folder, or a full absolute path like `C:/Audio/MyCassie`. |
| **`prefix`** | Invisible name sticker stuck on **every** clip from that folder. Example: folder has `attention.ogg`, `prefix: j` → registers as **`jattention`**. Super useful for a second voice pack without renaming every file. |
| **`bleed_time`** | Seconds trimmed off the **end** of each clip when timing the next word. If words feel like they overlap / cut too late, try a tiny value like `0.05`. Start at `0` and only touch this if something feels off. |
| **`should_list`** | Whether those words show up in list-style helpers. Usually leave default unless you care. |

`{labapi_configs}` is a shortcut that expands to your LabAPI configs root. You don’t have to type the long Windows/Linux path every time.

---

### Speakers & volume

| Setting | Plain English |
|---|---|
| **`use_global_speaker`** | One speaker everyone can hear (simple, usually what you want). |
| **`use_spatial_speakers`** | Speakers tied to facility PA positions (more “in the world”, more setup). |
| **`global_for_surface_only`** | Tweaks spatial/global behaviour around Surface. Leave alone unless you know you need it. |
| **`global_speaker_volume`** | Loudness of the global speaker object. |
| **`global_speaker_volume_multiplier`** | Extra multiplier on top for global. |
| **`cassie_volume`** | How loud the **custom words** are. Turn up if voice is quieter than the PA noise; turn down if it clips / hurts. |
| **`spatial_speaker_*`** | Distance / volume knobs for spatial mode. Ignore if spatial is off. |

**Beginner tip:** start with `use_global_speaker: true` and `use_spatial_speakers: false`. Get voice working first. Fancy spatial later.

---

### `cassie_override_config`

This whole block is about **automatic** announcements (MTF spawn, Chaos, SCP death), not just RA `cassie` commands.

| Setting | What it does |
|---|---|
| **`should_override_announcements`** | `true` = replace the built-in MTF/Chaos/termination lines with **your** templates below. Only turn this on when you actually have the clips those templates need. |
| **`should_override_all`** | Nuclear option: **every** CASSIE line tries to go through custom audio. Great for “full custom facility”. Bad if you’re missing half the words — you’ll get silence gaps. Use carefully. |
| **`ntf_wave_announcement` / `chaos_*` / `scp_termination_*` etc.** | The scripts for those events. `words` = what gets spoken (clip names). `translation` = subtitle text players read. |

Put your `customcassie` (or custom prefix) at the start of `words` when you want that line played from your pack.

---

## 6. How to actually play custom CASSIE

1. Put mono 48 kHz `.ogg` files in `LabAPI/configs/CASSIE Replacement/`.
2. Restart (or reload) so the plugin registers the new files.
3. In Remote Admin / console:

```text
cassie customcassie attention containment breach
```

4. Want **subtitles**? Put a semicolon `;` after the words, then the text:

```text
cassie customcassie attention containment breach ; Attention. Containment breach.
```

You’ll hear the normal CASSIE **bells/noise**, then your custom voice — same vibe as vanilla, different voice.

### Extra tokens you can put in the message

| Token | What it does |
|---|---|
| `prefix_j` | Next words look for clips named like `j` + word (`attention` → `jattention`) |
| `prefix_` | Clears that name prefix |
| `suffix_k` / `suffix_` | Same idea, but glued to the **end** of clip names |
| `pitch_1.2` | Speeds up / pitches following custom words |
| `jam_50_3` | Jam glitch effect (delay % + how many times) |
| `yield_0.5` | Silence for 0.5 seconds |
| `nocassie` | Skip the vanilla padding / noise path |
| `noparse` | Leave this announcement to **vanilla** CASSIE (plugin backs off) |

### Two voices? Read this

`prefix_j` does **not** magically invent a second voice. You need either:

- files literally named `jattention.ogg`, `jbreach.ogg`, … **or**
- a second folder in `base_directories` with `prefix: 'j'` (files stay `attention.ogg` but register as `jattention`)

One voice pack = `prefix_j` and `prefix_` will sound the same if you only have one set of files. That’s expected.

---

## 7. Handy examples

### Basic line

Files: `attention.ogg`, `containment.ogg`, `breach.ogg`

```text
cassie customcassie attention containment breach
```

### With subtitles

```text
cassie customcassie attention containment breach ; Attention. Containment breach in progress.
```

### Pitch mid-sentence

```text
cassie customcassie pitch_0.85 scp 0 4 9 containedsuccessfully
```

### Pause between words

```text
cassie customcassie attention yield_1.0 breach
```

### Folder that auto-adds a name prefix

```yaml
base_directories:
  - path: '{labapi_configs}/Nato'
    prefix: 'nato_'
    bleed_time: 0
```

File `foxtrot.ogg` becomes word `nato_foxtrot`:

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

### Override MTF entrance with your pack

```yaml
cassie_override_config:
  should_override_announcements: true
  ntf_wave_announcement:
    words: 'customcassie mtfunit epsilon 11 designated {letter} {number} hasentered'
    translation: 'MTF Epsilon-11 {letter}-{number} has entered the facility.'
```

Only enable overrides when those clips exist.

---

## 8. Remote Admin commands

| Command | Aliases | What it’s for |
|---|---|---|
| `registercassie <path> [bleed] [prefix]` | `register`, `registercc` | Add a clip folder **while the server is running** (no full restart) |
| `unregistercassie` | `unregister`, `unregistercc` | Unregister clips |
| `clearcustomcassie` | `clearcc`, `clearcustom` | Stop custom playback / clear the custom queue |
| `customcassievolume <value>` | `cassievolume`, `ccvolume` | Change custom voice volume on the fly |
| `listwords` | — | Helper / duration utility |

Examples:

```text
registercassie C:/SCP/Audio/Cassie 0
clearcustomcassie
customcassievolume 1.2
```

---

## 9. Announcement overrides (MTF / Chaos / SCP)

These live under `cassie_override_config`. You can use placeholders that get filled in when the event happens:

| Keyword | Fills in with |
|---|---|
| `{letter}` / `{number}` | MTF unit, like FOXTROT-18 |
| `{scps}` | How many SCPs are left (threat overview) |
| `{threatoverview}` | The “no SCPs / one SCP / many SCPs” line |
| `{scp}` | Which SCP got contained |
| `{deathcause}` | How they died / got contained |
| `{team}` | Killer team callsign |
| `{scpkiller}` | If another SCP did it |

Remember: start `words` with `customcassie` (or your prefix) when that line should use **your** voice pack.

---

## 10. EXILED note

There may be an `EXILED-WIP` build floating around. This fork is maintained for **LabAPI + SecretLabNAudio**. For a real server, use the LabAPI DLL.

---

## 11. Credits

| | |
|---|---|
| **Original idea & logic** | **icedchqi** ([icedchai](https://github.com/icedchai)) |
| **This LabAPI / SecretLabNAudio port** | Community fork update |
| **Audio engine** | [SecretLabNAudio](https://github.com/Axwabo/SecretLabNAudio) — Axwabo |

Huge respect to icedchqi for the original CassieReplacement. This fork keeps that behaviour, just on the current LabAPI audio stack.

---

## 12. Copy-paste config (ready to use)

After the first server start, LabAPI generates a config. You can replace it with this starter (or merge piece by piece).

**What this starter does:**

- Uses the default audio folder
- Global speaker on (simple)
- Custom prefix `customcassie`
- Announcement overrides **off** until you have a full pack (safer for beginners)

```yaml
# ============================================================
# CassieReplacement — beginner-friendly starter config
# Put .ogg clips in: LabAPI/configs/CASSIE Replacement/
# ============================================================

# Show extra plugin debug noise in console (keep false unless you're troubleshooting)
is_enabled: true
debug: false

# --- Speakers ------------------------------------------------
# Global = everyone hears it (easiest). Spatial = PA speakers in rooms (advanced).
use_global_speaker: true
use_spatial_speakers: false

# Only matters if you use spatial speakers near Surface
global_for_surface_only: false

# How far spatial speakers reach (ignored if spatial is false)
spatial_speaker_max_distance: 40
spatial_speaker_min_distance: 20
spatial_speaker_volume: 1

# Loudness of the global speaker object
global_speaker_volume: 1.5
# Extra multiplier for global custom words
global_speaker_volume_multiplier: 1

# --- Magic word ---------------------------------------------
# Type this in RA before your clip names, e.g.:
#   cassie customcassie attention breach
custom_cassie_prefix: customcassie

# --- Where your .ogg files live -----------------------------
# {labapi_configs} = LabAPI configs root (auto-expanded)
# prefix: glued onto every clip name from that folder ("" = none)
# bleed_time: seconds trimmed from the end of each clip for timing (start at 0)
base_directories:
  - path: '{labapi_configs}/CASSIE Replacement'
    prefix: ''
    bleed_time: 0
    should_list: true

# Optional second voice pack example (uncomment if you have a folder of "j" voice files):
#  - path: '{labapi_configs}/CASSIE Replacement/voice_j'
#    prefix: 'j'
#    bleed_time: 0
#    should_list: true

# Map a custom clip name to a vanilla word if you ever need it (usually empty)
words_to_basegame_override: {}

# Loudness of the spoken custom words (raise if quieter than PA noise)
cassie_volume: 1

# --- Automatic announcement overrides -----------------------
cassie_override_config:
  # false = keep vanilla MTF/Chaos/SCP lines until you're ready
  # true  = use the templates below (you NEED matching clips!)
  should_override_announcements: false

  # true = EVERY cassie line tries custom audio (dangerous if clips are missing)
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

  # How each SCP is spoken / subtitled when contained
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

  # What gets appended based on how they died
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

  # Team flavor text when a player contains an SCP
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

### Tiny checklist if something’s silent

1. SecretLabNAudio installed **and** loads before CassieReplacement (`0_SecretLabNAudio.dll` rename trick).
2. Clips are **mono `.ogg` @ 48000 Hz**.
3. Files are in `LabAPI/configs/CASSIE Replacement/`.
4. You typed the prefix: `cassie customcassie yourword`.
5. Server was restarted after adding new `.ogg` files.

You’re good. Go make CASSIE say something cursed.
