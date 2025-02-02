﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Engine.Display3D
{
    class CTerrain : IRenderable
    {
        /// <summary>
        /// Variables
        /// </summary>

        // Vertexes
        public VertexPositionNormalTexture[] vertices;
        VertexBuffer vertexBuffer;

        // Indexes
        int[] indices;
        IndexBuffer indexBuffer;

        // Array of all vertexes heights
        float[,] heights;

        // Maximum height of terrain
        float height;

        // Distance between vertices on x and z axes
        public float cellSize;

        // How many times we need to draw the texture
        float textureTiling;

        // Number of vertices on x and z axes
        public int width, length;

        // Middle of the terrain
        public Point terrainMiddle;

        // Used to improve calculations
        private float middleWidthRelative;
        private float middleLengthRelative;

        // BoundingBoxes
        public BoundingBox[] boundingChunks;
        public BoundingFrustum frustum;
        private int chunkAmounts = 32;
        private int chunkSizeSide;
        private int chunkSizeVertices;

        // Number of vertices and indices
        int nVertices, nIndices;

        private bool _isUnderWater = false;
        private float _waterHeight = 0f;
        public bool _enableUnderWaterFog = true;

        public bool isUnderWater
        {
            set
            {
                effect.Parameters["IsUnderWater"].SetValue(value);
                effect.Parameters["LightIntensity"].SetValue((value) ? 0.1f : 1.0f);
                _isUnderWater = value;
            }
            get
            {
                return _isUnderWater;
            }
        }

        public float waterHeight
        {
            set
            {
                effect.Parameters["FogWaterHeight"].SetValue(value);
                effect.Parameters["FogWaterHeightMore"].SetValue(value + 0.1f);
                _waterHeight = value;
            }
            get
            {
                return _waterHeight;
            }
        }

        // Classes
        public Effect effect;
        public Effect effectLight;
        GraphicsDevice GraphicsDevice;
        public Vector3 lightDirection;
        Texture2D heightMap;
        Texture2D baseTexture;

        // World matrix contains scale, position, rotation...
        Matrix World;

        public Texture2D RTexture, BTexture, GTexture, WeightMap;
        public Texture2D DetailTexture;
        public float DetailDistance = 2500;
        public float DetailTextureTiling = 100;

        private bool areDefaultParamsLoaded = false;
        private int usedTechniqueIndex = -1;

        public PrimitiveType terrainDrawPrimitive = PrimitiveType.TriangleList;
        public bool debugActivated = false;

        /// <summary>
        /// Constructor
        /// </summary>
        public CTerrain()
        {
        }

        /// <summary>
        /// Initializes the textures from LevelInfo
        /// </summary>
        /// <param name="textureDatas">Textures Datas from the XML file</param>
        public void InitializeTextures(Game.LevelInfo.TerrainTextures textureDatas, ContentManager Content)
        {
            try
            {
                heightMap = Content.Load<Texture2D>(textureDatas.HeightmapFile);
                WeightMap = Content.Load<Texture2D>(textureDatas.TextureFile);
                RTexture = Content.Load<Texture2D>(textureDatas.RTexture);
                GTexture = Content.Load<Texture2D>(textureDatas.GTexture);
                BTexture = Content.Load<Texture2D>(textureDatas.BTexture);
                baseTexture = Content.Load<Texture2D>(textureDatas.BaseTexture);
            }
            catch (Exception e)
            {
                Game.CConsole.WriteLogs("Terrain's textures initilizations failed");
                throw e;
            }
        }

        /// <summary>
        /// Initialize the Terrain Class
        /// </summary>
        /// <param name="HeightMap">The Heightmap texture</param>
        /// <param name="CellSize">The distance between vertices</param>
        /// <param name="Height">The maximum height of the terrain</param>
        /// <param name="BaseTexture">The texture to draw</param>
        /// <param name="TextureTiling">Number of texture we draw</param>
        /// <param name="LightDirection">Light Direction</param>
        /// <param name="GraphicsDevice">The graphics Device</param>
        /// <param name="Content">The ContentManager</param>
        public void LoadContent(float CellSize, float Height, float TextureTiling, Vector3 LightDirection, GraphicsDevice GraphicsDevice, ContentManager Content)
        {
            this.textureTiling = TextureTiling;
            this.lightDirection = LightDirection;
            this.width = heightMap.Width;
            this.length = heightMap.Height;
            this.cellSize = CellSize;
            this.height = Height;
            this.World = Matrix.CreateTranslation(new Vector3(0, 290, 0));

            this.GraphicsDevice = GraphicsDevice;

            effect = Content.Load<Effect>("Effects/Terrain");
            effectLight = Content.Load<Effect>("Effects/PPLight");

            // 1 vertex per pixel
            nVertices = width * length;

            // (Width-1) * (Length-1) cells, 2 triangles per cell, 3 indices per triangle
            nIndices = (width - 1) * (length - 1) * 6;

            // Bounding Box chunks
            chunkSizeSide = width / chunkAmounts;
            chunkSizeVertices = nVertices / chunkAmounts;
            boundingChunks = new BoundingBox[chunkAmounts];

            vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture),
                nVertices, BufferUsage.WriteOnly);

            indexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits,
                nIndices, BufferUsage.WriteOnly);

            getHeights();
            createVertices();
            createIndices();
            genNormals();
            genBoundingBoxes();

            vertexBuffer.SetData<VertexPositionNormalTexture>(vertices);
            indexBuffer.SetData<int>(indices);

            terrainMiddle = new Point((width - 1) / 2, (length - 1) / 2);
            middleWidthRelative = (width / 2) * cellSize;
            middleLengthRelative = (length / 2) * cellSize;
        }

        /// <summary>
        /// Generates bounding boxes for each chunks
        /// </summary>
        private void genBoundingBoxes()
        {

            Vector3 highestPoint = Vector3.Zero;
            Vector3 lowestPoint = Vector3.Zero;
            List<Vector3> points = new List<Vector3>();

            int chunk = 0;
            for (int x = 0; x < length; x++)
            {
                for (int y = 0; y < width; y++)
                {
                    Vector3 pos = vertices[y * width + x].Position;

                    if ((y == 0 || y == width - 1) && (x % chunkSizeSide == 0 || x % chunkSizeSide == chunkSizeSide - 1))
                        points.Add(pos);

                    if (highestPoint == Vector3.Zero || pos.Y > highestPoint.Y)
                        highestPoint = pos;
                    if (lowestPoint == Vector3.Zero || pos.Y < lowestPoint.Y)
                        lowestPoint = pos;
                }

                if (x > 0 && x % chunkSizeSide == chunkSizeSide - 1)
                {
                    points.Add(highestPoint);
                    points.Add(lowestPoint);
                    boundingChunks[chunk] = BoundingBox.CreateFromPoints(points);
                    points.Clear();
                    highestPoint = Vector3.Zero;
                    lowestPoint = Vector3.Zero;
                    chunk++;
                }
            }
        }

        /// <summary>
        /// Translate every heigtmap's pixels to height
        /// </summary>
        private void getHeights()
        {
            // Extract pixel data
            Color[] heightMapData = new Color[width * length];
            heightMap.GetData<Color>(heightMapData);

            // Create heights[,] array
            heights = new float[width, length];

            // For each pixel
            for (int y = 0; y < length; y++)
                for (int x = 0; x < width; x++)
                {
                    // Get color value (0 - 255)
                    float amt = heightMapData[y * width + x].R;

                    // Scale to (0 - 1)
                    amt /= 255.0f;

                    // Multiply by max height to get final height
                    heights[x, y] = amt * height;
                }
        }

        /// <summary>
        /// Create the vertices
        /// </summary>
        private void createVertices()
        {
            vertices = new VertexPositionNormalTexture[nVertices];

            // Calculate the position offset that will center the terrain at (0, 0, 0)
            Vector3 offsetToCenter = -new Vector3(((float)width / 2.0f) * cellSize, 0, ((float)length / 2.0f) * cellSize);

            // For each pixel in the image
            for (int z = 0; z < length; z++)
                for (int x = 0; x < width; x++)
                {
                    // Find position based on grid coordinates and height in heightmap
                    Vector3 position = new Vector3(x * cellSize,
                        heights[x, z], z * cellSize) + offsetToCenter;

                    // UV coordinates range from (0, 0) at grid location (0, 0) to 
                    // (1, 1) at grid location (width, length)
                    Vector2 uv = new Vector2((float)x / width, (float)z / length);

                    // Create the vertex
                    vertices[z * width + x] = new VertexPositionNormalTexture(position, Vector3.Zero, uv);
                }
        }

        /// <summary>
        /// Create the indices
        /// </summary>
        private void createIndices()
        {
            indices = new int[nIndices];

            int i = 0;

            // For each cell
            for (int x = 0; x < width - 1; x++)
                for (int z = 0; z < length - 1; z++)
                {
                    // Find the indices of the corners
                    int upperLeft = z * width + x;
                    int upperRight = upperLeft + 1;
                    int lowerLeft = upperLeft + width;
                    int lowerRight = lowerLeft + 1;

                    // Specify upper triangle
                    indices[i++] = upperLeft;
                    indices[i++] = upperRight;
                    indices[i++] = lowerLeft;

                    // Specify lower triangle
                    indices[i++] = lowerLeft;
                    indices[i++] = upperRight;
                    indices[i++] = lowerRight;
                }
        }

        /// <summary>
        /// Calculate normals for each vertex
        /// </summary>
        private void genNormals()
        {
            // For each triangle
            for (int i = 0; i < nIndices; i += 3)
            {
                // Find the position of each corner of the triangle
                Vector3 v1 = vertices[indices[i]].Position;
                Vector3 v2 = vertices[indices[i + 1]].Position;
                Vector3 v3 = vertices[indices[i + 2]].Position;

                // Cross the vectors between the corners to get the normal
                Vector3 normal = Vector3.Cross(v1 - v2, v1 - v3);
                normal.Normalize();

                // Add the influence of the normal to each vertex in the
                // triangle
                vertices[indices[i]].Normal += normal;
                vertices[indices[i + 1]].Normal += normal;
                vertices[indices[i + 2]].Normal += normal;
            }

            // Average the influences of the triangles touching each vertex
            for (int i = 0; i < nVertices; i++)
                vertices[i].Normal.Normalize();
        }

        /// <summary>
        /// Draw the terrain
        /// </summary>
        /// <param name="View">The camera View matrix</param>
        /// <param name="Projection">The camera Projection matrix</param>
        /// <param name="cameraPos">The camera position</param>
        public void Draw(Matrix View, Matrix Projection, Vector3 cameraPos)
        {
            GraphicsDevice.SetVertexBuffer(vertexBuffer);
            GraphicsDevice.Indices = indexBuffer;

            effect.Parameters["View"].SetValue(View);
            effect.Parameters["Projection"].SetValue(Projection);

            if (!areDefaultParamsLoaded)
            {
                SendEffectDefaultParameters();
                areDefaultParamsLoaded = true;
            }

            /* Determine techniques */
            /* Techniques:
             * 0: !fog && !underwater (T1)
             * 1: fog && !underwater (T2)
             * 2: !fog && underwater (T3)
             * 3: fog && underwater (T4)
            */

            int index = 3;
            if (!_enableUnderWaterFog)
                index = (_isUnderWater) ? 2 : 0;
            else
                if (!_isUnderWater)
                    index = 1;

            if (usedTechniqueIndex != index)
            {
                effect.CurrentTechnique = effect.Techniques[index];
                usedTechniqueIndex = index;
            }

            effect.Techniques[index].Passes[0].Apply();

            int startPrimitive = 0, endPrimitive = 0;
            GetFirstAndLastPrimitives(ref startPrimitive, ref endPrimitive);

            // Draw terrain
            if(terrainDrawPrimitive == PrimitiveType.TriangleList)
                GraphicsDevice.DrawIndexedPrimitives(terrainDrawPrimitive, 0, 0, nVertices, startPrimitive, nIndices / 3 - startPrimitive / 3 - endPrimitive / 3);
            else
                GraphicsDevice.DrawIndexedPrimitives(terrainDrawPrimitive, 0, 0, nVertices, 0, nIndices / 3);
        }

        public void SendEffectDefaultParameters()
        {
            effect.Parameters["BaseTexture"].SetValue(baseTexture);
            effect.Parameters["TextureTiling"].SetValue(textureTiling);
            effect.Parameters["LightDirection"].SetValue(lightDirection);

            effect.Parameters["RTexture"].SetValue(RTexture);
            effect.Parameters["GTexture"].SetValue(GTexture);
            effect.Parameters["BTexture"].SetValue(BTexture);
            effect.Parameters["WeightMap"].SetValue(WeightMap);

            effect.Parameters["DetailTexture"].SetValue(DetailTexture);
            effect.Parameters["DetailDistance"].SetValue(DetailDistance);
            effect.Parameters["DetailTextureTiling"].SetValue(DetailTextureTiling);
        }

        /// <summary>
        /// Determine the first and last chunks of vertices to draw
        /// </summary>
        /// <param name="startPrimitive">The first index of vertice visible</param>
        /// <param name="endPrimitive">The last index of vertice visible</param>
        public void GetFirstAndLastPrimitives(ref int startPrimitive, ref int endPrimitive)
        {
            int startChunk = -1;
            int endChunk = chunkAmounts;
            for (int i = 0; i < chunkAmounts; i++)
            {
                bool contains = frustum.Contains(boundingChunks[i]) == ContainmentType.Disjoint;
                if (!contains && startChunk == -1)
                    startChunk = i;
                if (!contains)
                    endChunk = i;
            }
            if (startChunk == -1)
                startChunk = 0;

            startPrimitive = startChunk * chunkSizeVertices * 5;
            endPrimitive = (chunkAmounts - endChunk) * chunkSizeVertices * 5;

            if (startPrimitive % 3 != 0)
                startPrimitive -= startPrimitive % 3;
        }

        /// <summary>
        /// Sets the clip plane for water reflection rendering
        /// </summary>
        public void SetClipPlane(Vector4? Plane)
        {
            effect.Parameters["ClipPlaneEnabled"].SetValue(Plane.HasValue);

            if (Plane.HasValue)
                effect.Parameters["ClipPlane"].SetValue(Plane.Value);
        }

        /// <summary>
        /// Get the position on the terrain from a screen position
        /// </summary>
        /// <param name="device">Graphics Device</param>
        /// <param name="view">View Matrix</param>
        /// <param name="projection">Projection Matrix</param>
        /// <param name="X">The X screen position</param>
        /// <param name="Y">The Y screen position</param>
        /// <returns>The position on the terrain</returns>
        public Vector3 Pick(Matrix view, Matrix projection, int X, int Y, out bool IsValid)
        {
            Vector3 NearestPoint = Vector3.Zero;

            Vector3 nearSource = GraphicsDevice.Viewport.Unproject(new Vector3(X, Y, GraphicsDevice.Viewport.MinDepth), projection, view, World);
            Vector3 farSource = GraphicsDevice.Viewport.Unproject(new Vector3(X, Y, GraphicsDevice.Viewport.MaxDepth), projection, view, World);
            Vector3 direction = farSource - nearSource;


            float t = 0f;

            while (true)
            {
                t += 0.0001f;

                Vector3 newPos = new Vector3(nearSource.X + direction.X * t, 0, nearSource.Z + direction.Z * t);

                if (newPos.X - cellSize < -middleWidthRelative || newPos.X + cellSize > middleWidthRelative || newPos.Z - cellSize < -middleLengthRelative || newPos.Z + cellSize > middleLengthRelative)
                    break;

                newPos.Y = nearSource.Y + direction.Y * t;

                float steepness;
                float IndHeight = GetHeightAtPosition(newPos.X, newPos.Z, out steepness, false);

                if(debugActivated)
                    Display3D.CSimpleShapes.AddBoundingSphere(new BoundingSphere(newPos, 0.1f), Color.Blue, 5f);

                if (IndHeight >= newPos.Y)
                {
                    IsValid = true;
                    return newPos;
                }
            }
            IsValid = false;
            return Vector3.Zero;
        }

        public Vector3 RayPick(Ray ray)
        {
            float t = 0f;

            Vector3 nearSource = ray.Position;
            Vector3 farSource = ray.Position + ray.Direction * 50;
            Vector3 direction = ray.Direction;

            while (true)
            {
                t += 0.0001f;

                Vector3 newPos = new Vector3(nearSource.X + direction.X * t, 0, nearSource.Z + direction.Z * t);

                if (newPos.X - cellSize < -middleWidthRelative || newPos.X + cellSize > middleWidthRelative || newPos.Z - cellSize < -middleLengthRelative || newPos.Z + cellSize > middleLengthRelative)
                    break;

                newPos.Y = nearSource.Y + direction.Y * t;

                float steepness;
                float IndHeight = GetHeightAtPosition(newPos.X, newPos.Z, out steepness, false);

                if (debugActivated)
                    Display3D.CSimpleShapes.AddBoundingSphere(new BoundingSphere(newPos, 0.1f), Color.Blue, 5f);

                if (IndHeight >= newPos.Y)
                {
                    return newPos;
                }
            }
            return Vector3.Zero;
        }

        /// <summary>
        /// Get the terrain height at a certain position
        /// </summary>
        /// <param name="X">Position X</param>
        /// <param name="Z">Position Y</param>
        /// <returns>The height & steepness at position X Y</returns>
        public float GetHeightAtPosition(float X, float Z, out float Steepness, bool calcSteepness = true)
        {

            // Clamp coordinates to locations on terrain
            X = MathHelper.Clamp(X, -middleWidthRelative, middleWidthRelative);
            Z = MathHelper.Clamp(Z, -middleLengthRelative, middleLengthRelative);

            // Map from (-Width/2->Width/2,-Length/2->Length/2) 
            // to (0->Width, 0->Length)
            X += middleWidthRelative;
            Z += middleLengthRelative;

            // Map to cell coordinates
            X /= cellSize;
            Z /= cellSize;

            int x = (int)X;
            int z = (int)Z;
            float fTX = X - x;
            float fTY = Z - z;

            if (z >= width - 1 || x >= length - 1)
            {
                Steepness = 0;
                return 0;
            }

            float fSampleH1 = heights[x, z];
            float fSampleH2 = heights[x + 1, z];
            float fSampleH3 = heights[x, z + 1];
            float fSampleH4 = heights[x + 1, z + 1];

            if (calcSteepness)
                Steepness = (float)Math.Atan(Math.Abs((fSampleH1 - fSampleH4)) / (cellSize * Math.Sqrt(2)));
            else
                Steepness = 1;

            return (fSampleH1 * (1.0f - fTX) + fSampleH2 * fTX) * (1.0f - fTY) + (fSampleH3 * (1.0f - fTX) + fSampleH4 * fTX) * (fTY);


            //return MathHelper.Lerp(h1, h2, leftOver);
        }


        /// <summary>
        /// Transform a world position to a terrain position
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Z"></param>
        /// <returns></returns>
        public Vector2 positionToTerrain(float X, float Z)
        {
            // Clamp coordinates to locations on terrain
            X = MathHelper.Clamp(X, -middleWidthRelative, middleWidthRelative);
            Z = MathHelper.Clamp(Z, -middleLengthRelative, middleLengthRelative);

            // Map from (-Width/2->Width/2,-Length/2->Length/2) 
            // to (0->Width, 0->Length)
            X += (width / 2f) * cellSize;
            Z += (length / 2f) * cellSize;

            // Map to cell coordinates
            X /= cellSize;
            Z /= cellSize;

            // Truncate coordinates to get coordinates of top left cell vertex
            int x1 = (int)X;
            int z1 = (int)Z;

            // Try to get coordinates of bottom right cell vertex
            int x2 = x1 + 1 == width ? x1 : x1 + 1;
            int z2 = z1 + 1 == length ? z1 : z1 + 1;

            return new Vector2(x2, z2);
        }

        /// <summary>
        /// Get the normal at a certain point of the world
        /// </summary>
        /// <param name="X"></param>
        /// <param name="Z"></param>
        /// <returns>The normal vector</returns>
        public Vector3 getNormalAtPoint(float X, float Z)
        {
            Vector2 translatedPosition = positionToTerrain(X, Z);
            int verticeIndex = (int)(translatedPosition.Y * width + translatedPosition.X);

            return vertices[verticeIndex].Normal;
        }



    }
}
