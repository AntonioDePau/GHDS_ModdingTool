using System;
using System.IO;
using System.Collections.Generic;

namespace NDS{

    public class FATFile{
        public uint Size;
        public uint RawSize;
        public uint Offset;
        public byte[] Bytes;
        public byte[] RawBytes;
        private uint alignment = 0x200;
        
        public FATFile(byte[] NDSBytes, FNTFile fnt){
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x48, SeekOrigin.Begin);
                Offset = br.ReadUInt32();
                
                ms.Seek(0x4c, SeekOrigin.Begin);
                Size = br.ReadUInt32();
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawSize = Size + (alignment - (Size % alignment));
                RawBytes = br.ReadBytes((int)RawSize);
                
                ms.Seek(Offset, SeekOrigin.Begin);
                Bytes = br.ReadBytes((int)Size);
                
                List<Asset> files = fnt.Assets.FindAll(x => !x.IsFolder);
                int fileCount = files.Count;
                
                using(MemoryStream msfat = new MemoryStream(Bytes))
                using(BinaryReader brfat = new BinaryReader(msfat)){
                    int count = (int)msfat.Length / 0x08;
                    for(uint i = 0; i < count; i++){
                        Asset file = files.Find(x => x.ID == i);
                        
                        uint offset_start = brfat.ReadUInt32();
                        uint offset_end = brfat.ReadUInt32();
                        uint size = offset_end - offset_start;

                        if(file != null){
                            file.Offset = offset_start;
                            file.Size = (uint)size;
                            file.FATIndex = i;
                            
                            ms.Seek((int)file.Offset, SeekOrigin.Begin);
                            file.SetBytes(br.ReadBytes((int)file.Size));
                        }
                    }
                }
                
                files.Sort((x,y) => x.Offset < y.Offset ? -1 : 1);
                
                Asset lastAsset = files[files.Count - 1];
                int c = 0;
                files.ForEach(asset => {
                    uint start = asset.Offset;
                    uint end = start + asset.Size;
                    if(asset != lastAsset){
                        Asset nextAsset = files[c + 1];
                        end = nextAsset.Offset;
                    }
                    
                    ms.Seek(start, SeekOrigin.Begin);
                    asset.RawBytes = br.ReadBytes((int)(end - start)); 
                    asset.RawSize = (uint)asset.RawBytes.Length;
                    
                    c++;
                });
            }
        }
               
        public void WriteBytes(string file = "test_FAT.bin"){
            File.WriteAllBytes(file, Bytes);
        }
    }
}