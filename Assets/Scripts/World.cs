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
        tileset = new Texture2D(512, 512);
        tileset.filterMode = FilterMode.Point;
        tileset.SetPixels32(Enumerable.Range(0, 512 * 512).Select(i => colors[Random.Range(0, 2)]).ToArray());
        tileset.Apply();

        tiles = new Sprite[256];
        tilemap = new byte[1024];
        palette = new Color[16];

        for (int i = 0; i < 16; ++i)
        {
            palette[i] = new Color(Random.value, Random.value, Random.value, 1);
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
