using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class kinetics : MonoBehaviour
{
    // Start is called before the first frame update

    public Vector3 startforce;
    void Start()
    {
        Rigidbody rigidbody = GetComponent<Rigidbody>();
        rigidbody.AddForce(startforce,ForceMode.Impulse);
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
