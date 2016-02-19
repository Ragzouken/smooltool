using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class SavedWorldPanel : MonoBehaviour 
{
    [SerializeField] private HostTab tab;

    [SerializeField] private Toggle toggle;
    [SerializeField] private Text nameText;
    [SerializeField] private Text lastPlayText;

    private World.Info world;

    private void Awake()
    {
        toggle.onValueChanged.AddListener(active =>
        {
            if (active) tab.SetWorld(world);
        });
    }

    public void Setup(World.Info world)
    {
        this.world = world;

        nameText.text = world.name;
        lastPlayText.text = world.lastPlayed.ToShortDateString();
    }
}
