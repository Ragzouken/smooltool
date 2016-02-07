using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

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
            int x = (int) (cursor.x + tileImage.sprite.textureRect.xMin);
            int y = (int) (cursor.y + tileImage.sprite.textureRect.yMin);

            tileImage.sprite.texture.SetPixel(x, y, Color.magenta);
            tileImage.sprite.texture.Apply();
        }
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
