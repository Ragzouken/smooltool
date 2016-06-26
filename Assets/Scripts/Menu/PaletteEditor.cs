using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class PaletteEditor : MonoBehaviour 
{
    [SerializeField] private Slider redSlider;
    [SerializeField] private Slider greenSlider;
    [SerializeField] private Slider blueSlider;

    [SerializeField] private Test test;
    [SerializeField] private Toggle[] toggles;
    private Image[] images = new Image[16];

    private World world;
    private int colour;

    private void Awake()
    {
        redSlider.onValueChanged.AddListener(OnChanged);
        greenSlider.onValueChanged.AddListener(OnChanged);
        blueSlider.onValueChanged.AddListener(OnChanged);

        for (int i = 0; i < 16; ++i)
        {
            int index = i;

            images[i] = toggles[i].GetComponent<Image>();
            toggles[i].onValueChanged.AddListener(active =>
            {
                if (active) SetColour(index);
            });
        }
    }

    private bool suppress;
    private void OnChanged(float value)
    {
        if (suppress) return;

        test.UpdatePalette((byte) colour, new Color(redSlider.value, greenSlider.value, blueSlider.value, 1f));
    }

    public void SetWorld(World world)
    {
        this.world = world;

        SetColour(0);

        Refresh();
    }

    private void Update()
    {
        Refresh();
    }

    public void Refresh()
    {
        if (world == null || images[0] == null) return;

        for (int i = 0; i < 16; ++i)
        {
            images[i].color = world.palette[i];
        }

        SetColour(colour);
    }

    private void SetColour(int index)
    {
        if (!toggles[index].isOn)
        {
            toggles[index].isOn = true;
        }

        colour = index;

        suppress = true;
        redSlider.value = world.palette[colour].r;
        greenSlider.value = world.palette[colour].g;
        blueSlider.value = world.palette[colour].b;
        suppress = false;
    }
}
