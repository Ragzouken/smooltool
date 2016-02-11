using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public static class Scraps
{
    public static bool Set<T>(this HashSet<T> set,
                              T value,
                              bool member)
    {
        if (!member && set.Contains(value))
        {
            set.Remove(value);

            return true;
        }
        else if (member && !set.Contains(value))
        {
            set.Add(value);

            return true;
        }

        return false;
    }
}
