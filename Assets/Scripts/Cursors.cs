using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Cursors : MonoBehaviourSingleton<Cursors>
{
    [System.Serializable]
    public class Cursor
    {
        public Texture2D texture;
        public Vector2 hotspot;
    }

    public Cursor picker;
    public Cursor fill;
    public Cursor erase;
    public Cursor draw;

    public void Set(Cursor cursor)
    {
        if (cursor != null)
        {
            UnityEngine.Cursor.SetCursor(cursor.texture, cursor.hotspot, CursorMode.Auto);
        }
        else
        {
            UnityEngine.Cursor.SetCursor(null, Vector2.zero, CursorMode.Auto);
        }
    }
}
