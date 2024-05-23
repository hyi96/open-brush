using UnityEngine;

public class SpawnStaticMesh : IntEventListener
{
    public GameObject[] staticMeshPrefabs;
    public GameObject spawned;

    protected override void HandleEvent(int param)
    {
        base.HandleEvent(param);
        if (spawned != null) Destroy(spawned);
        spawned = Instantiate(staticMeshPrefabs[param], transform);
    }
}
