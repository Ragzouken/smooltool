using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Assertions;
using System.Collections;
using System.Collections.Generic;

using UnityEngine.EventSystems;

public class TooltipTrigger : MonoBehaviour, 
                              IPointerEnterHandler,
                              IPointerExitHandler
{
    public string text;

    private void OnDisable()
    {
        Tooltip.Instance.Exit(this);
    }

    void IPointerEnterHandler.OnPointerEnter(PointerEventData eventData)
    {
        Tooltip.Instance.Enter(this);
    }

    void IPointerExitHandler.OnPointerExit(PointerEventData eventData)
    {
        Tooltip.Instance.Exit(this);
    }
}
