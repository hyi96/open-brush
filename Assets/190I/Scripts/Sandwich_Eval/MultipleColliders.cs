using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using TMPro; // Include the TextMeshPro namespace
public class MultipleColliders : MonoBehaviour
{
    // Array to hold all brush colliders
    public GameObject[] brushColliders;
    // Reference to the TextMeshPro text object
    public TMP_Text collisionText;
    // Start is called before the first frame update
    void Start()
    {
        // Find all objects with the tag "brushCollider"
        // Find all objects with the tag "brushCollider"
        //brushColliders = GameObject.FindGameObjectsWithTag("brushCollider");
        // Get the TextMeshPro text component on the same GameObject
        collisionText = GetComponent<TMP_Text>();
        collisionText.text = "Too far away from model";
    }

    // Update is called once per frame
    void Update()
    { // Initialize the text if not already set
        if (collisionText != GetComponent<TMP_Text>())
        {
            collisionText = GetComponent<TextMeshPro>();
            collisionText.text = "0%";
        }

        // Check each brush collider for collisions
        for(int i = brushColliders.Length - 1; i >=0; i--)
        {
           Collider collider3D = brushColliders[i].GetComponent<Collider>();

       
           if (collider3D != null)
            {
                // Check for collisions
                Collider[] results = Physics.OverlapBox(collider3D.bounds.center, collider3D.bounds.extents, collider3D.transform.rotation);
                Debug.Log(results);
                if(results == null)
                {
                    collisionText.text = "Get Closer";
                }
                foreach (var result in results)
                {
                    //collisionText.text = result.name;
                    if (result != null && result.CompareTag("ModelCollider"))
                    {
                       
                        switch (i)
                        {
                            case 0:
                                collisionText.text = "100%";
                                break;
                            case 1:
                                collisionText.text = "80%";
                                break;
                            case 2:
                                collisionText.text = "60%";
                                break;
                            case 3:
                                collisionText.text = "40%";
                                break;
                            // Add more cases as needed for other colliders
                            default:
                                collisionText.text = "Unknown collider";
                                break;
                        }

                        break;
                    }
                }
            }
          
        }
    }


}
