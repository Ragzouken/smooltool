using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Popups : MonoBehaviour 
{
    [SerializeField] private Text messageText;
    [SerializeField] private Button acceptButton;

    private System.Action OnAccept;

    private void Awake()
    {
        acceptButton.onClick.AddListener(OnClickedAccept);
    }

    public void Show(string message, System.Action OnAccept)
    {
        gameObject.SetActive(true);

        messageText.text = message;
        this.OnAccept = OnAccept;
    }

    private void OnClickedAccept()
    {
        gameObject.SetActive(false);

        var OnAccept = this.OnAccept;
        this.OnAccept = null;

        OnAccept();
    }
}
