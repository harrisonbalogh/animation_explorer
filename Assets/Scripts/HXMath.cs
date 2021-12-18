using UnityEngine;
using System.Collections;

public class Matrix3x3 {

    public Vector3[] rows = new Vector3[3];

    public float this[int row, int column] {
        get { return rows[row][column]; }
        set { rows[row][column] = value; }
    }

    public Vector3 this[int row] {
        get { return rows[row]; }
        set { rows[row] = value; }
    }

    public Matrix3x3(Vector3 r0, Vector3 r1, Vector3 r2) {
        rows[0] = r0;
        rows[1] = r1;
        rows[2] = r2;
    }

    public Matrix3x3() : this(new Vector3(0,0,0), new Vector3(0,0,0), new Vector3(0,0,0)) { }

    public Matrix3x3(Matrix4x4 mat4) {
        rows[0] = new Vector3(mat4.m00, mat4.m01, mat4.m02);
        rows[1] = new Vector3(mat4.m10, mat4.m11, mat4.m12);
        rows[2] = new Vector3(mat4.m20, mat4.m21, mat4.m22);
    }

    /// <summary>
    /// Creates a 3x3 rotation matrix from the angle and axis. WARNING: The axis must be normalized!
    /// </summary>
    /// <param name="angle">Angle in degrees.</param>
    /// <param name="axis">The axis represented by a NORMALIZED Vector3..</param>
    public Matrix3x3(float angle, Vector3 axis) {
        // http://euclideanspace.com/maths/geometry/rotations/conversions/angleToMatrix/index.htm
        float c = Mathf.Cos(angle);
        float s = Mathf.Sin(angle);
        float t = 1.0f - c;

        float m00 = c + axis.x * axis.x * t;
        float m11 = c + axis.y * axis.y * t;
        float m22 = c + axis.z * axis.z * t;

        float tmp1 = axis.x * axis.y * t;
        float tmp2 = axis.z * s;

        float m10 = tmp1 + tmp2;
        float m01 = tmp1 - tmp2;
        tmp1 = axis.x * axis.z * t;
        tmp2 = axis.y * s;
        float m20 = tmp1 - tmp2;
        float m02 = tmp1 + tmp2;
        tmp1 = axis.y * axis.z * t;
        tmp2 = axis.x * s;
        float m21 = tmp1 + tmp2;
        float m12 = tmp1 - tmp2;

        rows[0] = new Vector3(m00, m01, m02);
        rows[1] = new Vector3(m10, m11, m12);
        rows[2] = new Vector3(m20, m21, m22);
    }

    public static Matrix3x3 operator* (Matrix3x3 a, Matrix3x3 b) {

        Vector3 r1 = new Vector3(
            Vector3.Dot(a[0], new Vector3(b[0, 0], b[1, 0], b[2, 0])),
            Vector3.Dot(a[0], new Vector3(b[0, 1], b[1, 1], b[2, 1])),
            Vector3.Dot(a[0], new Vector3(b[0, 2], b[1, 2], b[2, 2]))
        );

        Vector3 r2 = new Vector3(
            Vector3.Dot(a[1], new Vector3(b[0, 0], b[1, 0], b[2, 0])),
            Vector3.Dot(a[1], new Vector3(b[0, 1], b[1, 1], b[2, 1])),
            Vector3.Dot(a[1], new Vector3(b[0, 2], b[1, 2], b[2, 2]))
        );

        Vector3 r3 = new Vector3(
            Vector3.Dot(a[2], new Vector3(b[0, 0], b[1, 0], b[2, 0])),
            Vector3.Dot(a[2], new Vector3(b[0, 1], b[1, 1], b[2, 1])),
            Vector3.Dot(a[2], new Vector3(b[0, 2], b[1, 2], b[2, 2]))
        );

        return new Matrix3x3(r1, r2, r3);
    }


    public static Vector3 operator *(Matrix3x3 a, Vector3 b) {
        return new Vector3(
            a[0].x * b.x + a[0].y * b.y + a[0].z * b.z,
            a[1].x * b.x + a[1].y * b.y + a[1].z * b.z,
            a[2].x * b.x + a[2].y * b.y + a[2].z * b.z
        ); ;
    }

    public Matrix3x3 inverse {
        get {
            // Manual 3x3 inverse calc b/c lazy
            float det = 
                  this[0, 0] * (this[1, 1] * this[2, 2] - this[1, 2] * this[2, 1])
                - this[0, 1] * (this[1, 0] * this[2, 2] - this[1, 2] * this[2, 0])
                + this[0, 2] * (this[1, 0] * this[2, 1] - this[1, 1] * this[2, 0]);
            Vector3 r1 = new Vector3(
                +(this[1, 1] * this[2, 2] - this[1, 2] * this[2, 1]) / det,
                -(this[0, 1] * this[2, 2] - this[0, 2] * this[2, 1]) / det,
                +(this[0, 1] * this[1, 2] - this[0, 2] * this[1, 1]) / det
            );
            Vector3 r2 = new Vector3(
                -(this[1, 0] * this[2, 2] - this[1, 2] * this[2, 0]) / det,
                +(this[0, 0] * this[2, 2] - this[0, 2] * this[2, 0]) / det,
                -(this[0, 0] * this[1, 2] - this[0, 2] * this[1, 0]) / det
            );
            Vector3 r3 = new Vector3(
                +(this[1, 0] * this[2, 1] - this[1, 1] * this[2, 0]) / det,
                -(this[0, 0] * this[2, 1] - this[0, 1] * this[2, 0]) / det,
                +(this[0, 0] * this[1, 1] - this[0, 1] * this[1, 0]) / det
            );
            return new Matrix3x3(r1, r2, r3);
        }
    }

    public Matrix3x3 transpose {
        get {
            return new Matrix3x3(
                new Vector3(rows[0][0], rows[1][0], rows[2][0]),
                new Vector3(rows[0][1], rows[1][1], rows[2][1]),
                new Vector3(rows[0][2], rows[1][2], rows[2][2])
                );
        }
    }

    public Vector3 column(int index) {
        return new Vector3(rows[0][index], rows[1][index], rows[2][index]);
    }
    
    /// <summary>
    /// Retrives the quaternion from a 3x3 rotation matrix. This is only guaranteed to work if
    /// the rotation matrix was formed by calling toMatrix3x3() on a quaternion.
    /// </summary>
    /// <returns></returns>
    public Quaternion ExtractRotation() {
        float w = Mathf.Sqrt(1 + rows[0][0] + rows[1][1] + rows[2][2]) / 2;
        return new Quaternion(
                (rows[2][1] - rows[1][2]) / (4 * w),
                (rows[0][2] - rows[2][0]) / (4 * w),
                (rows[1][0] - rows[0][1]) / (4 * w),
                w
            );
    }

    //public static Matrix3x3 toMatrix3x3(this Quaternion rot) {
    //    return new Matrix3x3(
    //            new Vector3(1 - 2 * rot.y * rot.y - 2 * rot.z * rot.z, 2 * rot.x * rot.y - 2 * rot.z * rot.w, 2 * rot.x * rot.z + 2 * rot.y * rot.w),
    //            new Vector3(2 * rot.x * rot.y + 2 * rot.z * rot.w, 1 - 2 * rot.x * rot.x - 2 * rot.z * rot.z, 2 * rot.y * rot.z - 2 * rot.x * rot.w),
    //            new Vector3(2 * rot.x * rot.z - 2 * rot.y * rot.w, 2 * rot.y * rot.z + 2 * rot.x * rot.w, 1 - 2 * rot.x * rot.x - 2 * rot.y * rot.y)
    //        );
    //}

    public override string ToString() {
        string stringBuilder = "";
        foreach (Vector3 row in rows) {
            for (int c = 0; c < 3; c++) {
                stringBuilder += row[c] + (c != 2 ? "/" : "");
            }
            if (row != rows[rows.Length-1])
                stringBuilder += "||";
        }
        return stringBuilder;
    }


}