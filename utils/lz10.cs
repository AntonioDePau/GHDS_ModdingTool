using System;
using System.IO;
using System.Linq;
using System.Text;
using System.Collections.Generic;

public class Chunk{
    public uint CompressedSize;
    public uint UncompressedSize;
    public bool IsCompressed;
    public uint Offset;
    public byte[] Bytes;
    
    public Chunk(byte[] bytes, bool compressed = true){
        Bytes = bytes;
        IsCompressed = compressed;
    }
}

public class SearchResults{
    public List<int> recordMatchPos = new List<int>{0};
    public List<int> recordMatchLen = new List<int>{0};
    
    public SearchResults(){}
}

public static class LZ10{
    
    public static byte[] Decompress(byte[] bytes, string name = null){
        if(bytes.Length < 0xc) return bytes;
        
        using(MemoryStream wms = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(wms))
        using(MemoryStream ms = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(ms)){
            List<Chunk> chunks = new List<Chunk>();
            
            while(true){
                byte magic = br.ReadByte();
            
                if(magic == 0x45){
                    // No more chunks
                    if(chunks.Count == 0) return bytes;
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
            
            if(DecompressedSize != decompressed.Length) return bytes;
                    
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
    
    public static byte[] Compress(byte[] bytes){
        byte[] decompressedTest = Decompress(bytes);
        if(decompressedTest.Length != bytes.Length){
            // File is already compressed!
            return bytes;
        }
        
        using(MemoryStream wms = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(wms))
        using(MemoryStream ms = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(ms)){
            //byte magic = br.ReadByte();
            
            byte startFlag = 0x4c;
            byte uncompressedFlag = 0x30;
            byte lastFlag = 0x45;
            
            /* Before a file is compressed, it needs to be split in chunks of 0x40 00 */
            /* Each 0x40 00 chunk is then compressed individually */
            List<Chunk> chunks = new List<Chunk>();
            
            //Console.WriteLine("About to read: " + bytes.Length.ToString("X8"));
            //Console.ReadLine();
            
            while(ms.Position < ms.Length){
                int nextRead = Math.Min(0x4000, (int)(ms.Length - ms.Position));
                //Console.WriteLine("Reading: " + nextRead.ToString("X8"));
                //Console.ReadLine();
                byte[] block = br.ReadBytes(nextRead);
                byte[] compressedChunk = CompressBlock(block);
                
                Chunk chunk = new Chunk(compressedChunk);
                chunk.UncompressedSize = (uint)block.Length;
                
                chunks.Add(chunk);
            }
            
            bw.Write(Enumerable.Repeat((byte)0xff, (chunks.Count + 1) * 0x04).ToArray());
            int headOffset = 0x0;
            long offset = wms.Length;
            
            chunks.ForEach(chunk => {
                wms.Seek(headOffset, SeekOrigin.Begin);
                
                byte flag = startFlag;
                if(chunk.Bytes.Length == chunk.UncompressedSize) flag = uncompressedFlag;
                
                //update header
                bw.Write((byte)flag);
                bw.Write(Helpers.UInt24ToBytes((uint)offset));
                headOffset += 4;
                
                //write bytes
                wms.Seek(offset, SeekOrigin.Begin);
                //bw.Write((byte)0x10);
                //bw.Write(Helpers.UInt24ToBytes((uint)chunk.UncompressedSize));
                bw.Write((byte)0x10);
                bw.Write(Helpers.UInt24ToBytes((uint)chunk.UncompressedSize));
                
                bw.Write(chunk.Bytes);
                offset = wms.Length;
            });
            
            
            wms.Seek(headOffset, SeekOrigin.Begin);
            bw.Write((byte)lastFlag);
            bw.Write(Helpers.UInt24ToBytes((uint)offset));
            
            return wms.ToArray();
        }
    }
    /*        
        int newLength = (int)Math.Min(inLength - readBytes, 0x12),
        byte* newPtr = instart + readBytes,
        int oldLength = Math.Min(readBytes, 0x1000),
        byte* oldPtr = instart + readBytes - oldLength,
        out int disp,
        int minDisp = 1
    */
    
    private static SearchResults GetOptimalCompressionLengths(byte[] bytes){
        SearchResults searchResults = new SearchResults();
        
        int[] lengths = new int[bytes.Length];
        int[] disps = new int[bytes.Length];
        int[] minLengths = new int[bytes.Length];

        for (int i = bytes.Length - 1; i >= 0; i--)
        {
            // first get the compression length when the next byte is not compressed
            minLengths[i] = int.MaxValue;
            lengths[i] = 1;
            if (i + 1 >= bytes.Length)
                minLengths[i] = 1;
            else
                minLengths[i] = 1 + minLengths[i + 1];
            // then the optimal compressed length
            int oldLength = Math.Min(0x1000, i);
            // get the appropriate disp while at it. Takes at most O(n) time if oldLength is considered O(n)
            // be sure to bound the input length with 0x12, as that's the maximum length for LZ-10 compressed blocks.
            SearchResults tSearchResults = compressionSearch(bytes, oldLength);
            int maxLen = tSearchResults.recordMatchLen[0];
            disps[i] = tSearchResults.recordMatchPos[0];

            if (disps[i] > i)
                throw new Exception("disp is too large");
            for (int j = 3; j <= maxLen; j++)
            {
                int newCompLen;
                if (i + j >= bytes.Length)
                    newCompLen = 2;
                else
                    newCompLen = 2 + minLengths[i + j];
                if (newCompLen < minLengths[i])
                {
                    lengths[i] = j;
                    minLengths[i] = newCompLen;
                }
            }
        }
        
        searchResults.recordMatchLen = lengths.ToList();
        searchResults.recordMatchPos = disps.ToList();
        
        return searchResults;
    }
    
    private static SearchResults compressionSearch(byte[] bytes, long current){
        using(MemoryStream newms = new MemoryStream(bytes))
        using(BinaryReader newbr = new BinaryReader(newms))
        using(MemoryStream oldms = new MemoryStream(bytes))
        using(BinaryReader oldbr = new BinaryReader(oldms)){
            
            SearchResults searchResults = new SearchResults();
            int newLength = (int)Math.Min(bytes.Length - current, 0x12);
            int newPtrOffset = (int)current;
            int oldLength = (int)Math.Min(current, 0x1000);
            int oldPtrOffset = (int)current - oldLength;
            int minDisp = 1;
            
            int disp = 0;
            if (newLength == 0) return searchResults;
            int maxLength = 0;
            // try every possible 'disp' value (disp = oldLength - i)
            for (int i = 0; i < oldLength - minDisp; i++)
            {
                // work from the start of the old data to the end, to mimic the original implementation's behaviour
                // (and going from start to end or from end to start does not influence the compression ratio anyway)
                int currentLength = 0;
                // determine the length we can copy if we go back (oldLength - i) bytes
                // always check the next 'newLength' bytes, and not just the available 'old' bytes,
                // as the copied data can also originate from what we're currently trying to compress.
                for (int j = 0; j < newLength; j++)
                {
                    // stop when the bytes are no longer the same
                    oldms.Seek(oldPtrOffset + i + j, SeekOrigin.Begin);
                    byte currentOldStart = oldbr.ReadByte();
                    
                    newms.Seek(newPtrOffset + j, SeekOrigin.Begin);
                    byte newPtr = newbr.ReadByte();
                    if(currentOldStart != newPtr)
                        break;
                    currentLength++;
                }

                // update the optimal value
                if (currentLength > maxLength)
                {
                    maxLength = currentLength;
                    searchResults.recordMatchLen[0] = maxLength;
                    disp = oldLength - i;
                    searchResults.recordMatchPos[0] = i;

                    // if we cannot do better anyway, stop trying.
                    if (maxLength == newLength)
                        break;
                }
            }
            
            return searchResults;
        }
    }
    
    private static SearchResults compressionSearchB(byte[] bytes, long pos){
        /* data,        posSubtract, maxMatchDiff, maxMatchLen, zerosAtEnd, searchReverse */
        /* data,        1,           0x1000,       18,          True,       False */
        SearchResults searchResults = new SearchResults();
        
        int maxMatchDiff = 0x1000;
        long start = Math.Max(0, pos - maxMatchDiff);

        int maxMatchLen = 18;

        int lower = 0;
        long upper = Math.Min(maxMatchLen, bytes.Length - pos);
        
        using(MemoryStream ms = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(ms)){
            
            while(lower <= upper){
                // Attempt to find a match at the middle length
                long matchLen = (lower + upper) / 2; // 2
                
                ms.Seek(pos, SeekOrigin.Begin);
                byte[] match = br.ReadBytes((int)(pos + matchLen));
                
                //Console.WriteLine("Problem in while?");
                //ms.Seek(start, SeekOrigin.Begin);
                //byte[] search = br.ReadBytes((int)(pos - start));
                
                //if(match.Length == 0 || search.Length == 0) break;
                int matchPos = -1;
                if(match.Length > 0 && start < bytes.Length){
                    matchPos = Array.IndexOf(bytes, match[0], (int)start);
                }
                if(matchLen > pos - matchPos) matchPos = -1;
                
                //Console.WriteLine("Not problem in while!");
                while(matchPos > -1){
                    //Console.WriteLine("Stuck in while loop?");
                    bool found = true;
                    
                    for(int i = 0; i < matchLen; i++){
                        byte a = match[i];
                        byte b = bytes[i + matchPos];
                        if(a != b){
                            found = false;
                            break;
                        }
                    }
                    
                    if(found) break;
                    
                    matchPos = Array.IndexOf(bytes, match[0], matchPos + 1);
                    if(matchLen > pos - matchPos) matchPos = -1;
                }

                if(matchPos == -1){
                    // No such match -- any matches will be smaller than this
                    upper = matchLen - 1;
                }else{
                    // Match found!
                    //foundIndex += (int)start;
                    if(matchLen > searchResults.recordMatchLen[0]){
                        searchResults.recordMatchPos[0] = (int)(matchPos);
                        searchResults.recordMatchLen[0] = (int)matchLen;
                    }
                    lower = (int)matchLen + 1;
                }
            }
        }
        
        return searchResults;
    }
    
    public static byte[] CompressBlock(byte[] bytes){
        /* data,        posSubtract, maxMatchDiff, maxMatchLen, zerosAtEnd, searchReverse */
        /* data,        1,           0x1000,       18,          True,       False */
        using(MemoryStream msw = new MemoryStream())
        using(BinaryWriter bw = new BinaryWriter(msw))
        using(MemoryStream msr = new MemoryStream(bytes))
        using(BinaryReader br = new BinaryReader(msr)){   
            long current = 0; // Index of current byte to compress
            
            int ignorableDataAmount = 0;
            int ignorableCompressedAmount = 0;

            int bestSavingsSoFar = 0;
            
            msr.Seek(0, SeekOrigin.Begin);
            
            /* For now, LookAhead doesn't provide a compressed output
               that can be decompressed correctly */
            bool LookAhead = false; 
            SearchResults searchResults = null;
            if(LookAhead) searchResults = GetOptimalCompressionLengths(bytes);

            while(current < msr.Length){
                int blockFlags = 0;

                // We'll go back and fill in blockFlags at the end of the loop.
                long blockFlagsOffset = msw.Length;
                msw.Seek(msw.Length, SeekOrigin.Begin);
                bw.Write((byte)0x0);
                ignorableCompressedAmount++;

                for(int i = 0; i < 8; i ++){
                    
                    if(current >= bytes.Length){
                        //bw.Write((byte)0x00);
                        continue;
                    }

                    // Not sure if this is needed. The DS probably ignores this data
                    int searchResultIndex = (int)current;
                    if(!LookAhead){
                        searchResultIndex = 0;
                        searchResults = compressionSearch(bytes, current);
                    }
                    
                    long searchPos = searchResults.recordMatchPos[searchResultIndex];
                    long searchLen = searchResults.recordMatchLen[searchResultIndex];
                    long searchDisp = Math.Min(current, 0x1000) - searchPos - 1;

                    if(!LookAhead && searchLen > 2 || LookAhead && searchLen > 1){
                        // We found a big match; let's write a compressed block
                        blockFlags |= 1 << (7 - i);
                        msw.Seek(msw.Length, SeekOrigin.Begin);
                        bw.Write((byte)((((searchLen - 3) & 0xf) << 4) | ((searchDisp >> 8) & 0x0F)));
                        bw.Write((byte)(searchDisp & 0xFF));
                        current += searchLen;

                        ignorableDataAmount += (int)searchLen;
                        ignorableCompressedAmount += 2;
                    }else{
                        msr.Seek(current, SeekOrigin.Begin);
                        msw.Seek(msw.Length, SeekOrigin.Begin);
                        bw.Write((byte)br.ReadByte());
                        current += 1;
                        ignorableDataAmount += 1;
                        ignorableCompressedAmount += 1;
                    }

                    long savingsNow = current - msw.Length;
                    if(savingsNow > bestSavingsSoFar){
                        ignorableDataAmount = 0;
                        ignorableCompressedAmount = 0;
                        bestSavingsSoFar = (int)savingsNow;
                    }
                }
                msw.Seek(blockFlagsOffset, SeekOrigin.Begin);
                bw.Write((byte)blockFlags);
            }

            return msw.ToArray();
        }
    }
        
    /* TO BE TESTED FOR GH/BH PURPOSE */
    private static byte[] CompressBlockB(byte[] bytes){
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
            bw.Write((byte)0x10);
            bw.Write(Helpers.UInt24ToBytes((uint)inLength));

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