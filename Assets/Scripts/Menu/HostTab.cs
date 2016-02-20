using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class HostTab : MonoBehaviour 
{
    [SerializeField] private Test test;

    [Header("List")]
    [SerializeField] private RectTransform worldParent;
    [SerializeField] private SavedWorldPanel worldPrefab;
    [SerializeField] private Toggle generateToggle;

    [Header("Details")]
    [SerializeField] private InputField nameInput;
    [SerializeField] private InputField passwordInput;
    [SerializeField] private Button hostButton;

    private MonoBehaviourPooler<World.Info, SavedWorldPanel> worlds;

    private World.Info selectedWorld;

    private void Awake()
    {
        hostButton.onClick.AddListener(OnClickedHost);

        worlds = new MonoBehaviourPooler<World.Info, SavedWorldPanel>(worldPrefab,
                                                                      worldParent,
                                                                      InitialiseWorld);

        generateToggle.onValueChanged.AddListener(active =>
        {
            if (active) selectedWorld = null;
        });
    }

    private void InitialiseWorld(World.Info world, SavedWorldPanel panel)
    {
        panel.Setup(world);
    }

    public void SetWorld(World.Info world)
    {
        nameInput.text = world.name;

        selectedWorld = world;
    }

    private void OnEnable()
    {
        worlds.SetActive(Test.GetSavedWorlds().OrderByDescending(w => w.lastPlayed.Ticks));
        generateToggle.transform.SetAsLastSibling();

        passwordInput.text = "";
    }

    private void OnClickedHost()
    {
        if (selectedWorld == null)
        {
            test.HostGame(nameInput.text, passwordInput.text);
        }
        else
        {
            test.HostGame(selectedWorld,
                          nameInput.text,
                          passwordInput.text);
        }
    }
}
