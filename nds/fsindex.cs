using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;

namespace NDS{
    
    public class IndexEntry{
        public uint Hash;
        public uint RAMOffset;
        public byte UnkByte2;
        public uint Size;
        public uint Offset;
        public uint SizeOffset;
        public int Index;
        public Asset asset;
        public string Filename = "N/D";
        
        public IndexEntry(uint offset, uint hash, uint ramoffset, byte unkbyte2, uint size_offset, uint size, int index){
            Offset = offset;
            Hash = hash;
            RAMOffset = ramoffset;
            UnkByte2 = unkbyte2;
            Size = size;
            SizeOffset = size_offset;
            Index = index;
        }
        
        public string ToString(){
            uint index        = (uint)Index;
            uint offset       = (uint)Offset;
            uint hash         = (uint)Hash;
            uint ramoffset    = (uint)RAMOffset;
            uint unkByte2     = (uint)UnkByte2;
            uint size         = (uint)Size;
            uint sizeOffset   = (uint)SizeOffset;
            string name       = Filename.Replace("\\", "\\\\");
            return $"{{\"Index\": {index}, \"Offset\": {offset}, \"Hash\": {hash}, \"RAMOffset\": {ramoffset}, \"UnkByte2\": {unkByte2}, \"Size\": {size}, \"SizeOffset\": {sizeOffset}, \"Filename\": \"{name}\"}}";
        }
    }
    
    public class FSINDEXFile{
        public byte[] Bytes;
        public int FSUnk1;
        public int FSUnk2;
        public Asset asset;
        public List<IndexEntry> IndexEntries;
        
        public FSINDEXFile(byte[] NDSBytes, FNTFile fnt){
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                IndexEntries = new List<IndexEntry>();
                asset = fnt.Assets.Find(x => x.Name.StartsWith("fsindex") && x.Name.EndsWith(".bin"));
                if(asset == null){
                    throw new Exception("No fsindex file could be found!");
                }
                
                ms.Seek(asset.Offset, SeekOrigin.Begin);
                byte[] Compressed = br.ReadBytes((int)asset.Size);
                Bytes = Compressed;
                
                //Console.WriteLine("FSIndex offset: " + asset.Offset);
                //Console.WriteLine("FSIndex size: " + Bytes.Length);
                
                using(MemoryStream msfs = new MemoryStream(Bytes))
                using(BinaryReader brfs = new BinaryReader(msfs)){
                    uint file_count = brfs.ReadUInt32();
                    //Console.WriteLine("FSIndex file count: " + file_count);
                    FSUnk1 = brfs.ReadInt32();
                    FSUnk2 = brfs.ReadInt32();
                    
                    for(int i = 0; i < file_count; i++){
                        uint Offset = (uint)msfs.Position;
                        uint Hash = brfs.ReadUInt32();
                        uint RAMOffset = brfs.ReadUInt32();
                        byte UnkByte2 = brfs.ReadByte();
                        uint SizeOffset = (uint)msfs.Position;
                        uint Size = Helpers.BytesToUInt24(brfs.ReadBytes(3));
                        IndexEntry indexEntry = new IndexEntry(Offset, Hash, RAMOffset, UnkByte2, SizeOffset, Size, i);
                        IndexEntries.Add(indexEntry);
                    }
                    
                    //Console.WriteLine("First: " + IndexEntries[0].Offset);
                    //Console.WriteLine("First: " + IndexEntries[0].Size);
                    
                    List<Asset> Assets = fnt.Assets;
                }
            }
        }
        
        public void SaveAsFile(string file, string ID){
            string str = "[\n\t" + String.Join(",\n\t", IndexEntries.Select(x => x.ToString()).ToArray()) + "\n]";
            File.WriteAllText(file, str);
        }
    }
}