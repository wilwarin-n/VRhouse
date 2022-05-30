using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using Valve.VR.Extras;

public class Laser : MonoBehaviour
{
    public SteamVR_LaserPointer laserPointer;

    void Awake()
    {
        laserPointer.PointerIn += PointerInside;
        laserPointer.PointerOut += PointerOutside;
        laserPointer.PointerClick += PointerClick;
    }

    public void PointerClick(object sender, PointerEventArgs e)
    {
        if (e.target.name == "ThrowableBall")
        {
            Debug.Log("Cube was clicked");
        }
        else if (e.target.name == "Intensity")
        {
            Debug.Log("Slider was clicked");
        }
    }

    public void PointerInside(object sender, PointerEventArgs e)
    {
        if (e.target.name == "ThrowableBall")
        {
            Debug.Log("Cube was entered");
        }
        else if (e.target.name == "Intensity")
        {
            Debug.Log("Button was entered");
        }
    }

    public void PointerOutside(object sender, PointerEventArgs e)
    {
        if (e.target.name == "ThrowableBall")
        {
            Debug.Log("Cube was exited");
        }
        else if (e.target.name == "Intensity")
        {
            Debug.Log("Button was exited");
        }
    }
}