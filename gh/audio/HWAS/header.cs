using System;
using System.IO;
using System.Text;

namespace HWAS{
    public class HwasFile{
        string signature = "sawh";
        int mem_alloc = 32768;
        int frequency = 19996;
        int channels = 1;
        int loop_start = 0;
        int data_length = 0;
        int loop_end = 0;
        int header_length = 0x200;
        
        byte[] ima_data;
        
        public HwasFile(byte[] data, int freq = 0){
            ima_data = data;
            data_length = data.Length;
            loop_end = data.Length;
            if(freq > 0) frequency = freq;
        }
        
        public byte[] GetAllBytes(){
            using(MemoryStream ms = new MemoryStream()){
                using(BinaryWriter bw = new BinaryWriter(ms)){
                    bw.Write(Encoding.ASCII.GetBytes(signature));
                    bw.Write(mem_alloc);
                    bw.Write(frequency);
                    bw.Write(channels);
                    bw.Write(loop_start);
                    bw.Write(data_length);
                    bw.Write(loop_end);
                    
                    while(ms.Length % header_length != 0){
                        bw.Write(0x00);
                    }
                    
                    bw.Write(ima_data);
                }
                return ms.ToArray();
            }
        }
    }
}