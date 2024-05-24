using UnityEngine;

public class EnableAllEventListener : VoidEventListener
{
    protected override void HandleEvent()
    {
        base.HandleEvent();
        foreach (Transform c in transform) c.gameObject.SetActive(true);
    }
}
