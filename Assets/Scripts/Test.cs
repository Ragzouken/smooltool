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

using System.Text.RegularExpressions;

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Test : MonoBehaviour
{
    [SerializeField] private Font[] fonts;

    [SerializeField] private new AudioSource audio;
    [SerializeField] private AudioClip speakSound;
    [SerializeField] private AudioClip placeSound;

    private Dictionary<Color32, byte> col2pal = new Dictionary<Color32, byte>();
    private Dictionary<byte, Color32> pal2col = new Dictionary<byte, Color32>();

    [SerializeField] private CanvasGroup group;
    [SerializeField] private Texture2D testtex;
    [SerializeField] private WorldView worldView;
    [SerializeField] private Popups popups;

    [Header("Host")]
    [SerializeField] private InputField titleInput;
    [SerializeField] private Button createButton;
    [SerializeField] private InputField hostPasswordInput;

    [Header("List")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private WorldPanel worldPrefab;
    [SerializeField] private GameObject newerVersionDisableObject;
    [SerializeField] private GameObject noWorldsDisableObject;
    [SerializeField] private GameObject cantConnectDisableObject;

    private MonoBehaviourPooler<MatchDesc, WorldPanel> worlds;

    [Header("Details")]
    [SerializeField] private GameObject detailsDisableObject;
    [SerializeField] private Text worldDescription;
    [SerializeField] private GameObject passwordObject;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button enterButton;

    [Header("Chat")]
    [SerializeField] private GameObject chatObject;
    [SerializeField] private InputField chatInput;
    [SerializeField] private RectTransform chatLogContainer;
    [SerializeField] private ChatLogElement chatLogPrefab;

    [SerializeField] private new Camera camera;
    [SerializeField] private Image tileCursor;
    [SerializeField] private GameObject pickerCursor;

    [Header("Tile Palette")]
    [SerializeField] private GameObject paletteObject;
    [SerializeField] private RectTransform paletteContainer;
    [SerializeField] private TileToggle tilePrefab;
    [SerializeField] private Button lockButton;

    [SerializeField] private TileEditor tileEditor;
    [SerializeField] private CustomiseTab customiseTab;

    [SerializeField] private Texture2D defaultAvatar;
    private Texture2D avatarGraphic;

    private struct LoggedMessage
    {
        public World.Avatar avatar;
        public string message;
    }

    private MonoBehaviourPooler<byte, TileToggle> tiles;
    private MonoBehaviourPooler<LoggedMessage, ChatLogElement> chatLog;
    private List<LoggedMessage> chats = new List<LoggedMessage>();

    private NetworkMatch match;

    private byte paintTile;

    private const byte maxTiles = 32;

    public enum Version
    {
        DEV,
        PRE1,
        PRE2,
        PRE3,
        PRE4,
    }

    public static Version version = Version.PRE4;

    private void Awake()
    {
        foreach (Font font in fonts) font.material.mainTexture.filterMode = FilterMode.Point;

        match = gameObject.AddComponent<NetworkMatch>();
        match.baseUri = new System.Uri("https://eu1-mm.unet.unity3d.com");

        createButton.onClick.AddListener(OnClickedCreate);
        enterButton.onClick.AddListener(OnClickedEnter);

        worlds = new MonoBehaviourPooler<MatchDesc, WorldPanel>(worldPrefab,
                                                                worldContainer,
                                                                InitialiseWorld);

        chatLog = new MonoBehaviourPooler<LoggedMessage, ChatLogElement>(chatLogPrefab,
                                                                         chatLogContainer,
                                                                         InitialiseChatLog);

        Application.runInBackground = true;

        StartCoroutine(SendMessages());

        avatarGraphic = BlankTexture.New(32, 32, Color.clear);
        ResetAvatar();

        customiseTab.Setup(tileEditor,
                           BlankTexture.FullSprite(avatarGraphic),
                           SaveConfig,
                           ResetAvatar);

        LoadConfig();
    }

    private void InitialiseWorld(MatchDesc desc, WorldPanel panel)
    {
        panel.SetMatch(desc);
    }

    private void InitialiseChatLog(LoggedMessage message, ChatLogElement element)
    {
        element.SetMessage(message.avatar, message.message);
    }

    private void ResetAvatar()
    {
        avatarGraphic.SetPixels32(defaultAvatar.GetPixels32());
        avatarGraphic.Apply();
    }

    private void OnClickedEditAvatar()
    {
        var palette = world.palette.ToArray();
        palette[0] = Color.clear;

        tileEditor.OpenAndEdit(palette,
                               BlankTexture.FullSprite(avatarGraphic),
                               SaveConfig,
                               delegate { });
    }

    private void SaveConfig()
    {
        var root = Application.persistentDataPath;

        System.IO.Directory.CreateDirectory(root + "/settings");
        System.IO.File.WriteAllBytes(root + "/settings/avatar.png", avatarGraphic.EncodeToPNG());
    }

    private void LoadConfig()
    {
        var root = Application.persistentDataPath;

        System.IO.Directory.CreateDirectory(root + "/settings");

        try
        {
            byte[] avatar = System.IO.File.ReadAllBytes(root + "/settings/avatar.png");

            avatarGraphic.LoadImage(avatar);
        }
        catch (System.Exception exception)
        {
            Debug.LogWarningFormat("Couldn't load an existing #smoolsona:\n{0}", exception);
        }
    }

    private void OnClickedCreate()
    {
        var create = new CreateMatchRequest();
        create.name = titleInput.text;
        create.size = 8;
        create.advertise = true;
        create.password = hostPasswordInput.text;

        create.name += "!" + (int)version;

        match.CreateMatch(create, OnMatchCreate);
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
    public Texture2D test;

    private void Start()
    {
        world = new World();

        for (int i = 0; i < 1024; ++i) world.tilemap[i] = (byte)Random.Range(0, 23);
        for (int i = 0; i < 256; ++i)
        {
            if (Random.value > 0.5f) world.walls.Add((byte)i);
        }

        worldView.SetWorld(world);

        test = world.tileset;

        StartCoroutine(RefreshList());

        tiles = new MonoBehaviourPooler<byte, TileToggle>(tilePrefab,
                                                          paletteContainer,
                                                          InitialiseTileToggle);

        lockButton.onClick.AddListener(delegate
        {
            RequestTile(paintTile);

            RefreshLockButtons();
        });

        RefreshLockButtons();
    }

    private void RefreshLockButtons()
    {
        bool locked = locks.ContainsKey(paintTile);

        lockButton.interactable = !locks.ContainsKey(paintTile);
    }

    private void SetPaintTile(byte tile)
    {
        paintTile = tile;

        RefreshLockButtons();
    }

    private void InitialiseTileToggle(byte tile, TileToggle toggle)
    {
        toggle.SetTile(world.tiles[tile], () => SetPaintTile(tile));
    }

    private int GetVersion(MatchDesc desc)
    {
        if (desc.name.Contains("!"))
        {
            string end = desc.name.Split('!').Last();
            int version;

            if (int.TryParse(end, out version))
            {
                return version;
            }
        }

        return 0;
    }

    private IEnumerator RefreshList()
    {
        while (true)
        {
            var request = new ListMatchRequest();
            request.nameFilter = "";
            request.pageSize = 32;

            match.ListMatches(request, matches =>
            {
                if (!matches.success)
                {
                    newerVersionDisableObject.SetActive(false);
                    noWorldsDisableObject.SetActive(false);
                    cantConnectDisableObject.SetActive(true);
                    worlds.Clear();

                    return;
                }

                var list = matches.matches;

                var valid = list.Where(m => GetVersion(m) == (int)version);
                var newer = list.Where(m => GetVersion(m) >  (int)version);

                newerVersionDisableObject.SetActive(newer.Any());
                noWorldsDisableObject.SetActive(!newer.Any() && !valid.Any());
                cantConnectDisableObject.SetActive(false);
                worlds.SetActive(valid);

                if (selected != null
                 && !list.Any(m => m.networkId == selected.networkId))
                {
                    DeselectMatch();
                }
            });

            yield return new WaitForSeconds(5);
        }
    }

    private MatchDesc selected;

    public void SelectMatch(MatchDesc desc)
    {
        selected = desc;
        detailsDisableObject.SetActive(true);
        worldDescription.text = string.Join("!", desc.name.Split('!')
                                                           .Reverse()
                                                           .Skip(1)
                                                           .Reverse()
                                                           .ToArray());

        passwordObject.SetActive(desc.isPrivate);
        passwordInput.text = "";
    }

    public void DeselectMatch()
    {
        selected = null;
        detailsDisableObject.SetActive(false);
    }

    private void OnClickedEnter()
    {
        if (selected != null)
        {
            group.interactable = false;

            match.JoinMatch(selected.networkId, passwordInput.text, OnMatchJoined);
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

        col2pal.Clear();
        pal2col.Clear();

        if (hosting)
        {
            for (int i = 0; i < 16; ++i)
            {
                col2pal[world.palette[i]] = (byte)i;
                pal2col[(byte)i] = world.palette[i];
            }

            var colors = testtex.GetPixels(0, 0, 512, 64)
                                .Select(color => ColorToPalette(color))
                                .Select(index => world.palette[index])
                                .ToArray();

            world.tileset.SetPixels(0, 0, 512, 64, colors);
            world.tileset.Apply();

            AddAvatar(NewAvatar(0));

            worldView.viewer = world.avatars[0];

            colors = avatarGraphic.GetPixels()
                                  .Select(color => ColorToPalette(color, true))
                                  .Select(index => index == 0 ? Color.clear : world.palette[index])
                                  .ToArray();

            worldView.viewer.graphic.texture.SetPixels(colors);
            worldView.viewer.graphic.texture.Apply();

            hostID = NetworkTransport.AddHost(topology, 9001);
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

        ReplicateAvatar,
        DestroyAvatar,
        MoveAvatar,
        GiveAvatar,
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

        SendWorld(connectionID, world);
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
                    () => SceneManager.LoadScene("test"));
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
        writer.Write((int)Type.ReplicateAvatar);
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
        writer.Write((int)Type.DestroyAvatar);
        writer.Write(avatar.id);

        return writer.AsArray();
    }

    private void ReceiveDestroyAvatar(NetworkReader reader)
    {
        int id = reader.ReadInt32();

        RemoveAvatar(world.avatars.Where(a => a.id == id).First());
    }

    private byte[] GiveAvatarMessage(World.Avatar avatar)
    {
        var writer = new NetworkWriter();
        writer.Write((int)Type.GiveAvatar);
        writer.Write(avatar.id);

        return writer.AsArray();
    }

    private void ReceiveGiveAvatar(NetworkReader reader)
    {
        int id = reader.ReadInt32();

        worldView.viewer = world.avatars.Where(a => a.id == id).First();

        var colors = avatarGraphic.GetPixels()
                                  .Select(color => ColorToPalette(color, true))
                                  .Select(index => index == 0 ? Color.clear : world.palette[index])
                                  .ToArray();

        worldView.viewer.graphic.texture.SetPixels(colors);
        worldView.viewer.graphic.texture.Apply();

        SendAll(AvatarInChunksMessages(world, worldView.viewer));
    }

    private byte[] MoveAvatarMessage(World.Avatar avatar,
                                     Vector2 destination)
    {
        var writer = new NetworkWriter();
        writer.Write((int)Type.MoveAvatar);
        writer.Write(avatar.id);
        writer.Write(destination);

        return writer.AsArray();
    }

    private byte[] ChatMessage(World.Avatar avatar, string message)
    {
        var writer = new NetworkWriter();
        writer.Write((int)Type.Chat);
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
        writer.Write((int)Type.SetTile);
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
        writer.Write((int)Type.SetWall);
        writer.Write(tile);
        writer.Write(wall);

        return writer.AsArray();
    }

    private byte[] LockTileMessage(World.Avatar avatar, byte tile)
    {
        var writer = new NetworkWriter();
        writer.Write((int)Type.LockTile);
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

        RefreshLockButtons();
    }

    private void OpenForEdit(byte tile)
    {
        tileEditor.OpenAndEdit(world.palette,
                               world.tiles[tile],
                               () => SendAll(TileInChunksMessages(world, tile)),
                               () => ReleaseTile(tile));
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

        RefreshLockButtons();
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

        return world.avatars.Any(a => a.destination == destination)
            || (avatar.destination - destination).magnitude > 1
            || !Rect.MinMaxRect(-16, -16, 16, 16).Contains(destination)
            || world.walls.Contains(tile);
    }

    private void Move(Vector2 direction)
    {
        var avatar = worldView.viewer;

        if (avatar != null
         && avatar.source == avatar.destination
         && !Blocked(avatar, avatar.destination + direction))
        {
            avatar.destination = avatar.destination + direction;

            worldView.RefreshAvatars();

            SendAll(MoveAvatarMessage(avatar, avatar.destination));
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

                Type type = (Type)(new NetworkReader(message.data).ReadInt32());

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

        chatLog.SetActive(chats.Reverse<LoggedMessage>().Take(8).Reverse());
    }

    private void Update()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;

        if (Input.GetKeyDown(KeyCode.Escape))
        {
            if (tileEditor.gameObject.activeSelf)
            {
                tileEditor.OnClickedSave();
            }
            else if (chatObject.gameObject.activeSelf)
            {
                chatObject.SetActive(false);
            }
            else if (hostID != -1)
            {
                OnApplicationQuit();
                SceneManager.LoadScene("test");
            }
            else
            {
                Application.Quit();
            }
        }

        if (hostID == -1) return;

        bool editing = tileEditor.gameObject.activeSelf;

        if (!chatObject.activeSelf
         && !editing)
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

        if (!editing)
        {
            if (Input.GetKeyDown(KeyCode.Return))
            {
                if (chatObject.activeSelf)
                {
                    string message = chatInput.text;

                    chatObject.SetActive(false);
                    chatInput.text = "";

                    var unlock = Regex.Match(message, @"(lock|unlock)\s(\d+)");

                    if (unlock.Success)
                    {
                        byte tile = byte.Parse(unlock.Groups[2].Value);
                        bool @lock = unlock.Groups[1].Value == "lock";

                        if (@lock && !locks.ContainsKey(tile))
                        {
                            RequestTile(tile);
                            audio.PlayOneShot(placeSound);
                        }
                        else if (!@lock && locks.ContainsKey(tile) && locks[tile] == worldView.viewer)
                        {
                            ReleaseTile(tile);
                            audio.PlayOneShot(placeSound);
                        }
                    }
                    else if (message.Trim().Length > 0)
                    {
                        SendAll(ChatMessage(worldView.viewer, message));

                        if (hosting) Chat(worldView.viewer, message);
                    }

                    UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
                }
                else
                {
                    chatObject.SetActive(true);
                    chatInput.text = "";

                    chatInput.Select();
                }
            }
            else if (chatObject.activeSelf && selected != chatInput.gameObject)
            {
                chatObject.SetActive(false);
            }
        }
        else
        {
            chatObject.SetActive(false);
            UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);
        }

        if (!chatObject.activeSelf && !editing && Input.GetKey(KeyCode.Space))
        {
            tiles.Clear();
            tiles.SetActive(Enumerable.Range(0, maxTiles).Select(i => (byte)i));
            paletteObject.SetActive(true);
        }
        else
        {
            paletteObject.SetActive(false);
        }

        if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()
         && Rect.MinMaxRect(0, 0, 512, 512).Contains(Input.mousePosition)
         && !editing)
        {
            bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);
            bool waller = !picker && (Input.GetKey(KeyCode.LeftShift) || Input.GetKey(KeyCode.RightShift));

            tileCursor.gameObject.SetActive(true);
            pickerCursor.SetActive(picker);
            tileCursor.sprite = this.world.tiles[paintTile];

            Vector2 mouse = Input.mousePosition;
            Vector3 world;

            RectTransformUtility.ScreenPointToWorldPointInRectangle(worldView.transform as RectTransform,
                                                                    mouse,
                                                                    camera,
                                                                    out world);

            int x = Mathf.FloorToInt(world.x / 32);
            int y = Mathf.FloorToInt(world.y / 32);

            tileCursor.transform.position = new Vector2(x * 32, y * 32);

            byte tile = paintTile;
            int location = (y + 16) * 32 + (x + 16);

            if (location > 0
             && location < 1024)
            {
                if (waller && Input.GetMouseButtonDown(0))
                {
                    tile = this.world.tilemap[location];
                    bool wall = !this.world.walls.Contains(tile);

                    SendAll(SetWallMessage(tile, wall));

                    if (this.world.walls.Set(tile, wall))
                    {
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

                        SendAll(SetTileMessage(location, tile));
                    }
                    else if (picker)
                    {
                        paintTile = this.world.tilemap[location];
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
                            () => SceneManager.LoadScene("test"));
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
                    Type type = (Type)reader.ReadInt32();

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

                            col2pal[world.palette[i]] = (byte)i;
                            pal2col[(byte)i] = world.palette[i];
                        }
                    }
                    else if (type == Type.Walls)
                    {
                        world.walls.Clear();

                        foreach (var wall in reader.ReadBytesAndSize())
                        {
                            world.walls.Add(wall);
                        }
                    }
                    else if (type == Type.Tileset)
                    {
                        int id = reader.ReadInt32();

                        tiledata[id] = reader.ReadBytesAndSize();
                    }
                    else if (type == Type.TileImage)
                    {
                        var colors = new Color32[1024];

                        int tile = reader.ReadByte();

                        colors = reader.ReadBytesAndSize().Select(pal => pal2col[pal]).ToArray();

                        int x = tile % 16;
                        int y = tile / 16;

                        world.tileset.SetPixels32(x * 32, y * 32, 32, 32, colors);
                        world.tileset.Apply();
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
                        int id = reader.ReadInt32();
                        Vector2 dest = reader.ReadVector2();

                        World.Avatar avatar = world.avatars.Where(a => a.id == id).First();

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
                        int id = reader.ReadInt32();
                        string message = reader.ReadString();

                        World.Avatar avatar = world.avatars.Where(a => a.id == id).First();

                        if (hosting)
                        {
                            if (connectionID == avatar.id)
                            {
                                SendAll(ChatMessage(avatar, message));

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

            if (error != 0) Debug.LogError("Failed to send message: " + (NetworkError)error);
        }

        NetworkTransport.Shutdown();
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

    private int ColorDistance(Color32 a, Color32 b)
    {
        return Mathf.Abs(a.r - b.r)
             + Mathf.Abs(a.g - b.g)
             + Mathf.Abs(a.b - b.b);
    }

    private float max = 0;

    private byte ColorToPalette(Color color, bool clearzero=false)
    {
        if (clearzero && color.a == 0) return 0;

        Color nearest = world.palette.Skip(clearzero ? 1 : 0).OrderBy(other => ColorDistance(color, other)).First();
        int index = Mathf.Max(System.Array.IndexOf(world.palette, nearest), 0);
        
        return (byte) index;
    }

    private byte ColorToPaletteFast(Color color, bool clearzero=false)
    {
        if (clearzero && color.a == 0) return 0;

        Color nearest = world.palette.Skip(clearzero ? 1 : 0).FirstOrDefault(other => ColorDistance(color, other) <= 3);
        int index = Mathf.Max(System.Array.IndexOf(world.palette, nearest), 0);

        return (byte)index;
    }

    private byte[][] AvatarInChunksMessages(World world,
                                            World.Avatar avatar,
                                            int size = 128)
    {
        Color32[] colors = avatar.graphic.texture.GetPixels32();
        byte[] bytes = colors.Select(c => ColorToPaletteFast(c, true)).ToArray();
        byte[] chunk;

        int offset = 0;

        var messages = new List<byte[]>();

        while (bytes.Any())
        {
            chunk = bytes.Take(size).ToArray();
            bytes = bytes.Skip(size).ToArray();

            var writer = new NetworkWriter();
            writer.Write((int) Type.AvatarChunk);
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
        int id = reader.ReadInt32();

        World.Avatar avatar = world.avatars.Where(av => av.id == id).First();
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
        byte[] bytes = colors.Select(c => ColorToPaletteFast(c)).ToArray();
        byte[] chunk;

        int offset = 0;

        var messages = new List<byte[]>();

        while (bytes.Any())
        {
            chunk = bytes.Take(size).ToArray();
            bytes = bytes.Skip(size).ToArray();

            var writer = new NetworkWriter();
            writer.Write((int)Type.TileChunk);
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

    private void SendWorld(int connectionID, World world)
    {
        {
            var writer = new NetworkWriter();

            writer.Write((int) Type.Tilemap);
            writer.WriteBytesFull(world.tilemap);

            Send(connectionID, writer.AsArray(), 1);
        }

        {
            var writer = new NetworkWriter();

            writer.Write((int) Type.Palette);
            
            for (int i = 0; i < 16; ++i) writer.Write((Color32) world.palette[i]);

            Send(connectionID, writer.AsArray(), 1);
        }

        {
            var writer = new NetworkWriter();

            writer.Write((int) Type.Walls);
            writer.WriteBytesFull(world.walls.ToArray());

            Send(connectionID, writer.AsArray(), 1);
        }

        foreach (var avatar in world.avatars)
        {
            Send(connectionID, ReplicateAvatarMessage(avatar), 0);

            if (avatar.id == connectionID)
            {
                Send(connectionID, GiveAvatarMessage(avatar), 0);
            }
            else
            {
                Send(connectionID, AvatarInChunksMessages(world, avatar));
            }
        }

        for (int i = 0; i < maxTiles; ++i)
        {
            Send(connectionID, TileInChunksMessages(world, (byte) i));
        }
    }
}
