using UnityEngine;
using System.Collections;

public class IK {

    public const int
        HL = 0,
        HR = 1,
        TL = 2,
        TR = 3;

    public float[] locks = new float[4];
    public Vector3[] position = new Vector3[4];
    public float[] height = new float[4];
    public float
        fade = 0.075f,
        threshold = 0.8f,
        smoothness = 0.5f,
        heel_height = 5,
        toe_height = 4;

    public IK() {
        // locks, position, height need to be initialized (demo lines 902-904)

        //float lock[4];
        //glm::vec3 position[4];
        //float height[4];

        //memset(lock, 4, sizeof(float));
        //memset(position, 4, sizeof(glm::vec3));
        //memset(height, 4, sizeof(float));

    }

    /* =============== functions =============== */

    public void Two_Joint(
            Vector3 a, Vector3 b,
            Vector3 c, Vector3 t, float eps,
            ref Matrix4x4 a_pR, ref Matrix4x4 b_pR,
            ref Matrix4x4 a_gR, ref Matrix4x4 b_gR,
            ref Matrix4x4 a_lR, ref Matrix4x4 b_lR) {

        float lc = (b - a).magnitude;
        float la = (b - c).magnitude;
        float lt = Mathf.Clamp((t - a).magnitude, eps, lc + la - eps);

        if ((c - t).magnitude < eps) { return; }

        float ac_ab_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (b - a).normalized), -1.0f, 1.0f));
        float ba_bc_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((a - b).normalized, (c - b).normalized), -1.0f, 1.0f));
        float ac_at_0 = Mathf.Acos(Mathf.Clamp(Vector3.Dot((c - a).normalized, (t - a).normalized), -1.0f, 1.0f));

        float ac_ab_1 = Mathf.Acos(Mathf.Clamp((la * la - lc * lc - lt * lt) / (-2 * lc * lt), -1.0f, 1.0f));
        float ba_bc_1 = Mathf.Acos(Mathf.Clamp((lt * lt - lc * lc - la * la) / (-2 * lc * la), -1.0f, 1.0f));

        Vector3 a0 = Vector3.Cross(b - a, c - a).normalized;
        Vector3 a1 = Vector3.Cross(t - a, c - a).normalized;

        Matrix3x3 r0 = new Matrix3x3((ac_ab_1 - ac_ab_0), -a0);
        Matrix3x3 r1 = new Matrix3x3((ba_bc_1 - ba_bc_0), -a0);
        Matrix3x3 r2 = new Matrix3x3((ac_at_0          ), -a1);

        Matrix3x3 a_lRR = (new Matrix3x3(a_pR)).inverse * (r2 * r0 * new Matrix3x3(a_gR));
        Matrix3x3 b_lRR = (new Matrix3x3(b_pR)).inverse * (r1 * new Matrix3x3(b_gR));

        for (int x = 0; x < 3; x++)
            for (int y = 0; y < 3; y++) {
                a_lR[x, y] = a_lRR[x, y];
                b_lR[x, y] = b_lRR[x, y];
            }

    }

}
