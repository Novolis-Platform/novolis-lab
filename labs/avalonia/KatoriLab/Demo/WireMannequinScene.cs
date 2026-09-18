using System.Numerics;
using Novolis.Avalonia.ThreeD.Session;
using Novolis.Math.Geometry;
using Novolis.ThreeD;
using Novolis.Simulation.Humanoid;

namespace KatoriLab.Demo;

/// <summary>
/// Orbitable 3D wire mannequin + bokken segment + hold markers.
/// Inspired by mannequin.js visuals only (GPL — not copied).
/// </summary>
internal sealed class WireMannequinScene
{
    const float BoneRadius = 0.035f;
    const float HeadRadius = 0.11f;

    readonly SceneSessionService _session;
    readonly MeshNode[] _bones;
    readonly MeshNode _head;
    readonly MeshNode _bokken;
    readonly MeshNode _tsuba;
    readonly MeshNode _holdPrimary;
    readonly MeshNode _holdSecondary;

    public WireMannequinScene(SceneSessionService session)
    {
        _session = session ?? throw new ArgumentNullException(nameof(session));
        var edgeCount = HumanoidDebugDraw.BuildSegments(
            HumanoidPoseSolver.SolveWorld(
                HumanoidBindPose.CreateDefaultTPose(1.72f),
                HumanoidPose.FromBind(HumanoidBindPose.CreateDefaultTPose(1.72f)))).Length;

        var doc = new SceneDocument
        {
            Name = "KatoriLab wire mannequin",
            CreatedAt = DateTimeOffset.UtcNow,
            ModifiedAt = DateTimeOffset.UtcNow,
        };
        var root = new GroupNode { Name = "MannequinRoot" };
        doc.Nodes.Add(root);

        // Long tatami: door (−Z) → center → shomen (+Z).
        doc.Nodes.Add(new MeshNode
        {
            Name = "Tatami",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Transform = new SceneTransform { Position = [0f, -0.02f, -0.4f], Scale = [3.2f, 0.04f, 6.2f] },
        });
        // Door posts
        doc.Nodes.Add(new MeshNode
        {
            Name = "DoorPost.L",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Transform = new SceneTransform
            {
                Position = [-0.55f, 0.9f, KenTimeline.DoorZ - 0.15f],
                Scale = [0.12f, 1.8f, 0.12f],
            },
        });
        doc.Nodes.Add(new MeshNode
        {
            Name = "DoorPost.R",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
            Transform = new SceneTransform
            {
                Position = [0.55f, 0.9f, KenTimeline.DoorZ - 0.15f],
                Scale = [0.12f, 1.8f, 0.12f],
            },
        });
        // Center mark (opening pose)
        doc.Nodes.Add(new MeshNode
        {
            Name = "CenterMark",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Cylinder,
            Transform = new SceneTransform
            {
                Position = [0f, 0.01f, KenTimeline.CenterZ],
                Scale = [0.35f, 0.02f, 0.35f],
            },
        });

        _bones = new MeshNode[edgeCount];
        for (var i = 0; i < edgeCount; i++)
        {
            _bones[i] = new MeshNode
            {
                Name = $"Bone.{i}",
                ParentId = root.Id,
                Primitive = MeshPrimitiveKind.Box,
            };
            doc.Nodes.Add(_bones[i]);
        }

        _head = new MeshNode
        {
            Name = "Head",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Sphere,
        };
        doc.Nodes.Add(_head);

        _bokken = new MeshNode
        {
            Name = "Bokken",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Box,
        };
        doc.Nodes.Add(_bokken);

        _tsuba = new MeshNode
        {
            Name = "Tsuba",
            ParentId = root.Id,
            Primitive = MeshPrimitiveKind.Cylinder,
        };
        doc.Nodes.Add(_tsuba);

        _holdPrimary = Marker(doc, root.Id, "Hold.Primary", 0.04f);
        _holdSecondary = Marker(doc, root.Id, "Hold.Secondary", 0.035f);

        doc.Nodes.Add(new CameraNode
        {
            Name = "ReviewCamera",
            ParentId = root.Id,
            Transform = new SceneTransform { Position = [3.8f, 2.1f, 2.2f] },
            Target = [0f, 0.9f, -0.8f],
            FovDeg = 40f,
        });

        doc.Nodes.Add(new LightNode
        {
            Name = "Key",
            ParentId = root.Id,
            LightKind = LightKind.Omni,
            Intensity = 2.4f,
            Transform = new SceneTransform { Position = [2.5f, 3.5f, 2f] },
        });
        doc.SelectionId = null;
        _session.ReplaceDocument(doc);
    }

    public void Update(KatoriKataDriver driver)
    {
        var segments = HumanoidDebugDraw.BuildSegments(driver.World);
        for (var i = 0; i < _bones.Length; i++)
        {
            if (i >= segments.Length)
            {
                _bones[i].Visible = false;
                continue;
            }

            WriteSegment(_bones[i], segments[i].Start, segments[i].End, BoneRadius);
            _session.Evaluator.NotifyNodeChanged(_bones[i]);
        }

        var headPos = driver.World.Position(HumanoidBone.Head) + new Vector3(0f, 0.06f, 0f);
        WriteSphere(_head, headPos, HeadRadius);
        _session.Evaluator.NotifyNodeChanged(_head);

        if (driver.HoldMode)
        {
            WriteSegment(_bokken, driver.Kashira, driver.Kissaki, 0.016f);
            WriteTsuba(_tsuba, driver.Tsuba, driver.Kissaki - driver.Kashira);
            PlaceMarker(_holdPrimary, driver.HoldPrimaryWorld, 0.035f);
            PlaceMarker(_holdSecondary, driver.HoldSecondaryWorld, 0.035f);
            _holdPrimary.Visible = true;
            _holdSecondary.Visible = true;
        }
        else
        {
            _bokken.Visible = false;
            _tsuba.Visible = false;
            _holdPrimary.Visible = false;
            _holdSecondary.Visible = false;
        }

        _session.Evaluator.NotifyNodeChanged(_bokken);
        _session.Evaluator.NotifyNodeChanged(_tsuba);
        _session.Evaluator.NotifyNodeChanged(_holdPrimary);
        _session.Evaluator.NotifyNodeChanged(_holdSecondary);
    }

    /// <summary>Thin disc (cylinder) perpendicular to the blade at the tsuba.</summary>
    static void WriteTsuba(MeshNode node, Vector3 center, Vector3 bladeDir)
    {
        if (bladeDir.LengthSquared() < 1e-8f)
        {
            node.Visible = false;
            return;
        }

        bladeDir = Vector3.Normalize(bladeDir);
        const float radius = 0.075f;
        const float thickness = 0.018f;
        // PrimitiveMesher.Cylinder is Y-up; align Y with blade so the disc faces along the blade.
        var mesh = PrimitiveMesher.Cylinder(radius, thickness, 16);
        var q = QuatFromTo(Vector3.UnitY, bladeDir);
        mesh.Transform(Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(center));
        MeshEditBake.WriteBaked(node, mesh);
        node.Transform = new SceneTransform();
        node.Visible = true;
    }

    /// <summary>
    /// Bakes a Y-up box along a→b into world-space verts.
    /// System.Numerics uses row vectors, so combine as R·T (rotate then translate).
    /// </summary>
    static void WriteSegment(MeshNode node, Vector3 a, Vector3 b, float radius)
    {
        var delta = b - a;
        var len = delta.Length();
        if (len < 1e-4f)
        {
            node.Visible = false;
            return;
        }

        var dir = delta / len;
        var mid = (a + b) * 0.5f;
        var mesh = PrimitiveMesher.Box(radius * 2f, len, radius * 2f);
        var q = QuatFromTo(Vector3.UnitY, dir);
        mesh.Transform(Matrix4x4.CreateFromQuaternion(q) * Matrix4x4.CreateTranslation(mid));
        MeshEditBake.WriteBaked(node, mesh);
        node.Transform = new SceneTransform();
        node.Visible = true;
    }

    static void WriteSphere(MeshNode node, Vector3 center, float radius)
    {
        var mesh = PrimitiveMesher.Sphere(radius, 12);
        mesh.Transform(Matrix4x4.CreateTranslation(center));
        MeshEditBake.WriteBaked(node, mesh);
        node.Transform = new SceneTransform();
        node.Visible = true;
    }

    static void PlaceMarker(MeshNode node, Vector3 position, float radius)
    {
        node.Transform = new SceneTransform
        {
            Position = [position.X, position.Y, position.Z],
            Scale = [radius, radius, radius],
        };
    }

    static MeshNode Marker(SceneDocument doc, Guid parentId, string name, float radius)
    {
        var node = new MeshNode
        {
            Name = name,
            ParentId = parentId,
            Primitive = MeshPrimitiveKind.Sphere,
            Transform = new SceneTransform { Scale = [radius, radius, radius] },
        };
        doc.Nodes.Add(node);
        return node;
    }

    static Quaternion QuatFromTo(Vector3 from, Vector3 to)
    {
        from = Vector3.Normalize(from);
        to = Vector3.Normalize(to);
        var dot = Vector3.Dot(from, to);
        if (dot > 0.999999f)
            return Quaternion.Identity;
        if (dot < -0.999999f)
        {
            var axis = Vector3.Cross(Vector3.UnitX, from);
            if (axis.LengthSquared() < 1e-8f)
                axis = Vector3.Cross(Vector3.UnitY, from);
            axis = Vector3.Normalize(axis);
            return Quaternion.CreateFromAxisAngle(axis, MathF.PI);
        }

        var c = Vector3.Cross(from, to);
        return Quaternion.Normalize(new Quaternion(c.X, c.Y, c.Z, 1f + dot));
    }
}
