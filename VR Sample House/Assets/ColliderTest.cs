using System.Collections;
using System.Collections.Generic;
using UnityEngine;


public class ColliderTest : MonoBehaviour
{
    public GameObject mlight;

    public static bool pLayerInZone;

    public GameObject txtDisplay;
    
    
    
    // Start is called before the first frame update
    void Start()
    {
       
       pLayerInZone = false;
       txtDisplay.SetActive(false);
    }

    // Update is called once per frame
    void Update()
    {
        if(pLayerInZone && Input.GetKeyDown(KeyCode.M))
        {
            mlight.SetActive(!mlight.activeSelf);
        }

        if (Input.GetKey(KeyCode.Escape))
        {
            Application.Quit();

        }
    }

    void OnTriggerEnter(Collider col)
    {
        Debug.Log("Trigger'a giri� yap�ld�.");

        if (col.tag == "Player")
        {
            
            pLayerInZone=true;
            txtDisplay.SetActive(true);
            
        }
        
    }

    public void OnTriggerExit(Collider col)
    {

        Debug.Log("Trigger'dan ��k�� yap�ld�.");

        if (col.tag == "Player")
        {
            pLayerInZone = false;
            txtDisplay.SetActive(false);
            
        }

    }


    

}
