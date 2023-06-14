using System;
using System.IO;
using System.Linq;

namespace NDS{
    
    public class ARMFile{
        public uint Offset;
        
        public uint Size;
        public uint RawSize;
        
        public byte[] Bytes;
        public byte[] RawBytes;
        
        private uint extra_data_separator = 0xDEC00621;
        private int extra_data_chunk = 0x0c;
        private int alignment = 0x200;
        
        public ARMFile(byte[] NDSBytes, bool isARM9 = true){
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x20 + (isARM9 ? 0x00 : 0x10), SeekOrigin.Begin);
                Offset = br.ReadUInt32();
                
                ms.Seek(0x2c + (isARM9 ? 0x00 : 0x10), SeekOrigin.Begin);
                Size = br.ReadUInt32();
                RawSize = (uint)(Size + (alignment - (Size % alignment)));
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawBytes = br.ReadBytes((int)(RawSize));
                
                ms.Seek(Offset, SeekOrigin.Begin);
                Bytes = br.ReadBytes((int)Size);
                
                uint extend = br.ReadUInt32();
                while(extend == extra_data_separator){
                    byte[] extended_bytes = br.ReadBytes(extra_data_chunk);
                    Bytes = Bytes.Concat(extended_bytes).ToArray();
                    extend = br.ReadUInt32();
                }
            }
        }
        
        public void WriteBytes(string file = "test_arm9.bin"){
            File.WriteAllBytes(file, ToBytes());
        }
        
        public byte[] ToBytes(bool padding = false){
            using(MemoryStream out_ms = new MemoryStream())
            using(MemoryStream in_ms = new MemoryStream(Bytes))
            using(BinaryReader br = new BinaryReader(in_ms))
            using(BinaryWriter bw = new BinaryWriter(out_ms)){
                bw.Write(br.ReadBytes((int)Size));
                int written = (int)Size;
                while(written < Bytes.Length){
                    bw.Write((int)extra_data_separator);
                    bw.Write(br.ReadBytes((int)extra_data_chunk));
                    written += extra_data_chunk;
                }
                if(padding) while(out_ms.Length % 0x200 > 0){
                    bw.Write((byte)0xff);
                }
                return out_ms.ToArray();
            }
        }
    }
}