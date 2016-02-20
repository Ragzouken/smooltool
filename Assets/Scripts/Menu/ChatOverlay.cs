using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class ChatOverlay : MonoBehaviour 
{
    [SerializeField] private InputField input;
    [SerializeField] private RectTransform logContainer;
    [SerializeField] private ChatLogElement logPrefab;
    [SerializeField] private Scrollbar logScrollbar;
    [SerializeField] private ScrollRect logScrollrect;

    private MonoBehaviourPooler<Test.LoggedMessage, ChatLogElement> log;

    private IEnumerable<Test.LoggedMessage> messages;
    private System.Action<string> chat;

    public void Setup(IEnumerable<Test.LoggedMessage> messages,
                      System.Action<string> chat)
    {
        this.messages = messages;
        this.chat = chat;
    }

    private void Awake()
    {
        log = new MonoBehaviourPooler<Test.LoggedMessage, ChatLogElement>(logPrefab,
                                                                          logContainer,
                                                                          InitialiseLog);
    }

    private void InitialiseLog(Test.LoggedMessage message, 
                               ChatLogElement element)
    {
        element.SetMessage(message.avatar, message.message);
    }

    /*
    private void Update()
    {
        if (UnityEngine.EventSystems.EventSystem.current.currentSelectedGameObject != input.gameObject)
        {
            Hide();
        }
    }
    */

    public void Show()
    {
        gameObject.SetActive(true);

        Refresh();
        
        input.text = "";
        input.Select();
        
        logScrollrect.verticalNormalizedPosition = 0;
    }

    public void Hide()
    {
        gameObject.SetActive(false);

        UnityEngine.EventSystems.EventSystem.current.SetSelectedGameObject(null);

        logScrollrect.verticalNormalizedPosition = 0;
    }

    public void OnClickedSend()
    {
        string message = input.text.Trim();

        if (message.Length > 0) chat(message);

        Hide();
    }

    public void Refresh()
    {
        if (log == null) return;

        log.SetActive(messages.Reverse().Take(64).Reverse());
    }
}
