using UnityEngine;
using System.Collections;
using System.IO;

public class Character {

    public float
        phase,
        strafe_amount = 0,
        strafe_target = 0,
        crouched_amount = 0,
        crouched_target = 0;
    public bool responsive = false;

    public const int
        JOINT_NUM = 31,
        JOINT_ROOT_L = 1,
        JOINT_HIP_L = 2,
        JOINT_KNEE_L = 3,
        JOINT_HEEL_L = 4,
        JOINT_TOE_L = 5,
        JOINT_ROOT_R = 6,
        JOINT_HIP_R = 7,
        JOINT_KNEE_R = 8,
        JOINT_HEEL_R = 9,
        JOINT_TOE_R = 10;

    public Vector3[] joint_positions = new Vector3[JOINT_NUM];
    public Vector3[] joint_velocities = new Vector3[JOINT_NUM];
    public Matrix3x3[] joint_rotations = new Matrix3x3[JOINT_NUM];

    public Matrix4x4[] joint_anim_xform = new Matrix4x4[JOINT_NUM];
    public Matrix4x4[] joint_rest_xform = new Matrix4x4[JOINT_NUM];
    public Matrix4x4[] joint_mesh_xform = new Matrix4x4[JOINT_NUM];
    public Matrix4x4[] joint_global_rest_xform = new Matrix4x4[JOINT_NUM];
    public Matrix4x4[] joint_global_anim_xform = new Matrix4x4[JOINT_NUM];

    public int[] joint_parents = new int[JOINT_NUM];

    public void Load(string filename_p, string filename_r) {
        // https://answers.unity.com/questions/8187/how-can-i-read-binary-files-from-resources.html
        TextAsset jointParentFile = Resources.Load("dynAnim/" + filename_p) as TextAsset;
        Stream jointStream = new MemoryStream(jointParentFile.bytes);
        BinaryReader jointStreamReader = new BinaryReader(jointStream);

        for (var i = 0; i < JOINT_NUM; i++) {
            joint_parents[i] = (int) jointStreamReader.ReadSingle();
        }

        var restFormFile = Resources.Load<TextAsset>("dynAnim/" + filename_r);
        Stream restFormStream = new MemoryStream(restFormFile.bytes);
        BinaryReader restFormReader = new BinaryReader(restFormStream);

        for (var i = 0; i < JOINT_NUM; i++) {
            for (var r = 0; r < 4; r++) {
                for (var c = 0; c < 4; c++) {
                    joint_rest_xform[i][r, c] = restFormReader.ReadSingle();
                }
            }
        }
    }

    public void Forward_Kinematics() {
        for (int i = 0; i < JOINT_NUM; i++) {
            joint_global_anim_xform[i] = joint_anim_xform[i];
            joint_global_rest_xform[i] = joint_rest_xform[i];
            int j = joint_parents[i];
            while (j != -1) {
                joint_global_anim_xform[i] = joint_anim_xform[j] * joint_global_anim_xform[i];
                joint_global_rest_xform[i] = joint_rest_xform[j] * joint_global_rest_xform[i];
                j = joint_parents[j];
            }
            joint_mesh_xform[i] = joint_global_anim_xform[i] * joint_global_rest_xform[i].inverse;
        }
    }
}
