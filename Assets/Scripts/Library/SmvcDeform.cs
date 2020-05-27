using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using System;

// from https://github.com/superboubek/QMVC/blob/master/coordinates/smvc/smvc.h
public class SmvcDeform
{
    static float GetAngleBetweenUnitVectors(Vector3 u1, Vector3 u2)
    {
        return 2.0f * Mathf.Asin((u1 - u2).magnitude / 2.0f);
    }

    static float GetTangentOfHalfAngleBetweenUnitVectors(Vector3 u1, Vector3 u2)
    {
        float factor = (u1 - u2).magnitude / 2.0f;
        return factor / Mathf.Sqrt(Mathf.Max(1 - factor * factor, 0.0f));
    }

    public static void ComputeCoordinates(
        Vector3 eta,
        int[][] cage_faces,
        Vector3[] cage_vertices,
        float[] weights, // output
        int offset = 0)
    {
        double epsilon = 1e-6;

        int n_vertices = cage_vertices.Length;
        int n_faces = cage_faces.Length;

        for (int i = 0; i < cage_vertices.Length; ++i) weights[offset+i] = 0;

        float[] w_weights = new float[n_vertices];// unnormalized weights
        for (int i = 0; i < cage_vertices.Length; ++i) w_weights[i] = 0;

        float[] d = new float[n_vertices];
        for (int i = 0; i < d.Length; ++i) d[i] = 0;

        float sumWeights = 0;

        Vector3[] u = new Vector3[n_vertices];

        for (int v = 0; v < n_vertices; ++v)
        {
            d[v] = (eta - cage_vertices[v]).magnitude;
            if (d[v] < epsilon)
            {
                weights[offset+v] = 1;
                return;
            }
            u[v] = (cage_vertices[v] - eta) / d[v];
        }

        for (int f = 0; f < n_faces; ++f)
        {
            int faceVertCount = cage_faces[f].Length;

            // the Norm is CCW :
            Vector3 faceMeanVector = Vector3.zero;
            for (int i = 0; i < faceVertCount; ++i)
            {
                int v0 = cage_faces[f][i];
                int v1 = cage_faces[f][(i + 1) % faceVertCount];

                Vector3 u0 = u[v0];
                Vector3 u1 = u[v1];

                if (Vector3.Cross(u0, u1).magnitude < epsilon)
                {
                    // then we are on an edge
                    float eLambda = Vector3.Dot(eta - cage_vertices[v0], cage_vertices[v1] - cage_vertices[v0]) /
                                (cage_vertices[v1] - cage_vertices[v0]).sqrMagnitude;
                    if (eLambda >= 0 && eLambda <= 1)
                    {
                        weights[offset+v0] = 1 - eLambda;
                        weights[offset+v1] = eLambda;
                        return;
                    }
                }

                float angle = GetAngleBetweenUnitVectors(u0, u1);
                Vector3 n = Vector3.Cross(u0, u1);
                n = n.magnitude < epsilon ? Vector3.zero : n.normalized;
                faceMeanVector += (angle / 2.0f) * n;
            }

            float denominator = 0;
            float[] lambdas = new float[faceVertCount];
            for (int i = 0; i < lambdas.Length; ++i) lambdas[i] = 0;

            for (int i = 0; i < faceVertCount; ++i)
            {
                int vi = cage_faces[f][i];
                int viplus1 = cage_faces[f][(i + 1) % faceVertCount];
                int viminus1 = cage_faces[f][(i + faceVertCount - 1) % faceVertCount];

                Vector3 ui = u[vi];
                Vector3 uiplus1 = u[viplus1];
                Vector3 uiminus1 = u[viminus1];

                float faceMeanVectorSqrNorm = faceMeanVector.sqrMagnitude;
                float vfnormDividedBysinthetai = faceMeanVectorSqrNorm;
                if (faceMeanVectorSqrNorm > epsilon)
                {
                    vfnormDividedBysinthetai /= Vector3.Cross(faceMeanVector, ui).magnitude;
                }


                float tanAlphaiBy2 = GetTangentOfHalfAngleBetweenUnitVectors(
                    Vector3.Cross(faceMeanVector, ui).normalized,
                    Vector3.Cross(faceMeanVector, uiplus1).normalized
                );

                float tanAlphaiMinus1By2 = GetTangentOfHalfAngleBetweenUnitVectors(
                    Vector3.Cross(faceMeanVector, uiminus1).normalized,
                    Vector3.Cross(faceMeanVector, ui).normalized
                );

                float tangents = tanAlphaiBy2 + tanAlphaiMinus1By2;

                lambdas[i] = vfnormDividedBysinthetai * tangents / d[vi];

                denominator += tangents * Vector3.Dot(faceMeanVector, ui) / Vector3.Cross(faceMeanVector, ui).magnitude;
            }

            if (Mathf.Abs(denominator) < epsilon && faceMeanVector.magnitude > epsilon)
            {
                // then we are on the face, and we output the unnormalized weights of the face, as they dominate in the final sum:
                float sumWeightsFace = 0;
                for (int i = 0; i < faceVertCount; ++i)
                {
                    int vi = cage_faces[f][i];
                    float lambdai = lambdas[i];
                    weights[offset+vi] = lambdai;
                    sumWeightsFace += lambdai;
                }
                if (sumWeightsFace > epsilon)
                {
                    for (int i = 0; i < faceVertCount; ++i)
                    {
                        int vi = cage_faces[f][i];
                        weights[offset+vi] /= sumWeightsFace;
                    }
                    return;
                }
            }

            if (Mathf.Abs(denominator) > epsilon && !float.IsInfinity(denominator))
            {
                for (int i = 0; i < faceVertCount; ++i)
                {
                    int vi = cage_faces[f][i];
                    float lambdai = lambdas[i] / denominator;
                    if (float.IsNaN(lambdai))
                    {
                        lambdai = 0;
                    }
                    w_weights[vi] += lambdai;
                    sumWeights += lambdai;
                }
            }

        }

        for (int v = 0; v < n_vertices; ++v)
        {
            weights[offset+v] = w_weights[v] / sumWeights;
            if (float.IsNaN(weights[offset+v]))
            {
                weights[offset+v] = 0;
            }
        }


        return;
    }
}
