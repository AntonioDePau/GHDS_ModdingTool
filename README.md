# GHDS_ModdingTool

## Description
This tool can be used to edit songs' information and files in order to create your own setlist for the following games:
- [ ] Guitar Hero: On Tour (main blocker for this one is the GOB archive file)
- [x] Guitar Hero: On Tour - Decades
- [x] Guitar Hero: On Tour - Modern Hits
- [x] Band Hero

## Instructions
### Initial setup
Compile the executable yourself or download the [latest release](https://github.com/AntonioDePau/GHDS_MOddingTool/releases).

Simply drag and drop a Guitar Hero or Band Hero game onto the executable in order to generate the following folder structure:
```
custom_songs
└── <GAME ID> - <GAME NAME> [GAME REGION] (GAME LANGUAGES)
    ├── <song 1>
    │   └── metadata.txt
    ├── <song 2>
    │   └── metadata.txt
    └── ...
    
```

### Update song information
The metadata.txt files contain information such as the title of the song, the band's name, year it was released, etc.
Simply open it with any text editor, update the information, and save the file.

### Update song assets
The actual song files are the ones with the following formats:
- .hwas (song main track, and drums track in Band Hero)
- .ogg (guitar and rhythm (bass) tracks)
- .qgm (note charts)
- .qb (vocal chart and lyrics)
- .qft (frets file that contains beatlines, IMPORTANT to have proper song length)

Here is the list of possible files to replace:
- _song.hwas
- _rhythm.ogg
- _guitar.ogg
- _drums.hwas
- _gems_easy.qgm
- _gems_med.qgm
- _gems_hard.qgm
- _gems_expert.qgm
- _gems_bass_easy.qgm
- _gems_bass_med.qgm
- _gems_bass_hard.qgm
- _gems_bass_expert.qgm
- _gems_drum_easy.qgm
- _gems_drum_med.qgm
- _gems_drum_hard.qgm
- _gems_drum_expert.qgm
- _vocal_lyrics.qb
- _vocal_note_range.qb
- _vocal_notes.qb
- _vocal_phrases.qb
- _frets.qft

To replace a file, simply place the new file in the song's folder.

### Apply the changes
Once the song information and files have edited/updated, simply drag and drop a Guitar Hero or Band Hero game onto the executable again.
A new ROM will be created with all the changes you've made.

## Features
- [x] Mod GHOTD, GHOTMH, and BH songs
- [ ] Make modding easier by making it possible to compress relevant files automatically **[PLANNED]**
- [ ] Make modding easier by supporting other sound formats (.wav) **[PLANNED]**
- [ ] Make modding easier by supporting other chart formats (.chart) **[PLANNED]**

## Support
If you need support for this tool, report an [issue](https://github.com/AntonioDePau/GHDS_MOddingTool/issues/new)
or contact me on Discord (antoniodepau), you can find me in the [GHDS Central](https://discord.gg/j83bQUh6) server as well.

## GHDS modding scene credits
- bromik
- evanmurray
- Tannister

## Extra credits
- SciresM
- The [NAudio team](https://github.com/naudio/NAudio)
- Flitskikker for the [IMAADPCM encoding](https://github.com/Flitskikker/IMAADPCMEncoder)
- Pigu-A for the [WAV implementation](https://github.com/Pigu-A/SidWiz/)