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

    private Action Save;

    private void Awake()
    {
        gameObject.SetActive(false);

        saveButton.onClick.AddListener(OnClickedSave);
    }

    private Vector2 prevCursor;
    private Vector2 currCursor;
    private bool drawing;

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

        if (Input.GetMouseButton(0))
        {
            prevCursor = currCursor;
            currCursor = cursor;

            if (drawing)
            {
                using (Brush line = Brush.Line(prevCursor, 
                                               currCursor, 
                                               Color.magenta, 
                                               3))
                {
                    Brush.Apply(line,
                                Vector2.zero,
                                tileImage.sprite,
                                Point.Zero,
                                Blend.Alpha);
                }
            }

            tileImage.sprite.texture.Apply();
        }

        drawing = Input.GetMouseButton(0);
    }

    public void OpenAndEdit(Sprite sprite, Action save)
    {
        gameObject.SetActive(true);

        tileImage.sprite = sprite;
        Save = save;
    }

    private void OnClickedSave()
    {
        gameObject.SetActive(false);

        Save();
    }
}
