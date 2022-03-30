using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SwitchLight : MonoBehaviour
{
    Light flight;

    // Start is called before the first frame update
    void Start()
    {
        flight = GetComponent<Light>();
    }

    // Update is called once per frame
    void Update()
    {

        if (Input.GetKeyDown(KeyCode.L))
        {
            flight.enabled = !flight.enabled;

        }
    }
}
