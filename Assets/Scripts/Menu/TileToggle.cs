using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class TileToggle : MonoBehaviour 
{
    [SerializeField] private Toggle toggle;
    [SerializeField] private Image image;

    private System.Action action;

    private void Awake()
    {
        toggle.onValueChanged.AddListener(OnToggled);
    }

    public void SetTile(Sprite sprite, System.Action action)
    {
        image.sprite = sprite;
        this.action = action;
    }

    private void OnToggled(bool on)
    {
        if (on) action();
    }
}
