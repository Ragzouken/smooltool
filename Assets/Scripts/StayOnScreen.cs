using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Linq;
using System.Collections;
using System.Collections.Generic;

public class StayOnScreen : MonoBehaviour 
{
    [SerializeField] private Camera camera;
    [SerializeField] private RectTransform world;
    [SerializeField] private RectTransform screen;
    [SerializeField] private RectTransform bounds;
    [SerializeField] private RectTransform target;

    private void Awake()
    {
        transform.SetParent(world, false);
    }

    private void Update()
    {
        var rtrans = transform as RectTransform;

        Vector3[] corners = new Vector3[4];

        bounds.GetWorldCorners(corners);

        Vector2 targetPoint = RectTransformUtility.WorldToScreenPoint(camera, target.position);

        Vector2 min = corners[0];
        Vector2 max = corners[2];

        // screen size
        float width  = max.x - min.x;
        float height = max.y - min.y;

        float xMin = 0   + width;
        float yMin = 0   + height;
        float xMax = 512 - width;
        float yMax = 512 - height;

        float x = Mathf.Clamp(targetPoint.x, xMin, xMax);
        float y = Mathf.Clamp(targetPoint.y, yMin, yMax);

        Vector2 screenActual = new Vector2(x, y);
        Vector3 worldActual = camera.ScreenToWorldPoint(screenActual);

        worldActual.z = 0;

        rtrans.position = worldActual;
    }
}
