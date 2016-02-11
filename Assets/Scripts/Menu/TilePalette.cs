using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class TilePalette : MonoBehaviour 
{
    [SerializeField] private RectTransform tileContainer;
    [SerializeField] private TileToggle tilePrefab;
    [SerializeField] private Button lockButton;

    public byte SelectedTile { get; private set; }

    private MonoBehaviourPooler<byte, TileToggle> tiles;

    private World world;
    private Dictionary<byte, World.Avatar> locks;
    private System.Action<byte> request;

    public void Setup(World world,
                      Dictionary<byte, World.Avatar> locks,
                      System.Action<byte> request)
    {
        this.world = world;
        this.locks = locks;
        this.request = request;
    }

    private void Awake()
    {
        lockButton.onClick.AddListener(OnClickedLock);

        tiles = new MonoBehaviourPooler<byte, TileToggle>(tilePrefab,
                                                          tileContainer,
                                                          InitialiseTile);
    }

    private void InitialiseTile(byte tile, TileToggle toggle)
    {
        toggle.SetTile(world.tiles[tile], () => SetSelectedTile(tile));
    }

    public void SetSelectedTile(byte tile)
    {
        SelectedTile = tile;

        Refresh();
    }

    public void Show()
    {
        gameObject.SetActive(true);

        tiles.Clear();
        tiles.SetActive(Enumerable.Range(0, Test.maxTiles).Select(i => (byte) i));

        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void Refresh()
    {
        tiles.Get(SelectedTile).Select();

        lockButton.interactable = !locks.ContainsKey(SelectedTile);
    }

    private void OnClickedLock()
    {
        request(SelectedTile);
    }
}
