﻿using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class Main : MonoBehaviour 
{
    [SerializeField] private new Camera camera;
    [SerializeField] private Transform mover;

    private Coroutine coroutine;

    private void Update()
    {
        if (coroutine != null) return;

        if (Input.GetKey(KeyCode.UpArrow))
        {
            Move(Vector2.up);
        }
        else if (Input.GetKey(KeyCode.LeftArrow))
        {
            Move(Vector2.left);
        }
        else if (Input.GetKey(KeyCode.RightArrow))
        {
            Move(Vector2.right);
        }
        else if (Input.GetKey(KeyCode.DownArrow))
        {
            Move(Vector2.down);
        }
    }

    private void Move(Vector2 direction)
    {
        coroutine = StartCoroutine(Move(mover.position, (Vector2) mover.position + direction * 32));
    }

    private IEnumerator Move(Vector2 start,
                             Vector2 end)
    {
        float d = 0.5f;
        float t = 0f;

        var curve = AnimationCurve.Linear(0, 0, d, 1);

        mover.position = start;

        while (t <= d)
        {
            mover.position = Vector2.Lerp(start, end, curve.Evaluate(t));

            t += Time.deltaTime;

            yield return null;
        }

        mover.position = end;

        coroutine = null;
    }
}
