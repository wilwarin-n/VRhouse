#if UNITY_EDITOR
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;

namespace com.zibra.liquid.Utilities
{
    public static class GizmosHelper
    {
        public static void DrawWireCapsule(Vector3 pos, Quaternion rot, float radius, float height,
                                           Color color = default(Color))
        {
            if (color != default(Color))
                Handles.color = color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
            using (new Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = height;

                // draw sideways
                Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.left, Vector3.back, -180, radius);
                Handles.DrawLine(new Vector3(0, pointOffset, -radius), new Vector3(0, -pointOffset, -radius));
                Handles.DrawLine(new Vector3(0, pointOffset, radius), new Vector3(0, -pointOffset, radius));
                Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.left, Vector3.back, 180, radius);
                // draw frontways
                Handles.DrawWireArc(Vector3.up * pointOffset, Vector3.back, Vector3.left, 180, radius);
                Handles.DrawLine(new Vector3(-radius, pointOffset, 0), new Vector3(-radius, -pointOffset, 0));
                Handles.DrawLine(new Vector3(radius, pointOffset, 0), new Vector3(radius, -pointOffset, 0));
                Handles.DrawWireArc(Vector3.down * pointOffset, Vector3.back, Vector3.left, -180, radius);
                // draw center
                Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, radius);
                Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, radius);
            }
        }

        public static void DrawWireCylinder(Vector3 pos, Quaternion rot, float radius, float height,
                                            Color color = default(Color))
        {
            if (color != default(Color))
                Handles.color = color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
            using (new Handles.DrawingScope(angleMatrix))
            {
                var pointOffset = height;
                Handles.DrawLine(new Vector3(0, pointOffset, -radius), new Vector3(0, -pointOffset, -radius));
                Handles.DrawLine(new Vector3(0, pointOffset, radius), new Vector3(0, -pointOffset, radius));
                Handles.DrawLine(new Vector3(-radius, pointOffset, 0), new Vector3(-radius, -pointOffset, 0));
                Handles.DrawLine(new Vector3(radius, pointOffset, 0), new Vector3(radius, -pointOffset, 0));
                Handles.DrawWireDisc(Vector3.up * pointOffset, Vector3.up, radius);
                Handles.DrawWireDisc(Vector3.down * pointOffset, Vector3.up, radius);
            }
        }

        public static void DrawWireTorus(Vector3 pos, Quaternion rot, float radiusSmall, float radiusLarge,
                                         Color color = default(Color))
        {
            if (color != default(Color))
                Handles.color = color;
            Matrix4x4 angleMatrix = Matrix4x4.TRS(pos, rot, Handles.matrix.lossyScale);
            using (new Handles.DrawingScope(angleMatrix))
            {
                // small circles
                for (float ang = 0.0f; ang < 360.0f; ang += 15.0f)
                {
                    float radians = Mathf.Deg2Rad * ang;
                    Vector3 direction = Vector3.left * Mathf.Sin(radians) + Vector3.forward * Mathf.Cos(radians);
                    Vector3 normal = Vector3.left * Mathf.Cos(radians) - Vector3.forward * Mathf.Sin(radians);
                    Handles.DrawWireDisc(direction * radiusLarge, normal, radiusSmall);
                }

                // large circles
                for (float ang = 0.0f; ang < 360.0f; ang += 15.0f)
                {
                    float radians = Mathf.Deg2Rad * ang;
                    float radius = radiusLarge + radiusSmall * Mathf.Sin(radians);
                    Vector3 center = radiusSmall * Vector3.up * Mathf.Cos(radians);
                    Handles.DrawWireDisc(center, Vector3.up, radius);
                }
            }
        }

        public static void DrawArrow(Vector3 origin, Vector3 vector, Color color, float arrowHeadLength = 0.25f,
                                     float arrowHeadAngle = 20.0f)
        {
            Gizmos.color = color;
            Gizmos.DrawRay(origin, vector);
            arrowHeadLength *= Vector3.Magnitude(vector);

            Vector3 right =
                Quaternion.LookRotation(vector) * Quaternion.Euler(0, 180 + arrowHeadAngle, 0) * new Vector3(0, 0, 1);
            Vector3 left =
                Quaternion.LookRotation(vector) * Quaternion.Euler(0, 180 - arrowHeadAngle, 0) * new Vector3(0, 0, 1);

            Gizmos.DrawRay(origin + vector, right * arrowHeadLength);
            Gizmos.DrawRay(origin + vector, left * arrowHeadLength);
        }

        static float Phi = 0.5f * (Mathf.Sqrt(5.0f) + 1.0f);

        // i-th point of n uniformly distibuted fibonacci points on a sphere
        static public Vector3 FibonacciSpherePoint(int i, int n)
        {
            Vector2 pt = new Vector2(2.0f * Mathf.PI * ((i / Phi) % 1.0f), Mathf.Acos(1.0f - (2.0f * i + 1.0f) / n));
            return new Vector3(Mathf.Cos(pt.x) * Mathf.Sin(pt.y), Mathf.Sin(pt.x) * Mathf.Sin(pt.y), Mathf.Cos(pt.y));
        }

        public static void DrawArrowsSphereRadial(Vector3 origin, float length, int n, Color color)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 point = FibonacciSpherePoint(i, n);
                DrawArrow(origin + point + 0.5f * length * point, -length * point, color);
            }
        }
        public static void DrawArrowsSphereTangent(Vector3 origin, Vector3 axis, int n, Color color)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 point = FibonacciSpherePoint(i, n);
                Vector3 direction = Vector3.Cross(point, axis);
                DrawArrow(origin + point - 0.5f * direction, direction, color);
            }
        }

        public static void DrawArrowsSphereDirectional(Vector3 origin, Vector3 direction, int n, Color color)
        {
            for (int i = 0; i < n; i++)
            {
                Vector3 point = FibonacciSpherePoint(i, n);
                DrawArrow(origin + point - 0.5f * direction, direction, color);
            }
        }
    }
}

#endif