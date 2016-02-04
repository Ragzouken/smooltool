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

        public float u;
    }

    public Texture2D tileset;
    public Sprite[] tiles;
    public byte[] tilemap;
    public List<Avatar> avatars = new List<Avatar>();

    public World()
    {
        tileset = new Texture2D(512, 512);
        tileset.filterMode = FilterMode.Point;

        tiles = new Sprite[256];
        tilemap = new byte[1024];

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

    public Avatar Occupied(Vector2 cell)
    {
        for (int i = 0; i < avatars.Count; ++i)
        {
            if (avatars[i].destination == cell) return avatars[i];
        }

        return null;
    }
}
