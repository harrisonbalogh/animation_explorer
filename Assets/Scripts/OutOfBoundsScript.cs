using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OutOfBoundsScript : MonoBehaviour {

	void OnTriggerEnter(Collider other)
    {
        transform.parent.Find("Character").gameObject.transform.position = new Vector3(0, 10, 0);
    }

}
