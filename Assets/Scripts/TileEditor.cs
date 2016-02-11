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

    private void Update()
    {
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

        bool inside = Rect.MinMaxRect(0, 0, 32, 32).Contains(cursor);
        bool picker = Input.GetKey(KeyCode.LeftAlt) || Input.GetKeyDown(KeyCode.RightAlt);

        brushCursor.gameObject.SetActive(inside && !picker);

        if (Input.GetMouseButton(0))
        {
            prevCursor = currCursor;
            currCursor = cursor;

            if (drawing && !picker)
            {
                var blend = brushColor.a == 1 ? Blend.Alpha
                                              : Blend.Subtract;

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
                brushColor = tileImage.sprite.texture.GetPixel((int) currCursor.x,
                                                               (int) currCursor.y);
            }
        }

        drawing = (drawing || inside) && Input.GetMouseButton(0);
    }

    public void OpenAndEdit(Color[] palette,
                            Sprite sprite, 
                            Action save,
                            Action commit)
    {
        gameObject.SetActive(true);

        this.palette = palette;
        tileImage.sprite = sprite;
        Save = save;
        Commit = commit;

        brushColor = palette[1];

        for (int i = 0; i < colorToggles.Length; ++i)
        {
            Toggle sizeToggle = colorToggles[i];
            Color color = palette[i];

            sizeToggle.GetComponent<Image>().color = color;
        }

        StartCoroutine(AutoSave());
    }

    public void OnClickedSave()
    {
        gameObject.SetActive(false);

        Save();
        Commit();
    }

    private IEnumerator AutoSave()
    {
        while (true)
        {
            float dt = Time.realtimeSinceStartup - lastSaveTime;

            if (dt > 5)
            {
                Save();
                lastSaveTime = Time.realtimeSinceStartup;
            }

            yield return null;
        }
    }
}
