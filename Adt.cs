using System.IO;
using SharpDX;
using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System;
using System.Collections.Generic;
using Device = SharpDX.Direct3D11.Device;
using Int2 = SharpDX.DrawingPoint;
using SharpDX.Direct3D11;

namespace adt5
{
    class Adt
    {
        public int X;
        public int Y;

        private float chunkSize = 1600 / 3f;
        private float border = 100 / 3f;

        public Vector4[] TerrainVertices
        {
            get
            {
                var vertices = new List<Vector4>();

                foreach (var mcnk in hora.Mcnk)
                {
                    vertices.AddRange(mcnk.TerrainVertices);
                }

                return vertices.ToArray();
            }
        }

        public Vector3[] TerrainVerticesTextured
        {
            get
            {
                var vertices = new List<Vector3>();

                foreach (var mcnk in hora.Mcnk)
                {
                    vertices.AddRange(mcnk.TerrainVerticesTextured);
                }

                //vertices.AddRange(hora.Mcnk[0,0].TerrainVerticesTextured);

                return vertices.ToArray();
            }
        }

        public  int[] TerrainIndices
        {
            get
            {
                return new[ ]
                {
                    1
                };
            }
        }

        public Adt(string file, Device device)
        {
            var s = file.Split(new[] { '_', '.', '\\' });

            int.TryParse(s[4], out X);

            int.TryParse(s[5], out Y);
            string map = s[3];

            var min = new Vector3((X - 32) * chunkSize - border, float.MinValue, (32-Y) * chunkSize + border);
            var max = new Vector3((X - 31) * chunkSize + border, float.MaxValue, (31-Y) * chunkSize - border);

            var boundingBox = new BoundingBox(min, max);

            /*for (int y = Y - 1; y <= Y + 1; y++)
            {
                for (int x = X - 1; x <= X + 1; x++)
                {
                    new AdtFile(new Int2(x, y), map, boundingBox);
                }
            }*/

            hora = new AdtFile(new Int2(X, Y), map, boundingBox, device);
        }

        private AdtFile hora;
    }

    struct AdtInfo
    {
        public MpqFile File;
        public int X;
        public int Y;
        public Device Device;
        public Texture2D[ ] Textures;
    }

    internal class AdtFile
    {
        public MpqFile File;
        public MCNK[,] Mcnk = new MCNK[16,16];
        private AdtInfo _info;

        public AdtFile(Int2 pos, string map, BoundingBox bbox, Device device)
        {
            File = new MpqFile(MpqArchive.Open(string.Format(@"World\Maps\{0}\{0}_{1}_{2}.adt", map, pos.X, pos.Y)));

            _info = new AdtInfo
            {
                File = File,
                X = pos.X,
                Y = pos.Y,
                Device = device
            };

            var mtex = new MTEX(GetChunkPosition("MTEX"), _info);
            _info.Textures = mtex.Textures;

            var mcin = new MCIN(GetChunkPosition("MCIN"), _info);

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Mcnk[x, y] = mcin[x, y];
                }
            }

            var textureView = new ShaderResourceView(device, _info.Textures[3]);

            var sampler = new SamplerState(device, new SamplerStateDescription()
            {
                Filter = Filter.MinMagMipLinear,
                AddressU = TextureAddressMode.Wrap,
                AddressV = TextureAddressMode.Wrap,
                AddressW = TextureAddressMode.Wrap,
                BorderColor = Color.Black,
                ComparisonFunction = Comparison.Never,
                MaximumAnisotropy = 16,
                MipLodBias = 0,
                MinimumLod = 0,
                MaximumLod = 16,
            });

            device.ImmediateContext.PixelShader.SetSampler(0, sampler);
            device.ImmediateContext.PixelShader.SetShaderResource(0, textureView);
        }

        public long GetChunkPosition(string id)
        {
            File.Seek(0, SeekOrigin.Begin);
            var fcc = id.ToArray().Reverse();

            while (true)
            {
                var header = File.ReadStruct<ChunkHeader>();
                if (header.Size == 0)
                    break;

                if (header.Id.SequenceEqual(fcc))
                    return File.Position - 8;

                File.Seek(header.Size, SeekOrigin.Current);
            }

            return 0;
        }
    }

    internal class MCNK : AdtChunk
    {
        //public MCNK() { }

        private Vector4 color = new Vector4(0f, 1f, 0f, 1f);
        private Vector4 color2 = new Vector4(0f, .5f, 0f, 1f);

        public Vector3[] TerrainVerticesTextured
        {
            get
            {

                var vertices = new List<Vector3>();

                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        vertices.AddRange(new[ ]
                        {
                            _outerPosition[x + 1, y], new Vector3(1, 1, 0),
                            _outerPosition[x, y], new Vector3(1, 0, 0),
                            _middlePosition[x, y], new Vector3(1, .5f, .5f),

                            _outerPosition[x + 1, y + 1], new Vector3(1, 1, 1),
                            _outerPosition[x + 1, y], new Vector3(1, 1, 0),
                            _middlePosition[x, y], new Vector3(1, .5f, .5f),

                            _outerPosition[x, y + 1], new Vector3(1, 0, 1),
                            _outerPosition[x + 1, y + 1], new Vector3(1, 1, 1),
                            _middlePosition[x, y], new Vector3(1, .5f, .5f),

                            _outerPosition[x, y], new Vector3(1, 0, 0),
                            _outerPosition[x, y + 1], new Vector3(1, 0, 1),
                            _middlePosition[x, y], new Vector3(1, .5f, .5f)
                        });
                    }
                }

                return vertices.ToArray();
            }
        }

        public Vector4[] TerrainVertices
        {
            get {
                var vertices = new List<Vector4>();

                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        vertices.AddRange(new[ ]
                        {
                            new Vector4(_outerPosition[x + 1, y], 1f), color,
                            new Vector4(_outerPosition[x, y], 1f), color,
                            new Vector4(_middlePosition[x, y], 1f), color,

                            new Vector4(_outerPosition[x + 1, y + 1], 1f), color2,
                            new Vector4(_outerPosition[x + 1, y], 1f), color2,
                            new Vector4(_middlePosition[x, y], 1f), color2,

                            
                            new Vector4(_outerPosition[x, y + 1], 1f), color,
                            new Vector4(_outerPosition[x + 1, y + 1], 1f), color,
                            new Vector4(_middlePosition[x, y], 1f), color,
                            
                            new Vector4(_outerPosition[x, y], 1f), color2,
                            new Vector4(_outerPosition[x, y + 1], 1f), color2,
                            new Vector4(_middlePosition[x, y], 1f), color2,

                            /*new Vector4(_outerPosition[x, y], 1f), color,
                            new Vector4(_outerPosition[x + 1, y], 1f), color,
                            new Vector4(_outerPosition[x + 1, y + 1], 1f), color,
                            new Vector4(_outerPosition[x, y + 1], 1f), color,
                            new Vector4(_middlePosition[x, y], 1f), color2,*/
                        });
                    }
                }

                return vertices.ToArray();
            }
        }

        public int[ ] TerrainIndices
        {
            get
            {
                List<int> indices = new List<int>();

                for (int y = 0; y < 8; y++)
                {
                    for (int x = 0; x < 8; x++)
                    {
                        indices.AddRange(new[ ]
                        {
                            x, x + 1, x + 4,
                            x + 1, x + 2, x + 4,
                            x + 2, x + 3, x + 4,
                            x + 3, x, x + 4
                        });
                    }
                    
                }

                return indices.ToArray();
            }
        }

        private Vector3[,] _outerPosition;
        private Vector3[,] _middlePosition;

        public MCNK(long offset, AdtInfo info)
            : base(offset, info)
        {
            info.File.Seek(offset + 8, SeekOrigin.Begin);
            var mcnkInfo = info.File.ReadStruct<MCNKInfo>();
            MCVT hora = new MCVT(mcnkInfo.HeightOffset + Position, info);

            _outerPosition = new Vector3[9,9];
            _middlePosition = new Vector3[8,8];

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    _outerPosition[x, y].X = x * 25 / 6f;
                    _outerPosition[x, y].Y = hora.Heights[(8 - y) * 17 + x];
                    _outerPosition[x, y].Z = y * 25 / 6f;
                    _outerPosition[x, y] += mcnkInfo.Position;
                    //_outerPosition[x, y].Y = 0;
                }
            }

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    _middlePosition[x, y].X = x * 25 / 6f + 25 / 12f;
                    _middlePosition[x, y].Y = hora.Heights[(7 - y) * 17 + 9 + x];
                    _middlePosition[x, y].Z = y * 25 / 6f + 25 / 12f;
                    _middlePosition[x, y] += mcnkInfo.Position;
                    //_middlePosition[x, y].Y = 0;
                }
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MCNKInfo
    {
        public int flags;
        public int X;
        public int Y;
        public int nLayers;
        public int nDoodads;
        public int HeightOffset;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 20)]
        private int[ ] pad;
        private Vector3 _position;

        public Vector3 Position
        {
            get { return new Vector3(-_position.Y, _position.Z, _position.X); }
        }
    }

    internal class MCVT : AdtChunk
    {
        public float[] Heights = new float[145];

        public MCVT(long offset, AdtInfo info) : base(offset, info)
        {
            info.File.Seek(offset+8, SeekOrigin.Begin);

            for (int i = 0; i < 145; i++)
            {
                Heights[i] = info.File.ReadSingle();
            }
        }
    }

    class AdtChunk
    {
        public long Position;
        protected AdtInfo _info;

        public AdtChunk(long offset, AdtInfo info)
        {
            _info = info;
            Position = offset;
        }
    }

    class MCIN
    {
        private long _offset;
        private MCINInfo _info = new MCINInfo();
        private AdtInfo _adtInfo;

        public MCIN(long offset, AdtInfo info)
        {
            _offset = offset;
            info.File.Seek(offset+8, SeekOrigin.Begin);

            _adtInfo = info;

            _info = info.File.ReadStruct<MCINInfo>();
        }

        public MCNK this[int x, int y]
        {
            get { return new MCNK(_info.Entries[y * 16 + x].Offset, _adtInfo); }
        }

    }

    

    [StructLayout(LayoutKind.Sequential)]
    internal struct ChunkHeader
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        public char[] Id;
        public int Size;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MCINInfo
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
        public MCINEntry[] Entries;
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MCINEntry
    {
        public uint Offset;
        public uint Size;
        public uint Flags;
        public uint Id;
    }
}