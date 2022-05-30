using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System.Collections;
using UnityEngine.Events;
using UnityEngine.UI;


namespace Valve.VR.InteractionSystem
{
    public class SwitchLightV2 : MonoBehaviour
    {
        public Light _light;

        public static bool playerIn;

        public Slider sliderInt;
        
        public Slider sliderR;
        public Slider sliderG;
        
        public float defaultValue;
        void Start()
        {
            playerIn = false;
            defaultValue = 2f;
            sliderInt.value = defaultValue;   
        }
        void Update()
        {
            _light.intensity = sliderInt.value;
            _light.color = Color.HSVToRGB(sliderR.value, sliderG.value, 1);
        }
        private void OnTriggerEnter(Collider other)
        {
            if (other.tag == "Player")
            {
                Debug.Log("Trigger ENTER!");
                playerIn = true;

                sliderInt.gameObject.SetActive(true);
                sliderR.gameObject.SetActive(true);
                sliderG.gameObject.SetActive(true);
            }
        }
        private void OnTriggerExit(Collider other)
        {
            if(other.tag == "Player")
            {
                    Debug.Log("Trigger EXIT!");
                    playerIn = false;

                    sliderInt.gameObject.SetActive(false);
                    sliderR.gameObject.SetActive(false);
                    sliderG.gameObject.SetActive(false);
  
            }
        }

        





    }
}
