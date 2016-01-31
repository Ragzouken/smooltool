using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class WorldView : MonoBehaviour 
{
    [SerializeField] private Image tilePrefab;
    [SerializeField] private Transform tileContainer;

    [SerializeField] private Transform avatarPrefab;
    [SerializeField] private Transform avatarContainer;

    private Image[] tiles;

    private MonoBehaviourPooler<World.Avatar, Transform> avatars;

    public World world { get; private set; }

    private void Awake()
    {
        avatars = new MonoBehaviourPooler<World.Avatar, Transform>(avatarPrefab,
                                                                   avatarContainer,
                                                                   InitialiseAvatar);

        tiles = new Image[1024];

        for (int i = 0; i < 1024; ++i)
        {
            int x = i % 32;
            int y = i / 32;

            Image tile = Instantiate(tilePrefab);

            tile.transform.SetParent(tileContainer, false);
            tile.transform.localPosition = new Vector2(x * 32 - 512, y * 32 - 512);
            tile.gameObject.SetActive(true);

            tiles[i] = tile;
        }
    }

    private void InitialiseAvatar(World.Avatar avatar, Transform view)
    {
        view.transform.position = avatar.destination;
    }

    public void SetWorld(World world)
    {
        this.world = world;

        for (int i = 0; i < 1024; ++i)
        {
            byte tile = world.tilemap[i];

            tiles[i].sprite = world.tiles[tile];
        }

        RefreshAvatars();
    }

    public void RefreshAvatars()
    {
        avatars.SetActive(world.avatars);
    }
}
