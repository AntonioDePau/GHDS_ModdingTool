using System;
using System.IO;
using System.Text;
using System.Diagnostics;
using System.Threading.Tasks;

namespace OGG{
    public static class Ogg{
        public static string ToolsPath;
        
        private static byte[] StartProcess(string FileName, string Arguments){//, byte[] InputBytes, bool returnText = false){
                var process = new Process{
                StartInfo = new ProcessStartInfo{
                    RedirectStandardInput = true,
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                    Arguments = Arguments,
                    FileName = FileName
                },
                EnableRaisingEvents = true
            };
            
            process.ErrorDataReceived += (sender, eventArgs) => {
                //Console.WriteLine(eventArgs.Data);
            };
            
            process.Start();
            process.BeginErrorReadLine();
            
            byte[] outputBytes = new byte[0];
            var outputTask = Task.Run(() => {
                using(var output = new MemoryStream())
                using(var br = new BinaryReader(process.StandardOutput.BaseStream))
                using(var bw = new BinaryWriter(output)){
                    byte[] buffer = new byte[4096];
                    int read;
                    while((read = br.Read(buffer, 0, buffer.Length)) > 0){
                        bw.Write(buffer);
                    }
                    outputBytes = output.ToArray();
                }
            });
            
            Task.WaitAll(outputTask);
            
            process.WaitForExit();
            
            return outputBytes;
        }
        
        private static byte[] CleanWav(byte[] wav){
            using(MemoryStream rms = new MemoryStream(wav))
            using(BinaryReader br = new BinaryReader(rms))
            using(MemoryStream wms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(wms)){
                int magic = br.ReadInt32();
                bw.Write(magic);
                
                int length = br.ReadInt32();
                bw.Write(length);
                
                byte[] bytes = br.ReadBytes(length);
                bw.Write(bytes);

                return wms.ToArray();
            }
        }
        
        public static byte[] Decode(string oggFile){
            byte[] bytes = File.ReadAllBytes(oggFile);
            return Decode(bytes);
        }
        
        public static byte[] Decode(byte[] oggFile){
            string tempFile = Path.Combine(ToolsPath, "oggtool_audio.temp");
            File.WriteAllBytes(tempFile, oggFile);
            
            string Arguments = $" --quiet --output - \"{tempFile}\"";
            byte[] output = StartProcess(Path.Combine(ToolsPath, "oggdec.exe"), Arguments);
            
            return output;
        }
        
        public static byte[] Encode(byte[] wavFile, int SampleRate, int BitRate = 18){
            string tempFile = Path.Combine(ToolsPath, "oggtool_audio.temp");
            File.WriteAllBytes(tempFile, wavFile);
            
            string Arguments = $" --quiet --bitrate {BitRate} --resample {SampleRate} --downmix --min-bitrate {BitRate} --max-bitrate {BitRate} -o - \"{tempFile}\"";

            byte[] output = StartProcess(Path.Combine(ToolsPath, "oggenc.exe"), Arguments);
            
            return output;
        }
        
        public static Format GetFormat(byte[] oggFile){
            string Arguments = $" -";
            return new Format(oggFile);
        }
        
        private static byte[] DummyBytes = GHDS_ModdingTool.GHDS_ModdingTool.GetEmbeddedResource("dummy.ogg");
        public static byte[] GetDummyBytes(){
            return DummyBytes;
        }
    }
    
    public class Format{
        public int SampleRate;
        public int NominalBitRate;
        public int MinBitRate;
        public int MaxBitRate;
        public int ChannelCount;
        public string Filename;
        public bool IsValid = false;
        
        public Format(byte[] output){
            using(MemoryStream ms = new MemoryStream(output))
            using(BinaryReader br = new BinaryReader(ms)){
                if(ms.Length < 34) return;
                
                string magic = Encoding.ASCII.GetString(br.ReadBytes(4));
                if(magic != "OggS") return;
                
                ms.Seek(0x27, SeekOrigin.Begin);
                ChannelCount = (int)br.ReadByte();
                
                SampleRate = br.ReadInt32();
                MinBitRate = br.ReadInt32();
                NominalBitRate = br.ReadInt32();
                MaxBitRate = br.ReadInt32();
            }
            IsValid = true;
        }
        
        public Format(int sampleRate, int bitRate){
            SampleRate = sampleRate;
            NominalBitRate = bitRate;
            ChannelCount = 1;
            IsValid = true;
        }
        
        public bool Matches(Format oggFormat){
            if(!oggFormat.IsValid) return false;
            if(oggFormat.SampleRate != SampleRate) return false;
            if(oggFormat.NominalBitRate != NominalBitRate) return false;
            if(oggFormat.ChannelCount != ChannelCount) return false;
            
            return true;
        }
        
        public Format Clone(){
            return new Format(SampleRate, NominalBitRate);
        }
        
        public void Print(){
            Console.WriteLine($"OGG File: {Filename}");
            Console.WriteLine($"Sample rate: {SampleRate}");
            Console.WriteLine($"Nominal Bit rate: {NominalBitRate}");
            Console.WriteLine($"Minimum Bit rate: {MinBitRate}");
            Console.WriteLine($"Maximum Bit rate: {MaxBitRate}");
            Console.WriteLine($"Channels: {ChannelCount}");
        }
    }
}