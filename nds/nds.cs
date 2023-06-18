using System;
using System.IO;
using System.Linq;
using System.Collections.Generic;
using System.Text;
using GHDS_ModdingTool;

namespace NDS{    
    public class HEADERFile{
        public uint Size;
        public uint Offset;
        public byte[] RawBytes;
        
        public HEADERFile(byte[] NDSBytes){
            Offset = 0;
            
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x84, SeekOrigin.Begin);
                Size = br.ReadUInt32();
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawBytes = br.ReadBytes((int)Size);
            }
        }
    }
    
    public class BANNERFile{
        public uint Size;
        public uint Offset;
        public byte[] RawBytes;
        
        public BANNERFile(byte[] NDSBytes){
            using(MemoryStream ms = new MemoryStream(NDSBytes))
            using(BinaryReader br = new BinaryReader(ms)){
                ms.Seek(0x68, SeekOrigin.Begin);
                Offset = br.ReadUInt32();

                Size = 0xa00;
                
                ms.Seek(Offset, SeekOrigin.Begin);
                RawBytes = br.ReadBytes((int)Size);
            }
        }
    }
    
    public class Asset{
        public string Name;
        public string Path;
        public bool IsFolder;
        public uint NameOffset;
        public uint Size;
        public uint DecompressedSize;
        public bool IsCompressed = false;
        public uint OriginalDecompressedSize = 0;
        public byte[] Bytes;
        public byte[] DecompressedBytes;
        public uint RawSize;
        public byte[] RawBytes;
        public uint Offset;
        public uint ParentFolderID;
        public uint ID;
        public Asset Parent;
        public List<Asset> Children;
        public List<uint> FSIndexSizeOffsets = new List<uint>();
        public int FSIndex = -1;
        public uint FATIndex;
        public IndexEntry FSEntry;
        public bool Edited = false;
        
        public Asset(string name, bool isFolder, uint nameOffset, uint id, uint parentFolderID){
            Name = name;
            IsFolder = isFolder;
            NameOffset = nameOffset;
            ParentFolderID = parentFolderID;
            ID = id;
        }
        
        public void SetBytes(byte[] bytes){
            //Console.WriteLine("Decompressing: " + Name);
            Bytes = bytes;
            DecompressedBytes = LZ10.Decompress(bytes, Name);
            DecompressedSize = (uint)DecompressedBytes.Length;
            if(bytes.Length != DecompressedSize) IsCompressed = true;
            if(OriginalDecompressedSize == 0) OriginalDecompressedSize = DecompressedSize;
            if(Name == "localize.English.bin"){
                File.WriteAllBytes("localize.English.bin.decompressed", DecompressedBytes);
            }
        }
        
        public string GetFullName(){
            return ".\\" + Path + Name;
        }
        
        public string ToString(int depth = 0){
            string ret = $"{{name: '{Name}', id: {ID}, offset: {Offset}, size: {Size}";
            //Console.WriteLine($"{Name} => {FSIndexSizeOffsets.Count}");
            string offsets = String.Join(", ", FSIndexSizeOffsets.ToArray());
            ret += $", fsindexOffsets: [{offsets}], fsindex: {FSIndex}";
            if(IsFolder){
                string children = String.Join($",\n{new string('\t', depth + 1)}", Children.Select(child => child.ToString(depth + 1)).ToArray());
                ret += $", children: [\n{new string('\t', depth + 1)}{children}\n{new string('\t', depth)}]";
            }
            return ret + "}";
        }
    }
    
    public class NDSFile{
        public uint Size;
        public string Name;
        public string ID;
        public HEADERFile HEADER;
        public ARMFile ARM9;
        public OverlayFile OL9;
        public ARMFile ARM7;
        public FNTFile FNT;
        public FATFile FAT;
        public BANNERFile BANNER;
        public FSINDEXFile FSINDEX;
        public List<Asset> Assets;
        public List<Song> Songs;
        public byte[] Bytes;
        
        public ModdingSettings moddingSettings;
        
        public NDSFile(string file, ModdingSettings mods){
            moddingSettings = mods;
            Bytes = File.ReadAllBytes(file);
            Size = (uint)Bytes.Length;
            using(MemoryStream ms = new MemoryStream(Bytes)){
                using(BinaryReader br = new BinaryReader(ms)){
                    Name = System.Text.Encoding.ASCII.GetString(br.ReadBytes(0x0c)).TrimEnd();
                    ID = System.Text.Encoding.ASCII.GetString(br.ReadBytes(0x06)).TrimEnd();

                    ms.Seek(0x0, SeekOrigin.Begin);
                    ushort crc16 = Helpers.CRC16(br.ReadBytes(0x015e));
                    //Console.WriteLine(crc16.ToString("X4"));
                }
            }
                
            /* DETECT GAME AND VERSION */
            GHGAME ghgame = GH.GHGAMES.Find(x => x.ID == ID);
            if(ghgame == null){
                Console.WriteLine("Could not find matching GH/BH game with ID: {ID}\n");
                return;
            }
            Console.WriteLine($"Detected game: {ghgame.Name} [{ghgame.Region}] ({ghgame.Languages})\n");
            
            if(ghgame.IsDemo){
                Console.WriteLine("This game appears to be a demo...\nThis tool doesn't mod demos!\n");
                return;
            }
            
            try{
                HEADER = new HEADERFile(Bytes);
                ARM9 = new ARMFile(Bytes);
                OL9 = new OverlayFile(Bytes);
                ARM7 = new ARMFile(Bytes, false);
                FNT = new FNTFile(Bytes);
                FAT = new FATFile(Bytes, FNT);
                BANNER = new BANNERFile(Bytes);
                FSINDEX = new FSINDEXFile(Bytes, FNT);
                Songs = new List<Song>();
                
                bool noCustomSongsDetected = GH.ManageSongs(ghgame, ARM9, FNT, FSINDEX, Songs, moddingSettings);
                if(noCustomSongsDetected) return;
                
                FNT.Assets.FindAll(x => !x.IsFolder && !x.Name.Contains("fsindex") && !x.GetFullName().StartsWith(".\\Art")).ForEach(x => {
                    string hash = GH.FilenameToHash(x.GetFullName());
                    IndexEntry fsindexentry = FSINDEX.IndexEntries.Find(x => x.Hash.ToString("X8") == hash); 
                    
                    if(fsindexentry == null){
                        Console.WriteLine("Could not matching fsindex hash for: " + x.GetFullName());
                        return;
                    }
                    x.FSEntry = fsindexentry;
                    fsindexentry.asset = x;
                    fsindexentry.Filename = x.GetFullName();
                });
                
                //GetAssetPadding(FNT.Assets.FindAll(x => !x.IsFolder));
                //FSINDEX.Update();
                RebuildNDS(Path.GetFileNameWithoutExtension(file));
            }catch(Exception e){
                Console.WriteLine(e.ToString() + "\n" + e.StackTrace.ToString());
            }
        }
        
        public void GetAssetPadding(List<Asset> assets){
            assets.Sort((x,y) => x.Offset < y.Offset ? -1 : 1);
            uint previousRAMOffset = 0;
            uint previousRAWSize = 0;
            assets.ForEach(asset => {
                uint urawSize = asset.RawSize;
                uint usize = asset.Size;
                uint isCompressed = (uint)(usize != asset.DecompressedSize ? 1 : 0);
                uint upadding = urawSize - usize;
                uint uendOffset = (uint)(asset.Offset + urawSize);
                string rawSize = "0x" + urawSize.ToString("X8");
                string rawSize2 = "0x" + (urawSize * 2).ToString("X8");
                string size = "0x" + usize.ToString("X8");
                string padding = "0x" + upadding.ToString("X8");
                string endOffset = "0x" + uendOffset.ToString("X8");
                string startOffset = "0x" + asset.Offset.ToString("X8");
                string name = asset.GetFullName();
                string ramoffset = "0xffffffff";
                string fssize = "0x00000000";
                string diff = "0x00000000";
                string diff2 = "0x00000000";
                string ramDiff = "00000000";
                if(asset.FSEntry != null){
                    ramoffset = "0x" + asset.FSEntry.RAMOffset.ToString("X8");
                    fssize = "0x" + asset.FSEntry.Size.ToString("X8");
                    diff = "0x" + ((asset.FSEntry.RAMOffset - previousRAMOffset)).ToString("X8");
                    diff2 = "0x" + ((asset.FSEntry.RAMOffset - previousRAMOffset) / 2).ToString("X8");
                    ramDiff = ((int)(asset.FSEntry.RAMOffset - previousRAMOffset) - (int)previousRAWSize).ToString().PadLeft(8, '0');
                    previousRAMOffset = asset.FSEntry.RAMOffset;
                }
                previousRAWSize = urawSize * 2;
                //Console.WriteLine($"{{RawSize: {rawSize}, Size: {size}, fssize: {fssize}, Diff/2: {diff2}, Diff: {diff}, RAMOffset: {ramoffset}, SO: {startOffset}, EO: {endOffset}, P: {padding}, N: '{name}'}},");
                //Console.WriteLine($"{{RawSize2: {rawSize2}, RAMDiff: {diff}, ExpectedRAMDiff: {ramDiff}, RAMOffset: {ramoffset}, C: {isCompressed}, N: '{name}'}},");
                Console.WriteLine($"{{RawSize2: {rawSize2}, RAMDiff: {diff}, ExpectedRAMDiff: {ramDiff}, RAMOffset: {ramoffset}, C: {isCompressed}, Offset: {startOffset}, N: '{name}'}},");
            });
        }
        
        public void RebuildNDS(string filename){
            uint GameFilesOffset = HEADER.Size;
            GameFilesOffset += ARM9.RawSize;
            GameFilesOffset += OL9.TotalRawSize;
            GameFilesOffset += ARM7.RawSize;
            GameFilesOffset += FNT.RawSize;
            GameFilesOffset += FAT.RawSize;
            GameFilesOffset += BANNER.Size;
            
            using(MemoryStream ms = new MemoryStream())
            using(BinaryReader br = new BinaryReader(ms))
            using(BinaryWriter bw = new BinaryWriter(ms)){
                /* WRITE TEMP HEADER */
                bw.Write(HEADER.RawBytes);
                
                /* WRITE ARM9 */
                Songs.ForEach(x => x.UpdateARM9(ARM9.RawBytes));
                bw.Write(ARM9.RawBytes);
                
                /* WRITE OVERLAY 9 */
                bw.Write(OL9.TotalRawBytes);
                
                /* WRITE ARM7 */
                bw.Write(ARM7.RawBytes);
                
                /* WRITE FNT */
                bw.Write(FNT.RawBytes);
                
                /* WRITE TEMP FAT */
                bw.Write(FAT.RawBytes);
                
                /* WRITE TEMP FAT */
                bw.Write(BANNER.RawBytes);
                
                List<Asset> files = FNT.Assets.FindAll(x => !x.IsFolder);
                //List<Asset> editedFiles = FNT.Assets.FindAll(x => !x.IsFolder && x.Edited);
                files.Sort((x,y) => x.Offset < y.Offset ? -1 : 1);
                
                //Asset beforeLastFile = files[files.Count - 2];
                Asset lastFile = files[files.Count - 1];
                List<Asset> fsEntries = files.FindAll(x => x.FSEntry != null);
                fsEntries.Sort((x,y) => x.FSEntry.RAMOffset < y.FSEntry.RAMOffset ? -1 : 1);
                
                Asset previousAsset = fsEntries[0];
                Asset fsindex = files.Find(x => x.Name.StartsWith("fsindex"));
                
                uint RAMOffset = 0x0;
                uint nextFSRamOffset = fsEntries[1].FSEntry.RAMOffset;
                uint newFSRamOffset = previousAsset.FSEntry.RAMOffset;
                //Console.WriteLine("Previous entry offset: " + newFSRamOffset);
                
                /* 
                    Repacking all files does not seem to have a major impact on the game,
                    other than potentially overflowing the ROM size onto the higher tier.
                */
                bool RepackDecompressedFiles = moddingSettings.KeepAllFilesUncompressed;
                if(RepackDecompressedFiles) Console.WriteLine($"All the game's assets will be repacked as uncompressed!");
                
                /* WRITE ALL FILES */
                files.ForEach(asset => {
                    uint padding = 0x04;
                            
                    byte[] bytes = asset.Bytes;
                    if(RepackDecompressedFiles){
                        bytes = asset.DecompressedBytes;
                    }
                    
                    if(asset == fsindex){
                        padding = (uint)(ms.Length + asset.RawBytes.Length);
                        RAMOffset = padding * 2;
                        //Console.WriteLine("Base RAM Offset: " + RAMOffset.ToString("X8"));
                    }
                    
                    if(asset.FSEntry != null){
                        /* Update the RAM offset */
                        if((!RepackDecompressedFiles && asset.IsCompressed) && RAMOffset % 2 == 0){
                            //Console.WriteLine($"Compressed(1) [{RAMOffset.ToString("X8")} => {(RAMOffset + 1).ToString("X8")}]: {asset.Name}");
                            RAMOffset++;
                        }
                        if((RepackDecompressedFiles || !asset.IsCompressed) && RAMOffset % 2 != 0){
                            //Console.WriteLine($"Compressed(0) [{RAMOffset.ToString("X8")} => {(RAMOffset - 1).ToString("X8")}]: {asset.Name}");
                            RAMOffset--;
                        }
                    }
                    
                    if(new List<string>{
                            ".texture",
                            ".paltexture",
                            ".uanimation",
                            ".animation",
                            ".hwas",
                            ".swav",
                            ".ogg",
                            ".nbfc",
                            ".nbfp"
                        }.Find(x => asset.Name.EndsWith(x)) != null ||
                        new List<string>{
                            "mood_",
                        }.Find(x => asset.Name.StartsWith(x)) != null
                    ){
                        uint startpadding = 0x200;

                        uint sdiff = (uint)ms.Length % startpadding;
                        if(sdiff > 0) while(startpadding - sdiff > 0x00){
                            bw.Write((byte)0xff);
                            sdiff++;
                        }
                        
                        /* To calculate the RAM offset, we need to double the padding */
                        startpadding = 0x400;
                        //Console.WriteLine($"RAM Offset pre-padding: {RAMOffset.ToString("X8")}");
                        uint ramoffsetdiff = RAMOffset % startpadding;
                        if(ramoffsetdiff > 0) while(startpadding - ramoffsetdiff > 0x00){
                            RAMOffset++;
                            ramoffsetdiff++;
                        }
                        //Console.WriteLine($"RAM Offset post-padding: {RAMOffset.ToString("X8")}");
                    }
                    
                    /* UPDATE FAT ENTRY */
                    uint offsetStart = (uint)ms.Length;
                    uint offsetEnd = (uint)(offsetStart + bytes.Length);
                    asset.Offset = offsetStart;
                    
                    ms.Seek(FAT.Offset + asset.FATIndex * 8, SeekOrigin.Begin);
                    bw.Write(offsetStart);
                    bw.Write(offsetEnd);
                    
                    /* WRITE FILE*/
                    ms.Seek(offsetStart, SeekOrigin.Begin);
                    bw.Write(bytes);
                    
                    /* PAD over 0x200 WITH 0xff */
                    if(padding > 0 && asset != lastFile){
                        uint diff = (uint)ms.Length % padding;
                        if(diff > 0) while(padding - diff > 0x00){
                            bw.Write((byte)0xff);
                            diff++;
                        }
                    }
                    
                    offsetEnd = (uint)ms.Length;
                    
                    
                    /* Update the FSINDEX entry for this asset */
                    if(asset.FSEntry != null){
                        uint rawSize = offsetEnd - offsetStart;
                        
                        /* Go to the entry's offset */
                        ms.Seek(fsindex.Offset + 0x0c + (0x0c * asset.FSEntry.Index), SeekOrigin.Begin);
                        ms.Seek(0x04, SeekOrigin.Current);
                        
                        int comp = asset.IsCompressed ? (RepackDecompressedFiles ? 0 : 1) : 0;
                        //Console.WriteLine($"New offset (Compressed({comp})) [{RAMOffset.ToString("X8")}]: {asset.Name}");
                        bw.Write(RAMOffset);
                        //if(RAMOffset != asset.FSEntry.RAMOffset){
                        //    Console.WriteLine($"Prev: Offset: {previousAsset.Offset.ToString("X8")}, RAMOffset: {previousAsset.FSEntry.RAMOffset.ToString("X8")}, RawSize2: {(previousAsset.RawSize * 2).ToString("X8")}, N: {previousAsset.Name}");
                        //    Console.WriteLine($"{asset.Offset.ToString("X8")} != {offsetStart.ToString("X8")}, N: {asset.Name}");
                        //    Console.WriteLine($"{asset.FSEntry.RAMOffset.ToString("X8")} != {RAMOffset.ToString("X8")}, N: {asset.Name}");
                        //    Console.ReadLine();
                        //}
                        
                        /* Skip the unknown byte */
                        ms.Seek(0x01, SeekOrigin.Current);
                        /* Changing the unknown byte does not seem to have any impact?!! */
                        //bw.Write((byte)0xff);
                        
                        /* Get the next entry, if there is one */
                        if(fsEntries.IndexOf(asset) + 1 < fsEntries.Count){
                            Asset nextFSEntry = fsEntries[fsEntries.IndexOf(asset) + 1];
                            nextFSRamOffset = nextFSEntry.FSEntry.RAMOffset;
                        }
                        
                        uint newSize = asset.FSEntry.Size;
                        uint diff = nextFSRamOffset - asset.FSEntry.RAMOffset;
                        
                        /* Check if the asset has been changed */
                        if(asset.OriginalDecompressedSize != asset.DecompressedSize){
                            //Console.WriteLine($"Expected {asset.OriginalDecompressedSize.ToString("X8")}, got {asset.DecompressedSize.ToString("X8")}, for: {asset.Name}");
                        }
                        
                        if(bytes.Length != asset.Size){
                            //Console.WriteLine($"File updated: {asset.Name}");
                            /* Assets need a buffer that's double their raw size */
                            diff = rawSize * 2;
                            
                            /* But the FS entry will tell the game to only read the actual UNCOMPRESSED file size */
                            newSize = (uint)asset.DecompressedSize;
                        }
                        RAMOffset += rawSize * 2;

                        bw.Write(Helpers.UInt24ToBytes(newSize));
                        ms.Seek(offsetEnd, SeekOrigin.Begin);
                        
                        newFSRamOffset += diff;
                    }
                    previousAsset = asset;
                });
                
                uint size = (uint)ms.Length;
                
                /* Calculate ROM capacity */
                byte capacity = 0;
                while(size > (uint)Math.Pow(2, 17 + capacity)){
                    //Console.WriteLine($"Capacity: {capacity} => {Math.Pow(2, 7 + capacity)} < Size: {size}");
                    capacity++;
                }
                
                uint padded_size = (uint)Math.Pow(2, 17 + capacity);
                
                uint diff = padded_size - size;
                while(diff > 0){
                    bw.Write((byte)0xff);
                    diff--;
                }
                
                /* Update ROM capacity in header */
                ms.Seek(0x14, SeekOrigin.Begin);
                bw.Write((byte)capacity);
                
                /* Update ROM size in header */
                ms.Seek(0x80, SeekOrigin.Begin);
                bw.Write(size);
                
                /* Update header CRC16 */
                ms.Seek(0x0, SeekOrigin.Begin);
                ushort crc16 = Helpers.CRC16(br.ReadBytes(0x015e));
                //Console.WriteLine(crc16.ToString("X4"));
                bw.Write(crc16);
                
                string outFilename = filename + "_out.nds";
                File.WriteAllBytes(outFilename, ms.ToArray());
                Console.WriteLine($"New ROM created: {outFilename}");
            }
        }
        
        public void PrintInfo(){
            try{
                Console.WriteLine("Size: " + Size);
                Console.WriteLine("Name: " + Name);
                Console.WriteLine("ID: " + ID);

                Console.WriteLine("ARM9 Offset: " + ARM9.Offset);
                Console.WriteLine("ARM9 Size: " + ARM9.Size);
                Console.WriteLine("ARM9 Bytes length: " + ARM9.Bytes.Length);
                Console.WriteLine(FNT.Bytes.Length);
                ARM9.WriteBytes(ID + "_ARM9.bin");
                FNT.WriteNames(ID + "_FNT_Names.js");
                FNT.WriteBytes(ID + "_FNT.bin");
                FAT.WriteBytes(ID + "_FAT.bin");
                FSINDEX.SaveAsFile(ID + "_fsindex.js", ID);
            }catch(Exception e){
                Console.WriteLine(e);
            }
        }
    }
}