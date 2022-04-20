/*using System.Collections;
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

            bool play = false;
            bool pause = false;
            
            bool seek = false;

            if (interactable.attachedToHand)
            {
                play = actionPlay.GetState(hand);
                pause = actionPause.GetState(hand);
                
                seek = actionSeek.GetState(hand);

            }

            video.play = play;
            video.pause = pause;
            
            video.seek = seek;
        }
    }
}*/
