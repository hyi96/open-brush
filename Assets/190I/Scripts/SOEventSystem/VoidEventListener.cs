using UnityEngine;

public class VoidEventListener : MonoBehaviour
{
    public VoidEventChannelSO eventChannel;

    private void OnEnable()
    {
        eventChannel.OnEventRaised += HandleEvent;
    }

    private void OnDisable()
    {
        eventChannel.OnEventRaised -= HandleEvent;
    }

    protected virtual void HandleEvent()
    {
        Debug.Log($"void event {eventChannel.description} is received");
    }
}
