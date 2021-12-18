using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.IO;
// https://answers.unity.com/questions/462042/unity-and-mathnet.html
using MathNet.Numerics.LinearAlgebra;

/**
 * Keeps track of neural network weights.
 */
public class PFNN {

    public const int
        XDIM = 342, YDIM = 311, HDIM = 512,
        MODE_CONSTANT = 50, MODE_LINEAR = 10, MODE_CUBIC = 4; // changed from 0, 1, 2

    private int mode;

    float[] Xmean, Xstd;
    float[] Ymean, Ystd;

    float[][,] W0, W1, W2;
    float[][] b0, b1, b2;

    public float[] Xp, Yp;
    float[] H0, H1;
    float[,] W0p, W1p, W2p;
    float[] b0p, b1p, b2p;

    // ctor
    public PFNN(int pfnnmode) {

        this.mode = pfnnmode;

        Xp = new float[XDIM];
        Yp = new float[YDIM];

        H0 = new float[HDIM];
        H1 = new float[HDIM];

        W0p = new float[HDIM, XDIM];
        W1p = new float[HDIM, HDIM];
        W2p = new float[YDIM, HDIM];

        b0p = new float[HDIM];
        b1p = new float[HDIM];
        b2p = new float[YDIM];

    }

    // ============== functions ==============

    // Unity resource folder: assets/resources/file
    // use: Resources.Load<TextAsset>("file");
    public static void load_weights(ref float[,] A, int rows, int cols, string file) {

        // https://answers.unity.com/questions/8187/how-can-i-read-binary-files-from-resources.html
        // The bin files may need .txt extension, or Resources.Load may need to be cast 'as TextAsset'
        TextAsset txtAsset = Resources.Load("dynAnim/" + file) as TextAsset;
        Stream s = new MemoryStream(txtAsset.bytes);
        BinaryReader br = new BinaryReader(s);

        A = new float[rows, cols];

        for (int x = 0; x < rows; x++)
            for (int y = 0; y < cols; y++) {
                float item = 0f;
                item = br.ReadSingle();
                A[x, y] = item;
            }

    }
    public static void load_weights(ref float[] V, int items, string file) {

        // https://answers.unity.com/questions/8187/how-can-i-read-binary-files-from-resources.html
        // The bin files may need .txt extension, or Resources.Load may need to be cast 'as TextAsset'
        TextAsset txtAsset = Resources.Load("dynAnim/" + file) as TextAsset;
        Stream s = new MemoryStream(txtAsset.bytes);
        BinaryReader br = new BinaryReader(s);

        V = new float[items];

        for (int i = 0; i < items; i++) {
            float item = 0f;
            item = br.ReadSingle();
            V[i] = item;
        }

    }

    public void Load() {

        load_weights(ref Xmean, XDIM, "network/pfnn/Xmean");
        load_weights(ref Xstd, XDIM, "network/pfnn/Xstd");
        load_weights(ref Ymean, YDIM, "network/pfnn/Ymean");
        load_weights(ref Ystd, YDIM, "network/pfnn/Ystd");

        switch (mode) {

            case MODE_CONSTANT:

                W0 = new float[MODE_CONSTANT][,];
                W1 = new float[MODE_CONSTANT][,];
                W2 = new float[MODE_CONSTANT][,];

                b0 = new float[MODE_CONSTANT][];
                b1 = new float[MODE_CONSTANT][];
                b2 = new float[MODE_CONSTANT][];

                for (int i = 0; i < MODE_CONSTANT; i++) {
                    load_weights(ref W0[i], HDIM, XDIM, "network/pfnn/W0_" + i.ToString("D3"));
                    load_weights(ref W1[i], HDIM, HDIM, "network/pfnn/W1_" + i.ToString("D3"));
                    load_weights(ref W2[i], YDIM, HDIM, "network/pfnn/W2_" + i.ToString("D3"));
                    load_weights(ref b0[i], HDIM, "network/pfnn/b0_" + i.ToString("D3"));
                    load_weights(ref b1[i], HDIM, "network/pfnn/b1_" + i.ToString("D3"));
                    load_weights(ref b2[i], YDIM, "network/pfnn/b2_" + i.ToString("D3"));
                }

                //for (int i = 0; i < 10; i++)
                //    MonoBehaviour.print("W0[0,"+i+"]: "+W0[0][0,i]);

                break;

            case MODE_LINEAR:

                W0 = new float[MODE_LINEAR][,];
                W1 = new float[MODE_LINEAR][,];
                W2 = new float[MODE_LINEAR][,];

                b0 = new float[MODE_LINEAR][];
                b1 = new float[MODE_LINEAR][];
                b2 = new float[MODE_LINEAR][];

                for (int i = 0; i < MODE_LINEAR; i++) {
                    load_weights(ref W0[i], HDIM, XDIM, "network/pfnn/W0_" + (i * 5).ToString("D3"));
                    load_weights(ref W1[i], HDIM, HDIM, "network/pfnn/W1_" + (i * 5).ToString("D3"));
                    load_weights(ref W2[i], YDIM, HDIM, "network/pfnn/W2_" + (i * 5).ToString("D3"));
                    load_weights(ref b0[i], HDIM, "network/pfnn/b0_" + (i * 5).ToString("D3"));
                    load_weights(ref b1[i], HDIM, "network/pfnn/b1_" + (i * 5).ToString("D3"));
                    load_weights(ref b2[i], YDIM, "network/pfnn/b2_" + (i * 5).ToString("D3"));
                }

                break;

            case MODE_CUBIC:

                W0 = new float[MODE_CUBIC][,];
                W1 = new float[MODE_CUBIC][,];
                W2 = new float[MODE_CUBIC][,];

                b0 = new float[MODE_CUBIC][];
                b1 = new float[MODE_CUBIC][];
                b2 = new float[MODE_CUBIC][];

                for (int i = 0; i < MODE_CUBIC; i++) {
                    load_weights(ref W0[i], HDIM, XDIM, "network/pfnn/W0_" + ((int)(i * 12.5)).ToString("D3"));
                    load_weights(ref W1[i], HDIM, HDIM, "network/pfnn/W1_" + ((int)(i * 12.5)).ToString("D3"));
                    load_weights(ref W2[i], YDIM, HDIM, "network/pfnn/W2_" + ((int)(i * 12.5)).ToString("D3"));
                    load_weights(ref b0[i], HDIM, "network/pfnn/b0_" + ((int)(i * 12.5)).ToString("D3"));
                    load_weights(ref b1[i], HDIM, "network/pfnn/b1_" + ((int)(i * 12.5)).ToString("D3"));
                    load_weights(ref b2[i], YDIM, "network/pfnn/b2_" + ((int)(i * 12.5)).ToString("D3"));
                }

                break;

            default:
                break;

        }

    }

    public static void ELU(ref float[] x) {
        for (int i = 0; i < x.Length; i++) {
            //if (i == 229) {
            //    MonoBehaviour.print("ELU. bfor 229: " + x[i]);
            //}
            x[i] = Mathf.Max(x[i], 0) + Mathf.Exp(Mathf.Min(x[i], 0)) - 1;
            //if (i == 229) {
            //    MonoBehaviour.print("ELU. aftr 229: " + x[i]);
            //}
        }
    }

    // Potential strip of y0, y1 parameters
    public static void Linear(ref float[] o, ref float[] y0, ref float[] y1, float mu) {
        for (int i = 0; i < o.Length; i++) {
            o[i] = (1.0f - mu) * y0[i] + mu * y1[i];
        }
    }

    public static void Linear(ref float[,] o, ref float[,] y0, ref float[,] y1, float mu) {
        for (int iR = 0; iR < o.GetLength(0); iR++) {
            for (int iC = 0; iC < o.GetLength(1); iC++) {
                o[iR, iC] = (1.0f - mu) * y0[iR, iC] + mu * y1[iR, iC];
            }
        }
    }

    public static void Cubic(ref float[] o, ref float[] y0, ref float[] y1, ref float[] y2, ref float[] y3, float mu) {
        for (int i = 0; i < o.Length; i++) {
            o[i] = (
                (-0.5f * y0[i] + 1.5f * y1[i] - 1.5f * y2[i] + 0.5f * y3[i]) * mu * mu * mu +
                (y0[i] - 2.5f * y1[i] + 2.0f * y2[i] - 0.5f * y3[i]) * mu * mu +
                (-0.5f * y0[i] + 0.5f * y2[i]) * mu +
                (y1[i]));
        }
    }

    public static void Cubic(ref float[,] o, ref float[,] y0, ref float[,] y1, ref float[,] y2, ref float[,] y3, float mu) {
        for (int iR = 0; iR < o.GetLength(0); iR++) {
            for (int iC = 0; iC < o.GetLength(1); iC++) {
                o[iR, iC] = (
                    (-0.5f * y0[iR, iC] + 1.5f * y1[iR, iC] - 1.5f * y2[iR, iC] + 0.5f * y3[iR, iC]) * mu * mu * mu +
                    (y0[iR, iC] - 2.5f * y1[iR, iC] + 2.0f * y2[iR, iC] - 0.5f * y3[iR, iC]) * mu * mu +
                    (-0.5f * y0[iR, iC] + 0.5f * y2[iR, iC]) * mu +
                    (y1[iR, iC]));
            }
        }
    }

    public void predict(float P) {

        var M = Matrix<float>.Build;

        float pamount;
        int pindex_0, pindex_1, pindex_2, pindex_3;

        for (int i = 0; i < Xp.Length; i++) {
            Xp[i] = (Xp[i] - Xmean[i]) / Xstd[i];
        }

        switch (mode) {
            case MODE_CONSTANT:

                pindex_1 = (int)((P / (2 * Mathf.PI)) * 50);

                var mM0 = (M.DenseOfArray(W0[pindex_1]) * Vector<float>.Build.DenseOfArray(Xp)).AsArray();
                for (int i = 0; i < H0.Length; i++) {
                    if (i == 229) {
                        //MonoBehaviour.print("mM0[" + i + "]: " + mM0[i] + " b0["+pindex_1+"]["+i+"]: " + b0[pindex_1][i]);
                    }
                    H0[i] = mM0[i] + b0[pindex_1][i];
                }
                ELU(ref H0); // BAD MATCH
                var mM1 = (M.DenseOfArray(W1[pindex_1]) * Vector<float>.Build.DenseOfArray(H0)).AsArray();
                for (int i = 0; i < H1.Length; i++) {
                    if (i == 229) {
                        //MonoBehaviour.print("mM1[" + i + "]: " + mM1[i] + " b1[" + pindex_1 + "][" + i + "]: " + b1[pindex_1][i]);
                    }
                    H1[i] = mM1[i] + b1[pindex_1][i];
                }
                ELU(ref H1);
                var mM2 = (M.DenseOfArray(W2[pindex_1]) * Vector<float>.Build.DenseOfArray(H1)).AsArray();
                for (int i = 0; i < Yp.Length; i++) {
                    if (i == 229) {
                        //MonoBehaviour.print("PFNN. bfor Yp "+i+": " + Yp[i] + " mM2["+i+"]: " + mM2[i] + " b2["+pindex_1+"]["+i+"]: " + b2[pindex_1][i]);
                    }
                    Yp[i] = mM2[i] + b2[pindex_1][i];
                    if (i == 229) {
                        //MonoBehaviour.print("PFNN. aftr Yp 229: " + Yp[i]);
                    }
                }
                break;

            case MODE_LINEAR:
                pamount = ((P / (2 * Mathf.PI)) * 10) % 1.0f;
                pindex_1 = (int)((P / (2 * Mathf.PI)) * 10);
                pindex_2 = ((pindex_1 + 1) % 10);
                Linear(ref W0p, ref W0[pindex_1], ref W0[pindex_2], pamount);
                Linear(ref W1p, ref W1[pindex_1], ref W1[pindex_2], pamount);
                Linear(ref W2p, ref W2[pindex_1], ref W2[pindex_2], pamount);
                Linear(ref b0p, ref b0[pindex_1], ref b0[pindex_2], pamount);
                Linear(ref b1p, ref b1[pindex_1], ref b1[pindex_2], pamount);
                Linear(ref b2p, ref b2[pindex_1], ref b2[pindex_2], pamount);
                var mM0a = (M.DenseOfArray(W0p) * Vector<float>.Build.DenseOfArray(Xp)).AsArray();
                for (int i = 0; i < H0.Length; i++) {
                    H0[i] = mM0a[i] + b0p[i];
                }
                ELU(ref H0);
                var mM1a = (M.DenseOfArray(W1p) * Vector<float>.Build.DenseOfArray(H0)).AsArray();
                for (int i = 0; i < H1.Length; i++) {
                    H1[i] = mM1a[i] + b1p[i];
                }
                ELU(ref H1);
                var mM2a = (M.DenseOfArray(W2p) * Vector<float>.Build.DenseOfArray(H1)).AsArray();
                for (int i = 0; i < Yp.Length; i++) {
                    Yp[i] = mM2a[i] + b2p[i];
                }
                break;

            case MODE_CUBIC:
                pamount = ((P / (2 * Mathf.PI)) * 4) % 1.0f;
                pindex_1 = (int)((P / (2 * Mathf.PI)) * 4);
                pindex_0 = ((pindex_1+3) % 4);
                pindex_2 = ((pindex_1+1) % 4);
                pindex_3 = ((pindex_1 + 2) % 4);
                Cubic(ref W0p, ref W0[pindex_0], ref W0[pindex_1], ref W0[pindex_2], ref W0[pindex_3], pamount);
                Cubic(ref W1p, ref W1[pindex_0], ref W1[pindex_1], ref W1[pindex_2], ref W1[pindex_3], pamount);
                Cubic(ref W2p, ref W2[pindex_0], ref W2[pindex_1], ref W2[pindex_2], ref W2[pindex_3], pamount);
                Cubic(ref b0p, ref b0[pindex_0], ref b0[pindex_1], ref b0[pindex_2], ref b0[pindex_3], pamount);
                Cubic(ref b1p, ref b1[pindex_0], ref b1[pindex_1], ref b1[pindex_2], ref b1[pindex_3], pamount);
                Cubic(ref b2p, ref b2[pindex_0], ref b2[pindex_1], ref b2[pindex_2], ref b2[pindex_3], pamount);
                var mM0b = (M.DenseOfArray(W0p) * Vector<float>.Build.DenseOfArray(Xp)).AsArray();
                for (int i = 0; i < H0.Length; i++) {
                    H0[i] = mM0b[i] + b0p[i];
                }
                ELU(ref H0);
                var mM1b = (M.DenseOfArray(W1p) * Vector<float>.Build.DenseOfArray(H0)).AsArray();
                for (int i = 0; i < H1.Length; i++) {
                    H1[i] = mM1b[i] + b1p[i];
                }
                ELU(ref H1);
                var mM2b = (M.DenseOfArray(W2p) * Vector<float>.Build.DenseOfArray(H1)).AsArray();
                for (int i = 0; i < Yp.Length; i++) {
                    Yp[i] = mM2b[i] + b2p[i];
                }
                break;

            default:
                break;
        }

        for (int i = 0; i < Yp.Length; i++) {
            Yp[i] = (Yp[i] * Ystd[i]) + Ymean[i];
        }

    }
}
