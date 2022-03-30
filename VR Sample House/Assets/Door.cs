using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Door : MonoBehaviour
{

    public GameObject Instruction;
    public GameObject AnimObject;

    


    // Start is called before the first frame update
    void Start()
    {
        Instruction.SetActive(false);
        ColliderTest.pLayerInZone = false;
    }

    // Update is called once per frame
    void Update()
    {
        if (Input.GetKeyDown(KeyCode.E))
        {
            if (ColliderTest.pLayerInZone)
            {
                Instruction.SetActive(true);
                AnimObject.GetComponent<Animator>().Play("door_anim");
            }
            
        }
    }

    void OnTriggerEnter(Collider col)
    {
        if(col.transform.tag == "Player")
        {
            Instruction.SetActive(true);
            ColliderTest.pLayerInZone = true;
        }

    }

    void OnTriggerExit(Collider col)
    {
        if(col.transform.tag == "Player")
        {
            Instruction.SetActive(false);
            ColliderTest.pLayerInZone=false;
        }
    }
}
