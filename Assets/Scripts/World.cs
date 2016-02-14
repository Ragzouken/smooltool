using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class World
{
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
    public byte[] tilemap;
    public HashSet<byte> walls = new HashSet<byte>();
    public List<Avatar> avatars = new List<Avatar>();
    public Color[] palette;

    private static Color32[] colors =
    {
        Color.black,
        Color.white,
    };

    public World()
    {
        Color32[] colors = new Color32[512 * 512];

        tileset = new Texture2D(512, 512);
        tileset.filterMode = FilterMode.Point;

        for (int i = 0; i < colors.Length; ++i)
        {
            colors[i] = colors[Random.Range(0, 2)];
        }

        tileset.SetPixels32(colors);
        tileset.Apply();

        tiles = new Sprite[256];
        tilemap = new byte[1024];
        palette = new Color[16];

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
}
