using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayMusic : MonoBehaviour


{
    // Start is called before the first frame update

    public AudioClip audioClip1;
    public AudioClip audioClip2;
    
    public AudioSource audioSource;

    public float volume = 0.5f;

    public Transform playPosition1;
    public Transform playPosition2;

    public GameObject handle;

    

    //public List<AudioClip> trackList = new List<AudioClip>();

    private void Awake()
    {
        
    }

    void Start()
    {
        handle = GetComponent<GameObject>();
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    private void OnTriggerStay(Collider other)
    {
        if (other.gameObject.tag == "Player")
        {

            audioSource.PlayOneShot(audioClip1, volume);
            //Point ile birden fazla müzik dinlemeyi saðlar...
            //AudioSource.PlayClipAtPoint(audioClip1, playPosition1.transform.position, volume);
            //AudioSource.PlayClipAtPoint(audioClip2, playPosition2.position, volume);
            Debug.Log("Track1 is playing");
            /*if (handle.transform.position == playPosition1.position)
            {
                

            }
            else if(handle.transform.position == playPosition2.position)
            {
                Debug.Log("Track2 is playing");


            }*/


        }
        
    }

    private void OnTriggerExit(Collider other)
    {
        if(other.gameObject.tag == "Player")
        {
            
            //Destroy(audioClip, audioClip.length);
            Debug.Log("Stop");
            audioSource.Stop();
        }
         
    }
}
