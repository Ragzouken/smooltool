using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class AvatarView : MonoBehaviour 
{
    [SerializeField] MonoBehaviour coroutine;
    [SerializeField] private Image image;

    [SerializeField] private GameObject chatObject;
    [SerializeField] private Text chatText;
    [SerializeField] private PixelBorder.BorderRenderer border;

    public void SetAvatar(World.Avatar avatar)
    {
        image.sprite = avatar.graphic;
        border.sourceSprite = avatar.graphic;
    }

    public void SetChat(string message)
    {
        coroutine.StartCoroutine(DisplayChat(message));
    }

    private IEnumerator DisplayChat(string message)
    {
        chatObject.SetActive(true);
        chatText.text = message;

        yield return new WaitForSeconds(Mathf.Max(3, message.Length * 0.075f));

        if (chatText.text == message)
        {
            chatObject.SetActive(false);
        }
    }
}
