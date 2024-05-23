using UnityEngine;

public class ObjectSelectionEventListener : IntEventListener
{
    protected override void HandleEvent(int param)
    {
        base.HandleEvent(param);
        Select(param);
    }
    private void DisableAllChildren()
    {
        foreach (Transform c in transform) c.gameObject.SetActive(false);
    }
    private void Select(int index)
    {
        DisableAllChildren();
        transform.GetChild(index).gameObject.SetActive(true);
    }
}
