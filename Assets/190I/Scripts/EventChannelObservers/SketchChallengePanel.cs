using UnityEngine;

public class SketchChallengePanel : IntEventListener
{
    public GameObject buttons;
    protected override void HandleEvent(int param)
    {
        buttons.SetActive(false);
    }
}
