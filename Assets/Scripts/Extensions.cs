using UnityEngine;
using System.Collections;

public static class Extensions
{

    public static Quaternion ExtractRotation(this Matrix4x4 matrix) {
        return Quaternion.LookRotation(matrix.GetColumn(2), matrix.GetColumn(1));
    }

    public static Matrix4x4 toMatrix4x4(this Quaternion rot) {
        var mmA = new Matrix4x4(
            new Vector4(rot.w, -rot.z, rot.y, -rot.x),
            new Vector4(rot.z, rot.w, -rot.x, -rot.y),
            new Vector4(-rot.y, rot.x, rot.w, -rot.z),
            new Vector4(rot.x, rot.y, rot.z, rot.w)
        );
        var mmB = new Matrix4x4(
            new Vector4(rot.w, -rot.z, rot.y, rot.x),
            new Vector4(rot.z, rot.w, -rot.x, rot.y),
            new Vector4(-rot.y, rot.x, rot.w, rot.z),
            new Vector4(-rot.x, -rot.y, -rot.z, rot.w)
        );
        return (mmA * mmB);
    }

    public static Matrix3x3 toMatrix3x3(this Quaternion rot) {
        return new Matrix3x3(
                new Vector3(1 - 2 * rot.y  *rot.y - 2 * rot.z * rot.z, 2 * rot.x * rot.y - 2 * rot.z * rot.w, 2 * rot.x * rot.z + 2 * rot.y * rot.w),
                new Vector3(2 * rot.x * rot.y + 2 * rot.z * rot.w, 1 - 2 * rot.x * rot.x - 2 * rot.z * rot.z, 2 * rot.y * rot.z  - 2 * rot.x * rot.w),
                new Vector3(2 * rot.x * rot.z - 2 * rot.y * rot.w, 2 * rot.y * rot.z + 2 * rot.x * rot.w, 1 - 2 * rot.x * rot.x - 2 * rot.y * rot.y)
            );
    }

    public static Vector3 ExtractPosition(this Matrix4x4 matrix) {
        return matrix.GetColumn(3);
    }

    public static Vector3 ExtractScale(this Matrix4x4 matrix) {
        return new Vector3(matrix.GetColumn(0).magnitude, matrix.GetColumn(1).magnitude, matrix.GetColumn(2).magnitude);
    }

    public static float Magnitude(this Quaternion quat) {
        return Mathf.Sqrt(quat.w * quat.w + quat.x * quat.x + quat.y * quat.y + quat.z * quat.z);
    }

}
