using SharpDX.Direct3D11;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using System;

namespace adt5
{
    class MTEX : AdtChunk
    {
        private List<Texture2D> _textures = new List<Texture2D>();

        public MTEX(long offset, AdtInfo info) : base(offset, info)
        {
            _info.File.Seek(offset, SeekOrigin.Begin);
            var header = _info.File.ReadStruct<ChunkHeader>();

            string[] textures = _info.File.ReadString(header.Size - 1).Split(new[ ] { '\0' });

            foreach (var texture in textures)
            {
                var file = new MpqFile(MpqArchive.Open(texture));
                var blp = new BLP(file);
                _textures.Add(Resource.FromMemory<Texture2D>(info.Device, blp.ToDDS()));
            }
        }

        public Texture2D[ ] Textures
        {
            get { return _textures.ToArray(); }
        }
    }



    [StructLayout(LayoutKind.Sequential)]
    struct BLP2Header
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] FourCC;

        public int Type;
        public byte Encoding;
        private byte AlphaDepth;
        private byte AlphaEncoding;
        private byte HasMips;
        public int Width;
        public int Height;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public int[] Offsets;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 16)]
        public int[] Lengths;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        private int[] Palette;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DDSPixelFormat
    {
        public int Size;
        public int Flags;
        public Magic FourCC;
        public int RGBBitCount;
        public int RBitMask;
        public int GBitMask;
        public int BBitMask;
        public int ABitMask;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct DDSHeader
    {
        public int Size;
        public DDSD Flags;
        public int Height;
        public int Width;
        public int PitchOrLinearSize;
        public int Depth;
        public int MipMapCount;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 11)]
        public int[] Reserved1;

        public DDSPixelFormat PixelFormat;
        public int Caps;
        public int Caps2;
        public int Caps3;
        public int Caps4;
        private int Reserved2;
    }

    class BLP
    {

        private byte[] _data;
        private int _width;
        private int _height;

        public BLP(MpqFile file)
        {
            file.Seek(0, SeekOrigin.Begin);
            var header = file.ReadStruct<BLP2Header>();
            var fcc = new string(header.FourCC);

            if (fcc != "BLP2")
                return;

            file.Seek(header.Offsets[0], SeekOrigin.Begin);
            _data = file.ReadBytes(header.Lengths[0]);
            _width = header.Width;
            _height = header.Height;
        }

        private const int DDSCapsTexture = 0x1000;
        private const int DDPFFourCC = 0x4;

        public byte[] ToDDS()
        {
            var file = new BinaryWriter(new MemoryStream());

            var header = new DDSHeader
            {
                Size = 124,
                Flags = DDSD.Caps & DDSD.HEIGHT & DDSD.Width & DDSD.PixelFormat,
                Caps = DDSCapsTexture,
                Height = _height,
                Width = _width,
                PixelFormat =
                {
                    Size = 32,
                    Flags = DDPFFourCC,
                    FourCC = Magic.DXTF1
                }
            };

            var buffer = new byte[Marshal.SizeOf(header)];
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(header, h.AddrOfPinnedObject(), false);
            h.Free();

            file.Write((int)Magic.DDS);
            file.Write(buffer);
            file.Write(_data);

            file.Seek(0, SeekOrigin.Begin);
            return new BinaryReader(file.BaseStream).ReadBytes((int)file.BaseStream.Length);
        }
    }

    enum Magic
    {
        DDS = 0x20534444,
        DXTF1 = 0x31545844,
    }

    [Flags]
    enum DDSD
    {
        Caps = 0x1,
        HEIGHT = 0x2,
        Width = 0x4,
        PixelFormat = 0x1000
    };
}