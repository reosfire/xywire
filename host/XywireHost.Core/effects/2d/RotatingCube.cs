using XywireHost.Core.core;
using XywireHost.Core.core.vectors;

namespace XywireHost.Core.effects._2d;

internal class RotatingCube(LedLine attachedLedLine) : RaytracedEffect(attachedLedLine)
{
    // Cube faces (each face is defined by 4 vertex indices)
    private readonly int[][] _cubeFaces =
    [
        [0, 1, 2, 3], // Back face
        [4, 5, 6, 7], // Front face
        [0, 1, 5, 4], // Bottom face
        [2, 3, 7, 6], // Top face
        [0, 3, 7, 4], // Left face
        [1, 2, 6, 5], // Right face
    ];

    // Cube vertices (8 corners of a cube centered at origin)
    private readonly Vec3d[] _cubeVertices =
    [
        new(-1, -1, -1),
        new(1, -1, -1),
        new(1, 1, -1),
        new(-1, 1, -1),
        new(-1, -1, 1),
        new(1, -1, 1),
        new(1, 1, 1),
        new(-1, 1, 1),
    ];

    // Colors for each face
    private readonly Color[] _faceColors =
    [
        Color.RGB(255, 0, 0), // Red - Back
        Color.RGB(0, 255, 0), // Green - Front
        Color.RGB(0, 0, 255), // Blue - Bottom
        Color.RGB(255, 255, 0), // Yellow - Top
        Color.RGB(255, 0, 255), // Magenta - Left
        Color.RGB(0, 255, 255), // Cyan - Right
    ];

    private double _rotationX;
    private double _rotationY;
    private double _rotationZ;

    protected override void MoveNext()
    {
        _rotationX += 0.02;
        _rotationY += 0.03;
        _rotationZ += 0.01;

        base.MoveNext();
    }

    protected override Color RenderPixel(double x, double y)
    {
        Vec3d cameraPos = new(0, 0, -2.5);
        Vec3d lightDir = new Vec3d(0.5, -0.5, -1).Normalize();

        Vec3d[] rotatedVertices = new Vec3d[_cubeVertices.Length];
        for (int i = 0; i < _cubeVertices.Length; i++)
        {
            rotatedVertices[i] = RotatePoint(_cubeVertices[i], _rotationX, _rotationY, _rotationZ);
        }

        Vec3d rayDir = new Vec3d(x, y, 1).Normalize();

        return CastRay(rayDir, cameraPos, lightDir, rotatedVertices);
    }


    private Color CastRay(Vec3d rayDir, Vec3d cameraPos, Vec3d lightDir, Vec3d[] rotatedVertices)
    {
        double closestDist = double.MaxValue;
        Color hitColor = Color.RGB(0, 0, 0);

        for (int faceIndex = 0; faceIndex < _cubeFaces.Length; faceIndex++)
        {
            int[] face = _cubeFaces[faceIndex];

            // Get the 4 vertices of the quad face
            Vec3d v0 = rotatedVertices[face[0]];
            Vec3d v1 = rotatedVertices[face[1]];
            Vec3d v2 = rotatedVertices[face[2]];
            Vec3d v3 = rotatedVertices[face[3]];

            // Calculate face normal
            Vec3d edge1 = v1 - v0;
            Vec3d edge2 = v3 - v0;
            Vec3d normal = edge1.Cross(edge2).Normalize();

            // Check if ray intersects the plane of the face
            double denom = normal.Dot(rayDir);
            if (!(Math.Abs(denom) > 0.0001)) continue;
            Vec3d p0 = (v0 + v1 + v2 + v3) * 0.25; // Center of face
            double t = (p0 - cameraPos).Dot(normal) / denom;

            if (!(t > 0) || !(t < closestDist)) continue;
            Vec3d hitPoint = cameraPos + rayDir * t;

            // Check if hit point is inside the quad face
            if (!IsPointInQuad(hitPoint, v0, v1, v2, v3, normal)) continue;

            // Calculate lighting
            double lightIntensity = Math.Max(0, -normal.Dot(lightDir));
            lightIntensity = 0.3 + lightIntensity * 0.7; // Ambient + diffuse

            Color faceColor = _faceColors[faceIndex];
            hitColor = Color.RGB(
                (int)(faceColor.Red * lightIntensity),
                (int)(faceColor.Green * lightIntensity),
                (int)(faceColor.Blue * lightIntensity)
            );

            closestDist = t;
        }

        return hitColor;
    }

    private bool IsPointInQuad(Vec3d p, Vec3d v0, Vec3d v1, Vec3d v2, Vec3d v3, Vec3d normal)
    {
        // Project point and quad vertices onto the plane
        // Check if point is inside by checking cross products
        Vec3d edge0 = v1 - v0;
        Vec3d edge1 = v2 - v1;
        Vec3d edge2 = v3 - v2;
        Vec3d edge3 = v0 - v3;

        Vec3d c0 = (v0 - p).Cross(edge0);
        Vec3d c1 = (v1 - p).Cross(edge1);
        Vec3d c2 = (v2 - p).Cross(edge2);
        Vec3d c3 = (v3 - p).Cross(edge3);

        // Check if all cross products point in the same direction as the normal
        bool sign0 = c0.Dot(normal) >= 0;
        bool sign1 = c1.Dot(normal) >= 0;
        bool sign2 = c2.Dot(normal) >= 0;
        bool sign3 = c3.Dot(normal) >= 0;

        return sign0 && sign1 && sign2 && sign3;
    }

    private Vec3d RotatePoint(Vec3d point, double rx, double ry, double rz)
    {
        // Rotate around X axis
        double cosX = Math.Cos(rx);
        double sinX = Math.Sin(rx);
        Vec3d p1 = new(
            point.X,
            point.Y * cosX - point.Z * sinX,
            point.Y * sinX + point.Z * cosX
        );

        // Rotate around Y axis
        double cosY = Math.Cos(ry);
        double sinY = Math.Sin(ry);
        Vec3d p2 = new(
            p1.X * cosY + p1.Z * sinY,
            p1.Y,
            -p1.X * sinY + p1.Z * cosY
        );

        // Rotate around Z axis
        double cosZ = Math.Cos(rz);
        double sinZ = Math.Sin(rz);
        Vec3d p3 = new(
            p2.X * cosZ - p2.Y * sinZ,
            p2.X * sinZ + p2.Y * cosZ,
            p2.Z
        );

        return p3;
    }

    protected override int StabilizeFps() => 30; // 30 FPS for smooth rotation
}
