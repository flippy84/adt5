using System;
/*using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;*/
using System.Windows.Forms;
using System.Drawing;

using SharpDX;
using SharpDX.Direct3D;
using SharpDX.Direct3D11;
using SharpDX.Windows;
using SharpDX.DXGI;
using Device = SharpDX.Direct3D11.Device;
using Buffer = SharpDX.Direct3D11.Buffer;
using SharpDX.D3DCompiler;
using Resource = SharpDX.Direct3D11.Resource;
using Color = SharpDX.Color;

using System.IO;
using MapFlags = SharpDX.Direct3D11.MapFlags;

namespace adt5
{
    class Shader
    {
        public VertexShader VertexShader { get; private set; }
        public PixelShader PixelShader { get; private set; }
        public ShaderSignature Signature { get; private set; }
        public InputLayout Layout { get; private set; }

        public Shader(Device device, string shader, InputElement[] elements)
        {
            var vertexShaderByteCode = ShaderBytecode.CompileFromFile(shader, "VS", "vs_4_0");
            VertexShader = new VertexShader(device, vertexShaderByteCode);

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile(shader, "PS", "ps_4_0");
            PixelShader = new PixelShader(device, pixelShaderByteCode);

            Signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);

            Layout = new InputLayout(device, Signature, elements);
        }
    }

    static class Program
    {
        [STAThread]
        private static void Main()
        {
            var form = new RenderForm("Adt5")
            {
                Icon = SystemIcons.Application,
            };

            var desc = new SwapChainDescription
            {
                BufferCount = 1,
                ModeDescription = new ModeDescription(form.ClientSize.Width, form.ClientSize.Height, new Rational(60, 1), Format.R8G8B8A8_UNorm),
                IsWindowed = true,
                OutputHandle = form.Handle,
                SampleDescription = new SampleDescription(1, 0),
                SwapEffect = SwapEffect.Discard,
                Usage = Usage.RenderTargetOutput
            };

            Device device;
            SwapChain swapChain;
            Device.CreateWithSwapChain(DriverType.Hardware, DeviceCreationFlags.Debug, desc, out device, out swapChain);
            var context = device.ImmediateContext;

            var constantBuffer = new Buffer(device, Utilities.SizeOf<Matrix>(), ResourceUsage.Default, BindFlags.ConstantBuffer, CpuAccessFlags.None, ResourceOptionFlags.None, 0);

            var vertices = new Buffer(device, new BufferDescription(400000 * Utilities.SizeOf<Vector4>() * 2, ResourceUsage.Dynamic, BindFlags.VertexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, Utilities.SizeOf<Vector4>() * 2));

            var indices = new Buffer(device, new BufferDescription(400000 * sizeof (int), ResourceUsage.Dynamic, BindFlags.IndexBuffer, CpuAccessFlags.Write, ResourceOptionFlags.None, sizeof (int)));


            #region Shaders

            var shaders = new[ ]
            {
                new Shader(device, "Color.fx", new[ ]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                    new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
                }),
                new Shader(device, "Texture.fx", new[ ]
                {
                    new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                    new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 0)
                })
            };

            /*var vertexShaderByteCode = ShaderBytecode.CompileFromFile("Color.fx", "VS", "vs_4_0");
            var vertexShader = new VertexShader(device, vertexShaderByteCode);

            var pixelShaderByteCode = ShaderBytecode.CompileFromFile("Color.fx", "PS", "ps_4_0");
            var pixelShader = new PixelShader(device, pixelShaderByteCode);

            var signature = ShaderSignature.GetInputSignature(vertexShaderByteCode);

            var layout = new InputLayout(device, signature, new[ ]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 0),
                new InputElement("COLOR", 0, Format.R32G32B32A32_Float, 16, 0)
            });*/

            /*var texPixelShaderBC = new PixelShader(ShaderBytecode.CompileFromFile("Texture.fx", "PS", "ps_4_0");
            var texPixelShader = new PixelShader(device, texPixelShaderBC);
            var texSignature = ShaderSignature.GetInputSignature(ver)*/

            /*var texLayout = new InputLayout(device, signature, new[ ]
            {
                new InputElement("POSITION", 0, Format.R32G32B32A32_Float, 0, 1),
                new InputElement("TEXCOORD", 0, Format.R32G32_Float, 16, 1),
            });*/

            #endregion


            context.InputAssembler.InputLayout = shaders[1].Layout;
            //context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertices, Utilities.SizeOf<Vector4>() * 2, 0));
            context.InputAssembler.SetVertexBuffers(0, new VertexBufferBinding(vertices, Utilities.SizeOf<Vector4>() + Utilities.SizeOf<Vector2>(), 0));
            context.InputAssembler.SetIndexBuffer(indices, Format.R32_SInt, 0);
            context.InputAssembler.PrimitiveTopology = PrimitiveTopology.TriangleList;
            context.VertexShader.SetConstantBuffer(0, constantBuffer);
            context.VertexShader.Set(shaders[1].VertexShader);
            context.PixelShader.Set(shaders[1].PixelShader);

            DataStream vertexStream;
            context.MapSubresource(vertices, MapMode.WriteNoOverwrite, MapFlags.None, out vertexStream);

            var adt = new Adt(@"World\Maps\Azeroth\Azeroth_32_48.adt", device);

            //vertexStream.WriteRange(adt.TerrainVertices);
            vertexStream.WriteRange(adt.TerrainVerticesTextured);

            context.UnmapSubresource(vertices, 0);

            int pitch = 230;
            int yaw = 210;
            float distance = 400;

            form.KeyDown += (target, arg) =>
            {
                if (arg.KeyCode == Keys.Up)
                    yaw += 5;

                if (arg.KeyCode == Keys.Down)
                    yaw -= 5;

                if (arg.KeyCode == Keys.Left)
                    pitch += 5;

                if (arg.KeyCode == Keys.Right)
                    pitch -= 5;

                if (arg.KeyCode == Keys.PageDown)
                    distance += 10;

                if (arg.KeyCode == Keys.PageUp)
                    distance -= 10;

                distance = MathUtil.Clamp(distance, 100, 400);

                if (pitch < 0)
                    pitch += 360;

                pitch = pitch % 360;
                yaw = MathUtil.Clamp(yaw, 200, 260);
            };

            var origin = new Vector3((adt.X - 32) * 1600 / 3f + 1600 / 6f, 0, (32 - adt.Y) * 1600 / 3f - 1600 / 6f);

            Texture2D backBuffer = null;
            RenderTargetView renderView = null;
            Texture2D depthBuffer = null;
            DepthStencilView depthView = null;
            context.Rasterizer.SetViewports(new Viewport(0, 0, form.ClientSize.Width, form.ClientSize.Height));

            var resized = true;
            var proj = Matrix.Identity;

            form.UserResized += (target, arg) => resized = true;

            RenderLoop.Run(form, () =>
            {
                if (resized)
                {
                    ComObject.Dispose(ref depthBuffer);
                    ComObject.Dispose(ref depthView);
                    ComObject.Dispose(ref backBuffer);
                    ComObject.Dispose(ref renderView);

                    swapChain.ResizeBuffers(desc.BufferCount, form.ClientSize.Width, form.ClientSize.Height, Format.Unknown, SwapChainFlags.None);
                    backBuffer = Resource.FromSwapChain<Texture2D>(swapChain, 0);

                    renderView = new RenderTargetView(device, backBuffer);
                    context.OutputMerger.SetTargets(renderView);

                    depthBuffer = new Texture2D(device, new Texture2DDescription
                    {
                        Format = Format.D32_Float_S8X24_UInt,
                        ArraySize = 1,
                        MipLevels = 1,
                        Width = form.ClientSize.Width,
                        Height = form.ClientSize.Height,
                        SampleDescription = new SampleDescription(1, 0),
                        Usage = ResourceUsage.Default,
                        BindFlags = BindFlags.DepthStencil,
                        CpuAccessFlags = CpuAccessFlags.None,
                        OptionFlags = ResourceOptionFlags.None
                    });

                    depthView = new DepthStencilView(device, depthBuffer);
                    context.Rasterizer.SetViewports(new Viewport(0, 0, form.ClientSize.Width, form.ClientSize.Height));
                    proj = Matrix.PerspectiveFovLH((float)Math.PI / 2.0f, form.ClientSize.Width / (float)form.ClientSize.Height, 0.1f, 1000.0f);

                    resized = false;
                }

                var camera = origin;
                camera.X += distance;

                var q = Quaternion.RotationAxis(Vector3.UnitY, MathUtil.DegreesToRadians(pitch));

                var axis = Vector3.Transform(camera - origin, q);
                q = Quaternion.RotationAxis(Vector3.Cross(Vector3.UnitY, axis), MathUtil.DegreesToRadians(yaw));
                axis = Vector3.Transform(axis, q);

                var view = Matrix.LookAtLH(axis + origin, origin, Vector3.UnitY);
                var viewProj = Matrix.Multiply(view, proj);
                var worldViewProj = viewProj;
                worldViewProj.Transpose();
                context.UpdateSubresource(ref worldViewProj, constantBuffer);

                form.Text = "Origin: " + origin + ", Distance: " + (camera - origin).Length() + ", Pitch: " + pitch + ", Yaw: " + yaw;

                context.ClearRenderTargetView(renderView, Color4.Black);
                context.ClearDepthStencilView(depthView, DepthStencilClearFlags.Depth, 1.0f, 0);
                
                context.Draw(196608, 0);
                swapChain.Present(0, PresentFlags.None);
            });
        }
    }
}