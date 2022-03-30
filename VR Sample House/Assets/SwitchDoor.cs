using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchDoor : MonoBehaviour
{
    // Start is called before the first frame update

    public GameObject ins;
    public GameObject AnimObject;
    public bool triggerOpen;
    public bool triggerClose;

    void Start()
    {
        
        ins.SetActive(false);

        


}

    // Update is called once per frame
    void Update()
    {
        if (triggerOpen && Input.GetKeyDown(KeyCode.E))
        {
            ins.SetActive(true);
            AnimObject.GetComponent<Animator>().Play("door_anim");
            ins.SetActive(false);

        }
        else if(triggerClose && Input.GetKeyDown(KeyCode.E))
        {
            ins.SetActive(true);
            AnimObject.GetComponent<Animator>().Play("doorClose");
            
        }
    }

    private void OnTriggerEnter(Collider other)
    {
        if (other.transform.tag == "Player")
        {

            ins.SetActive(true);
            
            
        }

        
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.transform.tag == "Player")
        {
            ins.SetActive(false);
            
        }


    }

}


    

