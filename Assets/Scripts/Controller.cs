using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Controller
{
    public readonly World world;
    public readonly bool hosting;

    public Controller(World world, bool hosting)
    {
        this.world = world;
        this.hosting = hosting;
    }
}
