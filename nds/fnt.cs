using System;
using System.IO;
using System.Collections.Generic;
using System.Text;
using System.Linq;

namespace NDS{
    
    public class FNTFile{
        public uint RawSize;
        public uint Size;
        public uint Offset;
        public byte[] Bytes;
        public byte[] RawBytes;
        public uint FolderCount;
        public List<Asset> Assets;
        private uint alignment = 0x200;
        
        public FNTFile(byte[] NDSBytes){
            Assets = new List<Asset>();
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x40, SeekOrigin.Begin);
                Offset = br.ReadUInt32();
                
                ms.Seek(0x44, SeekOrigin.Begin);
                Size = br.ReadUInt32();
                
                ms.Seek(Offset, SeekOrigin.Begin);
                Bytes = br.ReadBytes((int)Size);
                
                RawSize = Size + (alignment - (Size % alignment));
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawBytes = br.ReadBytes((int)RawSize);
                
                using(MemoryStream msfnt = new MemoryStream(Bytes))
                using(BinaryReader brfnt = new BinaryReader(msfnt)){
                    int table_offset = (int)msfnt.Position;
                    uint base_offset = brfnt.ReadUInt32();
                    
                    msfnt.Seek(0x06, SeekOrigin.Begin);                    
                    FolderCount = brfnt.ReadUInt16();
                    
                    msfnt.Seek(base_offset, SeekOrigin.Begin);
                    
                    for(int i = 0; i < (int)FolderCount; i++){
                        msfnt.Seek(table_offset + i * 8, SeekOrigin.Begin);
                        uint offset = brfnt.ReadUInt32();
                        uint firstID = brfnt.ReadUInt16();
                        uint folderID = brfnt.ReadUInt16();
                        
                        msfnt.Seek(offset, SeekOrigin.Begin);
                        
                        byte flag = brfnt.ReadByte();
                        while(flag != 0x00){
                            uint file_offset = (uint)msfnt.Position - 1;
                            bool isFolder = (flag & 0x80) != 0x00;
                            int length = flag & 0x7f;
                            string name = System.Text.Encoding.ASCII.GetString(brfnt.ReadBytes(length));
                            string type = isFolder ? "folder" : "file";
                            Asset asset = new Asset(name, isFolder, file_offset, firstID, (uint)(0xf000 + i));
                            Assets.Add(asset);
                            if(isFolder){
                                asset.ID = brfnt.ReadUInt16();
                            }else{
                                firstID++;
                            }
                            flag = brfnt.ReadByte();
                        }
                    }
                    Assets.FindAll(x => x.IsFolder).ForEach(x => GetChildren(Assets, x));
                }
            }
        }
        
        private List<Asset> GetChildren(List<Asset> assets, Asset asset){
            List<Asset> children = assets.FindAll(x => x.ParentFolderID == asset.ID);
            children.ForEach(x => x.Path += asset.Path + asset.Name + "\\");
            asset.Children = children;
            return children;
        }
        
        public void WriteNames(string filename = "FNT_file_names.json"){
            StringBuilder sb = new StringBuilder();
            string str = String.Join(",\n", Assets.FindAll(asset => asset.ParentFolderID == 0xf000).Select(asset => 
                asset.ToString(0)
            ).ToArray());
            File.WriteAllText(filename, str);
        }
        
        public void WriteBytes(string file = "test_FNT.bin"){
            File.WriteAllBytes(file, Bytes);
        }
        
        public byte[] ToBytes(){
            return new byte[0];
        }
    }
}