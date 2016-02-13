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
    [SerializeField] private Image tileImage;
    [SerializeField] private Image brushCursor;

    [SerializeField] private Toggle[] sizeToggles; 
    [SerializeField] private Toggle[] colorToggles;

    private Action Save;
    private Action Commit;
    private Action<Vector2, Vector2, Color, int> Stroke;
    private Color[] palette;

    private void Awake()
    {
        saveButton.onClick.AddListener(OnClickedSave);

        for (int i = 0; i < sizeToggles.Length; ++i)
        {
            Toggle sizeToggle = sizeToggles[i];
            int size = i + 1;

            sizeToggle.onValueChanged.AddListener(active =>
            {
                brushSize = size;
            });
        }

        for (int i = 0; i < colorToggles.Length; ++i)
        {
            Toggle sizeToggle = colorToggles[i];

            int index = i;

            sizeToggle.onValueChanged.AddListener(active =>
            {
                brushColor = palette[index];
            });
        }
    }

    private Vector2 prevCursor;
    private Vector2 currCursor;
    private bool drawing;

    private Brush cursorBrush;

    private Color brushColor = Color.magenta;
    private int brushSize = 3;

    private float lastSaveTime;
    private Color32[] clipboard;

    private void Update()
    {
        bool control = Input.GetKey(KeyCode.LeftControl) 
                    || Input.GetKey(KeyCode.RightControl);

        bool shift = Input.GetKey(KeyCode.LeftShift)
                  || Input.GetKey(KeyCode.RightShift);

        if (control && Input.GetKeyDown(KeyCode.C))
        {
            clipboard = tileImage.sprite.texture.GetPixels32();
        }
        else if (control
              && Input.GetKeyDown(KeyCode.V)
              && clipboard != null)
        {
            tileImage.sprite.texture.SetPixels32(clipboard);
            tileImage.sprite.texture.Apply();
        }

        var ttrans = tileImage.transform as RectTransform;

        Vector2 cursor;

        RectTransformUtility.ScreenPointToLocalPointInRectangle(ttrans,
                                                                Input.mousePosition,
                                                                null,
                                                                out cursor);
        
        cursor /= 7f;
        cursor.x = Mathf.Floor(cursor.x);
        cursor.y = Mathf.Floor(cursor.y);

        if (cursorBrush != null) Brush.Dispose(cursorBrush);

        cursorBrush = Brush.Circle(brushSize, brushColor);
        cursorBrush.sprite.texture.Apply();

        var btrans = brushCursor.transform as RectTransform;

        brushCursor.sprite = cursorBrush;
        brushCursor.SetNativeSize();
        btrans.pivot = new Vector2(cursorBrush.sprite.pivot.x / cursorBrush.sprite.rect.width,
                                   cursorBrush.sprite.pivot.y / cursorBrush.sprite.rect.height);
        btrans.anchoredPosition = cursor * 7;
        btrans.localScale = Vector3.one * 7 * 0.01f;

        var bounds = Rect.MinMaxRect(0, 0, 31, 31);

        bool inside = bounds.Contains(cursor);
        bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);
        bool fill = shift;

        brushCursor.gameObject.SetActive(inside && !picker);

        Sprite sprite = tileImage.sprite;

        if (Input.GetMouseButtonDown(0) && fill)
        {
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

            if (drawing && !picker)
            {
                var blend = brushColor.a == 1 ? Blend.Alpha
                                              : Blend.Subtract;

                if (Stroke != null) Stroke(prevCursor, currCursor, brushColor, brushSize);

                using (Brush line = Brush.Line(prevCursor, 
                                               currCursor,
                                               brushColor.a == 1 ? brushColor : Color.white,
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
            else if (drawing && picker)
            {
                int x = (int)(cursor.x + sprite.rect.x);
                int y = (int)(cursor.y + sprite.rect.y);

                brushColor = sprite.texture.GetPixel(x, y);
            }
        }

        drawing = (drawing || inside) 
               && !fill
               && !picker
               && Input.GetMouseButton(0);
    }

    private void Clamp(ref Vector2 coord, Rect bounds)
    {
        coord.x = Mathf.Clamp(coord.x, bounds.xMin, bounds.xMax);
        coord.y = Mathf.Clamp(coord.y, bounds.yMin, bounds.yMax);
    }

    public void OpenAndEdit(Color[] palette,
                            Sprite sprite, 
                            Action save,
                            Action commit,
                            Action<Vector2, Vector2, Color, int> stroke=null)
    {
        gameObject.SetActive(true);

        this.palette = palette;
        tileImage.sprite = sprite;
        Save = save;
        Commit = commit;

        Stroke = stroke;

        brushColor = palette[1];

        for (int i = 0; i < colorToggles.Length; ++i)
        {
            Toggle sizeToggle = colorToggles[i];
            Color color = palette[i];

            sizeToggle.GetComponent<Image>().color = color;
        }
    }

    public void OnClickedSave()
    {
        gameObject.SetActive(false);

        Save();
        Commit();
    }
}
