using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class CustomiseTab : MonoBehaviour 
{
    [SerializeField] private InputField nameInput;
    [SerializeField] private Button editAvatarButton;
    [SerializeField] private Button resetAvatarButton;
    [SerializeField] private Image avatarImage;

    private TileEditor editor;
    private Sprite avatar;
    private System.Action save;
    private System.Action reset;

    public void Setup(TileEditor editor,
                      Sprite avatar,
                      System.Action save,
                      System.Action reset)
    {
        this.editor = editor;
        this.avatar = avatar;
        this.save = save;
        this.reset = reset;

        avatarImage.sprite = avatar;
    }

    private void Awake()
    {
        nameInput.onEndEdit.AddListener(OnNameChanged);
        editAvatarButton.onClick.AddListener(OnClickedEditAvatar);
        resetAvatarButton.onClick.AddListener(OnClickedResetAvatar);
    }

    private Color[] RandomPalette()
    {
        return new [] { Color.clear }
               .Concat(Enumerable.Range(1, 19)
                                 .Select(i => new Color(Random.value,
                                                        Random.value,
                                                        Random.value,
                                                        1)))
               .ToArray();
    }

    private void OnEnable()
    {
        nameInput.text = avatar.texture.name;
    }

    private void OnDisable()
    {
        OnNameChanged(nameInput.text);
    }

    private void OnNameChanged(string name)
    {
        avatar.texture.name = name;

        save();
    }

    private void OnClickedEditAvatar()
    {
        editor.OpenAndEdit(RandomPalette(),
                           avatar,
                           save,
                           delegate { });
    }

    private void OnClickedResetAvatar()
    {
        reset();
    }
}
