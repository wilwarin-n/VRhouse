using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using System.Collections;

public class SwitchLightVR : MonoBehaviour
{
    [SerializeField] private float threshold = .1f;
    [SerializeField] private float deadzone = 0.025f;

    public bool isPressed;
    public Vector3 _startpos;
    public ConfigurableJoint _joint;
    public GameObject _light;

    public UnityEvent onPressed, onReleased;
    
    void Start()
    {
        _startpos = transform.localPosition;
        _joint = GetComponent<ConfigurableJoint>();
    }

    // Update is called once per frame
    void Update()
    {
        if(!isPressed && GetValue() + threshold >= 1)
            pressed();
        if(isPressed && GetValue() - threshold <=0)
            released();
        

                                     

    }
    
    private float GetValue()
    {
        var value = Vector3.Distance(_startpos, transform.localPosition) / _joint.linearLimit.limit;

        if(Mathf.Abs(value) < deadzone)
        {
            value = 0;
        }

        return Mathf.Clamp(value, -1f, 1f);
        
    }

    private void pressed()
    {
        isPressed = true;
        onPressed.Invoke();
        Debug.Log("Pressed");
        _light.SetActive(true);
    }

    private void released()
    {
        isPressed=false;
        onReleased.Invoke();
        Debug.Log("Released");
        _light.SetActive(false);
    }
}
