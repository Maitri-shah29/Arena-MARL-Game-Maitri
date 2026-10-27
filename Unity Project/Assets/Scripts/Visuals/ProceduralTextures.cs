using UnityEngine;

public static class ProceduralTextures
{
    private static Texture2D cachedFloorTexture;

    public static Texture2D GenerateCrackedConcrete(int width = 512, int height = 512)
    {
        if (cachedFloorTexture != null)
        {
            return cachedFloorTexture;
        }

        Texture2D texture = new Texture2D(width, height, TextureFormat.RGBA32, true);
        texture.name = "Procedural_CrackedConcrete";
        texture.filterMode = FilterMode.Bilinear;
        texture.wrapMode = TextureWrapMode.Repeat;

        Color[] pixels = new Color[width * height];

        // Seed Voronoi points on a 6x6 jittered grid for cracked slab patterns
        int gridX = 6;
        int gridY = 6;
        Vector2[] seeds = new Vector2[gridX * gridY];
        System.Random rnd = new System.Random(42);

        for (int gy = 0; gy < gridY; gy++)
        {
            for (int gx = 0; gx < gridX; gx++)
            {
                float px = (gx + (float)rnd.NextDouble() * 0.8f + 0.1f) / gridX;
                float py = (gy + (float)rnd.NextDouble() * 0.8f + 0.1f) / gridY;
                seeds[gy * gridX + gx] = new Vector2(px, py);
            }
        }

        Color baseConcrete = new Color(0.18f, 0.20f, 0.23f);     // Dark charcoal concrete
        Color lightSlab    = new Color(0.24f, 0.26f, 0.29f);     // Worn slab variation
        Color crackColor   = new Color(0.08f, 0.09f, 0.11f);     // Deep structural fissure
        Color rubbleGrain  = new Color(0.14f, 0.15f, 0.17f);     // Dirt & dust

        for (int y = 0; y < height; y++)
        {
            float v = (float)y / height;
            for (int x = 0; x < width; x++)
            {
                float u = (float)x / width;

                // 1. Two-octave Perlin noise for concrete grit and mottling
                float n1 = Mathf.PerlinNoise(u * 8f, v * 8f);
                float n2 = Mathf.PerlinNoise(u * 32f, v * 32f) * 0.3f;
                float grit = n1 + n2;

                // 2. Voronoi distance to closest and second closest seed for cellular cracks
                float d1 = float.MaxValue;
                float d2 = float.MaxValue;

                // Check wrap-around 3x3 tiles for seamless tiling
                for (int ox = -1; ox <= 1; ox++)
                {
                    for (int oy = -1; oy <= 1; oy++)
                    {
                        for (int i = 0; i < seeds.Length; i++)
                        {
                            Vector2 s = seeds[i] + new Vector2(ox, oy);
                            float dist = Vector2.Distance(new Vector2(u, v), s);
                            if (dist < d1)
                            {
                                d2 = d1;
                                d1 = dist;
                            }
                            else if (dist < d2)
                            {
                                d2 = dist;
                            }
                        }
                    }
                }

                // Voronoi edge detection: d2 - d1 is small near cell borders
                float edge = d2 - d1;
                // Add micro-noise jitter to edges so cracks look jagged
                float crackNoise = Mathf.PerlinNoise(u * 64f, v * 64f) * 0.02f;
                float crackFactor = Mathf.Clamp01((edge + crackNoise - 0.015f) / 0.035f);

                // Blend concrete shades
                Color slabCol = Color.Lerp(rubbleGrain, lightSlab, grit);
                Color pixelCol = Color.Lerp(crackColor, slabCol, crackFactor);

                // Subtle dust streaks
                float dust = Mathf.PerlinNoise(u * 3f + 10f, v * 3f + 10f);
                pixelCol = Color.Lerp(pixelCol, baseConcrete, dust * 0.4f);

                pixels[y * width + x] = pixelCol;
            }
        }

        texture.SetPixels(pixels);
        texture.Apply(true, false);
        cachedFloorTexture = texture;
        return texture;
    }
}
