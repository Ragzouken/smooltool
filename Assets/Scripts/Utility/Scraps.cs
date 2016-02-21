using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System;
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

    /*
    public static T Best<T>(this IList<T> items,
                            Func<T, float> rate,
                            bool lowest = false,
                            int offset = 0)
    {
        float bestRating = lowest ? Mathf.Infinity : Mathf.NegativeInfinity;
        T bestItem = items.FirstOrDefault();

        for (int i = offset; i < items.Count; ++i)
        {
            T item = items[i];
            float rating = rate(item);

            if ((lowest && rating <= bestRating) || rating >= bestRating)
            {
                bestRating = rating;
                bestItem = item;
            }
        }

        return bestItem;
    }
    */

    public static T Best<T>(this IEnumerable<T> items,
                            Func<T, float> rate,
                            bool lowest=false)
    {
        float bestRating = lowest ? Mathf.Infinity : Mathf.NegativeInfinity;
        T bestItem = items.FirstOrDefault();

        foreach (T item in items)
        {
            float rating = rate(item);

            if ((lowest && rating <= bestRating) || rating >= bestRating)
            {
                bestRating = rating;
                bestItem = item;
            }
        }

        return bestItem;
    }

    public static int ColorToPalette(this Color[] palette,
                                     Color color, 
                                     bool clearzero = false)
    {
        if (clearzero && color.a == 0) return 0;

        float bestDistance = 255f;
        int bestIndex = 0;

        for (int i = clearzero ? 1 : 0; i < palette.Length; ++i)
        {
            float distance = Test.ColorDistance(color, palette[i]);

            if (distance < bestDistance)
            {
                bestDistance = distance;
                bestIndex = i;
            }
        }

        return bestIndex;
    }
}
