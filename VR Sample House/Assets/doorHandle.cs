using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class doorHandle : MonoBehaviour
{
    // Start is called before the first frame update

    public Transform handler;
    public float distanc = 0.4f;
    void Start()
    {
        transform.localPosition = Vector3.zero;
    }

    // Update is called once per frame
    void Update()
    {
        if (Vector3.Distance(transform.position, handler.position) > distanc)
        {
            transform.position = transform.localPosition;
            
        }
    }
}

    
