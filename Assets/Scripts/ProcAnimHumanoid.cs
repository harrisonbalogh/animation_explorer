using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;
using MathNet.Numerics.LinearAlgebra;
using UnityEngine.UI;

public class ProcAnimHumanoid : MonoBehaviour {

    [SerializeField]
    private GameObject _shoulderPoint;
    [SerializeField]
    private Transform root;

    [SerializeField]
    private Transform head;

    [SerializeField]
    private GameObject[] joints;

    enum Axis {X, Y, Z};
    [SerializeField]
    private Dropdown position_axisMap_x;
    [SerializeField]
    private Dropdown position_axisMap_y;
    [SerializeField]
    private Dropdown position_axisMap_z;

    [SerializeField]
    private Text position_scale_x;
    [SerializeField]
    private Text position_scale_y;
    [SerializeField]
    private Text position_scale_z;

    [SerializeField]
    private Dropdown rotation_axisMap_x;
    [SerializeField]
    private Dropdown rotation_axisMap_y;
    [SerializeField]
    private Dropdown rotation_axisMap_z;

    [SerializeField]
    private Text rotation_scale_x;
    [SerializeField]
    private Text rotation_scale_y;
    [SerializeField]
    private Text rotation_scale_z;

    // PFNN Members
    Trajectory trajectory = new Trajectory();
    Character character = new Character();
    PFNN pfnn = new PFNN(PFNN.MODE_CONSTANT);
    IK ik = new IK();

    const bool
        ENABLE_IK = false,
        INVERT_Y = false;
    const float
        EXTRA_DIR_SMOOTH = 0.9f,
        EXTRA_VEL_SMOOTH = 0.9f,
        EXTRA_STRAFE_SMOOTH = 0.9f,
        EXTRA_CROUCH_SMOOTH = 0.9f,
        EXTRA_GAIT_SMOOTH = 0.1f,
        EXTRA_JOINT_SMOOTH = 0.5f;

    float SPEED = 5.0f;
    bool IS_SPRINTING = false;
    float HEIGHTMAP_PLACEHOLDER = 0.0f;

    int changingAxis = 0;
    int[] axisValues = new int[]{ 0, 0, 0 };

    void Start() {

        float stand_amount = .9740037f;
        float yp = .0007484521f;
        var nn3 = new Matrix3x3(stand_amount * yp, Vector3.up) * new Vector3(0,0,1);
        //print("stand_amount * yp: " + stand_amount*yp);
        //print("nn3: " + nn3.x + ", " + nn3.y + ", " + nn3.z);

        character.Load("network/character_parents", "network/character_xforms");
        pfnn.Load();
    }

    private void FixedUpdate() {
        if (Input.GetButtonDown("RBumper")) {
            changingAxis = (changingAxis + 1) % 4;
        }
    }

    private void Update() {
        Pre_Render();
        UpdateJoints();
    }

    private void LateUpdate() {
        Post_Render();
        count_updates++;
        //if (count_updates > 1) {
        //    Debug.Break();
        //}
    }

    void Pre_Render() {
        //print("======================== " + count_updates + " ===");
        // ============ Camera

        float yaw = Input.GetAxis("Horizontal_R") * 5 + _shoulderPoint.transform.localEulerAngles.y;
        if (changingAxis == 0)
            _shoulderPoint.transform.localEulerAngles = new Vector3(_shoulderPoint.transform.localEulerAngles.x, yaw, _shoulderPoint.transform.localEulerAngles.z);
        else {
            axisValues[changingAxis - 1] = (axisValues[changingAxis - 1] + (int) Input.GetAxis("Horizontal_R")) % 360;
            // print(axisValues[0] + " , " + axisValues[1] + " , " + axisValues[2]);
        }
        //head.localEulerAngles = new Vector3(_shoulderPoint.transform.localEulerAngles.x, yaw, _shoulderPoint.transform.localEulerAngles.z);
        float pitch = Input.GetAxis("Vertical_R") * 3 + _shoulderPoint.transform.localEulerAngles.x;
        // Clamp up/down looking
        if (pitch < 180) {
            if (pitch > 5) {
                pitch = 5;
            }
        }
        else if (pitch >= 180) {
            if (pitch < 360 - 30) {
                pitch = 360 - 30;
            }
        }
        if (changingAxis == 0)
            _shoulderPoint.transform.localEulerAngles = new Vector3(pitch, _shoulderPoint.transform.localEulerAngles.y, _shoulderPoint.transform.localEulerAngles.z);
        //head.localEulerAngles = new Vector3(pitch, _shoulderPoint.transform.localEulerAngles.y, _shoulderPoint.transform.localEulerAngles.z);

        // ============= Target Direction / Velocity

        float x_vel = -Input.GetAxis("Horizontal");
        float y_vel = Input.GetAxis("Vertical");

        float x = -Mathf.Cos(_shoulderPoint.transform.eulerAngles.y * Mathf.Deg2Rad);
        float z = -Mathf.Sin(_shoulderPoint.transform.eulerAngles.y * Mathf.Deg2Rad); // put neg here
        //Debug.Log("Vel: (" + x_vel + " , " + y_vel + ")");
        //Debug.Log("Dir: (" + x + " , " + z + ")");
        Vector3 trajectory_target_direction_new = (new Vector3(x, 0, z)).normalized;
        Matrix3x3 trajectory_target_rotation = new Matrix3x3(Mathf.Atan2(
                trajectory_target_direction_new.x,
                trajectory_target_direction_new.z), Vector3.up
        );

        float target_vel_speed = SPEED; // + sprint_modifier;

        Vector3 trajectory_target_velocity_new = target_vel_speed * (trajectory_target_rotation * new Vector3(x_vel, 0, y_vel));
        trajectory.target_vel = Vector3.LerpUnclamped(trajectory.target_vel, trajectory_target_velocity_new, EXTRA_VEL_SMOOTH);

        character.strafe_target = 0.5f; // strafe_modifier;
        character.strafe_amount = Mathf.LerpUnclamped(character.strafe_amount, character.strafe_target, EXTRA_STRAFE_SMOOTH);

        Vector3 trajectory_target_velocity_dir = trajectory.target_vel.magnitude < 0.0001 ? trajectory.target_dir : trajectory.target_vel.normalized;
        trajectory_target_direction_new = mix_directions(trajectory_target_velocity_dir, trajectory_target_direction_new, character.strafe_amount);
        trajectory.target_dir = mix_directions(trajectory.target_dir, trajectory_target_direction_new, EXTRA_DIR_SMOOTH);

        character.crouched_amount = Mathf.LerpUnclamped(character.crouched_amount, character.crouched_target, EXTRA_CROUCH_SMOOTH);

        // match good
        //print("crouch: " + character.crouched_amount + " strafe: " + character.strafe_amount + " targ: " + trajectory.target_dir + " vel: " + trajectory.target_vel);

        // ================== Gait

        if (trajectory.target_vel.magnitude < 0.1) {
            // Stand
            float stand_amount = 1.0f - Mathf.Clamp01(trajectory.target_vel.magnitude / 0.1f);
            trajectory.gait_stand[Trajectory.LENGTH / 2]    = Mathf.LerpUnclamped(trajectory.gait_stand[Trajectory.LENGTH / 2],  stand_amount, EXTRA_GAIT_SMOOTH);
            trajectory.gait_walk[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_walk[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jog[Trajectory.LENGTH / 2]      = Mathf.LerpUnclamped(trajectory.gait_jog[Trajectory.LENGTH / 2],    0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_crouch[Trajectory.LENGTH / 2]   = Mathf.LerpUnclamped(trajectory.gait_crouch[Trajectory.LENGTH / 2], 0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_jump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_bump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_bump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
        }
        else
        if (character.crouched_amount > 0.1) {
            // Crouch
            trajectory.gait_stand[Trajectory.LENGTH / 2]    = Mathf.LerpUnclamped(trajectory.gait_stand[Trajectory.LENGTH / 2],  0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_walk[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_walk[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jog[Trajectory.LENGTH / 2]      = Mathf.LerpUnclamped(trajectory.gait_jog[Trajectory.LENGTH / 2],    0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_crouch[Trajectory.LENGTH / 2]   = Mathf.LerpUnclamped(trajectory.gait_crouch[Trajectory.LENGTH / 2], character.crouched_amount, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_jump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_bump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_bump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
        }
        else
        if (IS_SPRINTING) {
            // Sprint
            trajectory.gait_stand[Trajectory.LENGTH / 2]    = Mathf.LerpUnclamped(trajectory.gait_stand[Trajectory.LENGTH / 2],  0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_walk[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_walk[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jog[Trajectory.LENGTH / 2]      = Mathf.LerpUnclamped(trajectory.gait_jog[Trajectory.LENGTH / 2],    1.0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_crouch[Trajectory.LENGTH / 2]   = Mathf.LerpUnclamped(trajectory.gait_crouch[Trajectory.LENGTH / 2], 0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_jump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_bump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_bump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
        }
        else {
            // Jog/Walk
            trajectory.gait_stand[Trajectory.LENGTH / 2]    = Mathf.LerpUnclamped(trajectory.gait_stand[Trajectory.LENGTH / 2],  0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_walk[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_walk[Trajectory.LENGTH / 2],   1.0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jog[Trajectory.LENGTH / 2]      = Mathf.LerpUnclamped(trajectory.gait_jog[Trajectory.LENGTH / 2],    0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_crouch[Trajectory.LENGTH / 2]   = Mathf.LerpUnclamped(trajectory.gait_crouch[Trajectory.LENGTH / 2], 0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_jump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_jump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
            trajectory.gait_bump[Trajectory.LENGTH / 2]     = Mathf.LerpUnclamped(trajectory.gait_bump[Trajectory.LENGTH / 2],   0f, EXTRA_GAIT_SMOOTH);
        }

        // match good
        //print("stand: " + trajectory.gait_stand[Trajectory.LENGTH / 2]);

        // ================ Trajectory Predictions

        Vector3[] trajectory_positions_blend = new Vector3[Trajectory.LENGTH];
        trajectory_positions_blend[Trajectory.LENGTH / 2] = trajectory.positions[Trajectory.LENGTH / 2];

        //print("  pos: " + trajectory.positions[Trajectory.LENGTH / 2]);
        //print("60 before pos: " + trajectory.positions[60] + " dir: " + trajectory.directions[60]);

        for (int i = Trajectory.LENGTH / 2 + 1; i < Trajectory.LENGTH; i++) {

            float bias_pos = (character.responsive) ? Mathf.LerpUnclamped(2.0f, 2.0f, character.strafe_amount) : Mathf.LerpUnclamped(0.5f, 1.0f, character.strafe_amount);
            float bias_dir = (character.responsive) ? Mathf.LerpUnclamped(5.0f, 3.0f, character.strafe_amount) : Mathf.LerpUnclamped(2.0f, 0.5f, character.strafe_amount);

            float scale_pos = (1.0f - Mathf.Pow(1.0f - ((float)(i - Trajectory.LENGTH / 2) / (Trajectory.LENGTH / 2)), bias_pos));
            float scale_dir = (1.0f - Mathf.Pow(1.0f - ((float)(i - Trajectory.LENGTH / 2) / (Trajectory.LENGTH / 2)), bias_dir));

            trajectory_positions_blend[i] = trajectory_positions_blend[i - 1] + Vector3.LerpUnclamped(
                trajectory.positions[i] - trajectory.positions[i - 1],
                trajectory.target_vel,
                scale_pos);

            // Wall collisions
            //for (int j = 0; j < WALL_COUNT; j++) {

            //}

            trajectory.directions[i] = mix_directions(trajectory.directions[i], trajectory.target_dir, scale_dir);

            trajectory.heights[i] = trajectory.heights[Trajectory.LENGTH / 2];

            trajectory.gait_stand[i]    = trajectory.gait_stand[Trajectory.LENGTH / 2];
            trajectory.gait_walk[i]     = trajectory.gait_walk[Trajectory.LENGTH / 2];
            trajectory.gait_jog[i]      = trajectory.gait_jog[Trajectory.LENGTH / 2];
            trajectory.gait_crouch[i]   = trajectory.gait_crouch[Trajectory.LENGTH / 2];
            trajectory.gait_jump[i]     = trajectory.gait_jump[Trajectory.LENGTH / 2];
            trajectory.gait_bump[i]     = trajectory.gait_bump[Trajectory.LENGTH / 2];
        }

        for (int i = Trajectory.LENGTH / 2 + 1; i < Trajectory.LENGTH; i++) {
            trajectory.positions[i] = trajectory_positions_blend[i];
        }

        //print("60 after  pos: " + trajectory.positions[60] + " dir: " + trajectory.directions[60]);

        // Jumps
        //for (int i = Trajectory.LENGTH/2; i < Trajectory.LENGTH; i++) {
        //}

        // Crouch
        //for (int i = Trajectory.LENGTH / 2 + 1; i < Trajectory.LENGTH; i++) {
        //}

        // Walls
        //for (int i = Trajectory.LENGTH / 2 + 1; i < Trajectory.LENGTH; i++) {
        //}

        // Rotate Trajectory
        for (int i = 0; i < Trajectory.LENGTH; i++) {
            //if (i == Trajectory.LENGTH / 2) {
            //    print("RootRot breakdown. dir.x: " + 
            //        trajectory.directions[i].x + " dir.z: " + 
            //        trajectory.directions[i].z + " atan2: " + 
            //        Mathf.Atan2(trajectory.directions[i].x, trajectory.directions[i].z));
            //}
            trajectory.rotations[i] = new Matrix3x3(Mathf.Atan2(
                    trajectory.directions[i].x, 
                    trajectory.directions[i].z), Vector3.up
            );
        }

        // Trajectory Heights
        for (int i = Trajectory.LENGTH / 2; i < Trajectory.LENGTH; i++) {
            trajectory.positions[i].y = HEIGHTMAP_PLACEHOLDER;
        }

        trajectory.heights[Trajectory.LENGTH / 2] = 0.0f;
        for (int i = 0; i < Trajectory.LENGTH; i += 10) {
            trajectory.heights[Trajectory.LENGTH / 2] += (trajectory.positions[i].y / (Trajectory.LENGTH / 10));
        }

        Vector3 root_position = new Vector3(
            trajectory.positions[Trajectory.LENGTH / 2].x,
            trajectory.heights[Trajectory.LENGTH / 2],
            trajectory.positions[Trajectory.LENGTH / 2].z
        );

        Matrix3x3 root_rotation = trajectory.rotations[Trajectory.LENGTH / 2];

        // Input trajectory positions / directions -- Xp[0 to 47]
        for (int i = 0; i < Trajectory.LENGTH; i += 10) {
            int w = Trajectory.LENGTH / 10;
            Vector3 pos = root_rotation.inverse * (trajectory.positions[i] - root_position);
            Vector3 dir = root_rotation.inverse * trajectory.directions[i];
            //if ((w * 0 + i / 10) == 0)
            //    print("traj.rot[curr]: " + trajectory.rotations[Trajectory.LENGTH / 2] + "\t traj.pos[0]: " + trajectory.positions[0] + "\t root_pos: " + root_position);
            pfnn.Xp[(w * 0) + i / 10] = pos.x; pfnn.Xp[(w * 1) + i / 10] = pos.z;
            pfnn.Xp[(w * 2) + i / 10] = dir.x; pfnn.Xp[(w * 3) + i / 10] = dir.z;
        }

        // check trajectory.directions
        // print("traj.dir 60: (" + trajectory.directions[60].x + ", " + trajectory.directions[60].y + ", " + trajectory.directions[60].z + ")");

        // Input trajectory gaits -- Xp[48 to 119]
        for (int i = 0; i < Trajectory.LENGTH; i += 10) {
            int w = Trajectory.LENGTH / 10;
            pfnn.Xp[(w * 4) + i / 10] = trajectory.gait_stand[i];
            pfnn.Xp[(w * 5) + i / 10] = trajectory.gait_walk[i];
            pfnn.Xp[(w * 6) + i / 10] = trajectory.gait_jog[i];
            pfnn.Xp[(w * 7) + i / 10] = trajectory.gait_crouch[i];
            pfnn.Xp[(w * 8) + i / 10] = trajectory.gait_jump[i];
            pfnn.Xp[(w * 9) + i / 10] = 0.0f;
        }

        // Input joint previous positions / velocities / rotations
        Vector3 prev_root_position = new Vector3(
            trajectory.positions[Trajectory.LENGTH / 2 - 1].x,
            trajectory.heights[Trajectory.LENGTH / 2 - 1],
            trajectory.positions[Trajectory.LENGTH / 2 - 1].z
        );

        Matrix3x3 prev_root_rotation = trajectory.rotations[Trajectory.LENGTH / 2 - 1];

        // Input previous rotations -- Xp[120 to 305]
        for (int i = 0; i < Character.JOINT_NUM; i++) {
            int o = (Trajectory.LENGTH / 10) * 10;
            Vector3 pos = prev_root_rotation.inverse * (character.joint_positions[i] - prev_root_position);
            Vector3 prv = prev_root_rotation.inverse * character.joint_velocities[i];
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 0 + i * 3 + 0] = pos.x;
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 0 + i * 3 + 1] = pos.y;
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 0 + i * 3 + 2] = pos.z;
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 1 + i * 3 + 0] = prv.x;
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 1 + i * 3 + 1] = prv.y;
            pfnn.Xp[o + Character.JOINT_NUM * 3 * 1 + i * 3 + 2] = prv.z;
        }

        // Input trajectory heights -- Xp[306 to 341]
        for (int i = 0; i < Trajectory.LENGTH; i += 10) {
            int o = (Trajectory.LENGTH / 10) * 10 + Character.JOINT_NUM * 3 * 2;
            int w = Trajectory.LENGTH / 10;
            Vector3 position_r = trajectory.positions[i] + (trajectory.rotations[i] * new Vector3(trajectory.width, 0, 0));
            Vector3 position_l = trajectory.positions[i] + (trajectory.rotations[i] * new Vector3(-trajectory.width, 0, 0));
            pfnn.Xp[o + w * 0 + i / 10] = HEIGHTMAP_PLACEHOLDER - root_position.y;
            pfnn.Xp[o + w * 1 + i / 10] = trajectory.positions[i].y - root_position.y;
            pfnn.Xp[o + w * 2 + i / 10] = HEIGHTMAP_PLACEHOLDER - root_position.y;
        }

        // check root_pos, root_rot - match good
        //print("rootPos: (" + root_position.x + ", " + root_position.y + ", " + root_position.z + ") rootRot: " + root_rotation);

        // Regression

        pfnn.predict(character.phase);
        
        // check Yp predict results

        // Local transforms

        for (int i = 0; i < Character.JOINT_NUM; i++) {
            int opos = 8 + Trajectory.LENGTH / 2 / 10 * 4 + Character.JOINT_NUM * 3 * 0;
            int ovel = 8 + Trajectory.LENGTH / 2 / 10 * 4 + Character.JOINT_NUM * 3 * 1;
            int orot = 8 + Trajectory.LENGTH / 2 / 10 * 4 + Character.JOINT_NUM * 3 * 2;

            Vector3 pos = root_rotation * new Vector3(pfnn.Yp[opos + i * 3 + 0], pfnn.Yp[opos + i * 3 + 1], pfnn.Yp[opos + i * 3 + 2]) + root_position;
            Vector3 vel = root_rotation * new Vector3(pfnn.Yp[ovel + i * 3 + 0], pfnn.Yp[ovel + i * 3 + 1], pfnn.Yp[ovel + i * 3 + 2]);
            if (i == 3) {
                //print("Yp " + (orot + i * 3 + 0) + ": " + pfnn.Yp[orot + i * 3 + 0] +
                    //" Yp " + (orot + i * 3 + 1) + ": " + pfnn.Yp[orot + i * 3 + 1] +
                    //" Yp " + (orot + i * 3 + 2) + ": " + pfnn.Yp[orot + i * 3 + 2]);
                //Quaternion qq = quat_exp(new Vector3(pfnn.Yp[orot + i * 3 + 0], pfnn.Yp[orot + i * 3 + 1], pfnn.Yp[orot + i * 3 + 2]));
                //print("quat_exp: quat(" + qq.w + ", {" + qq.x + ", " + qq.y + ", " + qq.z + "})");
                //print("toMat4: " + qq.toMatrix4x4());
                //print("toMat3: " + new Matrix3x3(qq.toMatrix4x4()));
                //print("toMat3: " + qq.toMatrix3x3());
                //Quaternion bak = (qq.toMatrix3x3()).ExtractRotation();
                //print("quat: quat(" + bak.w + ", {" + bak.x + ", " + bak.y + ", " + bak.z + "}");
            }
            Matrix3x3 rot = root_rotation * quat_exp(new Vector3(pfnn.Yp[orot + i * 3 + 0], pfnn.Yp[orot + i * 3 + 1], pfnn.Yp[orot + i * 3 + 2])).toMatrix3x3();
            //if (i == 3) {
            //    print("Rot: " + rot);
            //}

            // Author: Blend predicted positions and previous positions plus velocity to smooth out motion in case the two disagree

            character.joint_positions[i] = Vector3.LerpUnclamped(character.joint_positions[i] + vel, pos, EXTRA_JOINT_SMOOTH);
            character.joint_velocities[i] = vel;
            character.joint_rotations[i] = rot;

            //if (i == 3) {
            //    Vector3 rr = rot.ExtractRotation().eulerAngles;
            //    print("Joint " + i + " rot " + rr);
            //}

            // match good
            //print(i + ": " + pfnn.Yp[orot + i * 3 + 0] + " , " + pfnn.Yp[orot + i * 3 + 1] + " , " + pfnn.Yp[orot + i * 3 + 2]);
            //print("pos: " + character.joint_positions[0] + " vel: " + vel + " rot: " + rot);

            character.joint_global_anim_xform[i] = new Matrix4x4(
                new Vector4(rot[0,0], rot[1,0], rot[2,0], pos[0]),
                new Vector4(rot[0,1], rot[1,1], rot[2,1], pos[1]),
                new Vector4(rot[0,2], rot[1,2], rot[2,2], pos[2]),
                new Vector4(       0,        0,        0,      1)).transpose;
        }

        // Author: Convert to local space (inefficient)

        for (int i = 0; i < Character.JOINT_NUM; i++) {
            if (i == 0) {
                character.joint_anim_xform[i] = character.joint_global_anim_xform[i];
            } else {
                // match good
                character.joint_anim_xform[i] = character.joint_global_anim_xform[character.joint_parents[i]].inverse * character.joint_global_anim_xform[i];
                //if (i == 3) {
                //    print("Anim.Gxform: " + character.joint_global_anim_xform[character.joint_parents[i]].inverse + "\n: " + character.joint_global_anim_xform[i]);
                //    print("to Anim.Gxform: " + character.joint_anim_xform[i]);
                //    print(i + "----------------");
                //}
            }
        }

        character.Forward_Kinematics();

        //print(count_updates + " - Knee: " + character.joint_mesh_xform[3].ExtractPosition() + ", Heel: " + character.joint_mesh_xform[4].ExtractPosition());

        if (ENABLE_IK) {

            // get weights

            Vector4 ik_weight = new Vector4(pfnn.Yp[4 + 0], pfnn.Yp[4 + 1], pfnn.Yp[4 + 2], pfnn.Yp[4 + 3]);

            Vector3 key_hl = new Vector3(
                character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 0],
                character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 1],
                character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 2]);
            Vector3 key_tl = new Vector3(
                character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 0],
                character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 1],
                character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 2]);
            Vector3 key_hr = new Vector3(
                character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 0],
                character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 1],
                character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 2]);
            Vector3 key_tr = new Vector3(
                character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 0],
                character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 1],
                character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 2]);

            key_hl = Vector3.LerpUnclamped(key_hl, ik.position[IK.HL], ik.locks[IK.HL]);
            key_tl = Vector3.LerpUnclamped(key_tl, ik.position[IK.TL], ik.locks[IK.TL]);
            key_hr = Vector3.LerpUnclamped(key_hr, ik.position[IK.HR], ik.locks[IK.HR]);
            key_tr = Vector3.LerpUnclamped(key_tr, ik.position[IK.TR], ik.locks[IK.TR]);

            ik.height[IK.HL] = Mathf.LerpUnclamped(ik.height[IK.HL], ik.heel_height, ik.smoothness);
            ik.height[IK.TL] = Mathf.LerpUnclamped(ik.height[IK.TL], ik.toe_height, ik.smoothness);
            ik.height[IK.HR] = Mathf.LerpUnclamped(ik.height[IK.HR], ik.heel_height, ik.smoothness);
            ik.height[IK.TR] = Mathf.LerpUnclamped(ik.height[IK.TR], ik.toe_height, ik.smoothness);

            key_hl.y = Mathf.Max(key_hl.y, ik.height[IK.HL]);
            key_tl.y = Mathf.Max(key_tl.y, ik.height[IK.TL]);
            key_hr.y = Mathf.Max(key_hr.y, ik.height[IK.HR]);
            key_tr.y = Mathf.Max(key_tr.y, ik.height[IK.TR]);

            // rotate hip/knee
            {
                Vector3 hip_l = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HIP_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HIP_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HIP_L][3, 2]);
                Vector3 knee_l = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_KNEE_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_L][3, 2]);
                Vector3 heel_l = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 2]);

                Vector3 hip_r = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HIP_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HIP_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HIP_R][3, 2]);
                Vector3 knee_r = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_KNEE_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_R][3, 2]);
                Vector3 heel_r = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 2]);

                ik.Two_Joint(hip_l, knee_l, heel_l, key_hl, 1.0f,
                    ref character.joint_global_anim_xform[Character.JOINT_ROOT_L],
                    ref character.joint_global_anim_xform[Character.JOINT_HIP_L],
                    ref character.joint_global_anim_xform[Character.JOINT_HIP_L],
                    ref character.joint_global_anim_xform[Character.JOINT_KNEE_L],
                    ref character.joint_anim_xform[Character.JOINT_HIP_L],
                    ref character.joint_anim_xform[Character.JOINT_KNEE_L]);

                ik.Two_Joint(hip_r, knee_r, heel_r, key_hr, 1.0f,
                    ref character.joint_global_anim_xform[Character.JOINT_ROOT_R],
                    ref character.joint_global_anim_xform[Character.JOINT_HIP_R],
                    ref character.joint_global_anim_xform[Character.JOINT_HIP_R],
                    ref character.joint_global_anim_xform[Character.JOINT_KNEE_R],
                    ref character.joint_anim_xform[Character.JOINT_HIP_R],
                    ref character.joint_anim_xform[Character.JOINT_KNEE_R]);

                character.Forward_Kinematics();
            }

            // Rotate Heel

            {
                const float heel_max_bend_s = 4f;
                const float heel_max_bend_u = 4f;
                const float heel_max_bend_d = 4f;

                // Clamp magnitude
                Vector4 ik_toe_pos_blend = ik_weight * 2.5f;
                if (ik_toe_pos_blend.magnitude > 1) {
                    ik_toe_pos_blend = ik_toe_pos_blend.normalized;
                } else if (ik_toe_pos_blend.magnitude < 0) {
                    ik_toe_pos_blend = ik_toe_pos_blend.normalized * 0.0f;
                }

                Vector3 heel_l = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 2]);
                Vector4 side_h0_l = character.joint_global_anim_xform[Character.JOINT_HEEL_L] * new Vector4( 10, 0, 0, 1);
                Vector4 side_h1_l = character.joint_global_anim_xform[Character.JOINT_HEEL_L] * new Vector4(-10, 0, 0, 1);
                Vector3 side0_l = new Vector3(side_h0_l.x, side_h0_l.y, side_h0_l.z) / side_h0_l.w;
                Vector3 side1_l = new Vector3(side_h1_l.x, side_h1_l.y, side_h1_l.z) / side_h1_l.w;
                Vector3 floor_l = key_tl;

                side0_l.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, heel_l.y - heel_max_bend_s, heel_l.y + heel_max_bend_s);
                side1_l.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, heel_l.y - heel_max_bend_s, heel_l.y + heel_max_bend_s);
                floor_l.y = Mathf.Clamp(floor_l.y, heel_l.y - heel_max_bend_d, heel_l.y + heel_max_bend_u);

                Vector3 targ_z_l = (floor_l - heel_l).normalized;
                Vector3 targ_x_l = (side0_l - side1_l).normalized;
                Vector3 targ_y_l = Vector3.Cross(targ_x_l, targ_z_l).normalized;
                targ_x_l = Vector3.Cross(targ_z_l, targ_y_l);

                character.joint_anim_xform[Character.JOINT_HEEL_L] = mix_transforms(
                    character.joint_anim_xform[Character.JOINT_HEEL_L],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_L].inverse * new Matrix4x4(
                    new Vector4(targ_x_l.x, targ_x_l.y, targ_x_l.z, 0),
                    new Vector4(-targ_y_l.x, -targ_y_l.y, -targ_y_l.z, 0),
                    new Vector4(targ_z_l.x, targ_z_l.y, targ_z_l.z, 0),
                    new Vector4(heel_l.x, heel_l.y, heel_l.z, 1)), ik_toe_pos_blend.y);

                Vector3 heel_r = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 2]);
                Vector4 side_h0_r = character.joint_global_anim_xform[Character.JOINT_HEEL_R] * new Vector4(10, 0, 0, 1);
                Vector4 side_h1_r = character.joint_global_anim_xform[Character.JOINT_HEEL_R] * new Vector4(-10, 0, 0, 1);
                Vector3 side0_r = new Vector3(side_h0_r.x, side_h0_r.y, side_h0_r.z) / side_h0_r.w;
                Vector3 side1_r = new Vector3(side_h1_r.x, side_h1_r.y, side_h1_r.z) / side_h1_r.w;
                Vector3 floor_r = key_tr;

                side0_r.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, heel_r.z - heel_max_bend_s, heel_r.y + heel_max_bend_s);
                side1_r.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, heel_r.z - heel_max_bend_s, heel_r.y + heel_max_bend_s);
                floor_r.y = Mathf.Clamp(floor_r.y, heel_r.y - heel_max_bend_d, heel_r.y + heel_max_bend_u);

                Vector3 targ_z_r = (floor_r - heel_r).normalized;
                Vector3 targ_x_r = (side0_r - side1_r).normalized;
                Vector3 targ_y_r = Vector3.Cross(targ_z_r, targ_x_r).normalized;
                targ_x_r = Vector3.Cross(targ_z_r, targ_y_r);

                character.joint_anim_xform[Character.JOINT_HEEL_R] = mix_transforms(
                    character.joint_anim_xform[Character.JOINT_HEEL_R],
                    character.joint_global_anim_xform[Character.JOINT_KNEE_R].inverse * new Matrix4x4(
                    new Vector4(-targ_x_r.x, -targ_x_r.y, -targ_x_r.z, 0),
                    new Vector4(targ_y_r.x, targ_y_r.y, targ_y_r.z, 0),
                    new Vector4(targ_z_r.x, targ_z_r.y, targ_z_r.z, 0),
                    new Vector4(heel_r.x, heel_r.y, heel_r.z, 1)), ik_toe_pos_blend.w);

                character.Forward_Kinematics();
            }

            // Rotate toe

            {

                const float toe_max_bend_d = 0.0f;
                const float toe_max_bend_u = 10.0f;

                // Clamp magnitude
                Vector4 ik_toe_rot_blend = ik_weight * 2.5f;
                if (ik_toe_rot_blend.magnitude > 1) {
                    ik_toe_rot_blend = ik_toe_rot_blend.normalized;
                }
                else if (ik_toe_rot_blend.magnitude < 0) {
                    ik_toe_rot_blend = ik_toe_rot_blend.normalized * 0.0f;
                }

                Vector3 toe_l = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 2]);
                Vector4 fwrd_h_l  = character.joint_global_anim_xform[Character.JOINT_TOE_L] * new Vector4(0, 0, 10, 1);
                Vector4 side_h0_l = character.joint_global_anim_xform[Character.JOINT_TOE_L] * new Vector4( 10, 0, 0, 1);
                Vector4 side_h1_l = character.joint_global_anim_xform[Character.JOINT_TOE_L] * new Vector4(-10, 0, 0, 1);
                Vector3 fwrd_l = new Vector3(fwrd_h_l.x, fwrd_h_l.y, fwrd_h_l.z) / fwrd_h_l.w;
                Vector3 side0_l = new Vector3(side_h0_l.x, side_h0_l.y, side_h0_l.z) / side_h0_l.w;
                Vector3 side1_l = new Vector3(side_h1_l.x, side_h1_l.y, side_h1_l.z) / side_h1_l.w;

                fwrd_l.y  = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_l.y - toe_max_bend_d, toe_l.y + toe_max_bend_u);
                side0_l.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_l.y - toe_max_bend_d, toe_l.y + toe_max_bend_u);
                side1_l.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_l.y - toe_max_bend_d, toe_l.y + toe_max_bend_u);

                Vector3 side_l = (side0_l - side1_l).normalized;
                fwrd_l = (fwrd_l - toe_l).normalized;
                Vector3 upwr_l = Vector3.Cross(side_l, fwrd_l).normalized;
                side_l = Vector3.Cross(fwrd_l, upwr_l);

                character.joint_anim_xform[Character.JOINT_TOE_L] = mix_transforms(
                    character.joint_anim_xform[Character.JOINT_TOE_L],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L].inverse * new Matrix4x4(
                    new Vector4(side_l.x, side_l.y, side_l.z, 0),
                    new Vector4(-upwr_l.x, -upwr_l.y, -upwr_l.z, 0),
                    new Vector4(fwrd_l.x, fwrd_l.y, fwrd_l.z, 0),
                    new Vector4(toe_l.x, toe_l.y, toe_l.z, 1)), ik_toe_rot_blend.y);

                Vector3 toe_r = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 2]);
                Vector4 fwrd_h_r = character.joint_global_anim_xform[Character.JOINT_TOE_R] * new Vector4(0, 0, 10, 1);
                Vector4 side_h0_r = character.joint_global_anim_xform[Character.JOINT_TOE_R] * new Vector4(10, 0, 0, 1);
                Vector4 side_h1_r = character.joint_global_anim_xform[Character.JOINT_TOE_R] * new Vector4(-10, 0, 0, 1);
                Vector3 fwrd_r = new Vector3(fwrd_h_r.x, fwrd_h_r.y, fwrd_h_r.z) / fwrd_h_r.w;
                Vector3 side0_r = new Vector3(side_h0_r.x, side_h0_r.y, side_h0_r.z) / side_h0_r.w;
                Vector3 side1_r = new Vector3(side_h1_r.x, side_h1_r.y, side_h1_r.z) / side_h1_r.w;

                fwrd_r.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_r.y - toe_max_bend_d, toe_r.y + toe_max_bend_u);
                side0_r.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_r.y - toe_max_bend_d, toe_r.y + toe_max_bend_u);
                side1_r.y = Mathf.Clamp(HEIGHTMAP_PLACEHOLDER + ik.toe_height, toe_r.y - toe_max_bend_d, toe_r.y + toe_max_bend_u);

                Vector3 side_r = (side0_r - side1_r).normalized;
                fwrd_r = (fwrd_r - toe_r).normalized;
                Vector3 upwr_r = Vector3.Cross(side_r, fwrd_r).normalized;
                side_r = Vector3.Cross(fwrd_r, upwr_r);

                character.joint_anim_xform[Character.JOINT_TOE_R] = mix_transforms(
                    character.joint_anim_xform[Character.JOINT_TOE_R],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R].inverse * new Matrix4x4(
                    new Vector4(side_r.x, side_r.y, side_r.z, 0),
                    new Vector4(-upwr_r.x, -upwr_r.y, -upwr_r.z, 0),
                    new Vector4(fwrd_r.x, fwrd_r.y, fwrd_r.z, 0),
                    new Vector4(toe_r.x, toe_r.y, toe_r.z, 1)), ik_toe_rot_blend.w);

                character.Forward_Kinematics();
            }

            // Update locks

            if ((ik.locks[IK.HL] == 0) && (ik_weight.y >= ik.threshold)) {
                ik.locks[IK.HL] = 1.0f; ik.position[IK.HL] = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_L][3, 2]);
                ik.locks[IK.TL] = 1.0f; ik.position[IK.TL] = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_TOE_L][3, 2]);
            }

            if ((ik.locks[IK.HR] == 0.0f) && (ik_weight.w >= ik.threshold)) {
                ik.locks[IK.HR] = 1.0f; ik.position[IK.HR] = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_HEEL_R][3, 2]);
                ik.locks[IK.TR] = 1.0f; ik.position[IK.TR] = new Vector3(
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 0],
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 1],
                    character.joint_global_anim_xform[Character.JOINT_TOE_R][3, 2]);
            }

            if ((ik.locks[IK.HL] > 0.0f) && (ik_weight.y < ik.threshold)) {
                ik.locks[IK.HL] = Mathf.Clamp01(ik.locks[IK.HL] - ik.fade);
                ik.locks[IK.TL] = Mathf.Clamp01(ik.locks[IK.TL] - ik.fade);
            }

            if ((ik.locks[IK.HR] > 0.0f) && (ik_weight.w < ik.threshold)) {
                ik.locks[IK.HR] = Mathf.Clamp01(ik.locks[IK.HR] - ik.fade);
                ik.locks[IK.TR] = Mathf.Clamp01(ik.locks[IK.TR] - ik.fade);
            }

        }
    }

    void Post_Render() {

        // Update past trajectory
        for (int i = 0; i < Trajectory.LENGTH / 2; i++) {
            trajectory.positions[i] = trajectory.positions[i + 1];
            trajectory.directions[i] = trajectory.directions[i + 1];
            trajectory.rotations[i] = trajectory.rotations[i + 1];
            trajectory.heights[i] = trajectory.heights[i + 1];
            trajectory.gait_stand[i] = trajectory.gait_stand[i + 1];
            trajectory.gait_walk[i] = trajectory.gait_walk[i + 1];
            trajectory.gait_jog[i] = trajectory.gait_jog[i + 1];
            trajectory.gait_crouch[i] = trajectory.gait_crouch[i + 1];
            trajectory.gait_jump[i] = trajectory.gait_jump[i + 1];
            trajectory.gait_bump[i] = trajectory.gait_bump[i + 1];
        }

        // Update current trajectory
        float stand_amount = Mathf.Pow(1.0f - trajectory.gait_stand[Trajectory.LENGTH / 2], 0.25f);
        //print("Stand_amount: " + stand_amount + " -pfnn.Yp[2]: " + -pfnn.Yp[2]);

        Vector3 trajectory_update = trajectory.rotations[Trajectory.LENGTH / 2] * new Vector3(pfnn.Yp[0], 0, pfnn.Yp[1]);
        //print("Traj update: " + );
        trajectory.positions[Trajectory.LENGTH / 2] += stand_amount * trajectory_update;
        //print("Post_render. bfor traj.dir[curr]: " + trajectory.directions[Trajectory.LENGTH / 2].x + ", " + trajectory.directions[Trajectory.LENGTH / 2].y + ", " + trajectory.directions[Trajectory.LENGTH / 2].z);
        trajectory.directions[Trajectory.LENGTH / 2] = new Matrix3x3(stand_amount * -pfnn.Yp[2], Vector3.up) * trajectory.directions[Trajectory.LENGTH / 2];
        //print("Post_render. aftr traj.dir[curr]: " + trajectory.directions[Trajectory.LENGTH / 2].x + ", " + trajectory.directions[Trajectory.LENGTH / 2].y + ", " + trajectory.directions[Trajectory.LENGTH / 2].z);
        //print("Post_render. bfor traj.rot[curr]: " + trajectory.rotations[Trajectory.LENGTH / 2]);
        trajectory.rotations[Trajectory.LENGTH / 2] = new Matrix3x3(Mathf.Atan2(
                trajectory.directions[Trajectory.LENGTH / 2].x,
                trajectory.directions[Trajectory.LENGTH / 2].z), Vector3.up
        );
        //print("Post_render. aftr traj.rot[curr]: " + trajectory.rotations[Trajectory.LENGTH / 2]);

        //print("pfnn.Xp[0]: " + pfnn.Xp[0]);

        // Wall collisions
        //for (int j = 0; j < WALL_COUNT; j++) {
        //}

        // Update future trajectory
        for (int i = Trajectory.LENGTH / 2 + 1; i < Trajectory.LENGTH; i++) {
            int w = Trajectory.LENGTH / 2 / 10;
            float m = (((float)i - (Trajectory.LENGTH / 2)) / 10) % 1.0f;
            trajectory.positions[i].x  = (1 - m) * pfnn.Yp[8 + w * 0 + i / 10 - w] + m * pfnn.Yp[8 + w * 0 + i / 10 - w + 1];
            trajectory.positions[i].z  = (1 - m) * pfnn.Yp[8 + w * 1 + i / 10 - w] + m * pfnn.Yp[8 + w * 1 + i / 10 - w + 1];
            trajectory.directions[i].x = (1 - m) * pfnn.Yp[8 + w * 2 + i / 10 - w] + m * pfnn.Yp[8 + w * 2 + i / 10 - w + 1];
            trajectory.directions[i].z = (1 - m) * pfnn.Yp[8 + w * 3 + i / 10 - w] + m * pfnn.Yp[8 + w * 3 + i / 10 - w + 1];
            trajectory.positions[i] = trajectory.rotations[Trajectory.LENGTH / 2] * trajectory.positions[i] + trajectory.positions[Trajectory.LENGTH / 2];
            trajectory.directions[i] = (trajectory.rotations[Trajectory.LENGTH / 2] * trajectory.directions[i]).normalized;
            trajectory.rotations[i] = new Matrix3x3(Mathf.Atan2(
                trajectory.directions[i].x,
                trajectory.directions[i].z), Vector3.up
            );
        }

        // Update phase
        character.phase = (character.phase + (stand_amount * 0.9f + 0.1f) * 2 * Mathf.PI * pfnn.Yp[3]) % (2 * Mathf.PI);

        //print(cycleCount + " - pfnn.Yp[0]: " + pfnn.Yp[0] + " vs pfnn.Yp[1]: " + pfnn.Yp[1]);

        // Update camera - only used for rendering shadows???
        //camera.target = mix(camera.target, new Vector3(
        //    trajectory.positions[Trajectory.LENGTH / 2].x,
        //    trajectory.heights[Trajectory.LENGTH / 2] + 100,
        //    trajectory.positions[Trajectory.LENGTH / 2].z), 0.1f);
    }

    static Vector3 mix_directions(Vector3 x, Vector3 y, float a) {
        Quaternion x_q = Quaternion.AngleAxis(Mathf.Atan2(x.x, x.z) * Mathf.Rad2Deg, Vector3.up);
        Quaternion y_q = Quaternion.AngleAxis(Mathf.Atan2(y.x, y.z) * Mathf.Rad2Deg, Vector3.up);
        Quaternion z_q = Quaternion.SlerpUnclamped(x_q, y_q, a);
        return z_q * new Vector3(0, 0, 1);
    }

    // https://math.stackexchange.com/questions/1030737/exponential-function-of-quaternion-derivation
    // This forum shows the exponential function of a quaternion.

    static Quaternion quat_exp(Vector3 l) {
        float w = l.magnitude;
        Quaternion q = (w < 0.01) ? new Quaternion(0, 0, 0, 1) : new Quaternion(
            l.x * (Mathf.Sin(w) / w),
            l.y * (Mathf.Sin(w) / w),
            l.z * (Mathf.Sin(w) / w),
            Mathf.Cos(w));
        var len = Mathf.Sqrt(q.w * q.w + q.x * q.x + q.y * q.y + q.z * q.z);
        return new Quaternion(q.x / len, q.y / len, q.z / len, q.w / len);
    }

    static Matrix4x4 mix_transforms(Matrix4x4 x, Matrix4x4 y, float a) {
        var pos = Vector3.SlerpUnclamped(x.ExtractPosition(), y.ExtractPosition(), a);
        var rot = SlerpUnclampedPreserveMagnitude(x.ExtractRotation(), y.ExtractRotation(), a);
        var scl = Vector3.SlerpUnclamped(x.ExtractScale(), y.ExtractScale(), a);

        return Matrix4x4.TRS(pos, rot, scl);
    }

    static Quaternion SlerpUnclampedPreserveMagnitude(Quaternion a, Quaternion b, float t) {
        var result = Quaternion.SlerpUnclamped(a, b, t);
        var len = ((a.Magnitude() + b.Magnitude()) / 2.0f);
        result.w *= len;
        result.x *= len;
        result.y *= len;
        result.z *= len;
        return result;
    }

    void reset() {

        //ArrayXf Yp = pfnn->Ymean;

        //glm::vec3 root_position = glm::vec3(position.x, heightmap->sample(position), position.y);
        //glm::mat3 root_rotation = glm::mat3();

        //for (int i = 0; i < Trajectory::LENGTH; i++) {
        //    trajectory->positions[i] = root_position;
        //    trajectory->rotations[i] = root_rotation;
        //    trajectory->directions[i] = glm::vec3(0, 0, 1);
        //    trajectory->heights[i] = root_position.y;
        //    trajectory->gait_stand[i] = 0.0;
        //    trajectory->gait_walk[i] = 0.0;
        //    trajectory->gait_jog[i] = 0.0;
        //    trajectory->gait_crouch[i] = 0.0;
        //    trajectory->gait_jump[i] = 0.0;
        //    trajectory->gait_bump[i] = 0.0;
        //}

        //for (int i = 0; i < Character::JOINT_NUM; i++) {

        //    int opos = 8 + (((Trajectory::LENGTH / 2) / 10) * 4) + (Character::JOINT_NUM * 3 * 0);
        //    int ovel = 8 + (((Trajectory::LENGTH / 2) / 10) * 4) + (Character::JOINT_NUM * 3 * 1);
        //    int orot = 8 + (((Trajectory::LENGTH / 2) / 10) * 4) + (Character::JOINT_NUM * 3 * 2);

        //    glm::vec3 pos = (root_rotation * glm::vec3(Yp(opos + i * 3 + 0), Yp(opos + i * 3 + 1), Yp(opos + i * 3 + 2))) + root_position;
        //    glm::vec3 vel = (root_rotation * glm::vec3(Yp(ovel + i * 3 + 0), Yp(ovel + i * 3 + 1), Yp(ovel + i * 3 + 2)));
        //    glm::mat3 rot = (root_rotation * glm::toMat3(quat_exp(glm::vec3(Yp(orot + i * 3 + 0), Yp(orot + i * 3 + 1), Yp(orot + i * 3 + 2)))));

        //    character->joint_positions[i] = pos;
        //    character->joint_velocities[i] = vel;
        //    character->joint_rotations[i] = rot;
        //}

        //character->phase = 0.0;

        //ik->position[IK::HL] = glm::vec3(0, 0, 0); ik->lock[IK::HL] = 0; ik->height[IK::HL] = root_position.y;
        //ik->position[IK::HR] = glm::vec3(0, 0, 0); ik->lock[IK::HR] = 0; ik->height[IK::HR] = root_position.y;
        //ik->position[IK::TL] = glm::vec3(0, 0, 0); ik->lock[IK::TL] = 0; ik->height[IK::TL] = root_position.y;
        //ik->position[IK::TR] = glm::vec3(0, 0, 0); ik->lock[IK::TR] = 0; ik->height[IK::TR] = root_position.y;

    }

    int count_updates = 0;
    void UpdateJoints() {
        //character.joint_mesh_xform[0].ExtractPosition()

        //print(count + " Root: " + character.joint_mesh_xform[0].ExtractPosition() + " Hip: " + character.joint_mesh_xform[2].ExtractPosition() + " Knee: " + character.joint_mesh_xform[3].ExtractPosition());

        //joints[2].transform.position = character.joint_positions[2] / 100;
        //joints[3].transform.position = character.joint_positions[3] / 100;
        //joints[4].transform.position = character.joint_positions[4] / 100;
        //joints[5].transform.position = character.joint_positions[5] / 100;

        for (var j = 0; j < Character.JOINT_NUM; j++) {
            if (joints[j] != null) {

                Vector3 pos = character.joint_positions[j];
                float xScalePos = 0; try { xScalePos = float.Parse(position_scale_x.text);} catch (FormatException) {}
                float yScalePos = 0; try { yScalePos = float.Parse(position_scale_y.text);} catch (FormatException) {}
                float zScalePos = 0; try { zScalePos = float.Parse(position_scale_z.text);} catch (FormatException) {}
                pos.x = character.joint_positions[j][position_axisMap_x.value] * xScalePos;
                pos.y = character.joint_positions[j][position_axisMap_y.value] * yScalePos;
                pos.z = character.joint_positions[j][position_axisMap_z.value] * zScalePos;
                joints[j].transform.position = pos;
                
                //print((pos.z * -1 * 1/65) + " vs: " + pos[position_axisMap_x.value] * float.Parse(position_scale_x.text));

                // pos.x = pos.z;
                // pos.z = pos.x;
                // pos.x *= -1;
                // pos.z *= -1;
                // pos.x /= 65;
                // pos.y /= 65;
                // pos.z /= 65;
                // joints[j].transform.position = pos;

                //joints[j].transform.position = character.joint_positions[j];

                //if (j == 3) {
                //    print("J3 rot  : " + character.joint_rotations[j]);
                //    print("extract : " + character.joint_rotations[j].ExtractRotation());
                //    print("e angles: " + character.joint_rotations[j].ExtractRotation().eulerAngles);
                //}
                Vector3 rot = character.joint_rotations[j].ExtractRotation().eulerAngles;
                Vector3 buildRot = new Vector3();
                float xScaleRot = 0; try { xScaleRot = float.Parse(rotation_scale_x.text);} catch (FormatException) {}
                float yScaleRot = 0; try { yScaleRot = float.Parse(rotation_scale_y.text);} catch (FormatException) {}
                float zScaleRot = 0; try { zScaleRot = float.Parse(rotation_scale_z.text);} catch (FormatException) {}
                buildRot.x = (rotation_axisMap_x.value % 2 == 1 ? -1 : 1) * rot[rotation_axisMap_x.value/2] + xScaleRot;
                buildRot.y = (rotation_axisMap_y.value % 2 == 1 ? -1 : 1) * rot[rotation_axisMap_y.value/2] + yScaleRot;
                buildRot.z = (rotation_axisMap_z.value % 2 == 1 ? -1 : 1) * rot[rotation_axisMap_z.value/2] + zScaleRot;
                //if (j == 3) {
                //    print("ROT: " + rot);
                //}
                //rot.x = 180 - rot.x;
                //rot.y = 360 - rot.y - 90;
                //rot.z = rot.z;

                // rot.x += axisValues[0];
                // rot.y += axisValues[1];
                // rot.z += axisValues[2];

                //rot.x = 180 - 15;
                //rot.y = 360 - 333 - 90;
                //rot.z = 343;

                //rot.x = 180;
                //rot.y = 0;
                //rot.z = 0;

                //Vector3 rot = joints[j].transform.eulerAngles;
                //rot.x = 180 - character.joint_rotations[j].ExtractRotation().eulerAngles.x;
                //rot.y = 360 - character.joint_rotations[j].ExtractRotation().eulerAngles.y - 90;
                joints[j].transform.eulerAngles = buildRot;

                //joints[j].transform.rotation = character.joint_rotations[j].ExtractRotation(); // closer when x is += 180
            }
        }

        //Debug.Log("2: " + character.joint_positions[2]);
        //Debug.Log("3: " + character.joint_positions[3]);
        //Debug.Log("4: " + character.joint_positions[4]);
        //Debug.Log("5: " + character.joint_positions[5]);

    }
}
