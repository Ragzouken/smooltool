using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.Networking.Match;

public class WorldPanel : MonoBehaviour 
{
    [SerializeField] private Test list;
    [SerializeField] private Toggle toggle;
    [SerializeField] private Text nameText;
    [SerializeField] private Text playersText;

    private MatchDesc desc;

    private void Awake()
    {
        toggle.onValueChanged.AddListener(OnToggled);
    }

    public void SetMatch(MatchDesc desc)
    {
        string name = string.Join("!", desc.name.Split('!')
                                                .Reverse()
                                                .Skip(1)
                                                .Reverse()
                                                .ToArray());

        nameText.text = name;
        playersText.text = desc.currentSize + " / " + desc.maxSize;
        this.desc = desc;
    }

    private void OnToggled(bool active)
    {
        if (active) list.SelectMatch(desc);
    }
}
