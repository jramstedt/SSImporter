using UnityEngine;
using UnityEditor;

using System;
using System.IO;
using System.Text;
using System.Collections.Generic;
using System.Runtime.InteropServices;

using SystemShock.Resource;

namespace SSImporter.Resource {
    public partial class ResourceFile : IDisposable {
        private const string FILE_HEADER = "LG Res File v2\r\n";
        private const long CHUNK_DIRECTORY_POINTER_OFFSET = 0x7C;

        private FileStream fileStream;
        private BinaryReader binaryReader;

        private Dictionary<KnownChunkId, ChunkInfo> chunkInfoPointers;

        public ResourceFile(string filePath) {
            if (!File.Exists(filePath))
                throw new ArgumentException(@"File does not exist.");

            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read);
            binaryReader = new BinaryReader(fileStream, Encoding.ASCII);

            string header = new string(binaryReader.ReadChars(FILE_HEADER.Length));

            if (header != FILE_HEADER)
                throw new ArgumentException(string.Format(@"File type is not supported ({0})", header));

            fileStream.Position = CHUNK_DIRECTORY_POINTER_OFFSET;
            fileStream.Position = (long)binaryReader.ReadInt32(); // file offset to chunk directory

            #region Chunk directory
            ushort chunkCount = binaryReader.ReadUInt16(); // number of chunks in directory
            long chunkOffset = (long)binaryReader.ReadInt32(); // file offset to beginning of first chunk data

            chunkInfoPointers = new Dictionary<KnownChunkId, ChunkInfo>(chunkCount);

            while (chunkCount-- > 0) {
                FileChunkInfo fileChunkInfo = binaryReader.Read<FileChunkInfo>();
                ChunkInfo chunkInfo = new ChunkInfo() {
                    info = fileChunkInfo,
                    dataOffset = chunkOffset
                };

                chunkInfoPointers.Add(fileChunkInfo.Id, chunkInfo);

                chunkOffset += (fileChunkInfo.LengthPacked + 3) & ~0x3; // 4-byte alignment
            }
            #endregion
        }

        public ChunkInfo GetChunkInfo(KnownChunkId chunkId) {
            return chunkInfoPointers[chunkId];
        }

        public byte[] GetChunkData(KnownChunkId chunkId, ushort blockIndex = 0) {
            return GetChunkData(chunkInfoPointers[chunkId], blockIndex);
        }
        public byte[] GetChunkData(ChunkInfo chunkInfo, ushort blockIndex = 0) {
            FileChunkInfo fileChunkInfo = chunkInfo.info;

            if (blockIndex != 0 && !fileChunkInfo.HasBlocks)
                throw new Exception("Tried to access block but there are not any.");

            fileStream.Position = chunkInfo.dataOffset;
            byte[] rawChunk = binaryReader.ReadBytes(fileChunkInfo.LengthPacked);

            if (fileChunkInfo.ChunkType == ChunkType.FlatUncompressed) {
                return rawChunk;
            } else if (fileChunkInfo.ChunkType == ChunkType.FlatCompressed) {
                return UnpackChunk(fileChunkInfo, rawChunk);
            } else if (fileChunkInfo.ChunkType == ChunkType.BlocksUncompressed) {
                return ReadBlock(rawChunk, blockIndex);
            } else if (fileChunkInfo.ChunkType == ChunkType.BlocksCompressed) {
                return ReadBlock(rawChunk, blockIndex, fileChunkInfo);
            } else {
                throw new Exception("Unsupported chunk");
            }
        }

        public byte[][] GetChunkDatas(ChunkInfo chunkInfo) {
            FileChunkInfo fileChunkInfo = chunkInfo.info;

            fileStream.Position = chunkInfo.dataOffset;
            byte[] rawChunk = binaryReader.ReadBytes(fileChunkInfo.LengthPacked);

            if (fileChunkInfo.ChunkType == ChunkType.FlatUncompressed) {
                return new byte[][] { rawChunk };
            } else if (fileChunkInfo.ChunkType == ChunkType.FlatCompressed) {
                return new byte[][] { UnpackChunk(fileChunkInfo, rawChunk) };
            } else if (fileChunkInfo.ChunkType == ChunkType.BlocksUncompressed) {
                return ReadBlocks(rawChunk);
            } else if (fileChunkInfo.ChunkType == ChunkType.BlocksCompressed) {
                return ReadBlocks(rawChunk, fileChunkInfo);
            } else {
                throw new Exception("Unsupported chunk");
            }
        }

        public TextureSet[] ReadBitmaps(KnownChunkId chunkId, PaletteChunk palette) {
            byte[][] chunkBlockData = GetChunkDatas(chunkInfoPointers[chunkId]);

            TextureSet[] textures = new TextureSet[chunkBlockData.Length];

            for (int i = 0; i < chunkBlockData.Length; ++i)
                textures[i] = ReadBitmap(chunkBlockData[i], palette, chunkId.ToString(@"X") + ":" + i);

            return textures;
        }

        public string[] ReadStrings(KnownChunkId chunkId) {
            return ReadStrings(chunkInfoPointers[chunkId]);
        }

        public string[] ReadStrings(ChunkInfo chunkInfo) {
            if (chunkInfo.info.ContentType != ContentType.Text)
                throw new ArgumentException("Chunk is not text.");

            byte[][] chunkBlockData = GetChunkDatas(chunkInfo);

            string[] strings = new string[chunkBlockData.Length];

            for (int i = 0; i < chunkBlockData.Length; ++i)
                strings[i] = Encoding.ASCII.GetString(chunkBlockData[i]).TrimEnd('\0');

            return strings;
        }

        public FontSet ReadFont(KnownChunkId chunkId, PaletteChunk palette) {
            return ReadFont(chunkInfoPointers[chunkId], palette);
        }

        public FontSet ReadFont(ChunkInfo chunkInfo, PaletteChunk palette) {
            if (chunkInfo.info.ContentType != ContentType.Font)
                throw new ArgumentException("Chunk is not font.");

            const int PADDING = 1;

            using (MemoryStream ms = new MemoryStream(GetChunkData(chunkInfo))) {
                BinaryReader msbr = new BinaryReader(ms);

                BitmapFont font = msbr.Read<BitmapFont>();

                //Debug.Log(font.ToString());

                int charactersCount = font.LastAscii - font.FirstAscii + 1; // inclusive

                ms.Position = font.xOffset;

                // TODO Add padding per character.

                //ushort[] coordinates = new ushort[coordinatesCount];
                Rect[] characterRects = new Rect[charactersCount];
                Rect characterRect = new Rect(PADDING + msbr.ReadUInt16(), PADDING, 0, font.Height);
                ushort pixelWidth = 0;
                ushort pixelHeight = font.Height;
                for (int i = 0; i < charactersCount; ++i) {
                    pixelWidth = msbr.ReadUInt16();
                    characterRect.x += characterRect.width;
                    characterRect.width = pixelWidth - characterRect.x;
                    characterRects[i] = characterRect;
                }

                ms.Position = font.bitsOffset;

                byte[] pixelData = msbr.ReadBytes(font.Width * font.Height);

                Texture2D texture = new Texture2D(pixelWidth + PADDING * 2, pixelHeight + PADDING * 2, TextureFormat.ARGB32, true, true); // +1 pixel padding on all sides
                texture.Fill(new Color32(0, 0, 0, 0));
                texture.alphaIsTransparency = true;
                texture.wrapMode = TextureWrapMode.Clamp;
                texture.filterMode = FilterMode.Point;

                CharacterInfo[] characterInfo = new CharacterInfo[charactersCount];
                for (int c = 0; c < charactersCount; ++c) {
                    Rect rect = characterRects[c];
                    characterInfo[c] = new CharacterInfo() {
                        flipped = false,
                        index = font.FirstAscii + c,
                        size = font.Height,
                        style = FontStyle.Normal,
                        width = rect.width,
                        uv = new Rect(rect.x / (float)texture.width, rect.y / (float)texture.height, rect.width / (float)texture.width, rect.height / (float)texture.height),
                        vert = new Rect(0, 0, rect.width, -rect.height),
                    };
                }

                Font unityFont = new Font(chunkInfo.info.Id.ToString());
                unityFont.characterInfo = characterInfo;

                int lastY = pixelHeight - 1;
                if (font.DataType == BitmapFont.BitmapDataType.BlackWhite) {
                    for (int y = 0; y < pixelHeight; ++y) {
                        int pixelOffset = (lastY - y) * font.Width;
                        for (int x = 0; x < pixelWidth; ++x) {
                            byte block = pixelData[pixelOffset + (x >> 3)];
                            bool white = (block & (1 << (7 - (x & 7)))) != 0;

                            if (white)
                            texture.SetPixel(x + PADDING, y + PADDING, new Color(1f, 1f, 1f, 1f));
                        }
                    }
                } else if (font.DataType == BitmapFont.BitmapDataType.Color) {
                    for (int y = 0; y < pixelHeight; ++y) {
                        for (int x = 0; x < pixelWidth; ++x) {
                            byte paletteIndex = pixelData[((lastY - y) * pixelWidth) + x];
                            texture.SetPixel(x + PADDING, y + PADDING, palette[paletteIndex]);
                        }
                    }
                } else {
                    throw new ArgumentException("Unsupported font bitmap type.");
                }

                texture.Apply(true, false);
                //EditorUtility.CompressTexture(texture, TextureFormat.DXT5, TextureCompressionQuality.Best);

                return new FontSet() {
                    Font = unityFont,
                    Texture = texture,
                    Colored = font.DataType == BitmapFont.BitmapDataType.Color
                };
            }
        }

        public TextureSet ReadBitmap(KnownChunkId chunkId, PaletteChunk palette, string name, ushort blockIndex = 0) {
            return ReadBitmap(chunkInfoPointers[chunkId], palette, name, blockIndex);
        }

        public TextureSet ReadBitmap(ChunkInfo chunkInfo, PaletteChunk palette, string name, ushort blockIndex = 0) {
            if (chunkInfo.info.ContentType != ContentType.Bitmap)
                throw new ArgumentException("Chunk is not bitmap.");

            return ReadBitmap(GetChunkData(chunkInfo, blockIndex), palette, name);
        }

        private TextureSet ReadBitmap(byte[] chunkData, PaletteChunk palette, string name) {
            using (MemoryStream ms = new MemoryStream(chunkData)) {
                BinaryReader msbr = new BinaryReader(ms);

                Bitmap bitmap = msbr.Read<Bitmap>();

                byte[] pixelData;

                if (bitmap.BitmapType == BitmapType.Texture || bitmap.BitmapType == BitmapType.Uncompressed) {
                    pixelData = msbr.ReadBytes(bitmap.Width * bitmap.Height);
                } else if (bitmap.BitmapType == BitmapType.Compressed) {
                    pixelData = RunLengthDecode(bitmap, msbr);
                } else {
                    throw new Exception("Not supported bitmap type! " + bitmap.BitmapType);
                }

                if (ms.Length > (ms.Position + 4)) { // Optional data
                    int optionType = msbr.ReadInt32();
                    if (optionType == 0x000001) {
                        palette = msbr.Read<PaletteChunk>();
                    }
                }

                #region Create texture
                Texture2D diffuse = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false, true);
                Texture2D emission = new Texture2D(bitmap.Width, bitmap.Height, TextureFormat.RGBA32, false, true);
                emission.Fill(new Color32(0, 0, 0, 0));
                bool emissionHasPixels = false;
                //bool animated = false;

                int lastY = bitmap.Height - 1;
                for (int y = 0; y < bitmap.Height; ++y) {
                    for (int x = 0; x < bitmap.Width; ++x) {
                        byte paletteIndex = pixelData[((lastY - y) * bitmap.Width) + x];

                        if (paletteIndex > 2 && paletteIndex < 32) { // TODO check these against original game
                            emission.SetPixel(x, y, palette[paletteIndex]);
                            emissionHasPixels = true;
                        }
                        
                        //if (paletteIndex > 4 && paletteIndex < 32)
                        //    animated = true;

                        diffuse.SetPixel(x, y, palette[paletteIndex]);
                    }
                }

                if (!emissionHasPixels) {
                    Texture2D.DestroyImmediate(emission);
                    emission = null;
                }

                Vector2 pivot = new Vector2(    (bitmap.PivotMinX + bitmap.PivotMaxX) / 2f,
                                                lastY - (bitmap.PivotMinY + bitmap.PivotMaxY) / 2f);

                if (bitmap.PivotMinX == 0 && bitmap.PivotMaxX == 0)
                    pivot.x = bitmap.Width >> 1;

                if (bitmap.PivotMinY == 0 && bitmap.PivotMaxY == 0)
                    pivot.y = bitmap.Height >> 1;

                return new TextureSet() {
                    Name = name,
                    Diffuse = diffuse,
                    Emission = emission,
                    Emissive = emissionHasPixels,
                    Pivot = pivot
                };
                #endregion
            }
        }

        public PaletteChunk ReadPalette(KnownChunkId chunkId, ushort blockIndex = 0) {
            return ReadPalette(chunkInfoPointers[chunkId], blockIndex);
        }

        public PaletteChunk ReadPalette(ChunkInfo chunkInfo, ushort blockIndex = 0) {
            if (chunkInfo.info.ContentType != ContentType.Palette)
                throw new ArgumentException("Chunk is not palette.");

            using (MemoryStream ms = new MemoryStream(GetChunkData(chunkInfo, blockIndex))) {
                BinaryReader msbr = new BinaryReader(ms);
                return msbr.Read<PaletteChunk>();
            }
        }

        public T[] ReadArrayOf<T>(KnownChunkId chunkId, ushort blockIndex = 0) {
            return ReadArrayOf<T>(chunkInfoPointers[chunkId]);
        }

        public T[] ReadArrayOf<T>(ChunkInfo chunkInfo, ushort blockIndex = 0) {
            using (MemoryStream ms = new MemoryStream(GetChunkData(chunkInfo, blockIndex))) {
                BinaryReader msbr = new BinaryReader(ms);

                int structSize = Marshal.SizeOf(typeof(T));

                if (ms.Length % structSize != 0)
                    throw new ArgumentException(string.Format(@"Chunk length {0} is not divisible by struct size {1}.", ms.Length, structSize));

                T[] structs = new T[ms.Length / structSize];

                for (int i = 0; i < structs.Length; ++i)
                    structs[i] = msbr.Read<T>();

                return structs;
            }
        }

        public bool HasChunk(KnownChunkId chunkId) {
            return chunkInfoPointers.ContainsKey(chunkId);
        }

        public ICollection<KnownChunkId> GetChunkList() {
            return chunkInfoPointers.Keys;
        }

        public void Dispose() {
            fileStream.Dispose();
        }

        private byte[][] ReadBlocks(byte[] rawChunk) {
            using (MemoryStream ms = new MemoryStream(rawChunk)) {
                BinaryReader msbr = new BinaryReader(ms);

                ushort blockCount = msbr.ReadUInt16(); // number of sub blocks

                byte[][] blockDatas = new byte[blockCount][];

                long blockDirectory = ms.Position;

                for (int i = 0; i < blockCount; ++i) {
                    ms.Position = blockDirectory + (i * sizeof(int));

                    int blockStart = msbr.ReadInt32(); // pointer is from dataOffset
                    int blockEnd = msbr.ReadInt32();

                    ms.Position = blockStart;

                    blockDatas[i] = msbr.ReadBytes(blockEnd - blockStart);
                }

                return blockDatas;
            }
        }

        private byte[][] ReadBlocks(byte[] rawChunk, FileChunkInfo fileChunkInfo) {
            if (!fileChunkInfo.IsCompressed)
                return ReadBlocks(rawChunk);

            using (MemoryStream ms = new MemoryStream(rawChunk)) {
                BinaryReader msbr = new BinaryReader(ms);

                ushort blockCount = msbr.ReadUInt16(); // number of sub blocks

                long blockDirectory = ms.Position;

                int chunkDataStart = msbr.ReadInt32();
                ms.Position += blockCount * sizeof(int);
                int chunkDataEnd = msbr.ReadInt32();

                ms.Position = chunkDataStart;

                byte[] unpackedChunkData = UnpackChunk(fileChunkInfo, msbr.ReadBytes(chunkDataEnd - chunkDataStart));

                byte[][] blockDatas = new byte[blockCount][];

                using (MemoryStream upms = new MemoryStream(unpackedChunkData)) {
                    BinaryReader upmsbr = new BinaryReader(upms);

                    for (int i = 0; i < blockCount; ++i) {
                        ms.Position = blockDirectory + (i * sizeof(int));

                        int blockStart = msbr.ReadInt32(); // pointer is from dataOffset
                        int blockEnd = msbr.ReadInt32();

                        upms.Position = blockStart - chunkDataStart;

                        blockDatas[i] = upmsbr.ReadBytes(blockEnd - blockStart);
                    }
                }

                return blockDatas;
            }
        }

        private byte[] ReadBlock(byte[] rawChunk, ushort blockIndex) {
            using (MemoryStream ms = new MemoryStream(rawChunk)) {
                BinaryReader msbr = new BinaryReader(ms);

                ushort blockCount = msbr.ReadUInt16(); // number of sub blocks

                if (blockIndex >= blockCount)
                    throw new ArgumentOutOfRangeException(@"blockIndex", string.Format(@"Chunk has only {0} blocks", blockCount));

                ms.Position += blockIndex * sizeof(int);

                int blockStart = msbr.ReadInt32(); // pointer is from dataOffset
                int blockEnd = msbr.ReadInt32();

                ms.Position = blockStart;

                return msbr.ReadBytes(blockEnd - blockStart);
            }
        }

        private byte[] ReadBlock(byte[] rawChunk, ushort blockIndex, FileChunkInfo fileChunkInfo) {
            if (!fileChunkInfo.IsCompressed)
                return ReadBlock(rawChunk, blockIndex);

            using (MemoryStream ms = new MemoryStream(rawChunk)) {
                BinaryReader msbr = new BinaryReader(ms);

                ushort blockCount = msbr.ReadUInt16(); // number of sub blocks

                if (blockIndex >= blockCount)
                    throw new ArgumentOutOfRangeException(@"blockIndex", string.Format(@"Chunk has only {0} blocks", blockCount));

                long blockDirectory = ms.Position;

                int chunkDataStart = msbr.ReadInt32();
                ms.Position += blockCount * sizeof(int);
                int chunkDataEnd = msbr.ReadInt32();

                ms.Position = chunkDataStart;

                byte[] unpackedChunkData = UnpackChunk(fileChunkInfo, msbr.ReadBytes(chunkDataEnd - chunkDataStart));

                using (MemoryStream upms = new MemoryStream(unpackedChunkData)) {
                    BinaryReader upmsbr = new BinaryReader(upms);

                    ms.Position += blockDirectory + (blockIndex * sizeof(int));

                    int blockStart = msbr.ReadInt32(); // pointer is from dataOffset
                    int blockEnd = msbr.ReadInt32();

                    upms.Position = blockStart - chunkDataStart;

                    return upmsbr.ReadBytes(blockEnd - blockStart);
                }
            }
        }

        private byte[] UnpackChunk(FileChunkInfo fileChunkInfo, byte[] rawChunk) {
            const ushort KeyEndOfStream = 0x3FFF;
            const ushort KeyResetDictionary = 0x3FFE;
            const ushort MaxReferenceWords = KeyResetDictionary - 0x00FF;

            byte[] blockData = new byte[fileChunkInfo.LengthUnpacked];

            long[] offset = new long[MaxReferenceWords];
            short[] reference = new short[MaxReferenceWords];
            ushort[] unpackedLength = new ushort[MaxReferenceWords];

            for (int i = 0; i < MaxReferenceWords; ++i) {
                unpackedLength[i] = 1;
                reference[i] = -1;
            }

            using (MemoryStream rms = new MemoryStream(rawChunk), bms = new MemoryStream(blockData)) {
                BinaryReader rmsbr = new BinaryReader(rms);
                BinaryWriter bmsbw = new BinaryWriter(bms);

                int bits = 0;
                ulong bitBuffer = 0;
                ushort wordIndex = 0;

                while (rms.Position < rms.Length) {
                    while (bits < 14) {
                        bitBuffer = (bitBuffer << 8) | rmsbr.ReadByte(); // move buffer 8 bits to left, insert 8 bits to buffer
                        bits += 8; // added 8 bits.
                    }

                    bits -= 14; // consume 14 bits.
                    ushort value = (ushort)((bitBuffer >> bits) & 0x3FFF); // shift right to ignore unconsumed bits

                    if (value == KeyEndOfStream) {
                        break;
                    } else if (value == KeyResetDictionary) {
                        for (int i = 0; i < MaxReferenceWords; ++i) {
                            unpackedLength[i] = 1;
                            reference[i] = -1;
                        }
                        wordIndex = 0;
                    } else {
                        if (wordIndex < MaxReferenceWords) {
                            offset[wordIndex] = bms.Position; // set unpacked data position to wordIndex

                            if (value >= 0x0100) // value is index to reference word
                                reference[wordIndex] = (short)(value - 0x0100);
                        }

                        ++wordIndex;

                        if (value < 0x0100) { // byte value
                            bmsbw.Write((byte)(value & 0xFF));
                        } else { // check dictionary
                            value -= 0x0100;

                            if (unpackedLength[value] == 1) { // First time looking for reference
                                if (reference[value] != -1) { // reference found in dictionary
                                    unpackedLength[value] += unpackedLength[reference[value]]; // add length of referenced byte sequence
                                } else { // reference not found in dictionary
                                    unpackedLength[value] += 1; // increase length by one to read next uncompressed byte
                                }
                            }

                            for (int i = 0; i < unpackedLength[value]; ++i)
                                bmsbw.Write(blockData[i + offset[value]]);
                        }
                    }
                }
            }

            return blockData;
        }

        private byte[] RunLengthDecode(Bitmap bitmap, BinaryReader msbr) {
            byte[] bitmapData = new byte[bitmap.Width * bitmap.Height];
            using (MemoryStream ms = new MemoryStream(bitmapData)) {
                BinaryWriter msbw = new BinaryWriter(ms);

                while (ms.Position < ms.Length) {
                    byte cmd = msbr.ReadByte();

                    if (cmd == 0x00) { // 00 nn xx      write nn bytes of colour xx
                        byte amount = msbr.ReadByte();
                        byte data = msbr.ReadByte();

                        for (byte i = 0; i < amount; ++i)
                            msbw.Write(data);
                    } else if (cmd < 0x80) { // 0<nn<0x80	copy nn bytes direct
                        for (byte i = 0; i < cmd; ++i)
                            msbw.Write(msbr.ReadByte());
                    } else if (cmd == 0x80) {
                        byte param1 = msbr.ReadByte();
                        byte param2 = msbr.ReadByte();

                        if (param1 == 0x00 && param2 == 0x00) { // EOF
                            break;
                        } else if (param2 < 0x80) { // skip (nn*256+mm) bytes
                            for (int i = 0; i < (param2 * 256 + param1); ++i)
                                msbw.Write((byte)0x00);
                        } else if (param2 < 0xC0) { // copy ((nn&0x3f)*256+mm) bytes
                            for (int i = 0; i < ((uint)(param2 & 0x3F) * 256 + param1); ++i)
                                msbw.Write(msbr.ReadByte());
                        } else if (param2 > 0xC0) { // 0xC0<nn	write ((nn&0x3f)*256+mm) bytes of colour xx
                            byte color = msbr.ReadByte();
                            for (int i = 0; i < ((uint)(param2 & 0x3F) * 256 + param1); ++i)
                                msbw.Write(color);
                        } else {
                            throw new Exception(string.Format(@"Unhandled subcommand {0:2X}", param2));
                        }
                    } else { // 0x80<nn	skip (nn&0x7f) bytes
                        for (int i = 0; i < (cmd & 0x7F); ++i)
                            msbw.Write((byte)0x00);
                    }
                }
            }
            return bitmapData;
        }
    }

    public enum ChunkType : byte {
        FlatUncompressed,
        FlatCompressed,
        BlocksUncompressed,
        BlocksCompressed
    }

    public struct ChunkInfo {
        public FileChunkInfo info;
        public long dataOffset;
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    public struct FileChunkInfo {
        public KnownChunkId Id;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        private byte[] lengthUnpacked;

        public ChunkType ChunkType;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        private byte[] lengthPacked;

        public ContentType ContentType;

        public int LengthUnpacked { get { return (int)lengthUnpacked[0] | (int)lengthUnpacked[1] << 8 | (int)lengthUnpacked[2] << 16; } }
        public int LengthPacked { get { return (int)lengthPacked[0] | (int)lengthPacked[1] << 8 | (int)lengthPacked[2] << 16; } }

        public bool HasBlocks { get { return ChunkType == ChunkType.BlocksCompressed || ChunkType == ChunkType.BlocksUncompressed; } }
        public bool IsCompressed { get { return ChunkType == ChunkType.FlatCompressed || ChunkType == ChunkType.BlocksCompressed; } }

        public override string ToString() {
            return string.Format("Id = {0}, LengthUnpacked = {1}, ChunkType = {2}, LengthPacked = {3}, ContentType = {4}", Id, LengthUnpacked, ChunkType, LengthPacked, ContentType);
        }
    }

    public class TextureSet : IDisposable {
        public string Name;
        public Texture2D Diffuse;
        public Texture2D Emission;
        public bool Emissive;
        public Vector2 Pivot;

        public void Dispose() {
            if (Diffuse != null)
                Texture2D.DestroyImmediate(Diffuse);

            if (Emission != null)
                Texture2D.DestroyImmediate(Emission);
        }
    }

    public class FontSet : IDisposable {
        public Font Font;
        public Texture2D Texture;
        public bool Colored;

        public void Dispose() {
            Font.DestroyImmediate(Font);
            Texture2D.DestroyImmediate(Texture);
        }
    }
}


