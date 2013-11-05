﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.GamerServices;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Media;

namespace Editor.Display3D
{
    class CTerrain
    {
        /// <summary>
        /// Variables
        /// </summary>

        // Vertexes
        VertexPositionNormalTexture[] vertices;
        VertexBuffer vertexBuffer;

        // Indexes
        int[] indices;
        IndexBuffer indexBuffer;

        // Array of all vertexes heights
        float[,] heights;

        // Maximum height of terrain
        float height;

        // Distance between vertices on x and z axes
        float cellSize;

        // How many times we need to draw the texture
        float textureTiling;

        // Number of vertices on x and z axes
        int width, length;

        // Number of vertices and indices
        int nVertices, nIndices;

        /// <summary>
        /// Classes
        /// </summary>
        Effect effect;
        GraphicsDevice GraphicsDevice;
        Vector3 lightDirection;
        Texture2D heightMap;
        Texture2D baseTexture;

        // World matrix contains scale, position, rotation...
        Matrix World;

        /// <summary>
        /// Constructor
        /// </summary>
        public CTerrain()
        {
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
        /// <param name="GraphicsDevice"></param>
        /// <param name="Content"></param>
        public void LoadContent(Texture2D HeightMap, float CellSize, float Height, Texture2D BaseTexture, float TextureTiling, Vector3 LightDirection, GraphicsDevice GraphicsDevice, ContentManager Content)
        {
            this.baseTexture = BaseTexture;
            this.textureTiling = TextureTiling;
            this.lightDirection = LightDirection;
            this.heightMap = HeightMap;
            this.width = HeightMap.Width;
            this.length = HeightMap.Height;
            this.cellSize = CellSize;
            this.height = Height;
            this.World = Matrix.CreateTranslation(new Vector3(0, 0, 0));

            this.GraphicsDevice = GraphicsDevice;

            effect = Content.Load<Effect>("Effects/Terrain");

            // 1 vertex per pixel
            nVertices = width * length;

            // (Width-1) * (Length-1) cells, 2 triangles per cell, 3 indices per triangle
            nIndices = (width - 1) * (length - 1) * 6;

            vertexBuffer = new VertexBuffer(GraphicsDevice, typeof(VertexPositionNormalTexture),
                nVertices, BufferUsage.WriteOnly);

            indexBuffer = new IndexBuffer(GraphicsDevice, IndexElementSize.ThirtyTwoBits,
                nIndices, BufferUsage.WriteOnly);

            getHeights();
            createVertices();
            createIndices();
            genNormals();

            vertexBuffer.SetData<VertexPositionNormalTexture>(vertices);
            indexBuffer.SetData<int>(indices);
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
        public void Draw(Matrix View, Matrix Projection)
        {
            GraphicsDevice.SetVertexBuffer(vertexBuffer);
            GraphicsDevice.Indices = indexBuffer;

            effect.Parameters["View"].SetValue(View);
            effect.Parameters["Projection"].SetValue(Projection);
            effect.Parameters["BaseTexture"].SetValue(baseTexture);
            effect.Parameters["TextureTiling"].SetValue(textureTiling);
            effect.Parameters["LightDirection"].SetValue(lightDirection);

            effect.Techniques[0].Passes[0].Apply();

            GraphicsDevice.DrawIndexedPrimitives(PrimitiveType.TriangleList, 0, 0,
                nVertices, 0, nIndices / 3);
        }

        /// <summary>
        /// Get the position on the terrain from a screen position
        /// </summary>
        /// <param name="device"></param>
        /// <param name="view"></param>
        /// <param name="projection"></param>
        /// <param name="currentMouseState"></param>
        /// <returns></returns>
        public Vector3 Pick(GraphicsDevice device, Matrix view, Matrix projection, int X, int Y)
        {
            Vector3 NearestPoint = Vector3.Zero;

            Vector3 nearSource = device.Viewport.Unproject(new Vector3(X, Y, device.Viewport.MinDepth), projection, view, World);
            Vector3 farSource = device.Viewport.Unproject(new Vector3(X, Y, device.Viewport.MaxDepth), projection, view, World);
            Vector3 direction = farSource - nearSource;

            float zFactor = 0 / direction.Y;
            Vector3 zeroWorldPoint = nearSource + direction * zFactor;
            Ray ray = new Ray(zeroWorldPoint, direction);
            double distance;
            double ShortestDistance = 0;
            bool firstPass = true;

            for (int x = 0; x < width; x++)
            {
                for (int z = 0; z < length; z++)
                {
                    var position = new Vector3(); /// Converter para getData de vertexBuffer
                    position.X = 1.0f * (x - ((width - 1) / 2.0f));
                    position.Y = (heights[x, z] - 1);
                    position.Z = 1.0f * (z - ((length - 1) / 2.0f));

                    BoundingBox tmp = BoundingBox.CreateFromSphere(new BoundingSphere(position, 1.0f));
                    if (ray.Intersects(tmp) != null)
                    {
                        // Calculate the distance from us to the surface and keep the closest distance.
                        // Note that we don't really need to calculate the squares and hypotenuse to be useful (I think).
                        // TO BE OPTIMIZED
                        distance = (Math.Abs(position.X - zeroWorldPoint.X) + Math.Abs(position.Y - zeroWorldPoint.Y) + Math.Abs(position.Z - zeroWorldPoint.Z));
                        if (firstPass == true || distance < ShortestDistance)
                        {
                            firstPass = false;
                            return position;
                        }
                    }
                }
            }

            return Vector3.Zero;
        }
    }
}
