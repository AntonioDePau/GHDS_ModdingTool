using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public class Chunk{
    public uint CompressedSize;
    public bool IsCompressed;
    public uint Offset;
    public byte[] Bytes;
    
    public Chunk(byte[] bytes, bool compressed = true){
        Bytes = bytes;
        IsCompressed = compressed;
    }
}

public static class LZ10{
    
    public static byte[] Decompress(byte[] bytes, string name = null){
        if(bytes.Length < 0xc) return bytes;
        
        using(MemoryStream wms = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(wms))
        using(MemoryStream ms = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(ms)){
            List<Chunk> chunks = new List<Chunk>();
            
            byte magic = 0x4c;
            
            while(magic == 0x4c || magic == 0x30){
                magic = br.ReadByte();
            
                if(magic == 0x45){
                    // No more chunks
                    break;
                }
                
                if(magic != 0x4c && magic != 0x30){
                    // Uh... This was not supposed to happen!
                    // Unless the file is just not compressed after all!
                    return bytes;
                }
                
                uint chunkOffset = Helpers.BytesToUInt24(br.ReadBytes(3));
                long nextChunkPosition = ms.Position;
                br.ReadByte();
                
                if(bytes.Length < chunkOffset) return bytes;
                
                uint chunkEndOffset = Helpers.BytesToUInt24(br.ReadBytes(3));
                uint chunkSize = chunkEndOffset - chunkOffset;

                if(bytes.Length < chunkEndOffset) return bytes;
                
                ms.Seek(chunkOffset, SeekOrigin.Begin);
                byte[] compressedBytes = br.ReadBytes((int)chunkSize);
                
                chunks.Add(new Chunk(compressedBytes, magic != 0x30));                
                ms.Seek(nextChunkPosition, SeekOrigin.Begin);
            }
            
            List<byte[]> uncompressedBytes = new List<byte[]>();
            chunks.ForEach(chunk => {
               byte[] uncompressedChunk = chunk.Bytes;
               //if(!chunk.IsCompressed) Console.WriteLine("Uncompressed chunk!!!");
               if(chunk.IsCompressed) uncompressedChunk = DecompressChunk(chunk.Bytes);
               uncompressedBytes.Add(uncompressedChunk);
            });
            
            byte[] FullUncompressedBytes = uncompressedBytes.SelectMany(uncompressedChunk => uncompressedChunk).ToArray();
            
            return FullUncompressedBytes;
        }
    }
    
    
    /* CREDITS TO: https://github.com/SciresM/FEAT/blob/master/FEAT/DSDecmp/Formats/Nitro/LZ10.cs */
    private static byte[] DecompressChunk(byte[] bytes){
        using(MemoryStream wms = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(wms))
        using(MemoryStream ms = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(ms)){
            if(br.ReadByte() != 0x10) return bytes;
            
            uint DecompressedSize = Helpers.BytesToUInt24(br.ReadBytes(3));
            
            if(DecompressedSize == 0) DecompressedSize = br.ReadUInt32();
            
            // the maximum 'DISP-1' is 0xFFF.
            int bufferLength = 0x1000;
            byte[] buffer = new byte[bufferLength];
            int bufferOffset = 0;

            int currentOutSize = 0;
            int flags = 0, mask = 1;
            while(currentOutSize < DecompressedSize){
                // (throws when requested new flags byte is not available)
                // the current mask is the mask used in the previous run. So if it masks the
                // last flag bit, get a new flags byte.
                if(mask == 1){

                    flags = br.ReadByte();
                    if (flags < 0)
                        throw new Exception("StreamTooShortException");
                    mask = 0x80;
                }
                else{
                    mask >>= 1;
                }

                // bit = 1 <=> compressed.
                if((flags & mask) > 0){

                    int byte1 = br.ReadByte();// readBytes++;
                    int byte2 = br.ReadByte();// readBytes++;

                    // the number of bytes to copy
                    int length = byte1 >> 4;
                    length += 3;

                    // from where the bytes should be copied (relatively)
                    int disp = ((byte1 & 0x0F) << 8) | byte2;
                    disp += 1;

                    if(disp > currentOutSize)
                        throw new InvalidDataException("Cannot go back more than already written. "
                                + "DISP = 0x" + disp.ToString("X") + ", #written bytes = 0x" + currentOutSize.ToString("X")
                                + " at 0x" + (ms.Position - 2 - 5 - 3).ToString("X"));

                    int bufIdx = bufferOffset + bufferLength - disp;
                    for(int i = 0; i < length; i++){
                        byte next = buffer[bufIdx % bufferLength];
                        bufIdx++;
                        bw.Write(next);
                        buffer[bufferOffset] = next;
                        bufferOffset = (bufferOffset + 1) % bufferLength;
                    }
                    currentOutSize += length;
                }else{

                    int next = br.ReadByte();
                        
                    currentOutSize++;
                    bw.Write((byte)next);
                    buffer[bufferOffset] = (byte)next;
                    bufferOffset = (bufferOffset + 1) % bufferLength;
                }
            }
            
            byte[] decompressed = wms.ToArray();
                    
            try{
                byte[] furtherDecompressed = DecompressChunk(decompressed);
                decompressed = furtherDecompressed;
            }catch(Exception e){
                // Looks like it was not double compressed...
            }
            
            return decompressed;
        }
    }
    
    private static Tuple<int, int> GetOccurrenceLength(byte newPtr, int newLength, byte oldPtr, int oldLength, int minDisp = 1){
        int disp = 0;
        if (newLength == 0)
            return new Tuple<int, int>(0, disp);
        int maxLength = 0;
        // try every possible 'disp' value (disp = oldLength - i)
        for (byte i = 0; i < oldLength - minDisp; i++)
        {
            // work from the start of the old data to the end, to mimic the original implementation's behaviour
            // (and going from start to end or from end to start does not influence the compression ratio anyway)
            byte currentOldStart = (byte)(oldPtr + i);
            int currentLength = 0;
            // determine the length we can copy if we go back (oldLength - i) bytes
            // always check the next 'newLength' bytes, and not just the available 'old' bytes,
            // as the copied data can also originate from what we're currently trying to compress.
            for (int j = 0; j < newLength; j++)
            {
                // stop when the bytes are no longer the same
                if ((currentOldStart + j) != (newPtr + j))
                    break;
                currentLength++;
            }

            // update the optimal value
            if (currentLength > maxLength)
            {
                maxLength = currentLength;
                disp = oldLength - i;

                // if we cannot do better anyway, stop trying.
                if (maxLength == newLength)
                    break;
            }
        }
        return new Tuple<int, int>(maxLength, disp);
    }
        
    /* TO BE TESTED FOR GH/BH PURPOSE */
    public static byte[] Compress(byte[] bytes){
        // make sure the decompressed size fits in 3 bytes.
        // There should be room for four bytes, however I'm not 100% sure if that can be used
        // in every game, as it may not be a built-in function.
        int inLength = bytes.Length;
        if (inLength > 0xFFFFFF)
            throw new Exception("InputTooLarge");

        // use the other method if lookahead is enabled
        /*if (lookAhead)
        {
            return CompressWithLA(instream, inLength, outstream);
        }*/
        
        using(MemoryStream msw = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(msw))
        using(MemoryStream msr = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(msr)){            
            // save the input data in an array to prevent having to go back and forth in a file
            if (msr.Length != inLength)
                throw new Exception("StreamTooShort");

            // write the compression header first
            bw.Write(0x10);
            bw.Write((byte)(inLength & 0xFF));
            bw.Write((byte)((inLength >> 8) & 0xFF));
            bw.Write((byte)((inLength >> 16) & 0xFF));

            int compressedLength = 4;
            
            byte instart = 0x00;
            while (msr.Position < msr.Length)
            {
                instart = br.ReadByte();
                // we do need to buffer the output, as the first byte indicates which blocks are compressed.
                // this version does not use a look-ahead, so we do not need to buffer more than 8 blocks at a time.
                byte[] outbuffer = new byte[8 * 2 + 1];
                outbuffer[0] = 0;
                int bufferlength = 1, bufferedBlocks = 0;
                int readBytes = 0;
                while (readBytes < inLength)
                {
                    if (bufferedBlocks == 8)
                    {
                        bw.Write(outbuffer, 0, bufferlength);
                        compressedLength += bufferlength;
                        // reset the buffer
                        outbuffer[0] = 0;
                        bufferlength = 1;
                        bufferedBlocks = 0;
                    }

                    // determine if we're dealing with a compressed or raw block.
                    // it is a compressed block when the next 3 or more bytes can be copied from
                    // somewhere in the set of already compressed bytes.
                    int oldLength = Math.Min(readBytes, 0x1000);
                    var result = GetOccurrenceLength((byte)(instart + readBytes), (int)Math.Min(inLength - readBytes, 0x12),
                                                          (byte)(instart + readBytes - oldLength), oldLength);
                    int length = result.Item1;
                    int disp = result.Item2;
                    // length not 3 or more? next byte is raw data
                    if (length < 3)
                    {
                        outbuffer[bufferlength++] = (byte)(instart + (readBytes++));
                    }
                    else
                    {
                        // 3 or more bytes can be copied? next (length) bytes will be compressed into 2 bytes
                        readBytes += length;

                        // mark the next block as compressed
                        outbuffer[0] |= (byte)(1 << (7 - bufferedBlocks));

                        outbuffer[bufferlength] = (byte)(((length - 3) << 4) & 0xF0);
                        outbuffer[bufferlength] |= (byte)(((disp - 1) >> 8) & 0x0F);
                        bufferlength++;
                        outbuffer[bufferlength] = (byte)((disp - 1) & 0xFF);
                        bufferlength++;
                    }
                    bufferedBlocks++;
                }

                // copy the remaining blocks to the output
                if (bufferedBlocks > 0)
                {
                    bw.Write(outbuffer, 0, bufferlength);
                    compressedLength += bufferlength;
                    /*/ make the compressed file 4-byte aligned.
                    while ((compressedLength % 4) != 0)
                    {
                        outstream.WriteByte(0);
                        compressedLength++;
                    }/**/
                }
            }

            return msw.ToArray();
        }
    }
}