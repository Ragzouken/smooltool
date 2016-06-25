using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Networking;
using UnityEngine.Networking.Types;
using UnityEngine.Networking.Match;

using UnityEngine.SceneManagement;

using PixelDraw;
using Newtonsoft.Json;

using System.IO;

using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class GameListing
{
    public Test.Version version;
    public string name;
    public int count;
    public string address;
    public MatchDesc match;
}

public class Test : MonoBehaviour
{
    [JsonObject(IsReference = false)]
    private class Config
    {
        public string name;
        public bool hideTutorial;
    }

    private static Regex gamename = new Regex(@"(.*)!(\d+)\?(\d+)");

    [SerializeField] private Font[] fonts;

    [SerializeField] private Camera mapCamera;
    [SerializeField] private RenderTexture mapTexture;
    [SerializeField] private GameObject mapObject;

    [Header("Sounds")]
    [SerializeField] private new AudioSource audio;
    [SerializeField] private AudioClip speakSound;
    [SerializeField] private AudioClip placeSound;
    [SerializeField] private AudioSource blockSource;

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Texture2D testtex;
    [SerializeField] private WorldView worldView;
    [SerializeField] private Popups popups;

    [Header("List")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private WorldPanel worldPrefab;
    [SerializeField] private GameObject newerVersionDisableObject;
    [SerializeField] private GameObject noWorldsDisableObject;
    [SerializeField] private GameObject cantConnectDisableObject;

    private MonoBehaviourPooler<GameListing, WorldPanel> worlds;

    [Header("Details")]
    [SerializeField] private GameObject detailsDisableObject;
    [SerializeField] private Text worldDescription;
    [SerializeField] private GameObject passwordObject;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button enterButton;

    [SerializeField] private new Camera camera;
    [SerializeField] private Image tileCursor;
    [SerializeField] private GameObject pickerCursor;

    [Header("UI")]
    [SerializeField] private TileEditor tileEditor;
    [SerializeField] private HostTab hostTab;
    [SerializeField] private CustomiseTab customiseTab;
    [SerializeField] private TilePalette tilePalette;
    [SerializeField] private ChatOverlay chatOverlay;

    [Header("Tutorial")]
    [SerializeField] private GameObject tutorialObject;
    [SerializeField] private GameObject tutorialChat;
    [SerializeField] private GameObject tutorialMove;
    [SerializeField] private GameObject tutorialTile;
    [SerializeField] private GameObject tutorialWall;

    [Header("Direct IP")]
    [SerializeField] private GameObject ipObject;
    [SerializeField] private InputField ipInput;
    [SerializeField] private Button ipOpen, ipAccept, ipCancel;

    [SerializeField] private TestLAN testLAN;
    [SerializeField] private Material paletteMaterial;

    [SerializeField] private Texture2D defaultAvatar;
    private Texture2D avatarGraphic;

    [Range(0, 1)]
    [SerializeField] private float zoom;

    private Texture2D mapTextureLocal;

    public struct LoggedMessage
    {
        public World.Avatar avatar;
        public string message;
    }

    private List<LoggedMessage> chats = new List<LoggedMessage>();

    private NetworkMatch match;

    public const int maxTiles = 256;
    public static int scale = 1;

    public enum Version
    {
        DEV,
        PRE1,
        PRE2,
        PRE3,
        PRE4,
        PRE5,
        PRE6,
        PRE7,
        PRE8,
    }

    public static Version version = Version.PRE8;

    private void Awake()
    {
        foreach (Font font in fonts) font.material.mainTexture.filterMode = FilterMode.Point;

        tutorialObject.SetActive(false);

        match = gameObject.AddComponent<NetworkMatch>();
        match.baseUri = new System.Uri("https://eu1-mm.unet.unity3d.com");

        enterButton.onClick.AddListener(OnClickedEnter);

        worlds = new MonoBehaviourPooler<GameListing, WorldPanel>(worldPrefab,
                                                                  worldContainer,
                                                                  InitialiseWorld);

        Application.runInBackground = true;

        StartCoroutine(SendMessages());

        avatarGraphic = BlankTexture.New(32, 32, Color.clear);
        ResetAvatar();

        chatOverlay.Setup(chats,
                          message =>
                          {
                              tutorialChat.SetActive(false);

                              SendAll(ChatMessage(worldView.viewer, message));

                              Chat(worldView.viewer, message);
                          });

        customiseTab.Setup(tileEditor,
                           BlankTexture.FullSprite(avatarGraphic),
                           SaveConfig,
                           ResetAvatar);

        LoadConfig();

        mapTextureLocal = new Texture2D(1024, 1024);

        ipOpen.onClick.AddListener(() => { ipObject.SetActive(true); ipInput.text = ""; });
        ipAccept.onClick.AddListener(() => { PreConnect();  ConnectThroughLAN(ipInput.text); ipObject.SetActive(false); } );
        ipCancel.onClick.AddListener(() => ipObject.SetActive(false));
    }

    private void InitialiseWorld(GameListing desc, WorldPanel panel)
    {
        panel.SetMatch(desc);
    }

    private void ResetAvatar()
    {
        avatarGraphic.SetPixels32(defaultAvatar.GetPixels32());
        avatarGraphic.Apply();
    }

    private void SaveWorld(string path, string name=null)
    {
        var root = Application.persistentDataPath;
        var dir = root + "/worlds/" + path;
        var info = new World.Info
        {
            version = version,
            name = name ?? path,
            root = path,
            lastPlayed = System.DateTime.Now,
        };

        Directory.CreateDirectory(dir);

        File.WriteAllBytes(dir + "/tileset.png", 
                           world.tileset.EncodeToPNG());
        File.WriteAllText(dir + "/world.json",
                          JsonWrapper.Serialise(world));
        File.WriteAllText(dir + "/info.json",
                          JsonWrapper.Serialise(info));
    }

    private World LoadWorld(string path)
    {
        var root = Application.persistentDataPath;
        var dir = root + "/worlds/" + path;

        var info = JsonWrapper.Deserialise<World.Info>(File.ReadAllText(dir + "/info.json"));
        var world = JsonWrapper.Deserialise<World>(File.ReadAllText(dir + "/world.json"));

        float d = (float) (File.GetLastWriteTime(dir + "/tileset.png") - info.lastPlayed).TotalSeconds;

        world.tileset.LoadImage(File.ReadAllBytes(dir + "/tileset.png"));

        if (d > 5)
        {
            world.PalettiseTexture(world.tileset);
        }

        return world;
    }

    private static Config config;

    private void SaveConfig()
    {
        var root = Application.persistentDataPath;

        Directory.CreateDirectory(root + "/settings");
        File.WriteAllBytes(root + "/settings/avatar.png", avatarGraphic.EncodeToPNG());

        File.WriteAllText(root + "/settings/config.json", JsonWrapper.Serialise(config));
    }

    private void LoadConfig()
    {
        var root = Application.persistentDataPath;

        Directory.CreateDirectory(root + "/settings");

        try
        {
            byte[] avatar = File.ReadAllBytes(root + "/settings/avatar.png");

            avatarGraphic.LoadImage(avatar);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarningFormat("Couldn't load an existing #smoolsona:\n{0}", exception);
        }

        try
        {
            string data = File.ReadAllText(root + "/settings/config.json");

            config = JsonWrapper.Deserialise<Config>(data);

            avatarGraphic.name = config.name;
        }
        catch (System.Exception exception)
        {
            Debug.LogWarningFormat("Couldn't load an existing config:\n{0}", exception);

            config = new Config
            {
                name = "player name",
                hideTutorial = false,
            };
        }
    }

    public void HostGame(string name, string password)
    {
        world = new World();
        info = new World.Info
        {
            version = version,
            lastPlayed = System.DateTime.Now,
            name = name,
            root = string.Format("{0}-{1}", name, System.DateTime.Now.Ticks),
        };

        for (int i = 0; i < 1024; ++i) world.tilemap[i] = (byte) Random.Range(0, 23);
        for (int i = 0; i < 256; ++i)
        {
            if (Random.value > 0.5f) world.walls.Add((byte)i);
        }

        world.tileset.SetPixels32(testtex.GetPixels32());
        world.RandomisePalette();
        world.PalettiseTexture(world.tileset);

        SetWorld(world);

        var create = new CreateMatchRequest();
        create.name = name;
        create.size = 8;
        create.advertise = true;
        create.password = password;

        create.name += "!" + (int)version + "?0";

        testLAN.broadcastData = create.name;
        match.CreateMatch(create, OnMatchCreate);
        testLAN.StopBroadcast();
        testLAN.StartAsServer();
    }

    private string hostedname = "";

    public void HostGame(World.Info world,
                         string name,
                         string password)
    {
        this.world = LoadWorld(world.root);
        info = world;

        SetWorld(this.world);

        var create = new CreateMatchRequest();
        create.name = name;
        create.size = 8;
        create.advertise = true;
        create.password = password;


        hostedname = create.name;

        create.name += "!" + (int)version + "?0";

        match.CreateMatch(create, OnMatchCreate);

        testLAN.broadcastData = create.name;
        testLAN.Initialize();
        testLAN.StopBroadcast();
        testLAN.StartAsServer();
    }

    private void OnMatchCreate(CreateMatchResponse response)
    {
        if (response.success)
        {
            Utility.SetAccessTokenForNetwork(response.networkId,
                                             new NetworkAccessToken(response.accessTokenString));
            //NetworkServer.Listen(new MatchInfo(response), 9000);

            StartServer(response);
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        else
        {
            popups.Show(string.Format("Create match failed: \"{0}\"", response.extendedInfo),
                        delegate { });
        }
    }

    public World world;
    public World.Info info;

    private void Start()
    {
        StartCoroutine(RefreshList());
    }

    private void RefreshLockButtons()
    {
        tilePalette.Refresh();
    }

    private int GetVersion(MatchDesc desc)
    {
        return int.Parse(gamename.Match(desc.name).Groups[2].Value);
    }

    private Dictionary<string, GameListing> lanGames
        = new Dictionary<string, GameListing>();

    private List<GameListing> matchGames = new List<GameListing>();

    private GameListing ConvertListing(MatchDesc desc)
    {
        var listing = new GameListing();

        ParseName(desc.name,
                  out listing.name,
                  out listing.version,
                  out listing.count);

        listing.count = desc.currentSize;
        listing.match = desc;

        return listing;
    }

    private void RefreshListing()
    {
        worlds.SetActive(lanGames.Values.Concat(matchGames));
    }

    private bool ParseName(string full, 
                           out string name,
                           out Version version, 
                           out int count)
    {
        var match = gamename.Match(full);

        version = Version.DEV;
        name = "none";
        count = 0;

        if (match.Success)
        {
            name = match.Groups[1].Value;
            version = (Version) int.Parse(match.Groups[2].Value);
            count = int.Parse(match.Groups[3].Value);
        }

        Debug.Log(full + " = " + name + "/" + version + "/" + count);

        return match.Success;
    }

    private IEnumerator RefreshList()
    {
        testLAN.Initialize();
        //testLAN.StopBroadcast();
        testLAN.StartAsClient();
        bool test = false;
        testLAN.OnReceive += (ip, data) =>
        {
            var listing = new GameListing
            {
                address = ip,
            };

            lanGames[ip] = listing;

            ParseName(data,
                      out listing.name,
                      out listing.version,
                      out listing.count);

            listing.name = "(LOCAL) " + listing.name;
        };

        while (hostID == -1)
        {
            testLAN.broadcastData = hostedname + "!" + (int)version + "?" + clients.Count;

            var request = new ListMatchRequest();
            request.nameFilter = "";
            request.pageSize = 32;

            match.ListMatches(request, matches =>
            {
                matchGames.Clear();

                if (!matches.success)
                {
                    newerVersionDisableObject.SetActive(false);
                    noWorldsDisableObject.SetActive(false);
                    cantConnectDisableObject.SetActive(true);

                    RefreshListing();

                    return;
                }

                var list = matches.matches;

                var valid = list.Where(m => GetVersion(m) == (int)version);
                var newer = list.Where(m => GetVersion(m) >  (int)version);

                newerVersionDisableObject.SetActive(newer.Any());
                noWorldsDisableObject.SetActive(!newer.Any() && !valid.Any());
                cantConnectDisableObject.SetActive(false);
                worlds.SetActive(valid.Select(m => ConvertListing(m)));

                matchGames.Clear();
                matchGames.AddRange(valid.Select(m => ConvertListing(m)));

                if (selected != null
                 && selected.match != null
                 && !list.Any(m => m.networkId == selected.match.networkId))
                {
                    DeselectMatch();
                }

                RefreshListing();
            });

            yield return new WaitForSeconds(5);
        }
    }

    private GameListing selected;

    public void SelectMatch(GameListing desc)
    {
        selected = desc;
        detailsDisableObject.SetActive(true);
        worldDescription.text = desc.name;

        passwordObject.SetActive(desc.match != null && desc.match.isPrivate);
        passwordInput.text = "";
    }

    public void DeselectMatch()
    {
        selected = null;
        detailsDisableObject.SetActive(false);
    }

    private void SetWorld(World world)
    {
        worldView.SetWorld(world);

        tilePalette.Setup(world,
                          locks,
                          RequestTile);

        for (int i = 0; i < world.palette.Length; ++i)
        {
            //Debug.Log(string.Format("_Palette{0:D2}", i));

            paletteMaterial.SetColor(string.Format("_Palette{0:D2}", i), world.palette[i]);
        }
    }

    private void PreConnect()
    {
        world = new World();
        world.StaticiseTileset();
        SetWorld(world);

        group.interactable = false;
    }

    private void OnClickedEnter()
    {
        if (selected != null)
        {
            PreConnect();

            if (selected.match != null)
            {
                match.JoinMatch(selected.match.networkId, passwordInput.text, OnMatchJoined);
            }
            else
            {
                ConnectThroughLAN(selected.address);
            }
        }
    }

#if UNITY_EDITOR
    [MenuItem("Edit/Reset Playerprefs")]
    public static void DeletePlayerPrefs() { PlayerPrefs.DeleteAll(); }
#endif

    private void OnMatchJoined(JoinMatchResponse response)
    {
        if (response.success)
        {
            //gameObject.SetActive(false);

            ConnectThroughRelay(response);
        }
        else
        {
            group.interactable = true;

            popups.Show(string.Format("Couldn't join: \"{0}\"", response.extendedInfo),
                        delegate { });
        }
    }

    private int hostID = -1;

    private Sprite DefaultSmoolsona()
    {
        var texture = BlankTexture.New(32, 32, Color.clear);
        texture.SetPixels32(defaultAvatar.GetPixels32());
        texture.Apply();

        return BlankTexture.FullSprite(texture);
    }

    private void SetupHost(bool hosting)
    {
        Debug.Log("Initializing network transport");
        NetworkTransport.Init();
        var config = new ConnectionConfig();
        config.AddChannel(QosType.ReliableSequenced);
        config.AddChannel(QosType.Reliable);
        config.AddChannel(QosType.Unreliable);
        var topology = new HostTopology(config, 8);

        this.hosting = hosting;

        if (hosting)
        {
            AddAvatar(NewAvatar(0));

            worldView.viewer = world.avatars[0];
            worldView.viewer.graphic.texture.SetPixels32(avatarGraphic.GetPixels32());
            world.PalettiseTexture(worldView.viewer.graphic.texture, true);

            hostID = NetworkTransport.AddHost(topology, 9002);
        }
        else
        {
            hostID = NetworkTransport.AddHost(topology);
        }
    }

    private byte[] recvBuffer = new byte[65535];
    private bool connected;
    private bool hosting;
    private List<int> connectionIDs = new List<int>();

    private bool stickypalette;

    private Dictionary<int, byte[]> tiledata
        = new Dictionary<int, byte[]>();

    public enum Type
    {
        Tileset,
        Tilemap,
        Walls,
        Palette,

        TileImage,
        TileChunk,
        TileStroke,

        ReplicateAvatar,
        DestroyAvatar,
        MoveAvatar,
        GiveAvatar,
        NameAvatar,
        AvatarChunk,

        Chat,

        SetTile,
        SetWall,

        LockTile,
    }

    private World.Avatar NewAvatar(int connectionID)
    {
        return new World.Avatar
        {
            id = connectionID,
            destination = new Vector2(0, 0),
            source = new Vector2(0, 0),
            graphic = BlankTexture.FullSprite(BlankTexture.New(32, 32, Color.clear)),
        };
    }

    private void OnNewClientConnected(int connectionID)
    {
        Debug.Log("New client: " + connectionID);

        connectionIDs.Add(connectionID);

        var avatar = NewAvatar(connectionID);

        AddAvatar(avatar);

        clients.Add(new Client
        {
            connectionID = connectionID,
            avatar = avatar,
        });

        StartCoroutine(SendWorld(connectionID, world));
    }

    private void OnConnectedToHost(int connectionID)
    {
        Debug.Log("connected to host: " + connectionID);

        group.alpha = 0f;
        group.blocksRaycasts = false;

        connectionIDs.Add(connectionID);
    }

    private void OnClientDisconnected(int connectionID)
    {
        Debug.Log("Client disconnected: " + connectionID);

        var client = clients.Where(c => c.connectionID == connectionID).First();
        clients.Remove(client);
        connectionIDs.Remove(connectionID);

        RemoveAvatar(client.avatar);
    }

    private void OnDisconnectedFromHost(int connectionID)
    {
        popups.Show("Disconnected from host.",
                    () => SceneManager.LoadScene("Main"));
    }

    private class Client
    {
        public int connectionID;
        public World.Avatar avatar;
    }

    private List<Client> clients = new List<Client>();

    private void AddAvatar(World.Avatar avatar)
    {
        world.avatars.Add(avatar);

        if (hosting)
        {
            SendAll(ReplicateAvatarMessage(avatar), 0);
        }

        worldView.RefreshAvatars();
        Chat(avatar, "[ENTERED]");
    }

    private void RemoveAvatar(World.Avatar avatar)
    {
        world.avatars.Remove(avatar);

        if (hosting)
        {
            var unlock = locks.Where(pair => pair.Value == avatar)
                              .Select(pair => pair.Key)
                              .ToArray();

            foreach (byte tile in unlock)
            {
                locks.Remove(tile);

                SendAll(LockTileMessage(null, tile));
            }

            SendAll(DestroyAvatarMessage(avatar), 0);
        }

        Chat(avatar, "[EXITED]");
        worldView.RefreshAvatars();
    }

    private void Send(int connectionID, byte[][] data, int channelID = 0)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            Send(connectionID, data[i], channelID);
        }
    }

    private void Send(int connectionID, byte[] data, int channelID = 0)
    {
        sends.Enqueue(new Message
        {
            channel = channelID,
            connections = new int[] { connectionID },
            data = data,
        });
    }

    private void SendAll(byte[][] data,
                         int channelID = 0,
                         int except = -1)
    {
        for (int i = 0; i < data.Length; ++i)
        {
            SendAll(data[i], channelID, except);
        }
    }

    private void SendAll(byte[] data,
                         int channelID = 0,
                         int except = -1)
    {
        sends.Enqueue(new Message
        {
            channel = channelID,
            connections = hosting ? clients.Select(client => client.connectionID).Where(id => id != except).ToArray()
                                  : new [] { 1 },
            data = data,
        });
    }

    private byte[] ReplicateAvatarMessage(World.Avatar avatar)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.ReplicateAvatar);
        writer.Write(avatar.id);
        writer.Write(avatar.destination);
        writer.Write(avatar.source);

        return writer.AsArray();
    }

    private void ReceiveCreateAvatar(NetworkReader reader)
    {
        var avatar = new World.Avatar
        {
            id = reader.ReadInt32(),
            destination = reader.ReadVector2(),
            source = reader.ReadVector2(),
            graphic = BlankTexture.FullSprite(BlankTexture.New(32, 32, Color.clear)),
        };

        AddAvatar(avatar);
    }

    private byte[] DestroyAvatarMessage(World.Avatar avatar)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.DestroyAvatar);
        writer.Write(avatar.id);

        return writer.AsArray();
    }

    private void ReceiveDestroyAvatar(NetworkReader reader)
    {
        World.Avatar avatar = ID2Avatar(reader.ReadInt32());

        RemoveAvatar(avatar);
    }

    private byte[] GiveAvatarMessage(World.Avatar avatar)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.GiveAvatar);
        writer.Write(avatar.id);

        return writer.AsArray();
    }

    private void ReceiveGiveAvatar(NetworkReader reader)
    {
        World.Avatar avatar = ID2Avatar(reader.ReadInt32());

        worldView.viewer = avatar;

        avatar.graphic.texture.SetPixels32(avatarGraphic.GetPixels32());
        world.PalettiseTexture(avatar.graphic.texture, true);

        SendAll(AvatarInChunksMessages(world, worldView.viewer));
    }

    private byte[] MoveAvatarMessage(World.Avatar avatar,
                                     Vector2 destination)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.MoveAvatar);
        writer.Write(avatar.id);
        writer.Write(destination);

        return writer.AsArray();
    }

    private byte[] ChatMessage(World.Avatar avatar, string message)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.Chat);
        writer.Write(avatar.id);
        writer.Write(message);

        return writer.AsArray();
    }

    private void ReceiveSetTile(NetworkReader reader)
    {
        int location = reader.ReadInt32();
        byte tile = reader.ReadByte();

        if (hosting)
        {
            SendAll(SetTileMessage(location, tile));
        }

        if (world.tilemap[location] != tile)
        {
            audio.PlayOneShot(placeSound);
            worldView.SetTile(location, tile);
        }
    }

    private byte[] SetTileMessage(int location,
                                  byte tile)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.SetTile);
        writer.Write(location);
        writer.Write(tile);

        return writer.AsArray();
    }

    private void ReceiveSetWall(NetworkReader reader)
    {
        byte tile = reader.ReadByte();
        bool wall = reader.ReadBoolean();

        if (hosting)
        {
            SendAll(SetWallMessage(tile, wall));
        }

        if (world.walls.Contains(tile) != wall)
        {
            audio.PlayOneShot(placeSound);

            if (wall)
            {
                world.walls.Add(tile);
            }
            else
            {
                world.walls.Remove(tile);
            }
        }

        worldView.RefreshWalls();
    }

    private byte[] SetWallMessage(byte tile, bool wall)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.SetWall);
        writer.Write(tile);
        writer.Write(wall);

        return writer.AsArray();
    }

    private byte[] LockTileMessage(World.Avatar avatar, byte tile)
    {
        var writer = new NetworkWriter();
        writer.Write((byte) Type.LockTile);
        writer.Write(avatar != null ? avatar.id : -1);
        writer.Write(tile);

        return writer.AsArray();
    }

    public void RequestTile(byte tile)
    {
        if (locks.ContainsKey(tile)) return;

        if (hosting)
        {
            locks[tile] = worldView.viewer;

            OpenForEdit(tile);
        }

        SendAll(LockTileMessage(worldView.viewer, tile));
    }

    public void ReleaseTile(byte tile)
    {
        if (!locks.ContainsKey(tile)
         || locks[tile] != worldView.viewer) return;

        if (hosting)
        {
            locks.Remove(tile);
        }

        SendAll(LockTileMessage(null, tile));

        tilePalette.Refresh();
    }

    private void PackBits(NetworkWriter writer,
                          params uint[] values)
    {
        int offset = 0;
        uint final = 0;

        for (int i = 0; i < values.Length; i += 2)
        {
            uint length = values[i + 0];
            uint value  = values[i + 1];

            final |= (value << offset);

            offset += (int) length;
        }

        writer.Write(final);
    }
    
    private uint[] UnpackBits(NetworkReader reader,
                              params uint[] lengths)
    {
        int offset = 0;
        uint final = reader.ReadUInt32();

        uint[] values = new uint[lengths.Length];

        for (int i = 0; i < lengths.Length; ++i)
        {
            int length = (int) lengths[i];
            uint mask = ((uint) 1 << length) - 1;

            values[i] = (final & (mask << offset)) >> offset;

            offset += length;
        }

        return values;
    }
    
    // line only
    private byte[] StrokeMessage(byte tile,
                                 Color color,
                                 int size,
                                 Vector2 start,
                                 Vector2 end)
    {
        var writer = new NetworkWriter();

        uint sx = (uint) Mathf.FloorToInt(start.x);
        uint sy = (uint) Mathf.FloorToInt(start.y);
        uint ex = (uint) Mathf.FloorToInt(end.x);
        uint ey = (uint) Mathf.FloorToInt(end.y);

        byte index = world.ColorToPalette(color);

        writer.Write((byte) Type.TileStroke);
        writer.Write(tile);
        PackBits(writer,
                 5, sx,
                 5, sy,
                 5, ex,
                 5, ey,
                 4, index,
                 4, (uint) size);

        return writer.ToArray();
    }

    private void ReceiveTileStroke(NetworkReader reader,
                                   int connectionID)
    {
        byte tile = reader.ReadByte();
        uint[] values = UnpackBits(reader, 5, 5, 5, 5, 4, 4);

        Vector2 start = new Vector2(values[0], values[1]);
        Vector2 end = new Vector2(values[2], values[3]);
        Color color = world.palette[values[4]];
        int thickness = (int)values[5];

        if (hosting)
        {
            if (!locks.ContainsKey(tile) || locks[tile].id != connectionID) return;

            SendAll(StrokeMessage(tile, color, thickness, start, end), except: connectionID);
        }

        SpriteDrawing sprite = world.tiles[tile];

        if (thickness > 0)
        {
            sprite.DrawLine(start, end, thickness, color, Blend.Alpha);
        }
        else
        {
            sprite.Fill(start, color);
        }

        sprite.Sprite.texture.Apply();
    }

    public void SendStroke(byte tile,
                            Vector2 start,
                            Vector2 end,
                            Color color,
                            int size)
    {
        SendAll(StrokeMessage(tile, color, size, start, end));
    }

    private void OpenForEdit(byte tile)
    {
        tileEditor.OpenAndEdit(world.palette,
                               world.tiles[tile],
                               () => SendAll(TileInChunksMessages(world, tile)),
                               () => ReleaseTile(tile),
                               (start, end, color, size) => SendStroke(tile, start, end, color, size),
                               repeat: true);
    }

    private void ReceiveLockTile(NetworkReader reader)
    {
        World.Avatar avatar = ID2Avatar(reader.ReadInt32());
        byte tile = reader.ReadByte();

        if (avatar != null)
        {
            Debug.LogFormat("{0} locking {1}", avatar.id, tile);

            locks[tile] = avatar;

            if (avatar == worldView.viewer) OpenForEdit(tile);

            if (hosting) SendAll(LockTileMessage(avatar, tile));
        }
        else if (locks.ContainsKey(tile))
        {
            Debug.LogFormat("unlocking {0}", tile);

            locks.Remove(tile);

            if (hosting) SendAll(LockTileMessage(null, tile));
        }

        tilePalette.Refresh();
    }

    public World.Avatar ID2Avatar(int id)
    {
        if (id == -1) return null;

        return world.avatars.Where(a => a.id == id).First();
    }

    private bool Blocked(World.Avatar avatar,
                         Vector2 destination)
    {
        int location = (int)((destination.y + 16) * 32 + (destination.x + 16));
        byte tile = (location >= 0 && location < 1024) ? world.tilemap[location] : (byte)0;

        return (avatar.destination - destination).magnitude > 1
            || !Rect.MinMaxRect(-16, -16, 16, 16).Contains(destination)
            || world.walls.Contains(tile);
    }

    private void Move(Vector2 direction)
    {
        var avatar = worldView.viewer;

        if (avatar != null
         && avatar.source == avatar.destination)
        {
            if (Blocked(avatar, avatar.destination + direction))
            {
                if (!blockSource.isPlaying) blockSource.Play();
            }
            else
            {
                avatar.destination = avatar.destination + direction;

                worldView.RefreshAvatars();

                tutorialMove.SetActive(false);

                SendAll(MoveAvatarMessage(avatar, avatar.destination));
            }
        }
    }

    private class Message
    {
        public int[] connections;
        public int channel;
        public byte[] data;
    }

    private Queue<Message> sends = new Queue<Message>();
    private Dictionary<byte, World.Avatar> locks
        = new Dictionary<byte, World.Avatar>();

    private IEnumerator SendMessages()
    {
        while (true)
        {
            if (sends.Count > 0)
            {
                Message message = sends.Dequeue();

                byte error;

                for (int i = 0; i < message.connections.Length; ++i)
                {
                    while (!NetworkTransport.Send(hostID,
                                                  message.connections[i],
                                                  message.channel,
                                                  message.data,
                                                  message.data.Length,
                                                  out error))
                    {
                        Debug.LogFormat("CANT: {0} ({1} in queue)", (NetworkError)error, sends.Count);

                        if ((NetworkError) error == NetworkError.WrongConnection)
                        {
                            break;
                        }

                        yield return null;
                    }
                }

                //Type type = (Type)(new NetworkReader(message.data).ReadInt32());
                //Debug.LogFormat("SENT: {0}", type);
            }
            else
            {
                yield return null;
            }
        }
    }

    private void Chat(World.Avatar avatar, string message)
    {
        audio.PlayOneShot(speakSound);

        worldView.Chat(avatar, message);

        chats.Add(new LoggedMessage
        {
            avatar = avatar,
            message = message,
        });

        chatOverlay.Refresh();
    }

    private void OpenMenu()
    {
        
    }

    private void CloseMenu()
    {

    }

    private void Update()
    {
        bool editing = tileEditor.gameObject.activeSelf;
        bool chatting = chatOverlay.gameObject.activeSelf;
        bool mapping = Input.GetKey(KeyCode.Tab)
                    && !chatting;

        if (editing && !chatting)
        {
            tileEditor.CheckInput();
        }

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (chatting)
            {
                chatOverlay.Hide();
            }
            else if (editing)
            {
                tileEditor.OnClickedSave();
            }
            else if (hostID != -1)
            {
                OnApplicationQuit();
                SceneManager.LoadScene("Main");
                return;
            }
            else
            {
                Application.Quit();
                return;
            }
        }
        else if (Input.GetKeyDown(KeyCode.F12))
        {
            string selfies = Application.persistentDataPath + "/selfies";

            Directory.CreateDirectory(selfies);

            Application.CaptureScreenshot(string.Format("{0}/{1}.png", selfies, System.DateTime.Now.Ticks));
        }
        else if (Input.GetKeyDown(KeyCode.F11))
        {
            string maps = Application.persistentDataPath + "/maps";

            Directory.CreateDirectory(maps);

            mapCamera.Render();

            var old = RenderTexture.active;
            RenderTexture.active = mapTexture;
            mapTextureLocal.ReadPixels(Rect.MinMaxRect(0, 0, 1024, 1024), 0, 0);
            RenderTexture.active = old;

            File.WriteAllBytes(string.Format("{0}/{1}.png", maps, System.DateTime.Now.Ticks), mapTextureLocal.EncodeToPNG());
        }
        else if (Input.GetKeyDown(KeyCode.F2))
        {
            scale = 3 - scale;

            Screen.SetResolution(512 * scale, 512 * scale, false);
        }

        if (hostID == -1) return;


        config.hideTutorial |= !tutorialChat.activeSelf
                            && !tutorialMove.activeSelf
                            && !tutorialTile.activeSelf
                            && !tutorialWall.activeSelf;

        tutorialObject.SetActive(!config.hideTutorial);

        mapCamera.gameObject.SetActive(mapping);
        mapObject.SetActive(mapping);

        camera.orthographicSize = Mathf.Lerp(128, 32, zoom);

        if (Input.GetKeyDown(KeyCode.Return))
        {
            if (chatting)
            {
                chatOverlay.OnClickedSend();
            }
            else
            {
                chatOverlay.Show();
            }
        }

        if (!chatting && !editing)
        {
            if (Input.GetKey(KeyCode.UpArrow) || Input.GetKey(KeyCode.W))
            {
                Move(Vector2.up);
            }
            else if (Input.GetKey(KeyCode.LeftArrow) || Input.GetKey(KeyCode.A))
            {
                Move(Vector2.left);
            }
            else if (Input.GetKey(KeyCode.RightArrow) || Input.GetKey(KeyCode.D))
            {
                Move(Vector2.right);
            }
            else if (Input.GetKey(KeyCode.DownArrow) || Input.GetKey(KeyCode.S))
            {
                Move(Vector2.down);
            }
        }

        if (Input.GetKeyDown(KeyCode.Space) && stickypalette)
        {
            stickypalette = false;
        }

        if (!chatting 
         && !editing 
         && Input.GetKey(KeyCode.Space))
        {
            if (!tilePalette.gameObject.activeSelf)
            {
                stickypalette = Input.GetKey(KeyCode.LeftControl);

                tilePalette.Show();
            }
        }
        else if (!stickypalette)
        {
            tilePalette.Hide();
        }

        if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()
         && Rect.MinMaxRect(0, 0, Screen.width, Screen.height).Contains(Input.mousePosition)
         && !editing)
        {
            bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool waller = !picker && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            tileCursor.gameObject.SetActive(true);
            pickerCursor.SetActive(picker);
            tileCursor.sprite = this.world.tiles[tilePalette.SelectedTile];

            Vector2 mouse = Input.mousePosition;
            Vector3 world;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(worldView.transform as RectTransform,
                                                                    mouse,
                                                                    camera,
                                                                    out world);

            int x = Mathf.FloorToInt(world.x / 32);
            int y = Mathf.FloorToInt(world.y / 32);

            tileCursor.transform.position = new Vector2(x * 32, y * 32);

            byte tile = tilePalette.SelectedTile;
            int location = (y + 16) * 32 + (x + 16);

            if (location >= 0
             && location < 1024)
            {
                if (waller && Input.GetMouseButtonDown(0))
                {
                    tile = this.world.tilemap[location];
                    bool wall = !this.world.walls.Contains(tile);

                    SendAll(SetWallMessage(tile, wall));

                    if (this.world.walls.Set(tile, wall))
                    {
                        tutorialWall.SetActive(false);

                        audio.PlayOneShot(placeSound);
                    }

                    worldView.RefreshWalls();
                }
                else if (!waller && Input.GetMouseButton(0))
                {
                    if (!picker && this.world.tilemap[location] != tile)
                    {
                        audio.PlayOneShot(placeSound);
                        worldView.SetTile(location, tile);

                        if (tile != 0) tutorialTile.SetActive(false);

                        SendAll(SetTileMessage(location, tile));
                    }
                    else if (picker)
                    {
                        tilePalette.SetSelectedTile(this.world.tilemap[location]);
                    }
                }
            }
        }
        else
        {
            tileCursor.gameObject.SetActive(false);
        }

        var eventType = NetworkEventType.Nothing;
        int connectionID;
        int channelId;
        int receivedSize;
        byte error;

        do
        {
            // Get events from the server/client game connection
            eventType = NetworkTransport.ReceiveFromHost(hostID,
                                                         out connectionID,
                                                         out channelId,
                                                         recvBuffer,
                                                         recvBuffer.Length,
                                                         out receivedSize,
                                                         out error);
            if ((NetworkError)error != NetworkError.Ok)
            {
                group.interactable = true;

                popups.Show("Network Error: " + (NetworkError)error,
                            delegate { });
            }

            if (eventType == NetworkEventType.ConnectEvent)
            {
                connected = true;

                if (hosting)
                {
                    OnNewClientConnected(connectionID);
                }
                else
                {
                    OnConnectedToHost(connectionID);
                }
            }
            else if (eventType == NetworkEventType.DisconnectEvent)
            {
                if (hosting)
                {
                    OnClientDisconnected(connectionID);
                }
                else
                {
                    OnDisconnectedFromHost(connectionID);
                }
            }
            else if (eventType == NetworkEventType.DataEvent)
            {
                var reader = new NetworkReader(recvBuffer);

                {
                    Type type = (Type) reader.ReadByte();

                    if (type == Type.Tilemap)
                    {
                        world.tilemap = reader.ReadBytesAndSize();
                        worldView.SetWorld(world);
                    }
                    else if (type == Type.Palette)
                    {
                        for (int i = 0; i < 16; ++i)
                        {
                            world.palette[i] = reader.ReadColor32();
                            paletteMaterial.SetColor(string.Format("_Palette{0:D2}", i), world.palette[i]);
                        }
                    }
                    else if (type == Type.Walls)
                    {
                        world.walls.Clear();

                        foreach (var wall in reader.ReadBytesAndSize())
                        {
                            world.walls.Add(wall);
                        }

                        worldView.RefreshWalls();
                    }
                    else if (type == Type.Tileset)
                    {
                        int id = reader.ReadInt32();

                        tiledata[id] = reader.ReadBytesAndSize();
                    }
                    else if (type == Type.ReplicateAvatar)
                    {
                        ReceiveCreateAvatar(reader);
                    }
                    else if (type == Type.DestroyAvatar)
                    {
                        ReceiveDestroyAvatar(reader);
                    }
                    else if (type == Type.GiveAvatar)
                    {
                        ReceiveGiveAvatar(reader);
                    }
                    else if (type == Type.MoveAvatar)
                    {
                        World.Avatar avatar = ID2Avatar(reader.ReadInt32());
                        Vector2 dest = reader.ReadVector2();

                        if (hosting)
                        {
                            if (connectionID == avatar.id
                             && !Blocked(avatar, dest))
                            {
                                avatar.source = avatar.destination;
                                avatar.destination = dest;
                                avatar.u = 0;

                                SendAll(MoveAvatarMessage(avatar, avatar.destination), except: avatar.id);
                            }
                            else
                            {
                                Send(connectionID, MoveAvatarMessage(avatar, avatar.destination));
                            }
                        }
                        else
                        {
                            avatar.source = avatar.destination;
                            avatar.destination = dest;
                            avatar.u = 0;
                        }
                    }
                    else if (type == Type.Chat)
                    {
                        World.Avatar avatar = ID2Avatar(reader.ReadInt32());
                        string message = reader.ReadString();

                        if (hosting)
                        {
                            if (connectionID == avatar.id)
                            {
                                SendAll(ChatMessage(avatar, message), except: connectionID);

                                Chat(avatar, message);
                            }
                        }
                        else
                        {
                            Chat(avatar, message);
                        }
                    }
                    else if (type == Type.SetTile)
                    {
                        ReceiveSetTile(reader);
                    }
                    else if (type == Type.SetWall)
                    {
                        ReceiveSetWall(reader);
                    }
                    else if (type == Type.TileChunk)
                    {
                        ReceiveTileChunk(reader, connectionID);
                    }
                    else if (type == Type.LockTile)
                    {
                        ReceiveLockTile(reader);
                    }
                    else if (type == Type.AvatarChunk)
                    {
                        ReceiveAvatarChunk(reader, connectionID);
                    }
                    else if (type == Type.TileStroke)
                    {
                        ReceiveTileStroke(reader, connectionID);
                    }
                }
            }
        }
        while (eventType != NetworkEventType.Nothing);
    }

    private void OnApplicationQuit()
    {
        byte error;

        for (int i = 0; i < connectionIDs.Count; ++i)
        {
            NetworkTransport.Disconnect(hostID, connectionIDs[i], out error);
        }

        NetworkTransport.Shutdown();

        SaveConfig();

        if (hostID != -1)
        {
            SaveWorld(info.root, info.name);
        }
    }

    void StartServer(CreateMatchResponse response)
    {
        SetupHost(true);

        byte error;
        NetworkTransport.ConnectAsNetworkHost(hostID,
                                              response.address,
                                              response.port,
                                              response.networkId,
                                              Utility.GetSourceID(),
                                              response.nodeId,
                                              out error);
    }

    void ConnectThroughRelay(JoinMatchResponse response)
    {
        SetupHost(false);

        byte error;
        NetworkTransport.ConnectToNetworkPeer(hostID,
                                              response.address,
                                              response.port,
                                              0,
                                              0,
                                              response.networkId,
                                              Utility.GetSourceID(),
                                              response.nodeId,
                                              out error);
    }

    void ConnectThroughLAN(string address)
    {
        SetupHost(false);

        int port = 9002;

        byte error;
        NetworkTransport.Connect(hostID, address, port, 0, out error);

        Debug.Log((NetworkError) error);
    }

    public static int ColorDistance(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r)
             + Mathf.Abs(a.g - b.g)
             + Mathf.Abs(a.b - b.b);
    }

    private byte[][] AvatarInChunksMessages(World world,
                                            World.Avatar avatar,
                                            int size = 128)
    {
        Color32[] colors = avatar.graphic.texture.GetPixels32();
        byte[] bytes = colors.Select(c => world.ColorToPalette(c, true)).ToArray();
        byte[] chunk;

        int offset = 0;

        var messages = new List<byte[]>();

        while (bytes.Any())
        {
            chunk = bytes.Take(size).ToArray();
            bytes = bytes.Skip(size).ToArray();

            var writer = new NetworkWriter();
            writer.Write((byte) Type.AvatarChunk);
            writer.Write(avatar.id);
            writer.Write(offset);
            writer.WriteBytesFull(CrunchBytes(chunk));

            messages.Add(writer.AsArray());

            offset += size;
        }

        return messages.ToArray();
    }

    private void ReceiveAvatarChunk(NetworkReader reader, int connectionID)
    {
        World.Avatar avatar = ID2Avatar(reader.ReadInt32());
        int offset = reader.ReadInt32();
        byte[] chunk = UncrunchBytes(reader.ReadBytesAndSize());

        // if we're the host, disallow chunks not send by the owner
        if (hosting && avatar.id != connectionID) return;

        Color32[] colors = avatar.graphic.texture.GetPixels32();

        for (int i = 0; i < chunk.Length; ++i)
        {
            byte index = chunk[i];

            colors[i + offset] = index == 0 ? Color.clear : world.palette[index];
        }

        avatar.graphic.texture.SetPixels32(colors);
        avatar.graphic.texture.Apply();

        if (hosting)
        {
            SendAll(AvatarInChunksMessages(world, avatar));
        }
    }

    private byte[][] TileInChunksMessages(World world,
                                          byte tile,
                                          int size = 128)
    {
        int x = tile % 16;
        int y = tile / 16;

        Color[] colors = world.tileset.GetPixels(x * 32, y * 32, 32, 32);
        byte[] bytes = colors.Select(c => world.ColorToPalette(c)).ToArray();
        byte[] chunk;

        int offset = 0;

        var messages = new List<byte[]>();

        while (bytes.Any())
        {
            chunk = bytes.Take(size).ToArray();
            bytes = bytes.Skip(size).ToArray();

            var writer = new NetworkWriter();
            writer.Write((byte) Type.TileChunk);
            writer.Write(tile);
            writer.Write(offset);
            writer.WriteBytesFull(CrunchBytes(chunk));

            messages.Add(writer.AsArray());

            offset += size;
        }

        return messages.ToArray();
    }

    private byte[] CrunchBytes(byte[] bytes)
    {
        byte[] crunched = new byte[bytes.Length / 2];

        for (int i = 0; i < crunched.Length; ++i)
        {
            byte a = bytes[i * 2 + 0];
            byte b = bytes[i * 2 + 1];

            crunched[i] = (byte) ((a << 4) | b); 
        }

        return crunched;
    }

    private byte[] UncrunchBytes(byte[] crunched)
    {
        byte[] bytes = new byte[crunched.Length * 2];
        
        for (int i = 0; i < crunched.Length; ++i)
        {
            byte a = (byte) ((crunched[i] & 0xF0) >> 4);
            byte b = (byte) (crunched[i] & 0xF);

            bytes[i * 2 + 0] = a;
            bytes[i * 2 + 1] = b;
        }

        return bytes;
    }

    private void ReceiveTileChunk(NetworkReader reader, int connectionID)
    {
        byte tile = reader.ReadByte();
        int offset = reader.ReadInt32();
        byte[] chunk = UncrunchBytes(reader.ReadBytesAndSize());

        int x = tile % 16;
        int y = tile / 16;

        bool locked = locks.ContainsKey(tile);

        // if we're the host, disallow chunks not send by someone with a lock
        if (hosting && (!locked || locks[tile].id != connectionID)) return;

        // we're editing this tile, so ignore it
        if (locked && locks[tile] == worldView.viewer) return;

        Color[] colors = world.tileset.GetPixels(x * 32, y * 32, 32, 32);

        for (int i = 0; i < chunk.Length; ++i)
        {
            colors[i + offset] = world.palette[chunk[i]];
        }

        world.tileset.SetPixels(x * 32, y * 32, 32, 32, colors);
        world.tileset.Apply();

        if (hosting)
        {
            SendAll(TileInChunksMessages(world, tile));
        }
    }

    private IEnumerator SendWorld(int connectionID, World world)
    {
        {
            var writer = new NetworkWriter();

            writer.Write((byte) Type.Tilemap);
            writer.WriteBytesFull(world.tilemap);

            Send(connectionID, writer.AsArray());
        }

        {
            var writer = new NetworkWriter();

            writer.Write((byte) Type.Palette);
            
            for (int i = 0; i < 16; ++i) writer.Write((Color32) world.palette[i]);

            Send(connectionID, writer.AsArray());
        }

        {
            var writer = new NetworkWriter();

            writer.Write((byte) Type.Walls);
            writer.WriteBytesFull(world.walls.ToArray());

            Send(connectionID, writer.AsArray());
        }

        foreach (var avatar in world.avatars)
        {
            Send(connectionID, ReplicateAvatarMessage(avatar));

            if (avatar.id == connectionID)
            {
                Send(connectionID, GiveAvatarMessage(avatar));
            }
            else
            {
                Send(connectionID, AvatarInChunksMessages(world, avatar));
            }
        }

        for (int i = 0; i < maxTiles; ++i)
        {
            yield return new WaitForSeconds(0.125f);

            Send(connectionID, TileInChunksMessages(world, (byte) i));
        }
    }

    public static IEnumerable<World.Info> GetSavedWorlds()
    {
        var worlds = Application.persistentDataPath + "/worlds";

        Directory.CreateDirectory(worlds);

        foreach (var folder in Directory.GetDirectories(worlds))
        {
            string root = Path.GetFileName(folder);
            World.Info info = null;

            try
            {
                info = JsonWrapper.Deserialise<World.Info>(File.ReadAllText(folder + "/info.json"));
                info.root = root;
            }
            catch (System.Exception exception)
            {
                Debug.LogWarningFormat("Couldn't read world info for {0}\n{1}", root, exception);
            }

            if (info != null) yield return info;
        }
    }
}
