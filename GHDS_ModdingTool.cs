using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Diagnostics;
using System.Collections.Generic;
using System.Runtime.InteropServices;
using NDS;

namespace GHDS_ModdingTool{
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
        
        public static string version = "";
        [STAThread]
        static void Main(string[] args){
            if(Environment.OSVersion.Version.Major >= 6) SetProcessDPIAware();
            FileVersionInfo fvi = FileVersionInfo.GetVersionInfo(ExecutingAssembly.Location);
            version = "v" + fvi.ProductVersion;
            
            ParseArgs(args);
            
            Console.WriteLine("You can now press any key or close this window!");
            Console.ReadLine();
        }
        
        static bool ProcessArg(string arg){
            bool noNDS = true;
            byte[] compressed;
            byte[] decompressed;
            switch(Path.GetExtension(arg).ToLower()){
                case ".nds":
                    NDSFile nds = new NDSFile(arg);
                    noNDS = false;
                    break;
                case ".decompressed":
                    decompressed = File.ReadAllBytes(arg);
                    compressed = LZ10.Compress(decompressed);
                    if(compressed.Length != decompressed.Length){
                        File.WriteAllBytes(arg + ".compressed", compressed);
                        Console.WriteLine($"Compressed: {arg}");
                    }
                    break;
                default:
                    compressed = File.ReadAllBytes(arg);
                    decompressed = LZ10.Decompress(compressed);
                    if(compressed.Length != decompressed.Length){
                        File.WriteAllBytes(arg + ".decompressed", decompressed);
                        Console.WriteLine($"Decompressed: {arg}");
                    }
                    break;
            }
            return !noNDS;
        }
        
        static void ParseArgs(string[] args){
            bool noNDS = true;
            
            for(int i = 0; i < args.Length; i++){
                string arg = args[i];
                if(Directory.Exists(arg)){
                    Directory.GetFiles(arg).ToList().ForEach(file => {
                       bool nds = ProcessArg(file);
                       if(nds) noNDS = false;
                    });
                }else{
                    bool nds = ProcessArg(arg);
                    if(nds) noNDS = false;
                }
            }
            if(noNDS) Console.WriteLine("Please drag and drop a valid .NDS file on this executable!");
        }
    }
}
