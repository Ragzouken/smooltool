using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.SceneManagement;

public class Splash : MonoBehaviour 
{
    private void Start()
    {
        SceneManager.LoadSceneAsync("test");
    }
}
