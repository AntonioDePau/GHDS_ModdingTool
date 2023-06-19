using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using WAV;
using NAudio.Wave;
using NAudio;
using System.Collections;

namespace ImaAdpcm{
    // Credits to https://github.com/eurotools/es-ima-adpcm-encoder-decoder
    
    public static class ImaCodec{
        
        public static byte[] Encode(byte[] WavBytes, int frequency){
            WAVFile wav = new WAVFile();
            wav.OpenBytes(WavBytes, WAVFile.WAVFileMode.READ);
            if(frequency == -1) frequency = wav.SampleRateHz;
            //Console.WriteLine(wav.SampleRateHz);
            //Console.WriteLine(wav.BitsPerSample);
            //Console.WriteLine(wav.NumChannels);
            
            var outFormat = new WaveFormat(frequency, 16, 1);
            using (var msrsw = new MemoryStream(wav.ReadAllBytes()))
            using (var reader = new RawSourceWaveStream(msrsw, new WaveFormat(wav.SampleRateHz, wav.BitsPerSample, wav.NumChannels)))
            {
                using (var resampler = new MediaFoundationResampler(reader, outFormat))
                {
                    
                    using (MemoryStream wavData = new MemoryStream())
                    {
                        var ByteCount = 0;
                        var readBuffer = new byte[1024];
                        WaveFileWriter wfw = new WaveFileWriter(wavData, resampler.WaveFormat);
                        while((ByteCount = resampler.Read(readBuffer, 0, readBuffer.Length)) != 0){
                            wavData.Write(readBuffer, 0, ByteCount);
                        }
                        
                        using(BinaryWriter bw = new BinaryWriter(wavData)){
                            bw.Seek(4, SeekOrigin.Begin);
                            bw.Write((int)(wavData.Length - 8));
                            bw.Seek((int)0x2A, SeekOrigin.Begin);
                            bw.Write((int)(wavData.Length - 0x2E));
                            
                        }
                        wav = new WAVFile();
                        wav.OpenBytes(wavData.ToArray(), WAVFile.WAVFileMode.READ);
                    }
                }
            }
            
            //Check bits
            if(wav.BitsPerSample != 16){
                //Not 16 bits
                Console.WriteLine("ERROR: WAV should have 16 bits per sample. Press any key to exit...");
                Console.ReadLine();
                Environment.Exit(1);               
            }

            //Check bytes per sample
            if(wav.BytesPerSample / wav.NumChannels != 2){
                //Not 2 bytes
                Console.WriteLine("ERROR: WAV should have (wav.NumChannels * 2) bytes per sample. Press any key to exit...");
                Console.ReadLine();
                Environment.Exit(1);               
            }

            IMAADPCM.ADPCMState state = new IMAADPCM.ADPCMState();
            int nc = wav.NumChannels;

            MemoryStream ms = new MemoryStream();
            
            byte[] bytes = new byte[2];
            int loopValue = ((wav.DataSizeBytes - 8) / wav.BytesPerSample);
            //Console.WriteLine(loopValue);
            //Console.WriteLine((wav.DataSizeBytes - 8));
            //Console.WriteLine(wav.BytesPerSample);
            

            //Actual encode to 4-bit ADPCM
            for (long i = 0; i < loopValue / 2; i++) {
                bytes[0] = IMAADPCM.encodeADPCM(wav.GetNextSampleAs16Bit(), ref state);
                bytes[1] = IMAADPCM.encodeADPCM(wav.GetNextSampleAs16Bit(), ref state);
                //ms.Write(BitConverter.GetBytes(Convert.ToInt32(Convert.ToString((int)bytes[1], 2).PadLeft(4, '0') + Convert.ToString((int)bytes[0], 2).PadLeft(4, '0'), 2)), 0, 1);
                ms.Write(BitConverter.GetBytes(Convert.ToInt32(Convert.ToString((int)bytes[1], 2).PadLeft(4, '0') + Convert.ToString((int)bytes[0], 2).PadLeft(4, '0'), 2)), 0, 1);
            }
            
            return ms.ToArray();
        }
    }
}