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

#if UNITY_EDITOR
using UnityEditor;
#endif

public class Test : MonoBehaviour
{
    [SerializeField] private Font[] fonts;

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
    [SerializeField] private GameObject noWorldsDisableObject;

    private MonoBehaviourPooler<MatchDesc, WorldPanel> worlds;

    [Header("Details")]
    [SerializeField] private GameObject detailsDisableObject;
    [SerializeField] private Text worldDescription;
    [SerializeField] private Button enterButton;

    [SerializeField] private GameObject chatObject;
    [SerializeField] private InputField chatInput;

    private NetworkMatch match;

    private void Awake()
    {
        foreach (Font font in fonts) font.material.mainTexture.filterMode = FilterMode.Point;

        match = gameObject.AddComponent<NetworkMatch>();

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
        create.size = 4;
        create.advertise = true;
        create.password = "";

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

        for (int i = 0; i < 1024; ++i) world.tilemap[i] = (byte) Random.Range(0, 256);

        worldView.SetWorld(world);

        test = world.tileset;

        StartCoroutine(RefreshList());
    }

    private IEnumerator RefreshList()
    {
        while (true)
        {
            match.ListMatches(0, 64, "", matches =>
            {
                noWorldsDisableObject.SetActive(matches.matches.Count == 0);

                worlds.SetActive(matches.matches);

                if (selected != null
                 && !matches.matches.Any(m => m.networkId == selected.networkId))
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
        worldDescription.text = desc.name;
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
            group.alpha = 0f;
            group.blocksRaycasts = false;
        }
        else
        {
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
        var topology = new HostTopology(config, 4);

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
    }


    private void OnNewClientConnected(int connectionID)
    {
        Debug.Log("New client: " + connectionID);

        connectionIDs.Add(connectionID);

        var avatar = new World.Avatar
        {
            id = connectionID,
            destination = new Vector2(0, connectionID),
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
    }

    private void RemoveAvatar(World.Avatar avatar)
    {
        world.avatars.Remove(avatar);

        if (hosting)
        {
            SendAll(DestroyAvatarMessage(avatar), 0);
        }

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

    private void SendAll(byte[] data, int channelID = 0)
    {
        sends.Enqueue(new Message
        {
            channel = channelID,
            connections = hosting ? clients.Select(client => client.connectionID).ToArray() 
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

    private bool Blocked(World.Avatar avatar,
                         Vector2 destination)
    {
        return world.avatars.Any(a => a.destination == destination)
            || (avatar.destination - destination).magnitude > 1;
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

    private void Update()
    {
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

                if (message.Trim().Length > 0)
                {
                    SendAll(ChatMessage(worldView.viewer, message));

                    if (hosting) worldView.Chat(worldView.viewer, message);
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
                Debug.LogError("Error while receiveing network message: " + (NetworkError)error);
            }

            if (eventType == NetworkEventType.ConnectEvent)
            {
                Debug.Log("Connected through relay, ConnectionID:" + connectionID +
                    " ChannelID:" + channelId);
                connected = true;

                if (hosting)
                {
                    OnNewClientConnected(connectionID);
                }
                else
                {
                    OnConnectedToHost(connectionID);

                    Debug.Log("I AM: " + connectionID);
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
                            if (connectionID == avatar.id && !Blocked(avatar, dest))
                            {
                                avatar.destination = dest;
                                worldView.RefreshAvatars();

                                SendAll(MoveAvatarMessage(avatar, avatar.destination));
                            }
                            else
                            {
                                Send(connectionID, MoveAvatarMessage(avatar, avatar.destination));
                            }
                        }
                        else
                        {
                            avatar.destination = dest;
                            worldView.RefreshAvatars();
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

                                worldView.Chat(avatar, message);
                            }
                        }
                        else
                        {
                            worldView.Chat(avatar, message);
                        }
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

        Debug.Log(error);
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

        for (int i = 0; i < 256; ++i)
        {
            while (!SendTileImage(connectionID, world, (byte) i, out error))
            {
                yield return null;
            };
        }

        yield break;
    }
}
