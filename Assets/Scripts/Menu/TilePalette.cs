using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class TilePalette : MonoBehaviour 
{
    [Header("Pages")]
    [SerializeField] private RectTransform pageContainer;
    [SerializeField] private Toggle pagePrefab;

    [Header("Tiles")]
    [SerializeField] private RectTransform tileContainer;
    [SerializeField] private TileToggle tilePrefab;
    [SerializeField] private ToggleGroup tileGroup;

    [Header("Edit")]
    [SerializeField] private Button lockButton;

    public byte SelectedTile { get; private set; }

    private MonoBehaviourPooler<int, Toggle> pages;
    private MonoBehaviourPooler<byte, TileToggle> tiles;

    private World world;
    private Dictionary<byte, World.Avatar> locks;
    private System.Action<byte> request;
    private int page;

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

        pages = new MonoBehaviourPooler<int, Toggle>(pagePrefab,
                                                     pageContainer,
                                                     InitialisePage);

        pages.SetActive(Enumerable.Range(0, 8));
        pages.Get(0).isOn = true;
    }

    private void InitialiseTile(byte tile, TileToggle toggle)
    {
        toggle.SetTile(world.tiles[tile], 
                       () => SetSelectedTile(tile));
    }

    private void InitialisePage(int page, Toggle toggle)
    {
        toggle.onValueChanged.RemoveAllListeners();
        toggle.onValueChanged.AddListener(active =>
        {
            if (active) SetPage(page);
        });
    }

    public void SetSelectedTile(byte tile)
    {
        if (SelectedTile != tile)
        {
            SelectedTile = tile;

            Refresh();
        }
    }

    public void Show()
    {
        gameObject.SetActive(true);

        Refresh();
    }

    public void Hide()
    {
        gameObject.SetActive(false);
    }

    public void SetPage(int page)
    {
        this.page = page;

        Refresh();
    }

    public void Refresh()
    {
        if (tiles == null) return;

        tiles.SetActive(Enumerable.Range(page * 32, 32).Select(i => (byte)i));

        tileGroup.SetAllTogglesOff();
        tiles.DoIfActive(SelectedTile, toggle => toggle.Select());

        lockButton.interactable = !locks.ContainsKey(SelectedTile);
    }

    private void OnClickedLock()
    {
        request(SelectedTile);
    }
}
