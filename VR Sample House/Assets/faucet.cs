using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace com.zibra.liquid.Manipulators
{
    public class faucet : MonoBehaviour
    {
        public GameObject handle;
        public Vector3 handleRotation;
        public GameObject water;
        public ZibraLiquidEmitter emitter;

        private bool playerInZone;

        void Start()
        {

        }

        void Update()
        {
            handleRotation = new Vector3(0, 0, -45);
            transform.eulerAngles = handleRotation;
            openfaucet();
        }

        void openfaucet()
        {
            if (handle.transform.rotation.z <= 315 && 0 > handle.gameObject.transform.rotation.z )
            {
                emitter.ParticlesPerSec = 1000;
                //for (float i = 100; i <= 1000; i++)
                //{
                //    emitter.ParticlesPerSec += i;
                //}
            }
            else
            {
                emitter.ParticlesPerSec = 0;
            }
        }

        /*private void OnTriggerStay(Collider other)
        {
            if ( handle.transform.rotation.z >= 315 && 0 > handle.gameObject.transform.rotation.z && other.gameObject.tag == "Player")
            {
                emitter.ParticlesPerSec = 1000;
            }
            else
            {
                emitter.ParticlesPerSec = 0;
            }
        }*/

        /*private void OnTriggerExit(Collider other)
        {
            if (other.gameObject.tag ==  "Player")
            {
                emitter.ParticlesPerSec = 0;
            }
        }*/


    }
}
