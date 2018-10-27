// WaterRenderer.cs
// 
//  Modified MIT License (MIT)
//  
//  Copyright (c) 2015 Completely Fair Games Ltd.
// 
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
// 
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
// 
// The following content pieces are considered PROPRIETARY and may not be used
// in any derivative works, commercial or non commercial, without explicit 
// written permission from Completely Fair Games:
// 
// * Images (sprites, textures, etc.)
// * 3D Models
// * Sound Effects
// * Music
// 
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading;
using DwarfCorp.GameStates;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;

namespace DwarfCorp
{

    /// <summary>
    /// Handles the drawing routines for liquids.
    /// </summary>
    public class WaterRenderer : IDisposable
    {
        private RenderTarget2D reflectionRenderTarget = null;
        public Texture2D ReflectionMap = null;
        public Texture2D ShoreMap = null;

        public Dictionary<LiquidType, LiquidAsset> LiquidAssets = new Dictionary<LiquidType, LiquidAsset>();


        public bool DrawTerrainReflected
        {
            get { return GameSettings.Default.DrawChunksReflected; }
            set { GameSettings.Default.DrawChunksReflected = value; }
        }

        public bool DrawComponentsReflected
        {
            get { return GameSettings.Default.DrawEntityReflected; }
            set { GameSettings.Default.DrawEntityReflected = value; }
        }

        public bool DrawSkyReflected
        {
            get { return GameSettings.Default.DrawSkyReflected; }
            set { GameSettings.Default.DrawSkyReflected = value; }
        }

        public bool DrawReflections
        {
            get { return DrawSkyReflected || DrawTerrainReflected || DrawComponentsReflected; }
        }


        public void AddLiquidAsset(LiquidAsset asset)
        {
            LiquidAssets[asset.Type] = asset;
        }

        public WaterRenderer(GraphicsDevice device)
        {

        }
        
        public void CreateContent(GraphicsDevice device)
        {
            LiquidAssets.Clear();
            PresentationParameters pp = device.PresentationParameters;

            int width = Math.Min(pp.BackBufferWidth / 4, 4096);
            int height = Math.Min(pp.BackBufferHeight / 4, 4096);
            ReflectionMap = new Texture2D(device, width, height);
            reflectionRenderTarget = new RenderTarget2D(device, width, height, false, pp.BackBufferFormat, pp.DepthStencilFormat);
            ShoreMap = AssetManager.GetContentTexture(ContentPaths.Gradients.shoregradient);

            LiquidAsset waterAsset = new LiquidAsset
            {
                Type = LiquidType.Water,
                Opactiy = 0.8f,
                Reflection = 1.0f,
                WaveHeight = 0.1f,
                WaveLength = 0.05f,
                WindForce = 0.001f,
                BumpTexture = AssetManager.GetContentTexture(ContentPaths.Terrain.water_normal),
                BaseTexture = AssetManager.GetContentTexture(ContentPaths.Terrain.cartoon_water),
                MinOpacity = 0.4f,
                RippleColor = new Vector4(0.6f, 0.6f, 0.6f, 0.0f),
                FlatColor = new Vector4(0.3f, 0.3f, 0.9f, 1.0f)
            };
            AddLiquidAsset(waterAsset);


            LiquidAsset lavaAsset = new LiquidAsset
            {
                Type = LiquidType.Lava,
                Opactiy = 0.95f,
                Reflection = 0.0f,
                WaveHeight = 0.1f,
                WaveLength = 0.05f,
                WindForce = 0.001f,
                MinOpacity = 0.8f,
                BumpTexture = AssetManager.GetContentTexture(ContentPaths.Terrain.water_normal),
                BaseTexture = AssetManager.GetContentTexture(ContentPaths.Terrain.lava),
                RippleColor = new Vector4(0.5f, 0.4f, 0.04f, 0.0f),
                FlatColor = new Vector4(0.9f, 0.7f, 0.2f, 1.0f)
            };

            AddLiquidAsset(lavaAsset);
        }

        public Plane CreatePlane(float height, Vector3 planeNormalDirection, Matrix currentViewMatrix, bool clipSide)
        {
            planeNormalDirection.Normalize();
            Vector4 planeCoeffs = new Vector4(planeNormalDirection, height);
            if(clipSide)
            {
                planeCoeffs *= -1;
            }
            Plane finalPlane = new Plane(planeCoeffs);
            return finalPlane;
        }

        public static float GetTotalWaterHeightCells(VoxelHandle vox)
        {
            float tot = 0;

            var localVoxelCoordinate = vox.Coordinate.GetLocalVoxelCoordinate();
            for (var y = vox.Coordinate.Y; y < VoxelConstants.ChunkSizeY; y++)
            {
                var v = new VoxelHandle(vox.Chunk, new LocalVoxelCoordinate(
                    localVoxelCoordinate.X, y, localVoxelCoordinate.Z));
                tot += v.LiquidLevel / (float)WaterManager.maxWaterLevel;
                if (y > vox.Coordinate.Y && v.LiquidLevel == 0)
                    return tot;
            }

            return tot;
        }

        public float GetVisibleWaterHeight(ChunkManager chunkManager, Camera camera, Viewport port, float defaultHeight)
        {
            var vox = VoxelHelpers.FindFirstVisibleVoxelOnScreenRay(chunkManager.ChunkData, port.Width / 2, port.Height / 2, camera, port, 100.0f, false, null);

            if(vox.IsValid)
            {
                float h = GetTotalWaterHeightCells(vox) - 0.75f;
                if(h < 0.01f)
                    return defaultHeight;

                return (h + vox.Coordinate.Y + defaultHeight) / 2.0f + 0.5f;
            }
            else
            {
                return defaultHeight;
            }
        }
        private Timer reflectionTimer = new Timer(0.1f, false, Timer.TimerMode.Real);
        private Vector3 prevCameraPos = Vector3.Zero;
        private Vector3 prevCameraTarget = Vector3.Zero;

        public void DrawReflectionMap(IEnumerable<Body> Renderables, DwarfTime gameTime, WorldManager game, float waterHeight, Matrix reflectionViewMatrix, Shader effect, GraphicsDevice device)
        {
            if (!DrawReflections) return;
            ValidateBuffers();
            reflectionTimer.Update(gameTime);
            if (!reflectionTimer.HasTriggered && (prevCameraPos - game.Camera.Position).LengthSquared() < 0.001 && (prevCameraTarget - game.Camera.Target).LengthSquared() < 0.001)
                return;

            prevCameraPos = game.Camera.Position;
            prevCameraTarget = game.Camera.Target;

            Plane reflectionPlane = CreatePlane(waterHeight, new Vector3(0, -1, 0), reflectionViewMatrix, true);

            effect.ClipPlane = new Vector4(reflectionPlane.Normal, reflectionPlane.D);
            effect.ClippingEnabled = true;
            effect.GhostClippingEnabled = false;
            device.SetRenderTarget(reflectionRenderTarget);


            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer, Color.CornflowerBlue, 1.0f, 0);
            effect.View = reflectionViewMatrix;

            //game.DrawSky();

            if(DrawTerrainReflected)
            {
                game.DrawSky(gameTime, reflectionViewMatrix, 0.25f, effect.FogColor, false);
                game.Draw3DThings(gameTime, effect, reflectionViewMatrix);
            }
            else
            {
                game.DrawSky(gameTime, reflectionViewMatrix, 0.25f, effect.FogColor, false);
            }

            effect.View = reflectionViewMatrix;
            //Drawer3D.Render(device, effect, false);

            if(DrawComponentsReflected)
            {
                effect.View = reflectionViewMatrix;
                ComponentRenderer.Render(Renderables, gameTime, game.ChunkManager, game.Camera,
                    DwarfGame.SpriteBatch, game.GraphicsDevice, effect,
                    ComponentRenderer.WaterRenderType.Reflective, waterHeight);
                game.InstanceRenderer.Flush(device, effect, game.Camera, InstanceRenderMode.Normal);
            }

            effect.ClippingEnabled = false;
            device.SetRenderTarget(null);

            ReflectionMap = reflectionRenderTarget;
        }

        public void ValidateBuffers()
        {
            if (reflectionRenderTarget == null || reflectionRenderTarget.IsContentLost || reflectionRenderTarget.IsContentLost ||
                ShoreMap == null || ShoreMap.IsDisposed || ShoreMap.GraphicsDevice.IsDisposed)
            {
                CreateContent(GameState.Game.GraphicsDevice);
            }
        }

        public void DrawWaterFlat(GraphicsDevice device, Matrix view, Matrix projection, Shader effect, ChunkManager chunks)
        {
            ValidateBuffers();
            try // Release day hack fix. Just eat any exceptions in case the fix applied to LiquidPrimitive 
            // doesn't actually work.
            {
                effect.CurrentTechnique = effect.Techniques[Shader.Technique.WaterFlat];
                Matrix worldMatrix = Matrix.Identity;
                effect.World = worldMatrix;
                effect.View = view;
                effect.Projection = projection;

                foreach (KeyValuePair<LiquidType, LiquidAsset> asset in LiquidAssets)
                {
                    effect.FlatWaterColor = new Color(asset.Value.FlatColor);

                    foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        foreach (var c in chunks.ChunkData.GetChunkEnumerator())
                            c.Liquids[asset.Key].Render(device);
                    }
                }
            }
            catch (Exception) { }
        }
        

        public void DrawWater(GraphicsDevice device,
            float time,
            Shader effect,
            Matrix viewMatrix,
            Matrix reflectionViewMatrix,
            Matrix projectionMatrix,
            Vector3 windDirection,
            Camera camera,
            ChunkManager chunks)
        {
            ValidateBuffers();
            if (DrawReflections)
            {
                effect.CurrentTechnique = effect.Techniques[Shader.Technique.Water];
            }
            else
            {
                effect.CurrentTechnique = effect.Techniques[Shader.Technique.WaterTextured];
            }

            BlendState origState = device.BlendState;
            DepthStencilState origDepthState = device.DepthStencilState;
            device.DepthStencilState = DepthStencilState.Default;

            device.BlendState = BlendState.NonPremultiplied;
           

            Matrix worldMatrix = Matrix.Identity;
            effect.World = worldMatrix;
            effect.View = viewMatrix;
            effect.CameraPosition = camera.Position;
            if (DrawReflections)
            {
                effect.ReflectionView = reflectionViewMatrix;
            }

            effect.Projection = projectionMatrix;

            if (DrawReflections)
                effect.WaterReflectionMap = ReflectionMap;

            effect.WaterShoreGradient = ShoreMap;
            effect.Time = time;
            effect.WindDirection = windDirection;
            effect.CameraPosition = camera.Position;
            

            foreach (KeyValuePair<LiquidType, LiquidAsset> asset in LiquidAssets)
            {
                
                effect.WaveLength = asset.Value.WaveLength;
                effect.WaveHeight = asset.Value.WaveHeight;
                if (DrawReflections)
                {
                    effect.WaterBumpMap = asset.Value.BumpTexture;
                    effect.WaterReflectance = asset.Value.Reflection;
                }
                effect.MainTexture = asset.Value.BaseTexture;
                effect.WaterOpacity = asset.Value.Opactiy;
                effect.MinWaterOpacity = asset.Value.MinOpacity; 
                effect.RippleColor = new Color(asset.Value.RippleColor);


                foreach (EffectPass pass in effect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    foreach (var chunk in chunks.World.ChunkRenderer.RenderList)
                        chunk.Liquids[asset.Key].Render(device);
                }
            }
            device.BlendState = origState;
            device.DepthStencilState = origDepthState;
        }

        public void Dispose()
        {
            reflectionRenderTarget.Dispose();
            ReflectionMap.Dispose();
        }
    }

}
