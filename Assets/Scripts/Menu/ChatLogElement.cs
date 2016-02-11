using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class ChatLogElement : MonoBehaviour 
{
    [SerializeField] private Image avatarImage;
    [SerializeField] private Text messageText;

    public void SetMessage(World.Avatar avatar, string message)
    {
        avatarImage.sprite = avatar.graphic;
        messageText.text = message;
    }
}
