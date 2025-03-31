using System.Collections.Generic;
using Unity.Mathematics;
using UnityEngine;

public static class Utils
{
    public static float GetRotationAngle(Vector3 current, Vector3 target)
    {
        float angle = Vector3.Angle(current, target);
        Vector3 cross = Vector3.Cross(current, target);
        if (cross.y < 0) angle = -angle;
        return angle;
    }

    public static float GetRotationAngle(Vector2 current, Vector2 target)
    {
        float angle = Vector2.Angle(current, target);

        // Make counter clockwise rotation negative
        Vector2 tang = new Vector2(current.y, -current.x);
        float dir = Mathf.Sign(Vector2.Dot(target, tang));
        angle *= dir;

        return angle;
    }

    public static float SqrDistanceXZ(Bounds bounds, Vector3 pos)
    {
        float distanceX = 0;
        if (pos.x >= bounds.max.x)
        {
            distanceX = pos.x - bounds.max.x;
        }
        else if (pos.x <= bounds.min.x)
        {
            distanceX = pos.x - bounds.min.x;
        }

        float distanceZ = 0;
        if (pos.z >= bounds.max.z)
        {
            distanceZ = pos.z - bounds.max.z;
        }
        else if (pos.z <= bounds.min.z)
        {
            distanceZ = pos.z - bounds.min.z;
        }

        return distanceX * distanceX + distanceZ * distanceZ;
    }

    public static bool ContainsXZ(Bounds bounds, Vector3 pos)
    {
        return pos.x <= bounds.max.x && pos.x >= bounds.min.x &&
            pos.z <= bounds.max.z && pos.z >= bounds.min.z;
    }

    public static bool IntersectsXZ(Bounds bounds0, Bounds bounds1)
    {
        Rect rect0 = BoundsToRectXZ(bounds0);
        Rect rect1 = BoundsToRectXZ(bounds1);

        return rect0.Overlaps(rect1);
    }

    public static Vector3 InterpolateNormal(Vector3 a, Vector3 b, float lerp)
    {
        return Vector3.Normalize(Vector3.Lerp(a, b, lerp));
    }

    public static Rect BoundsToRectXZ(Bounds bounds)
    {
        return new Rect(bounds.min.x, bounds.min.z, bounds.size.x, bounds.size.z);
    }

    public static Vector3 GetDirectionOnXZPlane(Vector3 direction)
    {
        Vector3 directionXZ = direction;
        directionXZ.y = 0;
        return Vector3.Normalize(directionXZ);
    }

    public static float GetHighestPoint(List<Vector3> positions)
    {
        float highestPoint = float.MinValue;

        foreach (Vector3 position in positions)
        {
            if(position.y > highestPoint)
            {
                highestPoint = position.y;
            }
        }
        return highestPoint;
    }

    public static void UpdateBounds(ref Bounds bounds, float height)
    {
        Vector3 min = bounds.min;
        Vector3 max = bounds.max;
        max.y = math.max(max.y, height);

        bounds = new Bounds((min + max) * 0.5f, max - min);
    }

    public static void ConstructTangents(Vector3 normal, out Vector3 up, out Vector3 right)
    {
        // Contruct right tangent
        if (normal.y < 1 && normal.y > -1)
        {
            right = Vector3.Normalize(Vector3.Cross(normal, Vector3.up));
        }
        else
        {
            right = Vector3.Normalize(Vector3.Cross(normal, Vector3.forward));
            Debug.LogWarning("Wall normal has value " + normal);
        }

        // Construct up tangent
        up = -Vector3.Normalize(Vector3.Cross(normal, right));
    }
}
