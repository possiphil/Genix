using UnityEngine;

namespace Genix.Placement
{
    public readonly struct OrientedBounds
    {
        public Vector3 Center { get; }
        public Vector3 Size { get; }
        public Quaternion Rotation { get; }

        public Vector3 Extents => Size * 0.5f;
        public Vector3 Right => Rotation * Vector3.right;
        public Vector3 Up => Rotation * Vector3.up;
        public Vector3 Forward => Rotation * Vector3.forward;

        public OrientedBounds(Vector3 center, Vector3 size, Quaternion rotation)
        {
            Center = center;
            Size = new Vector3(
                Mathf.Max(0.01f, size.x),
                Mathf.Max(0.01f, size.y),
                Mathf.Max(0.01f, size.z));
            Rotation = rotation;
        }

        public Bounds ToLocalBounds()
        {
            return new Bounds(Center, Size);
        }

        public Bounds ToAxisAlignedBounds()
        {
            Vector3 extents = Extents;
            Vector3 right = Right;
            Vector3 up = Up;
            Vector3 forward = Forward;

            Vector3 axisAlignedExtents = new(
                Mathf.Abs(right.x) * extents.x + Mathf.Abs(up.x) * extents.y + Mathf.Abs(forward.x) * extents.z,
                Mathf.Abs(right.y) * extents.x + Mathf.Abs(up.y) * extents.y + Mathf.Abs(forward.y) * extents.z,
                Mathf.Abs(right.z) * extents.x + Mathf.Abs(up.z) * extents.y + Mathf.Abs(forward.z) * extents.z);

            return new Bounds(Center, axisAlignedExtents * 2f);
        }

        public bool Intersects(Bounds axisAlignedBounds)
        {
            return Intersects(new OrientedBounds(axisAlignedBounds.center, axisAlignedBounds.size, Quaternion.identity));
        }

        public bool Intersects(OrientedBounds other)
        {
            const float epsilon = 0.0001f;

            Vector3[] aAxes = { Right.normalized, Up.normalized, Forward.normalized };
            Vector3[] bAxes = { other.Right.normalized, other.Up.normalized, other.Forward.normalized };
            Vector3 aExtents = Extents;
            Vector3 bExtents = other.Extents;

            float[,] rotation = new float[3, 3];
            float[,] absRotation = new float[3, 3];

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    rotation[i, j] = Vector3.Dot(aAxes[i], bAxes[j]);
                    absRotation[i, j] = Mathf.Abs(rotation[i, j]) + epsilon;
                }
            }

            Vector3 translationWorld = other.Center - Center;
            float[] translation =
            {
                Vector3.Dot(translationWorld, aAxes[0]),
                Vector3.Dot(translationWorld, aAxes[1]),
                Vector3.Dot(translationWorld, aAxes[2])
            };

            for (int i = 0; i < 3; i++)
            {
                float radiusA = aExtents[i];
                float radiusB =
                    bExtents.x * absRotation[i, 0] +
                    bExtents.y * absRotation[i, 1] +
                    bExtents.z * absRotation[i, 2];

                if (Mathf.Abs(translation[i]) > radiusA + radiusB)
                    return false;
            }

            for (int j = 0; j < 3; j++)
            {
                float radiusA =
                    aExtents.x * absRotation[0, j] +
                    aExtents.y * absRotation[1, j] +
                    aExtents.z * absRotation[2, j];
                float radiusB = bExtents[j];
                float distance = Mathf.Abs(
                    translation[0] * rotation[0, j] +
                    translation[1] * rotation[1, j] +
                    translation[2] * rotation[2, j]);

                if (distance > radiusA + radiusB)
                    return false;
            }

            for (int i = 0; i < 3; i++)
            {
                for (int j = 0; j < 3; j++)
                {
                    float radiusA =
                        aExtents[(i + 1) % 3] * absRotation[(i + 2) % 3, j] +
                        aExtents[(i + 2) % 3] * absRotation[(i + 1) % 3, j];
                    float radiusB =
                        bExtents[(j + 1) % 3] * absRotation[i, (j + 2) % 3] +
                        bExtents[(j + 2) % 3] * absRotation[i, (j + 1) % 3];
                    float distance = Mathf.Abs(
                        translation[(i + 2) % 3] * rotation[(i + 1) % 3, j] -
                        translation[(i + 1) % 3] * rotation[(i + 2) % 3, j]);

                    if (distance > radiusA + radiusB)
                        return false;
                }
            }

            return true;
        }
    }
}
