using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;

namespace NDS{
    public class Partition{
        public uint Offset;
        
        public uint Size;
        public uint RawSize;
        
        public byte[] RawBytes;
        
        private uint alignment = 0x200;
        
        public Partition(uint offset, uint size, byte[] bytes){
            Offset = offset;
            Size = size;
            
            using(MemoryStream ms = new MemoryStream())
            using(BinaryWriter bw = new BinaryWriter(ms)){
                bw.Write(bytes);
                
                //Console.WriteLine(bytes.Length + " = " + size);
                
                uint diff = (uint)(ms.Length % alignment);
                uint padding = 0x00;
                if(diff > 0) padding = alignment - diff;
                while(padding > 0){
                    bw.Write((byte)0xff);
                    padding--;
                }
                RawBytes = ms.ToArray();
                RawSize = (uint)RawBytes.Length;
            }
            //Console.WriteLine("Raw Size: " + RawSize);
        }
    }
    
    public class OverlayFile{
        public uint Offset;
        
        public uint Size;
        public uint RawSize;
        
        public byte[] Bytes;
        public byte[] RawBytes;
        
        public uint TotalRawSize;
        public byte[] TotalRawBytes;
        
        public List<Partition> Partitions;
        
        private int alignment = 0x200;
        
        public OverlayFile(byte[] NDSBytes){
            Partitions = new List<Partition>();
            TotalRawSize = 0;
            TotalRawBytes = new byte[0];
            
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x50, SeekOrigin.Begin);
                Offset = br.ReadUInt32();
                
                ms.Seek(0x54, SeekOrigin.Begin);
                Size = br.ReadUInt32();
                RawSize = (uint)(Size + (alignment - (Size % alignment)));
                
                if(Offset <= 0 && Size <= 0) return;
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawBytes = br.ReadBytes((int)(RawSize));
                
                ms.Seek(Offset, SeekOrigin.Begin);
                Bytes = br.ReadBytes((int)Size);
                
                uint partitionOffset = (uint)(Offset + RawBytes.Length);
                
                //Console.WriteLine("Found " + (Bytes.Length / 0x20) + " overlay partitions!");
                
                for(int i=0; i<Bytes.Length / 0x20; i++){
                    ms.Seek(Offset + (0x20 * i) + 0x08, SeekOrigin.Begin);
                    
                    uint PartitionSize = br.ReadUInt32();
                    
                    //Console.WriteLine("Partiton " + i + " size: " + PartitionSize);
                    
                    ms.Seek(partitionOffset, SeekOrigin.Begin);
                    Partition partition = new Partition(partitionOffset, PartitionSize, br.ReadBytes((int)PartitionSize));
                    Partitions.Add(partition);
                    
                    partitionOffset = partition.Offset + partition.RawSize;
                }
            }
            
            List<byte> totalRawBytes = new List<byte>();
            totalRawBytes = totalRawBytes.Concat(RawBytes).ToList();
            
            Partitions.ForEach(partition => {
               totalRawBytes = totalRawBytes.Concat(partition.RawBytes).ToList();
            });
            
            TotalRawBytes = totalRawBytes.ToArray();
            TotalRawSize = (uint)TotalRawBytes.Length;
            
            //Console.WriteLine("Total overlay size: " + TotalRawSize);
            //Console.ReadLine();
        }
    }
}