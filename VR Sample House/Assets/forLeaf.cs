//using System.Collections;
//using System.Collections.Generic;
//using UnityEngine;

//public class forLeaf : MonoBehaviour
//{
//    // Start is called before the first frame update

//    public float rotateAmount = 20f;

//    public GameObject plant;
//    void Start()
//    {
        
//    }

//    // Update is called once per frame
//    void Update()
//    {
        
//    }


    

//    // This will handle when a player enters the collider on the plant or grass object
//    void OnTriggerEnter(Collider2D other)
//    {
//        // Check that the colliding object is the player object
//        if (other.gameObject.tag == "Player")

//            StartCoroutine(RotateMe(Vector3.right * rotateAmount, 5));
//        // Call the rotation
//    }

//    // This will handle when the colliding object leaves the plant or grass bit
//    void OnTriggerExit(Collider2D other)
//    {
//        // Check that the colliding object is the player object
//        if (other.gameObject.tag == "Player")

//            StartCoroutine(RotateMe(-Vector3.right * rotateAmount, 5));
//        // Call the rotation
//    }

//    IEnumerator RotateMe(Vector3 byAngles, float inTime)
//    {
//        Quaternion fromAngle = transform.rotation;
//        Quaternion toAngle = Quaternion.Euler(transform.eulerAngles + byAngles);

//        for (float t = 0f; t < 1; t += Time.deltaTime / inTime)
//        {
//            transform.rotation = Quaternion.Lerp(fromAngle, toAngle, t);
//            yield return null;
//        //}
//    }
//}
