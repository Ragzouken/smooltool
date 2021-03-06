﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using Newtonsoft.Json;

[JsonObject(MemberSerialization = MemberSerialization.OptIn)]
public class World
{
    [JsonObject(IsReference = false)]
    public class Info
    {
        public Test.Version version;
        public string name;
        [JsonIgnore]
        public string root;
        public System.DateTime lastPlayed;
    }

    public class Avatar
    {
        public int id;
        public Vector2 destination, source;
        public Sprite graphic;
        public string name;

        public float u;
    }

    public Texture2D tileset;
    public Sprite[] tiles;

    [JsonProperty]
    public byte[] tilemap;
    [JsonProperty]
    public HashSet<byte> walls = new HashSet<byte>();
    [JsonProperty]
    public Color[] palette;

    public List<Avatar> avatars = new List<Avatar>();

    public World()
    {
        tileset = new Texture2D(512, 512);
        tileset.filterMode = FilterMode.Point;

        tiles = new Sprite[256];
        tilemap = new byte[1024];
        palette = new Color[16];

        for (int i = 0; i < 256; ++i)
        {
            int x = i % 16;
            int y = i / 16;

            tiles[i] = Sprite.Create(tileset,
                                     new Rect(x * 32, y * 32, 32, 32),
                                     Vector2.zero,
                                     1,
                                     0U,
                                     SpriteMeshType.FullRect);
        }
    }

    public void StaticiseTileset()
    {
        Color32[] colors = new Color32[512 * 512];
        Color32[] colors2 =
        {
            Color.black,
            Color.white,
        };

        for (int i = 0; i < colors.Length; ++i)
        {
            colors[i] = colors2[Random.Range(0, 2)];
        }

        tileset.SetPixels32(colors);
        tileset.Apply();
    }

    // TODO: just generate 3 curves (HSL) then evaluate n palette colours w them
    public void RandomisePalette()
    {
        float phase = Random.value;
        var borders = Enumerable.Range(0, 4)
                                .Select(i => (i / 3f + Random.value * 0.1f + phase) % 1)
                                .OrderBy(i => i)
                                .ToArray();

        float[] ranges = new float[borders.Length];

        ranges[borders.Length - 1] = 1 - borders[borders.Length - 1] + borders[0];

        for (int i = 0; i < borders.Length - 1; ++i)
        {
            ranges[i] = borders[i + 1] - borders[i];
        }

        var hues = new AnimationCurve[borders.Length];
        var ligs = new AnimationCurve[borders.Length];

        for (int i = 0; i < borders.Length; ++i)
        {
            float offset = ranges[i] * 0.125f * Random.value;
            float min = borders[i] + offset;
            float max = min + ranges[i] - offset;

            hues[i] = AnimationCurve.Linear(0, min, 1, max);
            ligs[i] = AnimationCurve.Linear(0, 0.1f + 0.3f * Random.value, 1, 0.9f - 0.3f * Random.value);
        }
        
        for (int r = 0; r < 4; ++r)
        {
            bool fliph = Random.value > 0.5f;
            bool flipl = Random.value > 0.5f;

            for (int i = 0; i < 4; ++i)
            {
                float up = i / 3f;
                float down = 1 - up;

                float hue = hues[r].Evaluate(up) % 1;
                float sat = 0.75f + Random.value * 0.25f;
                float lig = ligs[r].Evaluate(flipl ? up : down) % 1;

                IList<double> RGB = HUSL.HUSLToRGB(new double[] { hue * 360, sat * 100, lig * 100 });
                Color color = new Color((float) RGB[0], (float) RGB[1], (float) RGB[2], 1);

                palette[r * 4 + i] = color;
            }
        }

        palette[0] = Color.clear;

        /*
        for (int i = 0; i < 16; ++i)
        {
            Color color;

            do
            {
                color = new Color(Random.value, Random.value, Random.value, 1);
            }
            while (palette.Take(i).Any(other => Test.ColorDistance(other, color) < 16f));

            palette[i] = color;
        }
        */
    }

    public byte ColorToPalette(Color color, bool clearzero = false)
    {
        return (byte) palette.ColorToPalette(color, clearzero);
    }

    private Color IndexToMask(byte index)
    {
        return index == 0 ? Color.clear : new Color(index / 15f, 0, 0, 1f);
    }

    public void PalettiseTexture(Texture2D texture, bool clearzero = false)
    {
        var colors = texture.GetPixels()
                            .Select(color => ColorToPalette(color, clearzero))
                            .Select(index => IndexToMask(index))
                            .ToArray();

        texture.SetPixels(colors);
        texture.Apply();
    }

    public void Convert()
    {
        palette[0] = Color.clear;

        PalettiseTexture(tileset);
    }
}
