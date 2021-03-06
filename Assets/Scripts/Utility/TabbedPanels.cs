﻿using UnityEngine;
using UnityEngine.UI;
using System.Collections;
using System.Collections.Generic;

public class TabbedPanels : MonoBehaviour
{
    [SerializeField] protected ToggleGroup ToggleGroup;
	[SerializeField] protected Transform TabContainer;
	[SerializeField] protected Transform PanelContainer;

	protected Dictionary<string, GameObject> Panels
		= new Dictionary<string, GameObject>();

    private Dictionary<string, Toggle> Toggles
        = new Dictionary<string, Toggle>();

    private List<GameObject> tabs = new List<GameObject>();

	public void Start()
	{
		foreach (Transform panel in PanelContainer)
		{
			Panels.Add(panel.name, panel.gameObject);
		}

		foreach (Transform tab in TabContainer)
		{
            if (!Panels.ContainsKey(tab.name)) continue;

			var panel = Panels[tab.name];
			var toggle = tab.GetComponentInChildren<Toggle>();

            if (toggle != null)
            {
                tabs.Add(tab.gameObject);
                Toggles.Add(tab.name, toggle);

                toggle.onValueChanged.AddListener((bool on) => panel.SetActive(on));
            }
		}

        SetTab(tabs[0].name);
	}

	public void SetTab(string name)
	{
        ToggleGroup.SetAllTogglesOff();
        Toggles[name].isOn = true;

		foreach (var pair in Panels)
		{
			pair.Value.SetActive(pair.Key == name);
		}
	}

    protected void Refresh()
    {

    }
}
