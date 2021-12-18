using UnityEngine;
using System.Collections;

public class Trajectory {

    public const int LENGTH = 120;

    public float width = 25;

    public Vector3[] positions = new Vector3[LENGTH];
    public Vector3[] directions = new Vector3[LENGTH];
    public Matrix3x3[] rotations = new Matrix3x3[LENGTH];

    public float[] heights = new float[LENGTH];

    public float[]
        gait_stand = new float[LENGTH],
        gait_walk = new float[LENGTH],
        gait_jog = new float[LENGTH],
        gait_crouch = new float[LENGTH],
        gait_jump = new float[LENGTH],
        gait_bump = new float[LENGTH];

    public Vector3
        target_dir = new Vector3(0, 0, 1),
        target_vel = new Vector3(0, 0, 0);

    public Trajectory() {
        // if resetting character, directions need to be set back to (0,0,1)
        for (var i = 0; i < directions.Length; i++) {
            directions[i] = new Vector3(0, 0, 1);
        }
    }

}
