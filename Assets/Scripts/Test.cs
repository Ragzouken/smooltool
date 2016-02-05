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

    [Header("List")]
    [SerializeField] private Transform worldContainer;
    [SerializeField] private WorldPanel worldPrefab;
    [SerializeField] private GameObject newerVersionDisableObject;
    [SerializeField] private GameObject noWorldsDisableObject;

    private MonoBehaviourPooler<MatchDesc, WorldPanel> worlds;

    [Header("Details")]
    [SerializeField] private GameObject detailsDisableObject;
    [SerializeField] private Text worldDescription;
    [SerializeField] private Button enterButton;

    [SerializeField] private GameObject chatObject;
    [SerializeField] private InputField chatInput;

    [SerializeField] private new Camera camera;
    [SerializeField] private Image tileCursor;
    [SerializeField] private GameObject pickerCursor;

    [Header("Tile Palette")]
    [SerializeField] private GameObject paletteObject;
    [SerializeField] private RectTransform paletteContainer;
    [SerializeField] private TileToggle tilePrefab;
    private MonoBehaviourPooler<byte, TileToggle> tiles;

    private NetworkMatch match;

    private byte paintTile;

    public enum Version
    {
        PRE1,
    }

    public static Version version = Version.PRE1;

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

        Application.runInBackground = true;

        byte index = 0;

        Color32[] colors = testtex.GetPixels32();

        for (int i = 0; i < colors.Length; ++i)
        {
            Color32 color = colors[i];

            if (!col2pal.ContainsKey(color))
            {
                col2pal.Add(color, index);
                pal2col.Add(index, color);

                index += 1;
            }
        }

        StartCoroutine(SendMessages());
    }

    private void InitialiseWorld(MatchDesc desc, WorldPanel panel)
    {
        panel.SetMatch(desc);
    }

    private void OnClickedCreate()
    {
        var create = new CreateMatchRequest();
        create.name = titleInput.text;
        create.size = 8;
        create.advertise = true;
        create.password = "";

        create.name += "!" + (int) version;

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

        for (int i = 0; i < 1024; ++i) world.tilemap[i] = (byte) Random.Range(0, 23);

        worldView.SetWorld(world);

        test = world.tileset;

        StartCoroutine(RefreshList());

        tiles = new MonoBehaviourPooler<byte, TileToggle>(tilePrefab,
                                                          paletteContainer,
                                                          InitialiseTileToggle);
    }

    private void InitialiseTileToggle(byte tile, TileToggle toggle)
    {
        toggle.SetTile(world.tiles[tile], () => paintTile = tile);
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
                var list = matches.matches;

                var valid = list.Where(m => GetVersion(m) == (int) version);
                var newer = list.Where(m => GetVersion(m) >  (int) version);

                newerVersionDisableObject.SetActive(newer.Any());
                noWorldsDisableObject.SetActive(!newer.Any() && !valid.Any());
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

            match.JoinMatch(selected.networkId, "", OnMatchJoined);
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
            world.tileset.SetPixels32(testtex.GetPixels32());
            world.tileset.Apply();

            AddAvatar(new World.Avatar
            {
                id = 0,
                destination = Vector2.zero,
                source = Vector2.zero,
            });

            worldView.viewer = world.avatars[0];

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

        TileImage,

        ReplicateAvatar,
        DestroyAvatar,
        MoveAvatar,
        GiveAvatar,

        Chat,

        SetTile,
    }


    private void OnNewClientConnected(int connectionID)
    {
        Debug.Log("New client: " + connectionID);

        connectionIDs.Add(connectionID);

        var avatar = new World.Avatar
        {
            id = connectionID,
            destination = new Vector2(0, connectionID),
            source = new Vector2(0, connectionID),
        };

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
            SendAll(DestroyAvatarMessage(avatar), 0);
        }

        Chat(avatar, "[EXITED]");
        worldView.RefreshAvatars();
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

    private void SendAll(byte[] data, 
                         int channelID = 0,
                         int except = -1)
    {
        sends.Enqueue(new Message
        {
            channel = channelID,
            connections = hosting ? clients.Select(client => client.connectionID).Where(id => id != except).ToArray() 
                                  : new int[] { 1 },
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
        };

        AddAvatar(avatar);
    }

    private byte[] DestroyAvatarMessage(World.Avatar avatar)
    {
        var writer = new NetworkWriter();
        writer.Write((int) Type.DestroyAvatar);
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
        writer.Write((int) Type.GiveAvatar);
        writer.Write(avatar.id);

        return writer.AsArray();
    }

    private void ReceiveGiveAvatar(NetworkReader reader)
    {
        int id = reader.ReadInt32();

        worldView.viewer = world.avatars.Where(a => a.id == id).First();
    }

    private byte[] MoveAvatarMessage(World.Avatar avatar,
                                     Vector2 destination)
    {
        var writer = new NetworkWriter();
        writer.Write((int) Type.MoveAvatar);
        writer.Write(avatar.id);
        writer.Write(destination);

        return writer.AsArray();
    }

    private byte[] ChatMessage(World.Avatar avatar, string message)
    {
        var writer = new NetworkWriter();
        writer.Write((int) Type.Chat);
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
        writer.Write((int) Type.SetTile);
        writer.Write(location);
        writer.Write(tile);

        return writer.AsArray();
    }

    private bool Blocked(World.Avatar avatar,
                         Vector2 destination)
    {
        return world.avatars.Any(a => a.destination == destination)
            || (avatar.destination - destination).magnitude > 1
            || !Rect.MinMaxRect(-16, -16, 16, 16).Contains(destination);
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
                        yield return null;
                    }
                }

                Type type = (Type) (new NetworkReader(message.data).ReadInt32());

                Debug.LogFormat("SENT: {0}", type);
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
    }

    private void Update()
    {
        GameObject selected = UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject;

        if (hostID == -1) return;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            Move(Vector2.up);
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            Move(Vector2.left);
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            Move(Vector2.right);
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            Move(Vector2.down);
        }
        else if (Input.GetKeyDown(KeyCode.Return))
        {
            if (chatObject.activeSelf)
            {
                string message = chatInput.text;

                chatObject.SetActive(false);
                chatInput.text = "";

                var match = Regex.Match(message, @"tile\s(\d+)\s*=\s*(\d+)");

                if (match.Success)
                {
                    int location = int.Parse(match.Groups[1].Value);
                    byte tile = byte.Parse(match.Groups[2].Value);

                    if (location < 1024)
                    {
                        audio.PlayOneShot(placeSound);
                        worldView.SetTile(location, tile);

                        SendAll(SetTileMessage(location, tile));
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

        if (!chatObject.activeSelf && Input.GetKey(KeyCode.Space))
        {
            tiles.Clear();
            tiles.SetActive(Enumerable.Range(0, 24).Select(i => (byte) i));
            paletteObject.SetActive(true);
        }
        else
        {
            paletteObject.SetActive(false);
        }

        if (!UnityEngine.EventSystems.EventSystem.current.IsPointerOverGameObject()
         && Rect.MinMaxRect(0, 0, 512, 512).Contains(Input.mousePosition))
        {
            bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKey(KeyCode.RightAlt);

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
             && location < 1024 
             && Input.GetMouseButton(0))
            {
                if (!picker && this.world.tilemap[location] != tile)
                {
                    audio.PlayOneShot(placeSound);
                    worldView.SetTile(location, tile);

                    SendAll(SetTileMessage(location, tile));
                }
                else
                {
                    paintTile = this.world.tilemap[location];
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
            if ((NetworkError) error != NetworkError.Ok)
            {
                group.interactable = true;

                popups.Show("Network Error: " + (NetworkError) error,
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
                        Debug.Log("tilemap");

                        world.tilemap = reader.ReadBytesAndSize();

                        worldView.SetWorld(world);
                    }
                    else if (type == Type.Tileset)
                    {
                        int id = reader.ReadInt32();

                        Debug.Log("receiving id " + id);

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

    private bool SendTileImage(int connectionID, 
                               World world, 
                               byte tile,
                               out byte error)
    {
        var writer = new NetworkWriter();

        writer.Write((int) Type.TileImage);
        writer.Write(tile);

        int x = tile % 16;
        int y = tile / 16;

        Color[] colors = world.tileset.GetPixels(x * 32, y * 32, 32, 32);
        byte[] bytes = colors.Select(color => col2pal[color]).ToArray();

        writer.WriteBytesFull(bytes);

        return NetworkTransport.Send(hostID,
                                     connectionID,
                                     1,
                                     writer.AsArray(),
                                     writer.Position,
                                     out error);
    }

    private IEnumerator SendWorld(int connectionID, World world)
    {
        byte error;
        var writer = new NetworkWriter();

        int id = 0;

        {
            writer.SeekZero();
            writer.Write((int)Type.Tilemap);
            writer.WriteBytesFull(world.tilemap);
            writer.Write(id);

            NetworkTransport.Send(hostID,
                                  connectionID,
                                  1,
                                  writer.AsArray(),
                                  writer.Position,
                                  out error);

            if ((NetworkError)error != NetworkError.Ok)
                Debug.LogError("Failed to send message: " + (NetworkError)error);
        }

        foreach (var avatar in world.avatars)
        {
            Send(connectionID, ReplicateAvatarMessage(avatar), 0);

            if (avatar.id == connectionID)
            {
                Send(connectionID, GiveAvatarMessage(avatar), 0);
            }
        }

        for (int i = 0; i < 24; ++i)
        {
            while (!SendTileImage(connectionID, world, (byte) i, out error))
            {
                yield return null;
            };
        }

        yield break;
    }
}
