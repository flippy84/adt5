﻿using System.Collections;
using System.Drawing.Imaging;
using System.IO;
using SharpDX;
//using SharpDX.DXGI;
using System.Runtime.InteropServices;
using System.Text;
using System.Linq;
using System;
using System.Collections.Generic;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Int2 = SharpDX.DrawingPoint;
using SharpDX.Direct3D11;
using Resource = SharpDX.Direct3D11.Resource;
using SharpDX.Toolkit.Graphics;
using Texture2D = SharpDX.Direct3D11.Texture2D;
using SamplerState = SharpDX.Direct3D11.SamplerState;
using Buffer = SharpDX.Direct3D11.Buffer;
using MapFlags = SharpDX.Direct3D11.MapFlags;

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

        public Vector4[] TerrainVerticesTextured
        {
            get
            {
                var vertices = new List<Vector4>();

                foreach (var mcnk in hora.Mcnk)
                {
                    vertices.AddRange(mcnk.TerrainVerticesTextured);
                }

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

        public AdtFile hora;
    }

    struct AdtInfo
    {
        public MpqFile File;
        public int X;
        public int Y;
        public Device Device;
        public ShaderResourceView[] Textures;
        public List<int> Doodads;
        public List<int> Wmos;
    }

    class Model
    {
        private static Buffer _vertexBuffer;
        private static Buffer _indexBuffer;
        private int index;
        private int count;
        private static long vertexOffset;

        public Model(Device device, Vector4[] vertices)
        {
            var context = device.ImmediateContext;
            if (_vertexBuffer == null)
            {
                _vertexBuffer = new Buffer(device, Utilities.SizeOf<Vector4>() * 2 * 100000, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, Utilities.SizeOf<Vector4>() * 2);
                context.InputAssembler.SetVertexBuffers(1, new VertexBufferBinding(_vertexBuffer, Utilities.SizeOf<Vector4>() * 2, 0));

                _indexBuffer = new Buffer(device, Utilities.SizeOf<short>(), ResourceUsage.Dynamic, BindFlags.IndexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, Utilities.SizeOf<short>());
                context.InputAssembler.SetIndexBuffer(_indexBuffer, Format.R16_UInt, 0);
            }

            DataStream stream;
            context.MapSubresource(_vertexBuffer, MapMode.WriteNoOverwrite, MapFlags.None, out stream);

            stream.Seek(vertexOffset, SeekOrigin.Begin);
            
            count = vertices.Length / 2;
            index = (int) stream.Position / 32;

            stream.WriteRange(vertices);

            vertexOffset = stream.Position;

            context.UnmapSubresource(_vertexBuffer, 0);
        }

        public void Render(Device device)
        {
            var context = device.ImmediateContext;

            context.Draw(count, index);
        }

        public static Vector4[] Parse(string s, Vector3 position, Vector3 rotation, float scale)
        {
            float d = (float)(Math.PI / 180);

            Matrix m = Matrix.Identity;
            m *= Matrix.RotationX(rotation.X * d);
            m *= Matrix.RotationY(-rotation.Y * d);
            m *= Matrix.RotationZ(rotation.Z * d);

            return Parse(s, position, m, scale);
        }

        public static Vector4[] Parse(string s, Vector3 position, Matrix rotation, float scale)
        {
            var vertices = new List<Vector4>();
            var color = new[]
            {
                new Color4(1, 0, 0, 1),
                new Color4(.8f, 0, 0, 1)
            };

            var file = new MpqFile(MpqArchive.Open(s));
            var header = file.ReadStruct<M2Header>();

            if(header.magic != "MD20")
                throw new NotSupportedException();

            if(header.version != 264)
                throw new NotSupportedException();

            if (header.numBoundingVertices == 0)
                return vertices.ToArray();

            var indices = new short[header.numBoundingTriangles];
            file.Seek(header.offsetBoundingTriangles, SeekOrigin.Begin);

            for (int i = 0; i < indices.Length; i++)
            {
                indices[i] = file.ReadInt16();
            }

            var vertices2 = new Vector4[header.numBoundingVertices];
            file.Seek(header.offsetBoundingVertices, SeekOrigin.Begin);
            float[] tmp = new float[3];

            var m = Matrix.Identity;
            m *= rotation;
            m *= Matrix.Scaling(scale);
            m *= Matrix.Translation(position);

            Vector4 pos = new Vector4(position, 0);

            for (int i = 0; i < vertices2.Length; i++)
            {
                tmp[0] = file.ReadSingle();
                tmp[1] = file.ReadSingle();
                tmp[2] = file.ReadSingle();

                vertices2[i] = new Vector4(tmp[1], tmp[2], -tmp[0], 1);
                vertices2[i] = Vector4.Transform(vertices2[i], m);
            }

            for (int i = 0; i < indices.Length; i += 3)
            {
                vertices.AddRange(new[]
                {
                    vertices2[indices[i + 2]], color[i / 3 % 2].ToVector4(),
                    vertices2[indices[i + 1]], color[i / 3 % 2].ToVector4(),
                    vertices2[indices[i]], color[i / 3 % 2].ToVector4(),
                });
            }

            return vertices.ToArray();
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    struct M2Header
    {
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 4)]
        private char[] _magic;
        public uint version;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 0xD0)]
        private byte[] pad;

        public uint numBoundingTriangles;
        public uint offsetBoundingTriangles;
        public uint numBoundingVertices;
        public uint offsetBoundingVertices;

        public string magic
        {
            get { return new string(_magic, 0, 4); }
        }
    }

    internal class AdtFile
    {
        public MpqFile File;
        public MCNK[,] Mcnk = new MCNK[16,16];
        private AdtInfo _info;
        public List<Model> adtmodels = new List<Model>();
        public List<Wmo> wmo_models = new List<Wmo>(); 

        private byte[,] alphamap = new byte[8,64 * 64 * 16 * 16];

        private void ByteArrayToTexture(Device device, byte[ ] alpha)
        {
            var dds = new DDSHeader
            {
                Size = 124,
                Flags = DDSD.Caps | DDSD.Height | DDSD.Width | DDSD.PixelFormat,
                Caps = DDSCAPS.Texture,
                Height = 1024,
                Width = 1024,
                PixelFormat = new DDSPixelFormat
                {
                    Size = 32,
                    Flags = DDPF.Rgb,
                    RGBBitCount = 24,
                    RBitMask = 0x00FF0000,
                    GBitMask = 0x0000FF00,
                    BBitMask = 0x000000FF
                }
            };

            //var jude = BitConverter.ToInt32(new [ ] { (byte) 'D', (byte) 'X', (byte) '1', (byte) '0' }, 0);

            var file = new BinaryWriter(new MemoryStream());

            var buffer = new byte[Marshal.SizeOf(dds)];
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(dds, h.AddrOfPinnedObject(), false);
            h.Free();

            file.Write((int)Magic.DDS);
            file.Write(buffer);

            /*buffer = new byte[Marshal.SizeOf(dds2)];
            h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(dds2, h.AddrOfPinnedObject(),false);
            h.Free();

            file.Write(buffer);*/

            for (int i = 0; i < alpha.Length; i++)
            {
                var b = alpha[i];
                file.Write(b);
            }

            file.Seek(0, SeekOrigin.Begin);

            var bytes = new BinaryReader(file.BaseStream).ReadBytes((int)file.BaseStream.Length);

            var hora = new FileStream("kuk.dds", FileMode.Create);
            hora.Write(bytes, 0, bytes.Length);
            hora.Close();

            var resource = Resource.FromMemory<Texture2D>(device, bytes);

            device.ImmediateContext.PixelShader.SetShaderResource(4, new ShaderResourceView(device, resource));
        }

        public AdtFile(Int2 pos, string map, BoundingBox bbox, Device device)
        {
            File = new MpqFile(MpqArchive.Open(string.Format(@"World\Maps\{0}\{0}_{1}_{2}.adt", map, pos.X, pos.Y)));

            _info = new AdtInfo
            {
                File = File,
                X = pos.X,
                Y = pos.Y,
                Device = device,
                Doodads = new List<int>(),
                Wmos = new List<int>()
            };

            var mtex = new MTEX(GetChunkPosition("MTEX"), _info);
            _info.Textures = mtex.Textures;

            var mcin = new MCIN(GetChunkPosition("MCIN"), _info);

            byte[ ] alpha = new byte[4096 * 3 * 16 * 16];

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    Mcnk[x, y] = mcin[x, y];
                }
            }

            for (int y = 0; y < 1024; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    var kuk = Mcnk[x, 15 - (y / 64)].mcal.alpha2;
                    Array.Copy(kuk, (y % 64) * 64 * 3, alpha, (y * 1024 + x * 64) * 3, 64 * 3);
                }
            }

            for (int y = 0; y < 16; y++)
            {
                for (int x = 0; x < 16; x++)
                {
                    for (int i = 1; i < Mcnk[x,y].mcly.NumLayers; i++)
                    {
                        var index = Mcnk[x, y].mcly.Layers[i].TextureId;
                        var kuk = Mcnk[x, y].mcal.alpha[i - 1];
                    }
                }
            }

            ByteArrayToTexture(device, alpha);

            var sampler = new SamplerState(device, new SamplerStateDescription
            {
                Filter = Filter.MinMagMipPoint,
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

            //Models

            _info.Doodads = _info.Doodads.Distinct().OrderBy(x => x).ToList();
            _info.Wmos = _info.Wmos.OrderBy(wmo => wmo).Distinct().ToList();

            var models = new MDDF(GetChunkPosition("MDDF"), _info);

            _info.File.Seek(GetChunkPosition("MMDX"), SeekOrigin.Begin);
            var header = _info.File.ReadStruct<ChunkHeader>();
            var files = _info.File.ReadString(header.Size - 1).Split(new[] { '\0' });

            foreach (var doodad in _info.Doodads)
            {
                var model = models[doodad];
                var vertices = Model.Parse(files[model.mmidEntry], model.position, model.rotation, model.Scale);
                if (vertices.Any() == false)
                    continue;
                adtmodels.Add(new Model(device, vertices));
            }

            var wmos = new MODF(GetChunkPosition("MODF"), _info);

            _info.File.Seek(GetChunkPosition("MWMO"), SeekOrigin.Begin);
            header = _info.File.ReadStruct<ChunkHeader>();
            files = _info.File.ReadString(header.Size - 1).Split(new[] { '\0' });

            foreach (var wmo in _info.Wmos)
            {
                var kuk = wmos[wmo];
                var vertices = Wmo.Parse(files[wmo], kuk.Position, kuk.Rotation);
                if (vertices.Any() == false)
                    continue;
                wmo_models.Add(new Wmo(device, vertices));
            }
        }

        class MODF : AdtChunk
        {
            private MODFEntry[] _entries;
            public MODF(long position, AdtInfo info) : base(position, info)
            {
                info.File.Seek(position, SeekOrigin.Begin);

                var header = info.File.ReadStruct<ChunkHeader>();
                var num = header.Size / Utilities.SizeOf<MODFEntry>();

                _entries = new MODFEntry[num];

                for (int i = 0; i < num; i++)
                {
                    _entries[i] = info.File.ReadStruct<MODFEntry>();
                }
            }

            public MODFEntry this[int i]
            {
                get { return _entries[i]; }
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        struct MODFEntry
        {
            private int mwidEntry;
            private int uniqueId;
            private Vector3 _position;
            private Vector3 _rotation;
            private Vector3 lowerBounds;
            private Vector3 upperBounds;
            private short flags;
            private short doodadSet;
            private short nameSet;
            private short padding;

            public Vector3 Position
            {
                get
                {
                    float d = 1600 * 32 / 3f;
                    Vector3 v = new Vector3(_position.X - d, _position.Y, -(_position.Z - d));
                    return v;
                }
            }

            public Vector3 Rotation
            {
                get { return _rotation; }
            }
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

    public class Wmo
    {
        private static Buffer _vertexBuffer;
        private int count;
        private int index;
        private static long vertexOffset;

        public Wmo(Device device, Vector4[] vertices)
        {
            var context = device.ImmediateContext;
            if (_vertexBuffer == null)
            {
                _vertexBuffer = new Buffer(device, Utilities.SizeOf<Vector4>() * 2 * 10000000, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, Utilities.SizeOf<Vector4>() * 2);
                context.InputAssembler.SetVertexBuffers(2, new VertexBufferBinding(_vertexBuffer, Utilities.SizeOf<Vector4>() * 2, 0));
            }

            DataStream stream;
            context.MapSubresource(_vertexBuffer, MapMode.WriteNoOverwrite, MapFlags.None, out stream);

            stream.Seek(vertexOffset, SeekOrigin.Begin);

            count = vertices.Length / 2;
            index = (int)stream.Position / 32;

            stream.WriteRange(vertices);

            vertexOffset = stream.Position;

            context.UnmapSubresource(_vertexBuffer, 0);
        }

        public void Render(Device device)
        {
            var context = device.ImmediateContext;

            context.Draw(count, index);
        }

        public static Vector4[] Parse(string s, Vector3 position, Vector3 rotation)
        {
            List<Vector4> vertices = new List<Vector4>();

            var kuk = new RootWmo(s);
            foreach (string hej in kuk.GroupFiles)
            {
                vertices.AddRange(GroupWmo.Parse(hej, position, rotation));
            }

            return vertices.ToArray();
        }

        private class RootWmo
        {
            public string[] GroupFiles;

            public RootWmo(string s)
            {
                var file = new MpqFile(MpqArchive.Open(s));
                file.Seek(file.GetChunkPosition("MOHD"), SeekOrigin.Begin);
                var header = file.ReadStruct<ChunkHeader>();
                var mohd = file.ReadStruct<MOHD>();

                var root = s.Split(new[] { '.' })[0];

                GroupFiles = new string[mohd.nGroups];
                for (int i = 0; i < mohd.nGroups; i++)
                {
                    GroupFiles[i] = string.Format("{0}_{1:000}.WMO", root, i);
                }

                /*foreach (var group in GroupFiles)
                {
                    GroupWmo.Parse(group);
                }*/
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct MOHD
        {
            private int nMaterials;
            public int nGroups;
            private int nPortals;
            private int nLights;
            private int nModels;
            private int nDoodads;
            private int nSets;
            private int ambient_color;
            private int WMO_ID;
            public Vector3 upperBounds;
            public Vector3 lowerBounds;
            private int unknown;
        }

        private class GroupWmo
        {
            /*public GroupWmo(string s)
            {

            }*/

            public static Vector4[] Parse(string s, Vector3 position, Vector3 rotation)
            {
                List<Vector4> vertices = new List<Vector4>();
                var color = new[]
                {
                    new Color4(1, 1, 0, 1),
                    new Color4(.8f, .8f, 0, 1)
                };
                var file = new MpqFile(MpqArchive.Open(s));

                var offset = file.GetChunkPosition("MOGP");
                offset += 0x4C;
                file.Seek(file.GetChunkPosition("MOVT", offset), SeekOrigin.Begin);
                var header = file.ReadStruct<ChunkHeader>();

                var num = header.Size / Vector3.SizeInBytes;
                float[] tmp = new float[3];
                Vector4[] vertices2 = new Vector4[num];

                for (int i = 0; i < num; i++)
                {
                    tmp[0] = file.ReadSingle();
                    tmp[1] = file.ReadSingle();
                    tmp[2] = file.ReadSingle();

                    vertices2[i] = new Vector4(tmp[1], tmp[2], -tmp[0], 1);
                }

                Matrix m = Matrix.Identity;
                float d = (float)(Math.PI / 180);
                m *= Matrix.RotationX(rotation.X * d);
                m *= Matrix.RotationY(-rotation.Y * d);
                m *= Matrix.RotationZ(rotation.Z * d);
                m *= Matrix.Translation(position);

                Vector4.Transform(vertices2, ref m, vertices2);

                file.Seek(file.GetChunkPosition("MOVI", offset), SeekOrigin.Begin);
                header = file.ReadStruct<ChunkHeader>();
                num = header.Size / sizeof(short);

                short[] indices = new short[num];

                for (int i = 0; i < num; i++)
                {
                    indices[i] = file.ReadInt16();
                }

                for (int i = 0; i < num; i += 3)
                {
                    vertices.AddRange(new[]
                    {
                        vertices2[indices[i + 2]], color[i / 3 % 2].ToVector4(),
                        vertices2[indices[i + 1]], color[i / 3 % 2].ToVector4(),
                        vertices2[indices[i]], color[i / 3 % 2].ToVector4()
                    });
                }

                return vertices.ToArray();
            }
        }
    }

    internal class MDDF : AdtChunk, IEnumerable<MDDFEntry>
    {
        private readonly MDDFEntry[] _entries;

        public MDDF(long position, AdtInfo info)
            : base(position, info)
        {
            info.File.Seek(position, SeekOrigin.Begin);

            var header = info.File.ReadStruct<ChunkHeader>();
            var num = header.Size / Marshal.SizeOf(new MDDFEntry());
            _entries = new MDDFEntry[num];
            Count = num;

            for (int i = 0; i < num; i++)
            {
                _entries[i] = info.File.ReadStruct<MDDFEntry>();
            }
        }

        public int Count { get; private set; }

        public MDDFEntry this[int i]
        {
            get { return _entries[i]; }
        }

        #region Enumerator

        IEnumerator IEnumerable.GetEnumerator()
        {
            return GetEnumerator();
        }

        public IEnumerator<MDDFEntry> GetEnumerator()
        {
            return new MDDFEnum(_entries.ToList());
        }

        internal class MDDFEnum : IEnumerator<MDDFEntry>
        {
            private readonly MDDFEntry[] _entries;
            private int _position = -1;

            public MDDFEnum(List<MDDFEntry> entries)
            {
                _entries = entries.ToArray();
            }

            public bool MoveNext()
            {
                _position++;
                return _position < _entries.Length;
            }

            public void Reset()
            {
                _position = -1;
            }

            object IEnumerator.Current
            {
                get { return Current; }
            }

            MDDFEntry IEnumerator<MDDFEntry>.Current
            {
                get { return Current; }
            }

            public MDDFEntry Current
            {
                get { return _entries[_position]; }
            }

            public void Dispose()
            {
            }
        }

        #endregion
    }

    [StructLayout(LayoutKind.Sequential)]
    struct MDDFEntry
    {
        public int mmidEntry;
        public int uniqueId;
        private Vector3 _position;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        private float[] _rotation;
        private short _scale;
        public short flags;

        public Vector3 position
        {
            get
            {
                float step = 1600 / 3f * 32;
                var ret = new Vector3(_position[0] - step, _position[1], -(_position[2] - step));
                return ret;
            }
        }

        public  Vector3 rotation
        {
            get
            {
                return new Vector3(_rotation);
            }
        }

        public float Scale { get { return _scale / 1024f; } }
    }

    internal class MCNK : AdtChunk
    {
        //public MCNK() { }

        private Vector4 color = new Vector4(0f, 1f, 0f, 1f);
        private Vector4 color2 = new Vector4(0f, .5f, 0f, 1f);
        private ShaderResourceView[ ] _textures;
        public MCLY mcly;
        private int vertexCount;

        public int StartIndex { get; set; }

        public void Render(Device device)
        {
            for (int i = 0; i < 4; i++)
            {
                device.ImmediateContext.PixelShader.SetShaderResource(i, _textures[i < mcly.NumLayers ? mcly.Layers[i].TextureId : 0]);
            }

            /*if(_hasAlphaMap)
                device.ImmediateContext.PixelShader.SetShaderResource(4, mcal.maps);*/

            device.ImmediateContext.Draw(vertexCount, StartIndex);
        }

        const float step = 0.0625f;

        #region Terrain vertices, textured or colored

        public Vector4[ ] TerrainVerticesTextured
        {
            get
            {
                var vertices = new List<Vector4>();
                var holes = mcnkInfo.Holes;

                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        if ((holes >> (y * 8 + x) & 1) == 1)
                        {
                            continue;
                        }

                        vertices.AddRange(new[ ]
                        {
                            new Vector4(_outerPosition[x + 1, y], 1), new Vector4(1, 0, _outerUV[x + 1, y].X, _outerUV[x + 1, y].Y),
                            new Vector4(_outerPosition[x, y], 1), new Vector4(0, 0, _outerUV[x, y].X, _outerUV[x, y].Y),
                            new Vector4(_middlePosition[x, y], 1), new Vector4(.5f, .5f, _middleUV[x, y].X, _middleUV[x, y].Y),

                            new Vector4(_outerPosition[x + 1, y + 1], 1), new Vector4(1, 1, _outerUV[x + 1, y + 1].X, _outerUV[x + 1, y + 1].Y),
                            new Vector4(_outerPosition[x + 1, y], 1), new Vector4(1, 0, _outerUV[x + 1, y].X, _outerUV[x + 1, y].Y),
                            new Vector4(_middlePosition[x, y], 1), new Vector4(.5f, .5f, _middleUV[x, y].X, _middleUV[x, y].Y),

                            new Vector4(_outerPosition[x, y + 1], 1), new Vector4(0, 1, _outerUV[x, y + 1].X, _outerUV[x, y + 1].Y),
                            new Vector4(_outerPosition[x + 1, y + 1], 1), new Vector4(1, 1, _outerUV[x + 1, y + 1].X, _outerUV[x + 1, y + 1].Y),
                            new Vector4(_middlePosition[x, y], 1), new Vector4(.5f, .5f, _middleUV[x, y].X, _middleUV[x, y].Y),

                            new Vector4(_outerPosition[x, y], 1), new Vector4(0, 0, _outerUV[x, y].X, _outerUV[x, y].Y),
                            new Vector4(_outerPosition[x, y + 1], 1), new Vector4(0, 1, _outerUV[x, y + 1].X, _outerUV[x, y + 1].Y),
                            new Vector4(_middlePosition[x, y], 1), new Vector4(.5f, .5f, _middleUV[x, y].X, _middleUV[x, y].Y)
                        });
                    }
                }

                vertexCount = vertices.Count / 2;

                if (vertexCount < 768)
                {
                    for (int i = 0; i < 768 - vertexCount; i++)
                    {
                        vertices.Add(Vector4.Zero);
                        vertices.Add(Vector4.Zero);
                    }
                }

                return vertices.ToArray();
            }
        }

        public Vector4[ ] TerrainVertices
        {
            get
            {
                var vertices = new List<Vector4>();
                var holes = mcnkInfo.Holes;

                for (var y = 0; y < 8; y++)
                {
                    for (var x = 0; x < 8; x++)
                    {
                        if ((holes >> (y * 8 + x) & 1) == 1)
                        {
                            continue;
                        }

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
                            new Vector4(_middlePosition[x, y], 1f), color2
                        });
                    }
                }

                vertexCount = vertices.Count / 2;

                if (vertexCount < 768)
                {
                    for (int i = 0; i < 768 - vertexCount; i++)
                    {
                        vertices.Add(Vector4.Zero);
                        vertices.Add(Vector4.Zero);
                    }
                }

                return vertices.ToArray();
            }
        }

        #endregion

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

        private Vector2[,] _outerUV;
        private Vector2[,] _middleUV;

        private MCNKInfo mcnkInfo;

        public MCAL mcal;

        private bool _hasAlphaMap = false;

        public MCNK(long offset, AdtInfo info)
            : base(offset, info)
        {
            info.File.Seek(offset + 8, SeekOrigin.Begin);
            mcnkInfo = info.File.ReadStruct<MCNKInfo>();
            MCVT hora = new MCVT(mcnkInfo.HeightOffset + Position, info);

            //var mcrf = new MCRF(mcnkInfo.RefOffset + Position, info);

            mcly = new MCLY(mcnkInfo.LayerOffset + Position, info);

            if (mcnkInfo.sizeAlpha - 8 != 0)
            {
                _hasAlphaMap = true;

                mcal = new MCAL(mcnkInfo.AlphaOffset + Position, info, mcly.Layers);
            }

            info.File.Seek(mcnkInfo.RefOffset + Position + 8, SeekOrigin.Begin);
            for (int i = 0; i < mcnkInfo.nDoodads; i++)
            {
                 info.Doodads.Add(info.File.ReadInt32());
            }

            for (int i = 0; i < mcnkInfo.nMapObjs; i++)
            {
                info.Wmos.Add(info.File.ReadInt32());
            }

            StartIndex = (mcnkInfo.X * 16 + mcnkInfo.Y) * 768;

            _textures = info.Textures;

            _outerPosition = new Vector3[9,9];
            _middlePosition = new Vector3[8,8];

            _outerUV = new Vector2[9, 9];
            _middleUV = new Vector2[8, 8];

            for (int y = 0; y < 9; y++)
            {
                for (int x = 0; x < 9; x++)
                {
                    _outerPosition[x, y].X = x * 25 / 6f;
                    _outerPosition[x, y].Y = hora.Heights[(8 - y) * 17 + x];
                    _outerPosition[x, y].Z = y * 25 / 6f - 1600 / 48f;
                    _outerPosition[x, y] += mcnkInfo.Position;

                    _outerUV[x, y].X = x * 1 / 128f + 1 / 16f * mcnkInfo.X;
                    _outerUV[x, y].Y = y * 1 / 128f + 1 / 16f * (15 - mcnkInfo.Y);
                }
            }

            for (int y = 0; y < 8; y++)
            {
                for (int x = 0; x < 8; x++)
                {
                    _middlePosition[x, y].X = x * 25 / 6f + 25 / 12f;
                    _middlePosition[x, y].Y = hora.Heights[(7 - y) * 17 + 9 + x];
                    _middlePosition[x, y].Z = y * 25 / 6f + 25 / 12f - 1600 / 48f;
                    _middlePosition[x, y] += mcnkInfo.Position;

                    _middleUV[x, y].X = (x) * 1 / 128f + 1 / 256f + 1 / 16f * mcnkInfo.X;
                    _middleUV[x, y].Y = (y) * 1 / 128f + 1 / 256f + 1 / 16f * (15 - mcnkInfo.Y);
                }
            }
        }
    }

    internal class MCRF : AdtChunk
    {
        public MCRF(long offset, AdtInfo info)
            : base(offset, info)
        {
        }
    }

    internal class MCAL : AdtChunk
    {
        public ShaderResourceView maps;
        public byte[ ][ ] alpha = { new byte[64 * 64], new byte[64 * 64], new byte[64 * 64] };
        public byte[] alpha2 = new byte[4096 * 3];

        public MCAL(long offset, AdtInfo info, MCLYLayer[] layers)
            : base(offset, info)
        {
            info.File.Seek(offset, SeekOrigin.Begin);
            var header = info.File.ReadStruct<ChunkHeader>();

            /*if(header.Size == 0)
                return;*/
            int numLayers = 0;

            foreach (var mclyLayer in layers)
            {
                if ((mclyLayer.Flags & 0x100) > 1)
                    numLayers++;
            }

            var size = header.Size / numLayers;
            
            for (int i = 0; i < numLayers; i++)
            {
                switch (size)
                {
                    case 2048:

                        byte b = 0;
                        for (int y = 0; y < 64; y++)
                        {
                            for (int x = 0; x < 64; x++)
                            {
                                if (x % 2 == 0)
                                {
                                    b = info.File.ReadBytes(1)[0];
                                    alpha[i][(63 - y) * 64 + x] = (byte)((b & 0xF) * 17);
                                }
                                else
                                {
                                    alpha[i][(63 - y) * 64 + x] = (byte)((b >> 4) * 17);
                                }
                            }
                        }

                        break;
                    case 4096:
                        throw new NotImplementedException();
                        //break;
                    default:
                        throw new NotImplementedException();
                        //break;
                }
            }

            DDSHeader dds = new DDSHeader
            {
                Size = 124,
                Flags = DDSD.Caps | DDSD.Height | DDSD.Width | DDSD.PixelFormat,
                Caps = DDSCAPS.Texture,
                Height = 64,
                Width = 64,
                PixelFormat = new DDSPixelFormat
                {
                    Size = 32,
                    Flags = DDPF.Rgb,
                    RGBBitCount = 24,
                    RBitMask = 0x00FF0000,
                    GBitMask = 0x0000FF00,
                    BBitMask = 0x000000FF
                }
            };

            var file = new BinaryWriter(new MemoryStream());

            var buffer = new byte[Marshal.SizeOf(dds)];
            GCHandle h = GCHandle.Alloc(buffer, GCHandleType.Pinned);
            Marshal.StructureToPtr(dds, h.AddrOfPinnedObject(), false);
            h.Free();

            file.Write((int)Magic.DDS);
            file.Write(buffer);

            for (int i = 0; i < 64 * 64; i++)
            {
                file.Write(new[ ] { alpha[0][i], alpha[1][i], alpha[2][i] });

                //alpha2[i * 3 + 3] = byte.MaxValue;
                alpha2[i * 3] = alpha[0][i];
                alpha2[i * 3 + 1] = alpha[1][i];
                alpha2[i * 3 + 2] = alpha[2][i];
            }

            file.Seek(0, SeekOrigin.Begin);

            var bytes = new BinaryReader(file.BaseStream).ReadBytes((int)file.BaseStream.Length);
            maps = new ShaderResourceView(info.Device, Resource.FromMemory<Texture2D>(info.Device, bytes));
        }

        /*private Texture2D ToTexture(Device device, float[ ] alpha)
        {
            return null;
        }*/
    }

    internal class MCLY : AdtChunk
    {
        public int NumLayers { get; private set; }
        public MCLYLayer[] Layers { get; private set; }

        public MCLY(long offset, AdtInfo info)
            : base(offset, info)
        {
            info.File.Seek(offset, SeekOrigin.Begin);
            var header = info.File.ReadStruct<ChunkHeader>();

            NumLayers = header.Size / 16;
            Layers = new MCLYLayer[NumLayers];

            for (int i = 0; i < NumLayers; i++)
            {
                Layers[i] = info.File.ReadStruct<MCLYLayer>();
            }
        }
    }

    [StructLayout(LayoutKind.Sequential)]
    internal struct MCLYLayer
    {
        public int TextureId;
        public int Flags;
        public int Offset;
        public int Effect;
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
        private int kuk;
        public int LayerOffset;
        public int RefOffset;
        public int AlphaOffset;
        public int sizeAlpha;
        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 3)]
        private int[] pad;
        public int nMapObjs;
        private int _holes;

        [MarshalAs(UnmanagedType.ByValArray, SizeConst = 10)]
        private int[ ] pad2;
        private Vector3 _position;

        public Vector3 Position
        {
            get { return new Vector3(-_position.Y, _position.Z, _position.X); }
        }

        public ulong Holes
        {
            get
            {
                ulong ret = 0;
                var holes = (ushort) (_holes & 0xFFFF);
                for (int y = 0; y < 4; y++)
                {
                    for (int x = 0; x < 4; x++)
                    {
                        if ((holes >> (y * 4 + x) & 1) == 1)
                        {
                            ret |= (ulong) 3 << ((3 - y) * 16 + x * 2);
                            ret |= (ulong) 3 << ((3 - y) * 16 + 8 + x * 2);
                        }
                    }
                }
                return ret;
            }
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