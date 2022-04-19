using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;


namespace Valve.VR.InteractionSystem

{


    public class SwitchLightV2 : MonoBehaviour
    {
        // Start is called before the first frame update

        

        public GameObject _light;

        //public UnityEvent switchs;

        void Start()
        {
            /*if(switchs != null)
                switchs = new UnityEvent();
            switchs.AddListener(switchlight);
            _light.SetActive(false);*/
        }

        // Update is called once per frame
        void Update()
        {
            /*if (switchs != null)
            {
                switchs.Invoke();
            }*/
        }

        /*void switchlight()
        {
            Debug.Log("Switch Light");
        }*/

        public void switchlightUp(Hand fromHand)
        {
            
            _light.SetActive(false);
        }

        public void switchlightDown(Hand fromhand)
        {
            _light.SetActive(true);
        }
        

        


    }
}
