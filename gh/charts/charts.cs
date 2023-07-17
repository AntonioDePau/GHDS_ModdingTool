using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Melanchall.DryWetMidi;
using Melanchall.DryWetMidi.Core;

namespace CHARTS{    
    public static class Charts{
        
        public class Settings{
            public int SongLength = -1;
            public long DeltaTicksPerQuarterNote;
            public bool HasEndEvent = false;
            public int TicksPerBeat;
            public double Tick;
            public bool ConvertOrangeToGreen;
            
            public void SetTicksPerBeat(int ticksPerBeat){
                TicksPerBeat = ticksPerBeat;
                Tick = (ticksPerBeat / 3) + 1;
            }
            
            public Settings(string file){
                List<string> lines = File.ReadAllLines(file).ToList();
                lines.ForEach(line => {
                    string[] parts = line.Split('=');
                    if(parts.Length < 2) return;
                    string key = parts[0].Trim();
                    string val = parts[1].Trim();
                    if(key == "song_length") SongLength = Int32.Parse(val);
                });
            }
        }
        
        public class Gems{
            private bool purple = false;
            private bool orange = false;
            private bool blue = false;
            private bool yellow = false;
            private bool red = false;
            private bool green = false;
            
            public bool Purple {
                get => purple ;
                set {
                    Count += value == true ? 1 : -1;
                    purple = value;
                }
            }
            public bool Orange {
                get => orange ;
                set {
                    Count += value == true ? 1 : -1;
                    orange = value;
                }
            }
            public bool Blue {
                get => blue ;
                set {
                    Count += value == true ? 1 : -1;
                    blue = value;
                }
            }
            public bool Yellow {
                get => yellow ;
                set {
                    Count += value == true ? 1 : -1;
                    yellow = value;
                }
            }
            public bool Red {
                get => red ;
                set {
                    Count += value == true ? 1 : -1;
                    red = value;
                }
            }
            public bool Green {
                get => green ;
                set {
                    Count += value == true ? 1 : -1;
                    green = value;
                }
            }
            
            public int Count = 0;
            
            public bool Matches(Gems gem){
                return Purple == gem.Purple &&
                       Orange == gem.Orange &&
                       Blue == gem.Blue &&
                       Yellow == gem.Yellow &&
                       Red == gem.Red &&
                       Green == gem.Green;
            }
            
            public void ToggleAll(bool Value){
                Green = Value;
                Red = Value;
                Yellow = Value;
                Blue = Value;
                Orange = Value;
                Purple = Value;
            }
            
            public Gems(){}
        }

        public class GHNote{
            public Gems Gem = new Gems();
            public double Time = 0;
            public double Threshold = 0;
            public double Length = 0;
            public bool Hopo = false;
            public bool IsStrum = false;
            public bool IsTap = false;
            public bool Starpower = false;
            public bool StarpowerStart = false;
            public bool StarpowerEnd = false;
            
            public void ToggleAll(bool Value = false){
                Gem.ToggleAll(Value);
            }
            public GHNote(){}
        }
        
        public class Difficulty{
            public string Name;
            public string Id;
            public List<GHNote> Notes;
            
            public Difficulty(string name){
                Name = name;
                switch(name){
                    case "Easy":
                        Id = "easy";
                        break;
                    case "Medium":
                        Id = "med";
                        break;
                    case "Hard":
                        Id = "hard";
                        break;
                    default:
                        Id = "expert";
                        break;
                }
            }
        }
        
        public class Phrase{
            public int Start;
            public int End;
            public bool IsStarPower = false;
            public bool IsLast = false;
            public bool IsPitchless = false;
            
            public Phrase(double start, double end){
                Start = (int)Math.Round((double)start);
                End = (int)Math.Round((double)end);
            }
        }
        
        public class Range{
            public int Min = 999;
            public int Max = -999;
            
            public Range(){}
        }
        
        public class VocalNote{
            public int Time;
            public byte Note;
            public ushort Length;
            public bool Extended = false;
            public bool Pitchless = false;
            public bool Starpower = false;
            
            public VocalNote(double time, byte note, double length){
                Time = (int)Math.Round((double)time);
                Note = note;
                Length = (ushort)Math.Round((double)length);
            }
        }
        
        public class Lyric{
            public int Time;
            public string Text;
            public bool Extended = false;
            public bool Truncated = false;
            public bool Pitchless = false;
            
            public Lyric(double time, string text, bool lastTruncated = false){
                Time = (int)Math.Round((double)time);
                
                Text = text.Replace("=", "");
                
                if(lastTruncated) Text = "=" + Text;
                
                if(Text.Contains("#")){
                    Text = Text.Replace("#", "");
                    Pitchless = true;
                }
                
                if(Text.Contains("+")){
                    Text = Text.Replace("+", "");
                    Extended = true;
                }
                
                if(Text.EndsWith("-")){
                    Text = Text.Replace("-", "");
                    Truncated = true;
                }
            }
        }
        
        public class Vocals{
            public List<Charts.Lyric> Lyrics = new List<Lyric>();
            public List<Charts.VocalNote> Notes = new List<VocalNote>();
            public List<Phrase> Phrases = new List<Phrase>();
            public Range Range = new Range();
            
            public Vocals(){}
            
            public void WriteToFile(string folder){
                /* LYRICS */
                string lyrics_file = Path.Combine(folder, "Vocals_vocal_lyrics.qb");
                using(var ms = new MemoryStream())
                using(var bw = new BinaryWriter(ms)){
                    Lyrics.ForEach(lyric => {
                        if(lyric.Text != ""){
                            bw.Write(lyric.Time);
                            byte[] textBytes = System.Text.Encoding.UTF8.GetBytes(lyric.Text);
                            bw.Write(textBytes, 0, Math.Min(11, textBytes.Length));
                            while(ms.Length % 0x10 != 0){
                                bw.Write((byte)0x0);
                            }
                        }
                    });
                    File.WriteAllBytes(lyrics_file, ms.ToArray());
                }
                
                /* NOTES */
                string notes_file = Path.Combine(folder, "Vocals_vocal_notes.qb");
                using(var ms = new MemoryStream())
                using(var bw = new BinaryWriter(ms)){
                    Notes.ForEach(note => {
                        byte flags = (byte)0;
                        if(note.Starpower) flags ^= 1;
                        if(note.Pitchless){
                            flags ^= 2;
                            note.Note = (byte)Math.Round((double)((Range.Min + Range.Max) / 2));
                        }
                        if(note.Extended) flags ^= 8;
                        bw.Write(note.Time);
                        bw.Write((ushort)note.Length);
                        bw.Write((byte)note.Note);
                        bw.Write((byte)flags);
                    });
                    File.WriteAllBytes(notes_file, ms.ToArray());
                }
        
        
                /* PHRASES */
                string phrases_file = Path.Combine(folder, "Vocals_vocal_phrases.qb");
                using(var ms = new MemoryStream())
                using(var bw = new BinaryWriter(ms)){
                    Phrases.ForEach(phrase => {
                        bw.Write(phrase.Start);
                    });
                    File.WriteAllBytes(phrases_file, ms.ToArray());
                }
                
                /* RANGE */
                string range_file = Path.Combine(folder, "Vocals_vocal_note_range.qb");
                File.WriteAllBytes(range_file, new byte[2]{(byte)Range.Min, (byte)Range.Max});
            }
        }
        
        public class Instrument{
            public string Name;
            public string Id;
            public string TrackName;
            public List<Difficulty> Difficulties;
            public bool IsDrums;
            
            public Instrument(string name, bool isDrums = false){
                Name = name;
                switch(name){
                    case "Bass":
                        TrackName = "PART BASS";
                        Id = "bass_";
                        break;
                    case "Drums":
                        TrackName = "PART DRUMS";
                        Id = "drum_";
                        break;
                    default:
                        TrackName = "PART GUITAR";
                        Id = "";
                        break;
                }
                IsDrums = isDrums;
                Difficulties = new List<Difficulty>{
                    new Difficulty("Easy"),
                    new Difficulty("Medium"),
                    new Difficulty("Hard"),
                    new Difficulty("Expert"),
                };
            }
            
            public void WriteToFile(string folder){
                Difficulties.ForEach(difficulty => {
                    using(MemoryStream ms = new MemoryStream())
                    using(BinaryWriter bw = new BinaryWriter(ms)){
                        string filename = Path.Combine(folder, Name + "_gems_" + Id + difficulty.Id + ".qgm");
                        
                        difficulty.Notes.ForEach(note => {
                            byte gems = 0;
                            if(note.Gem.Green ) gems ^= 0x01;
                            if(note.Gem.Red   ) gems ^= 0x02;
                            if(note.Gem.Yellow) gems ^= 0x04;
                            if(note.Gem.Blue  ) gems ^= 0x08;
                            if(note.Gem.Purple) gems ^= 0x10;
                            
                            byte flag = 0;
                            if(note.Starpower   ) flag ^= 0x01;
                            if(note.StarpowerEnd) flag ^= 0x02;
                            if(note.Hopo        ) flag ^= 0x40;
                            
                            bw.Write((int)note.Time);
                            bw.Write((short)note.Length);
                            bw.Write(flag);
                            bw.Write(gems);
                        });
                        
                        File.WriteAllBytes(filename, ms.ToArray());
                    }
                });
            }
        }
        
        public static void WriteFrets(string folder, List<double> frets){
            using(MemoryStream ms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(ms)){
                string filename = Path.Combine(folder, "_frets.qft");
                
                frets.ForEach(fret => {
                   bw.Write((uint)Math.Round((double)fret)); 
                });
                
                File.WriteAllBytes(filename, ms.ToArray());
            }
        }
        
        public static void ParseMidi(string file){
            string midiFilePath = Path.Combine(file, "notes.mid");
            if(!File.Exists(midiFilePath)){
                Helpers.UpdateLine("Could not find: " + Path.Combine(Path.GetFileName(midiFilePath), "notes.mid"));
                Console.WriteLine(" ");
                return;
            }
            
            string songIniFilePath = Path.Combine(file, "song.ini");
            if(!File.Exists(songIniFilePath)){
                Helpers.UpdateLine("Could not find: " + Path.Combine(Path.GetFileName(songIniFilePath), "song.ini"));
                Console.WriteLine(" ");
                return;
            }
            
            
            var settings = new Settings(songIniFilePath);
            settings.ConvertOrangeToGreen = true;
            
            var MidFile = new MidFile(midiFilePath, settings);

            var Track = MidiHelpers.MergeTracks(
                new List<Track>{
                    MidFile.Sync,
                    MidFile.Event,
                    MidFile.Guitar,
                    MidFile.Bass,
                    MidFile.Drums,
                    MidFile.Vocals
                }, settings);
                
            List<double> frets = MidiHelpers.GetFretTempos(Track, settings);
            
            if(settings.SongLength == -1){
                Console.WriteLine("Could not find a song length for this song!");
                return;
            }

            var AllNotes = MidiHelpers.GetMidiNotes(Track, settings);
            
            WriteFrets(file, frets);

            Instrument ghGuitar = MidiHelpers.GetGHNotes(AllNotes, "Guitar", settings);
            ghGuitar.WriteToFile(file);
            
            Instrument ghBass = MidiHelpers.GetGHNotes(AllNotes, "Bass", settings);
            ghBass.WriteToFile(file);
            
            Instrument ghDrum = MidiHelpers.GetGHNotes(AllNotes, "Drums", settings, true);
            ghDrum.WriteToFile(file);
            
            Vocals vocals = MidiHelpers.GetVocalNotes(AllNotes, "Vocals", settings);
            vocals.WriteToFile(file);
        }
    }
}