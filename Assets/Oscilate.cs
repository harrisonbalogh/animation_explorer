using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Oscilate : MonoBehaviour
{
    public float TargetHeight = 0;

    private bool goingUp = true;
    private float scaleYOffset = 0.2f;
    private float dScale = 0.02f;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    public void SetHeight(float h, float offset = 0) {
        TargetHeight = h;
        this.transform.localScale = new Vector3(1, h - (Mathf.Sqrt(offset) * dScale), 1);
    }

    // Update is called once per frame
    void Update()
    {
        if (goingUp) {
            if (this.transform.localScale.y > TargetHeight + scaleYOffset) {
                goingUp = !goingUp;
            } else {
                this.transform.localScale += new Vector3(0,dScale, 0);
            }
        } else {
            if (this.transform.localScale.y < TargetHeight - scaleYOffset) {
                goingUp = !goingUp;
            } else {
                this.transform.localScale -= new Vector3(0, dScale, 0);
            }
        }

    }
}
