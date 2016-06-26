using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using PixelDraw;

public class TileEditor : MonoBehaviour 
{
    [SerializeField] private Button saveButton;
    [SerializeField] private Button copyButton;
    [SerializeField] private Button pasteButton;

    [SerializeField] private Image tileImage;
    [SerializeField] private Image brushCursor;

    [SerializeField] private Toggle[] sizeToggles; 
    [SerializeField] private Toggle[] colorToggles;

    [SerializeField] private GameObject repeatObject;
    [SerializeField] private Image[] repeatImages;

    private Action Save;
    private Action Commit;
    private Action<Vector2, Vector2, Color, int> Stroke;
    private Color[] palette;

    private void Awake()
    {
        saveButton.onClick.AddListener(OnClickedSave);
        copyButton.onClick.AddListener(Copy);
        pasteButton.onClick.AddListener(Paste);

        for (int i = 0; i < sizeToggles.Length; ++i)
        {
            Toggle sizeToggle = sizeToggles[i];
            int size = i + 1;

            sizeToggle.onValueChanged.AddListener(active =>
            {
                SetBrushSize(size);
            });
        }

        for (int i = 0; i < colorToggles.Length; ++i)
        {
            Toggle sizeToggle = colorToggles[i];

            int index = i;

            sizeToggle.onValueChanged.AddListener(active =>
            {
                SetBrushColorIndex(index);
            });
        }
    }

    private Vector2 prevCursor;
    private Vector2 currCursor;
    private bool drawing;

    private Brush cursorBrush;

    private Color brushColor = Color.magenta;
    private int brushColorIndex = 0;
    private int brushSize = 3;

    private float lastSaveTime;
    private Color[] clipboard;

    private KeyCode[] numberCodes = {
        KeyCode.Alpha0,
        KeyCode.Alpha1,
        KeyCode.Alpha2,
        KeyCode.Alpha3,
        KeyCode.Alpha4,
        KeyCode.Alpha5,
        KeyCode.Alpha6,
        KeyCode.Alpha7,
        KeyCode.Alpha8,
        KeyCode.Alpha9,
    };

    private KeyCode[] paletteCodes =
    {
        KeyCode.Q, KeyCode.W, KeyCode.E, KeyCode.R, KeyCode.T, KeyCode.Y, KeyCode.U, KeyCode.I,
        KeyCode.A, KeyCode.S, KeyCode.D, KeyCode.F, KeyCode.G, KeyCode.H, KeyCode.J, KeyCode.K,
    };

    public void CheckInput()
    {
        bool control = Input.GetKey(KeyCode.LeftControl) 
                    || Input.GetKey(KeyCode.RightControl);

        bool shift = Input.GetKey(KeyCode.LeftShift)
                  || Input.GetKey(KeyCode.RightShift);

        for (int i = 1; i <= 6; ++i)
        {
            if (Input.GetKeyDown(numberCodes[i]))
            {
                SetBrushSize(i);
            }
        }

        for (int i = 0; i < 16; ++i)
        {
            if (Input.GetKeyDown(paletteCodes[i]))
            {
                SetBrushColorIndex(i);
            }
        }

        if (control && Input.GetKeyDown(KeyCode.C))
        {
            Copy();
        }
        else if (control && Input.GetKeyDown(KeyCode.V))
        {
            Paste();
        }
    }

    public void SetBrushSize(int size, bool force=false)
    {
        if (size != brushSize || force)
        {
            brushSize = size;

            sizeToggles[0].group.SetAllTogglesOff();
            sizeToggles[size - 1].isOn = true;
        }
    }

    public void SetBrushColorIndex(int index, bool force = false)
    {
        if (index != brushColorIndex || force)
        {
            brushColorIndex = index;
            brushColor = palette[index];

            colorToggles[0].group.SetAllTogglesOff();
            colorToggles[index].isOn = true;
        }
    }

    public void Copy()
    {
        Rect rect = tileImage.sprite.textureRect;

        clipboard = tileImage.sprite.texture.GetPixels((int)rect.x,
                                                       (int)rect.y,
                                                       (int)rect.width,
                                                       (int)rect.height);
    }

    public void Paste()
    {
        if (clipboard == null) return;

        Rect rect = tileImage.sprite.textureRect;

        tileImage.sprite.texture.SetPixels((int)rect.x,
                                           (int)rect.y,
                                           (int)rect.width,
                                           (int)rect.height,
                                           clipboard);
        tileImage.sprite.texture.Apply();

        Save();
    }

    private void Update()
    {
        bool control = Input.GetKey(KeyCode.LeftControl) 
                    || Input.GetKey(KeyCode.RightControl);

        bool shift = Input.GetKey(KeyCode.LeftShift)
                  || Input.GetKey(KeyCode.RightShift);

        pasteButton.interactable = clipboard != null;

        var ttrans = tileImage.transform as RectTransform;

        Vector2 cursor;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(ttrans,
                                                                Input.mousePosition,
                                                                null,
                                                                out cursor);
        
        cursor /= 7f;
        cursor.x = Mathf.Floor(cursor.x);
        cursor.y = Mathf.Floor(cursor.y);

        var bounds = Rect.MinMaxRect(0, 0, 32, 32);

        bool inside = bounds.Contains(cursor);
        bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
        bool fill = shift;

        if (cursorBrush != null) Brush.Dispose(cursorBrush);

        Rect rect = tileImage.sprite.textureRect;

        bool flash = picker || fill || brushColor.a == 0;
        bool single = picker || fill;

        cursorBrush = Brush.Circle(single ? 1 : brushSize, flash ? CycleHue.Flash(1, 1, .75f) : brushColor);
        cursorBrush.sprite.texture.Apply();

        var btrans = brushCursor.transform as RectTransform;

        brushCursor.sprite = cursorBrush;
        brushCursor.SetNativeSize();
        btrans.pivot = new Vector2(cursorBrush.sprite.pivot.x / cursorBrush.sprite.rect.width,
                                   cursorBrush.sprite.pivot.y / cursorBrush.sprite.rect.height);
        btrans.anchoredPosition = cursor * 7;
        btrans.localScale = Vector3.one * 7 * 0.01f;

        brushCursor.gameObject.SetActive(inside);

        Sprite sprite = tileImage.sprite;

        if (picker)
        {
            Cursors.Instance.Set(Cursors.Instance.picker);
        }
        else if (fill)
        {
            Cursors.Instance.Set(Cursors.Instance.fill);
        }
        else if (brushColor.a == 0)
        {
            Cursors.Instance.Set(Cursors.Instance.erase);
        }
        else
        {
            Cursors.Instance.Set(null);
        }

        if (Input.GetMouseButtonDown(0) && fill)
        {
            if (Stroke != null) Stroke(cursor, cursor, brushColor, 0);

            sprite.texture.FloodFillAreaNPO2((int) cursor.x, 
                                             (int) cursor.y, 
                                             brushColor, 
                                             sprite.rect);
            sprite.texture.Apply();
        }
        else if (Input.GetMouseButton(0))
        {
            prevCursor = currCursor;
            currCursor = cursor;

            Clamp(ref prevCursor, bounds);
            Clamp(ref currCursor, bounds);

            if (drawing)
            {
                Color maskColor = new Color(brushColorIndex / 15f, 0, 0, 1);

                var blend = brushColor.a == 1 ? Blend.Alpha
                                              : Blend.Subtract;

                if (Stroke != null) Stroke(prevCursor, currCursor, brushColor, brushSize);

                using (Brush line = Brush.Line(prevCursor, 
                                               currCursor,
                                               brushColor.a == 1 ? maskColor : Color.white,
                                               brushSize))
                {
                    Brush.Apply(line,
                                Vector2.zero,
                                tileImage.sprite,
                                Point.Zero,
                                blend);
                }

                tileImage.sprite.texture.Apply();
            }
            else if (picker)
            {
                int x = (int)(cursor.x + rect.x);
                int y = (int)(cursor.y + rect.y);

                Color sample = sprite.texture.GetPixel(x, y);
                bool clear = palette[0].a == 0;

                SetBrushColorIndex(palette.ColorToPalette(sample, clear));
            }
        }

        drawing = (drawing || inside) 
               && !fill
               && !picker
               && Input.GetMouseButton(0);

        colorToggles[0].GetComponent<Image>().color = CycleHue.Flash(1, 1, .75f);
    }

    private void Clamp(ref Vector2 coord, Rect bounds)
    {
        coord.x = Mathf.Clamp(coord.x, bounds.xMin, bounds.xMax - 1);
        coord.y = Mathf.Clamp(coord.y, bounds.yMin, bounds.yMax - 1);
    }

    public void OpenAndEdit(Color[] palette,
                            Sprite sprite, 
                            Action save,
                            Action commit,
                            Action<Vector2, Vector2, Color, int> stroke=null,
                            bool repeat=false)
    {
        gameObject.SetActive(true);

        this.palette = palette;
        tileImage.sprite = sprite;
        Save = save;
        Commit = commit;

        Stroke = stroke;
        
        SetBrushColorIndex(brushColorIndex, force: true);
        SetBrushSize(brushSize, force: true);

        for (int i = 0; i < colorToggles.Length; ++i)
        {
            Toggle sizeToggle = colorToggles[i];
            Color color = palette[i];

            sizeToggle.GetComponent<Image>().color = color;
        }

        repeatObject.SetActive(repeat);

        for (int i = 0; i < repeatImages.Length; ++i)
        {
            repeatImages[i].sprite = sprite;
        }
    }

    public void OnClickedSave()
    {
        gameObject.SetActive(false);

        Save();
        Commit();
    }
}
