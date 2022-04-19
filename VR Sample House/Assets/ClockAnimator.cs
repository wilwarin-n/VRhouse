using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System;
using UnityEngine;


public class ClockAnimator : MonoBehaviour
{
    // Start is called before the first frame update

    private const float 
        hourstoDegrees = 360f / 12F,
        minutestoDegrees = 360f / 60f,
        secondstoDegrees = 360f / 60f;

    public Transform hour, minute, second;
    public bool analog;

    void Start()
    {
        
        
    }

    // Update is called once per frame
    void Update()
    {
        

        if (analog)
        {
            TimeSpan timespan = DateTime.Now.TimeOfDay;

            hour.localRotation = Quaternion.Euler((float)timespan.TotalHours * hourstoDegrees, 0f, 0f);
            minute.localRotation = Quaternion.Euler((float)timespan.TotalMinutes * minutestoDegrees, 0f, 0f);
            second.localRotation = Quaternion.Euler((float)timespan.TotalSeconds * secondstoDegrees, 0f, 0f);
        }
        else
        {
            DateTime time = DateTime.Now;

            hour.localRotation = Quaternion.Euler(time.Hour * hourstoDegrees, 0f, 0f);
            minute.localRotation = Quaternion.Euler(time.Minute * minutestoDegrees, 0f, 0f);
            second.localRotation = Quaternion.Euler(time.Second * secondstoDegrees, 0f, 0f);
        }
        

        
    }
}
