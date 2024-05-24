using UnityEngine;

public class ColliderDetector : IntEventRaiser
{
    private void OnTriggerEnter(Collider other)
    {
        TriggerEvent(1);
    }
    private void OnTriggerExit(Collider other)
    {
        TriggerEvent(0);
    }
}
