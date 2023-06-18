using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NDS;

namespace GHDS_ModdingTool{
    public class ModdingSettings{
        public bool KeepAllFilesUncompressed = false;
        
        public bool KeepHWASUserSampleRate = false;
        public int HWASSampleRate = -1;
        
        public bool KeepOGGUserSampleRate = false;
        public int OGGSampleRate = -1;
        
        
        public List<string> NDSFiles = new List<string>();
        
        public ModdingSettings(){}
        
        private int TryParseInt32(string val, int min = 10000, int max = 48000){
            try{
                int number = Int32.Parse(val);
                if(number > max || number < min) number = -1;
                return number;
            }catch(Exception e){
                return -1;
            }
        }
        
        public void Parse(string key, string val){
            byte[] compressed;
            byte[] decompressed;
            int number;
            
            switch(key){
                case "--KeepAllFilesUncompressed":
                case "-KeepAllFilesUncompressed":
                    KeepAllFilesUncompressed = true;
                    break;
                case "--KeepHWASSampleRate":
                case "-KeepHWASSampleRate":
                    KeepHWASUserSampleRate = true;
                    break;
                case "--KeepOGGSampleRate":
                case "-KeepOGGSampleRate":
                    KeepOGGUserSampleRate = true;
                    break;
                case "--HWASSampleRate":
                case "-HWASSampleRate":
                    number = TryParseInt32(val);
                    if(number == -1) Console.WriteLine($"Invalid number provided for {key}: {val}\nExpected whole number between 0 and 48000.");
                    HWASSampleRate = number;
                    break;
                case "--OGGSampleRate":
                case "-OGGSampleRate":
                    number = TryParseInt32(val);
                    if(number == -1) Console.WriteLine($"Invalid number provided for {key}: {val}\nExpected whole number between 0 and 48000.");
                    OGGSampleRate = number;
                    break;
            }
            
            if(File.Exists(key)){
                switch(Path.GetExtension(key).ToLower()){
                    case ".nds":
                        NDSFiles.Add(key);
                        break;
                    case ".decompressed":
                        decompressed = File.ReadAllBytes(key);
                        compressed = LZ10.Compress(decompressed);
                        if(compressed.Length != decompressed.Length){
                            File.WriteAllBytes(key + ".compressed", compressed);
                            Console.WriteLine($"Compressed: {key}");
                        }
                        break;
                    default:
                        compressed = File.ReadAllBytes(key);
                        decompressed = LZ10.Decompress(compressed);
                        if(compressed.Length != decompressed.Length){
                            File.WriteAllBytes(key + ".decompressed", decompressed);
                            Console.WriteLine($"Decompressed: {key}");
                        }
                        break;
                }
            }
        }
    }
    
    public class GHDS_ModdingTool{
        static Assembly ExecutingAssembly = Assembly.GetExecutingAssembly();
        static string[] EmbeddedLibraries = ExecutingAssembly.GetManifestResourceNames().Where(x => x.EndsWith(".dll")).ToArray();

        static Assembly CurrentDomain_AssemblyResolve(object sender, ResolveEventArgs args){
            
            var assemblyName = new AssemblyName(args.Name).Name + ".dll";

            var resourceName = EmbeddedLibraries.FirstOrDefault(x => x.EndsWith(assemblyName));
            if(resourceName == null){
                return null;
            }
            using (var stream = ExecutingAssembly.GetManifestResourceStream(resourceName)){
                var bytes = new byte[stream.Length];
                stream.Read(bytes, 0, bytes.Length);
                return Assembly.Load(bytes);
            }
        }
        
        static GHDS_ModdingTool(){
            AppDomain.CurrentDomain.AssemblyResolve += CurrentDomain_AssemblyResolve;
        }
        
        [System.Runtime.InteropServices.DllImport("user32.dll")]
        private static extern bool SetProcessDPIAware();
        
        public static ModdingSettings moddingSettings = new ModdingSettings();
        
        public static string version = "";
        [STAThread]
        static void Main(string[] args){
            if(Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutingAssembly.Location);
            version = "v" + fvi.ProductVersion;
            
            ParseArgs(args);
            if(moddingSettings.NDSFiles.Count == 0){
                Console.WriteLine("Please drag and drop a valid .NDS file on this executable!");
            }else{
                moddingSettings.NDSFiles.ForEach(NDSF => {
                    NDSFile nds = new NDSFile(NDSF, moddingSettings);
                });
            }
            
            Console.WriteLine("You can now press any key or close this window!");
            Console.ReadLine();
        }
        
        static void ProcessArg(string arg){            
            string argKey = arg;
            string[] splitted = arg.Split('=');
            string argVal = "";
            if(splitted.Length == 2){
                argKey = splitted[0];
                argVal = splitted[1];
            }
            moddingSettings.Parse(argKey, argVal);
        }
        
        static void ParseArgs(string[] args){          
            for(int i = 0; i < args.Length; i++){
                string arg = args[i];
                if(Directory.Exists(arg)){
                    Directory.GetFiles(arg).ToList().ForEach(file => {
                       ProcessArg(file);
                    });
                }else{
                    ProcessArg(arg);
                }
            }
        }
    }
}
