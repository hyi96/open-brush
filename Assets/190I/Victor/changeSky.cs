using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class changeSky : MonoBehaviour
{
    public Material mat1;
    // Names of the objects to be removed
    private string[] objectNames = new string[]
    {
        "Geo_StandardStage",
        "Geo_FloorBackground",
        "Geo_FloorForeground",
        "Geo_Spikes",
        "GroundDisk"
    };

    // Start is called before the first frame update
    void Start()
    {
       
        RenderSettings.skybox = mat1;
    }

    // Update is called once per frame
    void Update()
    { 
        mat1.SetColor("_Tint", Color.grey);
        RenderSettings.skybox = mat1;
        // Loop through each object name
        foreach (string objectName in objectNames)
        {
            // Find the GameObject by name
            GameObject obj = GameObject.Find(objectName);

            // If the object is found, destroy it
            if (obj != null)
            {
                Destroy(obj);
              //  Debug.Log(objectName + " has been removed from the scene.");
            }
            else
            {
               // Debug.LogWarning(objectName + " could not be found in the scene.");
            }
        }
    }
}
