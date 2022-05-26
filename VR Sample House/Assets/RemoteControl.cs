using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace Valve.VR.InteractionSystem
{


    public class RemoteControl : MonoBehaviour
    {
        // Start is called before the first frame update


        public Transform btnplay;
        public Transform btnpause;
       
        public Transform btnseek;

        private Interactable interactable;

        public SteamVR_Action_Boolean actionPlay = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("remote", "Play");

        public SteamVR_Action_Boolean actionPause = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("remote", "Pause"); 
        
        public SteamVR_Action_Boolean actionSeek = SteamVR_Input.GetAction<SteamVR_Action_Boolean>("remote", "Seek");

        public PlayVideo video;

        void Start()
        {
            
        }

        // Update is called once per frame
        void Update()
        {
            SteamVR_Input_Sources hand = interactable.attachedToHand.handType;

            bool b_play = false;
            bool b_pause = false;
            
            bool seek = false;
            float play = 0;

            if (interactable.attachedToHand)
            {
                b_play = actionPlay.GetState(hand);
                b_pause = actionPause.GetState(hand);
                seek = actionSeek.GetState(hand);

                play = b_play ? 1 : 0; 
            }

            btnplay.localScale = new Vector3(1, 1, b_play ? 0.4f : 1.0f);
            btnpause.localScale = new Vector3(1, 1, b_pause ? 0.4f : 1.0f);
            
        }
    }
}
