using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class WorldView : MonoBehaviour 
{
    [SerializeField] private new Camera camera;

    [SerializeField] private Image tilePrefab;
    [SerializeField] private Transform tileContainer;

    [SerializeField] private AvatarView avatarPrefab;
    [SerializeField] private Transform avatarContainer;

    [SerializeField] private Image wallPrefab;
    [SerializeField] private Transform wallContainer;

    private Image[] tiles;
    private Image[] walls;

    private MonoBehaviourPooler<World.Avatar, AvatarView> avatars;

    public World world { get; private set; }

    public World.Avatar viewer;

    private Dictionary<World.Avatar, float> animations
        = new Dictionary<World.Avatar, float>();

    private void Awake()
    {
        avatars = new MonoBehaviourPooler<World.Avatar, AvatarView>(avatarPrefab,
                                                                    avatarContainer,
                                                                    InitialiseAvatar);

        tiles = new Image[1024];
        walls = new Image[1024];

        for (int i = 0; i < 1024; ++i)
        {
            int x = i % 32;
            int y = i / 32;

            Image tile = Instantiate(tilePrefab);

            tile.transform.SetParent(tileContainer, false);
            tile.transform.localPosition = new Vector2(x * 32 - 512, y * 32 - 512);
            tile.gameObject.SetActive(true);

            tiles[i] = tile;

            Image wall = Instantiate(wallPrefab);
            wall.transform.SetParent(wallContainer, false);
            wall.transform.localPosition = new Vector2(x * 32 - 512, y * 32 - 512);

            walls[i] = wall;
        }
    }

    private void InitialiseAvatar(World.Avatar avatar, AvatarView view)
    {
        animations[avatar] = 0;

        view.transform.position = avatar.destination * 32 + Vector2.one * 16;
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
        RefreshWalls();
    }

    public void RefreshAvatars()
    {
        avatars.SetActive(world.avatars);
    }

    public void RefreshWalls()
    {
        for (int i = 0; i < 1024; ++i)
        {
            byte tile = world.tilemap[i];
            bool wall = world.walls.Contains(tile);

            walls[i].gameObject.SetActive(wall);
        }
    }

    public void Chat(World.Avatar avatar, string message)
    {
        avatars.Get(avatar).SetChat(message);
    }

    public void SetTile(int location, byte tile)
    {
        world.tilemap[location] = tile;
        tiles[location].sprite = world.tiles[tile];

        RefreshWalls();
    }

    private void Update()
    {
        if (viewer == null) return;

        var view = avatars.Get(viewer);

        float x = view.transform.position.x;
        float y = view.transform.position.y;

        float tSize = 32;
        float vSize = 512 / 2;
        float mSize = tSize * 32;

        float edge = (mSize / 2f) - (vSize / 2f);// + tSize / 2;

        camera.transform.position = new Vector3(Mathf.Round(Mathf.Clamp(x, -edge, edge) * 2) / 2f,
                                                Mathf.Round(Mathf.Clamp(y, -edge, edge) * 2) / 2f,
                                                -10);

        float period = 0.25f;

        wallContainer.gameObject.SetActive(Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

        avatars.MapActive((a, v) =>
        {
            if (a.source != a.destination) a.u += Time.deltaTime;

            float u = Mathf.Min(1, a.u / period);

            if (u >= 1)
            {
                a.source = a.destination;
                a.u = 0;
            }
            else
            {
                v.transform.position = Vector2.Lerp(a.source, a.destination, u) * 32 + Vector2.one * 16;
            }
        });
    }
}
