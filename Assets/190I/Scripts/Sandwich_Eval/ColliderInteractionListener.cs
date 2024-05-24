using TMPro;

public class ColliderInteractionListener : IntEventListener
{
    protected override void HandleEvent(int param)
    {
        base.HandleEvent(param);
        GetComponent<TextMeshPro>().text = param==1 ? "IN" : "OUT";
    }
}
