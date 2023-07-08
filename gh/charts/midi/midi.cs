using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using Melanchall.DryWetMidi;
using Melanchall.DryWetMidi.Core;

namespace CHARTS{

    public class TimedMessage{
        public double Ticks;
        public double Time;
        public double Velocity;
        public long Tempo;
        public byte Note;
        public bool HasNote = false;
        public bool IsNote = false;
        public double Length;
        public double Threshold;
        public string Text;
        public byte[] Data;
        public string TrackName;
        public int TrackIndex;
        public MidiModifier Modifier;
        public bool Valid;
        public MidiEventType Type;
        public MidiEvent Event;
        
        public TimedMessage(MidiEvent ev, int index, string name){
            Event = ev;
            TrackIndex = index;
            TrackName = name;
            Type = ev.EventType;
            
            switch(Type){
                case MidiEventType.SetTempo:
                    Tempo = (ev as SetTempoEvent).MicrosecondsPerQuarterNote;
                    break;
                case MidiEventType.NoteOn:
                case MidiEventType.NoteOff:
                    IsNote = true;
                    var note = ev as NoteEvent;
                    Note = note.NoteNumber;
                    HasNote = true;
                    Velocity = note.Velocity;
                    break;
                case MidiEventType.Text:
                case MidiEventType.Lyric:
                    var textEvent = ev as BaseTextEvent;
                    Text = textEvent.Text;
                    break;
                case MidiEventType.NormalSysEx:
                    var sysEx = ev as SysExEvent;
                    Data = sysEx.Data;
                    break;
                case MidiEventType.EndOfTrack:
                    break;
                default:
                    return;
                //case MidiEventType.TimeSignature:
                //case MidiEventType.SequenceTrackName:
            }
            
            Valid = true;
        }
    }
    
    public enum MidiNoteType{
        Note = 0x00,
        Modifier = 0xff
    }
    
    public class MidiModifier{
        public bool Valid = false;
        public string Difficulty;
        public string Type;
        public bool Value;
        
        public MidiModifier(byte[] bytes){
            using(MemoryStream ms = new MemoryStream(bytes))
            using(BinaryReader br = new BinaryReader(ms)){                
                ms.Seek(3, SeekOrigin.Begin);
                
                // Check modifier
                byte modifier = br.ReadByte();
                if(modifier != (byte)0x0) return;
                
                // Check difficulty
                byte difficulty = br.ReadByte();
                switch(difficulty){
                    case 0x0:
                        Difficulty = "Easy";
                        break;
                    case 0x1:
                        Difficulty = "Medium";
                        break;
                    case 0x2:
                        Difficulty = "Hard";
                        break;
                    case 0x3:
                        Difficulty = "Expert";
                        break;
                    case 0xff:
                        Difficulty = "Any";
                        break;
                    default:
                        Console.WriteLine("Unsupported midi note modifier difficulty: " + difficulty);
                        return;
                }
                
                // Check modifier type
                byte type = br.ReadByte();
                switch(type){
                    case 0x01:
                        Type = "Purple";
                        break;
                    case 0x04:
                        Type = "Tap";
                        break;
                    default:
                        Console.WriteLine("Unsupported midi note modifier type: " + type);
                        return;
                }

                // Check value
                byte val = br.ReadByte();
                switch(val){
                    case 0x00:
                        Value = false;
                        break;
                    case 0x01:
                        Value = true;
                        break;
                    default:
                        Console.WriteLine("Unsupported midi note modifier value: " + val);
                        return;
                }
                                
                Valid = true;
            }
        }
    }
    
    public class MidiNote{
        public double Time;
        public double Length;
        public byte Note;
        public MidiNoteType Type;
        public MidiModifier Modifier;
        
        public MidiNote(double time, double length, byte note = 0x00, MidiNoteType midiNoteType = MidiNoteType.Note, MidiModifier midiModifier = null){
            Time = time;
            Length = length;
            Note = note;
            Type = midiNoteType;
            Modifier = midiModifier;
        }
    }

    public class Track{
        public string Name;
        public int Index;
        public List<TimedMessage> Events;
        
        public Track(string name, IList<MidiEvent> events, int index){
            Name = name;
            Index = index;
            Events = new List<TimedMessage>(events.ToList().Select(e => new TimedMessage(e, index, name)));
        }
    }
    
    public class MidFile{
        public Track Sync;
        public Track Event;
        public Track Guitar;
        public Track Bass;
        public Track Drums;
        public Track Vocals;
        
        public List<Track> Tracks;
        
        public MidFile(string midiFilePath, Charts.Settings settings){
            Tracks = new List<Track>(){
                Sync, Event, Guitar, Bass, Drums, Vocals
            };
            var midiFile = MidiFile.Read(midiFilePath, new ReadingSettings{
                EndOfTrackStoringPolicy = EndOfTrackStoringPolicy.Store
            });
            List<Track> MidiTracks = new List<Track>();
            
            var Chunks = midiFile.GetTrackChunks().ToList();
            
            int index = -1;
            
            Chunks.ForEach(chunkTrack => {
                index++;
                string trackName = "";
                
                if(Chunks.IndexOf(chunkTrack) == 0){
                    trackName = "SYNC";
                }else{
                    var firstEvent = chunkTrack.Events.ToList()[0];
                    if(firstEvent.EventType == MidiEventType.SequenceTrackName){
                        var sequenceTrackNameEvent = firstEvent as SequenceTrackNameEvent;
                        trackName = sequenceTrackNameEvent.Text;
                    }
                }
                
                //Console.WriteLine("Found track: " + trackName);
                
                if(trackName != ""){
                    var track = new Track(trackName, chunkTrack.Events.ToList(), index);
                    MidiTracks.Add(track);
                    if(trackName == "SYNC") Sync = track;
                    if(trackName == "EVENTS") Event = track;
                    if(trackName == "PART GUITAR") Guitar = track;
                    if(trackName == "PART BASS") Bass = track;
                    if(trackName == "PART DRUMS") Drums = track;
                    if(trackName == "PART VOCALS") Vocals = track;
                }
            });
            
            settings.DeltaTicksPerQuarterNote = ((TicksPerQuarterNoteTimeDivision)midiFile.TimeDivision).TicksPerQuarterNote;
            settings.SetTicksPerBeat(((TicksPerQuarterNoteTimeDivision)midiFile.TimeDivision).TicksPerQuarterNote);
        }
    }
    
    public static class MidiHelpers{
        
        public static long GetEndOfTrackTime(Track track){
            long currentTime = 0;
            long endOfTrackTime = -1;
            
            if(track == null){
                Console.WriteLine("Track is null!");
                return endOfTrackTime;
            }
            
            if(track.Events == null){
                Console.WriteLine("No events found for this track!");
                return endOfTrackTime;
            }
            
            track.Events.ForEach(midiEvent => {
                currentTime += midiEvent.Event.DeltaTime;
                if(midiEvent.Type == MidiEventType.EndOfTrack){
                    endOfTrackTime = currentTime;
                }
            });
            
            return endOfTrackTime;
        }
        
        public static List<TimedMessage> MergeTracks(List<Track> tracks, Charts.Settings settings){
            var TrackChunks = new List<Track>();
            long maxEndTrackTime = -1;
            Track maxEndTrack = null;
            
            tracks.ForEach(old_track => {
                long endOfTrackTime = GetEndOfTrackTime(old_track);
                if(endOfTrackTime > maxEndTrackTime){
                    maxEndTrackTime = endOfTrackTime;
                    maxEndTrack = old_track;
                }
                
                TrackChunks.Add(old_track);
            });
            
            if(maxEndTrack != null){
                TrackChunks.ForEach(track => {
                    if(track != maxEndTrack){
                        var EndOfTrack = track.Events.ToList().Find(x => x.Type == MidiEventType.EndOfTrack);
                        track.Events.Remove(EndOfTrack);
                    }
                });
            }
            
            var NewTracks = new List<Track>();
            
            TrackChunks.ForEach(track => {
                long ticks = 0;
                var newTrack = new Track(track.Name, new List<MidiEvent>(), track.Index);
                newTrack.Events = track.Events;
                
                newTrack.Events.ToList().ForEach(message => {
                    ticks += message.Event.DeltaTime;
                    //Console.WriteLine($"Time: {message.Event.DeltaTime}, Ticks: {ticks}");
                    //Console.ReadLine();
                    message.Ticks = ticks;
                });
                NewTracks.Add(newTrack);
            });
            
            List<TimedMessage> mergedTracks = NewTracks.SelectMany(d => d.Events).OrderBy(s => s.Ticks).ThenBy(s => s.TrackIndex).ToList();
            
            double current_time  = 0;
            double current_ticks = 0;
            double delta_ticks   = 0;
            double tick_duration = 0;
            mergedTracks.ForEach(message => {
                delta_ticks = message.Ticks - current_ticks;
                current_ticks = message.Ticks;
                
                current_time += Math.Round((double)(delta_ticks * tick_duration));
                message.Time = (long)current_time;
                //Console.WriteLine($"Msg ticks: {message.Ticks}, Msg time: {message.Time}, delta: {delta_ticks}, current_time: {current_time}");
                
                if(message.Type == MidiEventType.SetTempo){
                    var setTempoEvent = message.Event as SetTempoEvent;
                    var tempo = setTempoEvent.MicrosecondsPerQuarterNote;
                    var bpm = 60 * 1e6 / tempo * 4 / 4;
                    var tpb = bpm * settings.TicksPerBeat;
                    tick_duration = 60000 / tpb;
                    //Console.WriteLine($"Tempo change => {tick_duration}");
                }
                //Console.ReadLine();
            });
            //Console.ReadLine();
            
            return mergedTracks;
        }
        
        public static List<double> GetFretTempos(List<TimedMessage> Track, Charts.Settings settings, bool LogMessage = false){
            List<double> tempos = new List<double>();
            
            double newTempo = 0;
            double currentDelta = 0;
            
            TimedMessage endOfTrack = Track.Find(x => x.Type == MidiEventType.EndOfTrack);
            if(settings.SongLength == -1 && endOfTrack != null){
                settings.SongLength = (int)endOfTrack.Time;
            }
            
            if(settings.SongLength == -1){
                Console.WriteLine("No song length was found, returning empty frets!");
                return tempos;
            }
            
            int count = 0;
            List<TimedMessage> Tempos = Track.Where(x => x.Type == MidiEventType.SetTempo).ToList();
            Tempos.ForEach(message => {
                TimedMessage nextTempo = null;
                double nextTime = 0;
                
                count++;
                if(count < Tempos.Count){
                    nextTempo = Tempos[count];
                    nextTime = nextTempo.Time;
                }else{
                    nextTime = settings.SongLength;
                }
                
                currentDelta = message.Tempo / 1000;
        
                while(newTempo < nextTime){
                    tempos.Add(newTempo);
                    newTempo += currentDelta;
                }
            });
            
            //Console.WriteLine("Managed to get tempos to generate frets: " + tempos.Count);
            
            return tempos;
        }
        
        public static List<TimedMessage> GetMidiNotes(List<TimedMessage> Track, Charts.Settings settings, bool LogMessage = false){
            Dictionary<string, double> currentNotes = new Dictionary<string, double>();
            Dictionary<string, double> currentModifiers = new Dictionary<string, double>();
            string note_name = "";
            double length = 0;
            double threshold = 0;
            
            //Console.WriteLine("Track length: " + Track.Count);
            List<TimedMessage> NewTrack = new List<TimedMessage>();
            Track.ForEach(message => {
                
                switch(message.Type){
                    case MidiEventType.SetTempo:
                        double tick = settings.Tick;
                        double scale = message.Tempo * 1e-6 / settings.TicksPerBeat;
                        double second = tick * scale;
                        threshold = second * 1000;
                        //Console.WriteLine($"Time: {message.Time}, Threshold: {threshold}, Tempo: {message.Tempo}");
                        break;
                    
                    case MidiEventType.NormalSysEx:
                        //Console.WriteLine("Modifier: " + BitConverter.ToString(message.Data).Replace("-", " "));
                        //Console.ReadLine();
                        var modifier = new MidiModifier(message.Data);
                        message.Modifier = modifier;
                        //string modifier_name = message.TrackName + "_" + modifier.Difficulty + "_" + modifier.Type;
                        //if(modifier.Value){
                        //    //Console.WriteLine("On");
                        //    if(!currentModifiers.ContainsKey(modifier_name)){
                        //        currentModifiers.Add(modifier_name, message.Time);
                        //    }else{
                        //        currentModifiers[modifier_name] = message.Time;
                        //    }
                        //}else{
                        //    //Console.WriteLine("Off");
                        //    length = message.Time - currentModifiers[modifier_name];
                        //    message.Length = length;
                        //    NewTrack.Add(message);
                        //}
                        NewTrack.Add(message);
                        break;
                    
                    case MidiEventType.NoteOn:
                        note_name = message.TrackName + "_" + message.Note.ToString("X2");
                        double time = message.Time;
                        message.Threshold = threshold;
                        if(message.Velocity > 0){
                            if(!currentNotes.ContainsKey(note_name)){
                                currentNotes.Add(note_name, time);
                            }else{
                                currentNotes[note_name] = time;
                            }
                            //Console.WriteLine("On: v1 => " + note_name + " at " + message.Time);
                            //Console.ReadLine();
                        }else{
                            length = time - currentNotes[note_name];
                            message.Time = currentNotes[note_name];
                            if(message.TrackName != "PART VOCALS") length = length >= threshold ? length : 0;
                            message.Length = length;
                            NewTrack.Add(message);
                            //Console.WriteLine("On: v0 => " + note_name + " at " + message.Time + " for " + message.Length);
                            //Console.ReadLine();
                        }
                        break;
                        
                    case MidiEventType.NoteOff:
                        note_name = message.TrackName + "_" + message.Note.ToString("X2");
                        length = message.Time - currentNotes[note_name];
                        if(message.TrackName != "PART VOCALS") length = length >= threshold ? length : 0;
                        message.Length = length;
                        message.Time = currentNotes[note_name];
                        message.Threshold = threshold;
                        NewTrack.Add(message);
                        //Console.WriteLine("Off: " + note_name + " at " + message.Time + " for " + message.Length);
                        //Console.ReadLine();
                        break;
                    default:
                        NewTrack.Add(message);
                        break;
                }
            });
            //Console.ReadLine();
            
            return NewTrack.OrderBy(s => s.Time).ThenBy(s => s.TrackIndex).ToList();
        }
        
        public static Charts.Vocals GetVocalNotes(List<TimedMessage> MidiNotes, string intrumentName, Charts.Settings settings){
            int Starpower = 116;
            int NewPhrase = 105;
            
            MidiNotes = MidiNotes.Where(x => x.TrackName == "PART VOCALS").ToList();
            
            List<double> TimeList = new List<double>();
            MidiNotes.ForEach(note => {
                if(!TimeList.Contains(note.Time)) TimeList.Add(note.Time);
            });
            
            var Vocals = new Charts.Vocals();
            
            bool lastTruncated = false;
            
            Charts.VocalNote previousVocalNote = null;
            
            Charts.Phrase currentPhrase = null;
            double lastPhrase = -1;
            double nextPhrase = -1;
            
            TimeList.ForEach(time => {
                List<TimedMessage> notes = MidiNotes.Where(note => note.Time == time).ToList();
                double nextTime = TimeList.Find(x => x > time);
                if(nextTime > 0) nextTime = settings.SongLength;
                
                
                string noteNumbers = String.Join(",", notes.Where(note => note.HasNote).ToList().Select(note => note.Note));
                //Console.WriteLine(noteNumbers);
                
                notes.ForEach(note => {
                    if(note.Note == NewPhrase){
                        TimedMessage nextPhraseNote = MidiNotes.Find(x => x.Time > time && x.Note == NewPhrase);
                        nextPhrase = nextPhraseNote != null ? nextPhraseNote.Time : settings.SongLength;
                        currentPhrase = new Charts.Phrase(time, nextPhrase);
                        
                        List<TimedMessage> PhraseNotes = MidiNotes.Where(x => x.Time >= currentPhrase.Start && x.Time < currentPhrase.End).ToList();
                        if(PhraseNotes.Where(x => x.HasNote && x.Note > 35 && x.Note < 85).ToList().Count == 0){
                            currentPhrase.IsPitchless = true;
                        }
                        
                        lastPhrase = currentPhrase.Start;
                        Vocals.Phrases.Add(currentPhrase);
                    }
                    if(note.Note == Starpower) currentPhrase.IsStarPower = true;
                });
                
                bool isNewPhrase = false;
                
                Charts.VocalNote currentNote = null;
                
                List<TimedMessage> ValidNotes = notes.Where(note => note.HasNote).ToList();
                ValidNotes.ForEach(note => {
                    if((note.Note > 35 && note.Note < 85) || (note.Note == 0 && (currentPhrase != null && currentPhrase.IsPitchless))){
                        if(lastPhrase > -1 && time >= lastPhrase){
                            isNewPhrase = true;
                            lastPhrase = -1;
                        }
                        //Console.WriteLine(currentPhrase);
                        if(note.Note == 0 && currentPhrase != null && !currentPhrase.IsPitchless){
                            TimedMessage nextNote = MidiNotes.Find(note => note.Time == nextTime && note.HasNote & note.Note > 35 && note.Note < 85);
                            if(nextNote != null){
                                short diff = (short)Math.Round((double)((nextNote.Note - previousVocalNote.Note) / 2));
                                int average = previousVocalNote.Note - diff;
                                note.Note = (byte)average;
                                note.Length = nextNote.Time;
                            }
                        }
                        if(note.Note < Vocals.Range.Min && note.Note > 0) Vocals.Range.Min = note.Note;
                        if(note.Note > Vocals.Range.Max && note.Note > 0) Vocals.Range.Max = note.Note;
                        var newVocalNote = new Charts.VocalNote(time, note.Note, note.Length);
                        if(currentPhrase != null && currentPhrase.IsStarPower) newVocalNote.Starpower = true;
                        Vocals.Notes.Add(newVocalNote);
                        currentNote = newVocalNote;
                    }
                });
                
                notes.ForEach(note => {
                    bool hasNote = currentNote != null;
                    if(hasNote && note.Text != null){
                        
                        var lyric = new Charts.Lyric(time, note.Text, lastTruncated);
                        if(lyric.Extended){
                            currentNote.Extended = true;
                        }
                        if(hasNote && (lyric.Pitchless || (currentPhrase != null && currentPhrase.IsPitchless))){
                            currentNote.Pitchless = true;
                            if(nextTime < nextPhrase) currentNote.Length = (ushort)Math.Round((double)(nextTime - currentNote.Time));
                        }
                        Vocals.Lyrics.Add(lyric);
                        lastTruncated = lyric.Truncated;
                        
                        string text = note.Text + new string(' ', 10 - note.Text.Length);
                        string lyricText = lyric.Text + new string(' ', 10 - lyric.Text.Length);
                        string isNewPhraseText = isNewPhrase ? "True " : "False";
                        string isPitchlessText = currentPhrase.IsPitchless ? "True " : "False";
                        string hasNoteText = hasNote ? "True " : "False";
                        string noteText = hasNote ? currentNote.Note.ToString().PadLeft(2, '0') : "-1";
                        string isStarPowerText = currentPhrase.IsStarPower ? "True " : "False";
                        
                        //Console.WriteLine("Time: " + time + " [isNewPhrase: " + isNewPhraseText + "][isPitchless: " + isPitchlessText + "][hasNote: " + hasNoteText + "][note: " + noteText + "][isStarPower: " + isStarPowerText + "]: " + text + " => " + lyricText);
                    }
                });
                
                previousVocalNote = currentNote;
            });
            
            //Console.ReadLine();
            
            return Vocals;
        }
        
        public static Charts.Instrument GetGHNotes(List<TimedMessage> MidiNotes, string instrumentName, Charts.Settings settings, bool isDrums = false){
            int Starpower = 116;
            
            List<byte> PURPLE = new List<byte>{0x3B, 0x47, 0x53, 0x5F};
            List<byte> GREEN  = new List<byte>{0x3C, 0x48, 0x54, 0x60};
            List<byte> RED    = new List<byte>{0x3D, 0x49, 0x55, 0x61};
            List<byte> YELLOW = new List<byte>{0x3E, 0x4A, 0x56, 0x62};
            List<byte> BLUE   = new List<byte>{0x3F, 0x4B, 0x57, 0x63};
            List<byte> ORANGE = new List<byte>{0x40, 0x4C, 0x58, 0x64};
            List<byte> HOPO   = new List<byte>{0x41, 0x4D, 0x59, 0x65};
            List<byte> STRUM  = new List<byte>{0x42, 0x4E, 0x6A, 0x66};
            
            if(isDrums){
                PURPLE = new List<byte>{0x3C, 0x48, 0x54, 0x60};
                GREEN  = new List<byte>{0x3D, 0x49, 0x55, 0x61};
                RED    = new List<byte>{0x3E, 0x4A, 0x56, 0x62};
                YELLOW = new List<byte>{0x3F, 0x4B, 0x57, 0x63};
                BLUE   = new List<byte>{0x40, 0x4C, 0x58, 0x64};
                ORANGE = new List<byte>{0x41, 0x4D, 0x59, 0x65};
                HOPO   = new List<byte>{0xff, 0xff, 0xff, 0xff};
                STRUM  = new List<byte>{0xff, 0xff, 0xff, 0xff};
            }
            
            Charts.Instrument instrument = new Charts.Instrument(instrumentName, isDrums);
            
            MidiNotes = MidiNotes.Where(x => x.TrackName == instrument.TrackName).ToList();
            
            int difficultyIndex = -1;
            instrument.Difficulties.ForEach(difficulty => {
                difficultyIndex++;
                
                List<Charts.GHNote> ghnotes = new List<Charts.GHNote>();
                
                List<double> TimeList = new List<double>();
                MidiNotes.ForEach(note => {
                    if(!TimeList.Contains(note.Time)) TimeList.Add(note.Time);
                });
                
                int DIFF_PURPLE = PURPLE[difficultyIndex];
                int DIFF_GREEN  =  GREEN[difficultyIndex];
                int DIFF_RED    =    RED[difficultyIndex];
                int DIFF_YELLOW = YELLOW[difficultyIndex];
                int DIFF_BLUE   =   BLUE[difficultyIndex];
                int DIFF_ORANGE = ORANGE[difficultyIndex];
                int DIFF_HOPO   =   HOPO[difficultyIndex];
                int DIFF_STRUM  =  STRUM[difficultyIndex];
                
                int t_count = -1;
                double StarpowerEnd = -1;
                Charts.GHNote lastNote = null;
                MidiModifier currentModifier = null;
                
                TimeList.ForEach(time => {
                    t_count++;
                    List<TimedMessage> notes = MidiNotes.Where(note => note.Time == time).ToList();
                    bool hasOpenFlag = false;
                    bool hasTapFlag = false;
                    
                    if(notes.Count > 0){
                        Charts.GHNote ghnote = new Charts.GHNote();
                        int count = 0;
                        
                        notes.ForEach(note => {
                            count++;
                            //Console.WriteLine(time + ": " + note.Type + ", Length: " + note.Length + ", Number: " + note.Note);
                            
                            // Make all 1/12 close notes hopos!
                            if(lastNote != null && note.HasNote){
                                if(note.Time - lastNote.Time <= note.Threshold){
                                    ghnote.Hopo = true;
                                }
                            }
                            
                            if(note.Modifier != null && note.Modifier.Valid){
                                if(note.Modifier.Difficulty == difficulty.Name || note.Modifier.Difficulty == "Any"){
                                    currentModifier = note.Modifier.Value ? note.Modifier : null;
									if(instrumentName == "Bass" && difficulty.Name == "Expert"){
										//Console.WriteLine($"[{time}] Modifier {note.Modifier.Type}: {note.Modifier.Value}");
									}
                                }
                            }
                            
                            if(currentModifier != null && currentModifier.Type == "Purple") hasOpenFlag = true;
                            if(currentModifier != null && currentModifier.Type == "Tap") hasTapFlag = true;
                            
                            if(DIFF_GREEN   == note.Note) ghnote.Gem.Green = true;
                            if(DIFF_RED     == note.Note) ghnote.Gem.Red = true;
                            if(DIFF_YELLOW  == note.Note) ghnote.Gem.Yellow = true;
                            if(DIFF_BLUE    == note.Note) ghnote.Gem.Blue = true;
                            if(DIFF_ORANGE  == note.Note) ghnote.Gem.Orange = true;
                            if(DIFF_PURPLE  == note.Note) ghnote.Gem.Purple = true;
                            if(DIFF_HOPO    == note.Note) ghnote.Hopo = true;
                            if(DIFF_STRUM   == note.Note) ghnote.IsStrum = true;
                            
                            if(Starpower    == note.Note){
                                ghnote.StarpowerStart = true;
                                StarpowerEnd = note.Time + note.Length;
                            }else{ 
                                ghnote.Time = note.Time;
                                ghnote.Length = note.Length;
                            }
                            
                            if(StarpowerEnd > -1 && note.Time < StarpowerEnd) ghnote.Starpower = true;
                            
                            if(StarpowerEnd > -1 && note.Time >= StarpowerEnd){
                                if(lastNote != null) lastNote.StarpowerEnd = true;
                                StarpowerEnd = -1;
                            }
                        });
                        
                        
                        if(ghnote.Gem.Count > 0){
                        
                            if(instrumentName == "Drums" && difficulty.Name == "Expert"){
                                //Console.WriteLine($"{time}: {string.Join(",", notes.Select(x => x.Note).ToArray())}");
                            }
                            
                            /* Some GHDS/BHDS fixes */
                            
                            // Can't be a hopo with forced strum
                            if(ghnote.IsStrum) ghnote.Hopo = false;
                            
                            // Drums don't have HOPOs
                            if(ghnote.Hopo && isDrums) ghnote.Hopo = false;
                            
                            // Some bass tracks have 102 as non-open note marker
                            // If the note is green (96 for Expert), then it's open
                            if(hasOpenFlag && !isDrums){// && ghnote.Gem.Green){
                                ghnote.ToggleAll();
                                //ghnote.Gem.Green = true;
                                ghnote.Gem.Purple = true;
                                ghnote.Length = 0;
                            }
                            
                            // Tannister's suggestion to make drums more fun!
                            if((hasOpenFlag || ghnote.Gem.Purple) && isDrums){
                                ghnote.Gem.Purple = false;
                                
                                if(ghnote.Gem.Count < 2){
                                    if(ghnote.Gem.Green){
                                        ghnote.Gem.Red = true;
                                    }else{
                                        ghnote.Gem.Green = true;
                                    }
                                }
                            }
                            
                            if(isDrums){
                                if(ghnote.Gem.Green && ghnote.Gem.Red){
                                    ghnote.Gem.Red = false;
                                    ghnote.Gem.Yellow = true;
                                }
                                if(ghnote.Gem.Yellow && ghnote.Gem.Blue){
                                    ghnote.Gem.Yellow = false;
                                    ghnote.Gem.Red = true;
                                }
                            }
                            
                            // GHDS does not have tapping, turn them into HOPOs
                            if(hasTapFlag && !isDrums){
                                ghnote.IsTap = true;
                                
                                // But we don't want two hopos of the same color in a row
                                // We also don't want the first tap to be a hopo
                                // Actually, as described by Tannister, the engine is robust
                                // and will allow to "tap" consecutive hopos
                                ghnote.Hopo = true;
                                //if(lastNote != null && lastNote.IsTap && !lastNote.Gem.Matches(ghnote.Gem)){
                                //    ghnote.Hopo = true;
                                //}
                            }
                            
                            // Turn orange into green due to 4 fret limitation
                            if(settings.ConvertOrangeToGreen && ghnote.Gem.Orange){
                                ghnote.Gem.Orange = false;
                                ghnote.Gem.Green = true;
                            }
                            
                            // Prevent normal notes from turning into short sustains
                            // Note: this is not required anymore, now that Tannister
                            //       has figured out threshold and sustain checking
                            //if(ghnote.Length <= settings.TicksPerBeat) ghnote.Length = 0;
                            
                            if(lastNote != null){
                                
                                // Prevent sustains overlapping next notes
                                if(lastNote.Length > 0){
                                    double lastNoteEnd = lastNote.Length + lastNote.Time;
                                    double sustainDiff = lastNoteEnd - ghnote.Time;
                                    if(sustainDiff > 0) lastNote.Length -= sustainDiff;
                                }
                                
                                // Prevent consecutive hopos of the same gems (due to missing orange)
                                if(ghnote.Hopo){
                                    if(ghnote.Gem.Matches(lastNote.Gem)){
                                        //ghnote.Hopo = false;
                                    }
                                }
                            }
                            
                            ghnotes.Add(ghnote);
                            lastNote = ghnote;
                            if(difficulty.Name == "Expert" && instrumentName == "Drums"){
                                //VisualiseNote(ghnote);
                            }
                        }
                    }
                });
                
                difficulty.Notes = ghnotes;
                
                lastNote = null;
                //ghnotes.ForEach(ghnote => {
                //    
                //    if(difficulty.Name == "Expert" && instrumentName == "Drums"){
                //        //VisualiseNote(ghnote);
                //    }
                //    lastNote = ghnote;
                //});
                if(difficulty.Name == "Expert" && instrumentName == "Drums"){
                    //Console.ReadLine();
                }
            });
            
            return instrument;
        }
        
        public static void VisualiseNote(Charts.GHNote ghnote){
            char[] gems = new char[]{' ', ' ', ' ', ' ', ' '};
            
            char noteLook = 'o';
            if(ghnote.Starpower) noteLook = '*';
            
            char localNoteLook = noteLook;
            if(ghnote.Hopo && noteLook == 'o') localNoteLook = '@';
            if(ghnote.Hopo && noteLook == '*') localNoteLook = '#';
            
            if(ghnote.Gem.Green) gems[0] = localNoteLook;
            if(ghnote.Gem.Red) gems[1] = localNoteLook;
            if(ghnote.Gem.Yellow) gems[2] = localNoteLook;
            if(ghnote.Gem.Blue) gems[3] = localNoteLook;
            if(ghnote.Gem.Orange) gems[4] = localNoteLook;
            string notes = new string(gems);
            if(ghnote.Gem.Purple){
                notes = ghnote.Hopo ? "----" : "____";
                if(noteLook == '*') notes = ghnote.Hopo ? "-**-" : "_**_";
            }
            
            string hopoPadding = ghnote.Hopo ? " " : "";
            string purplePadding = ghnote.Gem.Purple ? " " : "";
            Console.WriteLine($"Time: {ghnote.Time.ToString().PadLeft(8, '0')}, Length: {ghnote.Length.ToString().PadLeft(8, '0')}, Hopo: {ghnote.Hopo.ToString()}{hopoPadding}, Open: {ghnote.Gem.Purple.ToString()}{purplePadding}, Gems: {notes}");
        }
    }
}