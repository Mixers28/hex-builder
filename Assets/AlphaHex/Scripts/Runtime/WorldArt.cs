using System;
using System.Collections.Generic;
using AlphaHex.Core;
using UnityEngine;
using UnityEngine.Rendering;
using Object = UnityEngine.Object;

namespace AlphaHex.Runtime
{
    /// <summary>Small, shared procedural art kit. No downloaded models or textures are required.</summary>
    internal sealed partial class WorldArt : IDisposable
    {
        public readonly Material Cliff, PitFloor, Water, WaterGlint, Selection, Road, Concrete;
        public readonly Material Timber, Bark, Steel, DarkSteel, PaintedBlue, Ochre, Cream, Glass;
        public readonly Material[] Leaves;
        public readonly Mesh Cone, Rock, SelectionRing;

        private readonly Material[,] _surfaces = new Material[6, 2];
        private readonly List<Material> _materials = new List<Material>();
        private readonly List<Mesh> _meshes = new List<Mesh>();

        public WorldArt()
        {
            Color[] colors =
            {
                Hex("78966A"), Hex("587C59"), Hex("9B9788"),
                Hex("777D79"), Hex("CCBD96"), Hex("315F73")
            };
            for (int terrain = 0; terrain < colors.Length; terrain++)
            {
                _surfaces[terrain, 1] = Material("Ground / " + (TerrainKind)terrain, colors[terrain]);
                _surfaces[terrain, 0] = Material("Unsurveyed / " + (TerrainKind)terrain,
                    Color.Lerp(colors[terrain], Hex("566A68"), 0.38f));
            }

            Cliff = Material("Layered warm stone", Hex("79796F"));
            PitFloor = Material("Quarry floor", Hex("454E50"));
            Water = Material("Coastal water", Hex("315F73"), 0.35f);
            WaterGlint = Material("Water highlights", Hex("4D7D8A"), 0.4f);
            Selection = Material("Selected hex", Hex("41B8FF"), 0.25f, emission: 0.35f);
            Road = Material("Compacted haul road", Hex("A49F89"));
            Concrete = Material("Concrete", Hex("B8BDB2"));
            Timber = Material("Cut timber", Hex("C09662"));
            Bark = Material("Tree bark", Hex("635445"));
            Steel = Material("Galvanised steel", Hex("9CAAAA"), 0.3f, 0.45f);
            DarkSteel = Material("Dark machinery", Hex("33464B"), 0.22f, 0.45f);
            PaintedBlue = Material("Depot blue", Hex("365F70"), 0.2f);
            Ochre = Material("Industrial yellow", Hex("D5A04E"), 0.2f);
            Cream = Material("Building cladding", Hex("D3D0BB"));
            Glass = Material("Windows", Hex("304F5B"), 0.5f);
            Leaves = new[]
            {
                Material("Pine foliage / deep", Hex("365D48")),
                Material("Pine foliage / mid", Hex("487657")),
                Material("Pine foliage / light", Hex("5B8560"))
            };
            Cone = Register(BuildCone());
            Rock = Register(BuildRock());
            SelectionRing = Register(BuildRing(1.005f, 0.963f));
            GableRoof = Register(BuildGableRoof());
        }

        public Material Surface(TerrainKind terrain, bool claimed)
        {
            return _surfaces[Mathf.Clamp((int)terrain, 0, 5), claimed ? 1 : 0];
        }

        public Material CargoMaterial(ResourceKind resource)
        {
            switch (resource)
            {
                case ResourceKind.Timber: return Timber;
                case ResourceKind.Ore: return Ochre;
                case ResourceKind.Food: return Leaves[2];
                default: return Steel;
            }
        }

        public static Color Hex(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString("#" + value, out color) ? color : Color.magenta;
        }

        private Material Material(string name, Color color, float smoothness = 0.08f,
            float metallic = 0f, float emission = 0f)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Lit") ?? Shader.Find("Standard");
            if (shader == null) shader = Shader.Find("Universal Render Pipeline/Simple Lit");
            if (shader == null) shader = Shader.Find("Hidden/InternalErrorShader");
            var material = new Material(shader) { name = name };
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_Smoothness")) material.SetFloat("_Smoothness", smoothness);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", smoothness);
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", metallic);
            if (emission > 0 && material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", color * emission);
            }
            _materials.Add(material);
            return material;
        }

        private Mesh Register(Mesh mesh)
        {
            _meshes.Add(mesh);
            return mesh;
        }

        public static GameObject MeshObject(string name, Transform parent, Mesh mesh,
            params Material[] materials)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterials = materials;
            renderer.shadowCastingMode = ShadowCastingMode.On;
            renderer.receiveShadows = true;
            return go;
        }

        public static GameObject Primitive(string name, PrimitiveType type, Transform parent,
            Vector3 position, Vector3 scale, Material material, Quaternion? rotation = null)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = position;
            go.transform.localScale = scale;
            go.transform.localRotation = rotation ?? Quaternion.identity;
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                collider.enabled = false;
                Release(collider);
            }
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        public static GameObject Box(string name, Transform parent, Vector3 position,
            Vector3 size, Material material, Quaternion? rotation = null)
        {
            return Primitive(name, PrimitiveType.Cube, parent, position, size, material, rotation);
        }

        public static GameObject Beam(string name, Transform parent, Vector3 from, Vector3 to,
            float width, float thickness, Material material)
        {
            Vector3 delta = to - from;
            return Box(name, parent, (from + to) * 0.5f,
                new Vector3(width, thickness, delta.magnitude), material,
                Quaternion.LookRotation(delta.normalized, Vector3.up));
        }

        public static Vector3 Corner(int index, float radius, float y)
        {
            float angle = (30 + index * 60) * Mathf.Deg2Rad;
            return new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
        }

        /// <summary>Quarries have actual stepped geometry and a floor, rather than a decal.</summary>
        public static Mesh BuildTerrain(float surface, int depth, bool quarry)
        {
            var mesh = new MeshBuilder(3);
            const float outer = 0.987f;
            const float bottom = -0.24f;
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                Vector3 a = Corner(i, outer, surface);
                Vector3 b = Corner(next, outer, surface);
                Vector3 outward = (a + b).normalized;
                outward.y = 0;
                mesh.Quad(a, b, Corner(next, outer, bottom), Corner(i, outer, bottom), outward, 1);
                mesh.Triangle(new Vector3(0, bottom, 0), Corner(i, outer, bottom),
                    Corner(next, outer, bottom), Vector3.down, 1);
            }

            if (quarry)
            {
                float floor = Mathf.Max(0.115f, surface - 0.13f - depth * 0.035f);
                float middle = Mathf.Lerp(surface, floor, 0.52f);
                AddBand(mesh, outer, surface, 0.58f, surface, 0);
                AddBand(mesh, 0.58f, surface, 0.49f, middle, 1);
                AddBand(mesh, 0.49f, middle, 0.36f, middle, 0);
                AddBand(mesh, 0.36f, middle, 0.27f, floor, 1);
                for (int i = 0; i < 6; i++)
                    mesh.Triangle(new Vector3(0, floor, 0), Corner(i, 0.27f, floor),
                        Corner((i + 1) % 6, 0.27f, floor), Vector3.up, 2);
            }
            else
            {
                for (int i = 0; i < 6; i++)
                    mesh.Triangle(new Vector3(0, surface, 0), Corner(i, outer, surface),
                        Corner((i + 1) % 6, outer, surface), Vector3.up, 0);
            }
            return mesh.Finish("Hex terrain");
        }

        private static void AddBand(MeshBuilder mesh, float outerRadius, float outerHeight,
            float innerRadius, float innerHeight, int material)
        {
            for (int i = 0; i < 6; i++)
                mesh.Quad(Corner(i, outerRadius, outerHeight), Corner((i + 1) % 6, outerRadius, outerHeight),
                    Corner((i + 1) % 6, innerRadius, innerHeight), Corner(i, innerRadius, innerHeight),
                    Vector3.up, material);
        }

        private static Mesh BuildRing(float outerRadius, float innerRadius)
        {
            var mesh = new MeshBuilder(1);
            AddBand(mesh, outerRadius, 0, innerRadius, 0, 0);
            return mesh.Finish("Hex outline");
        }

        private static Mesh BuildCone()
        {
            var mesh = new MeshBuilder(1);
            for (int i = 0; i < 6; i++)
            {
                var a = Corner(i, 0.5f, 0);
                var b = Corner((i + 1) % 6, 0.5f, 0);
                mesh.Triangle(a, b, Vector3.up, (a + b + Vector3.up).normalized, 0);
                mesh.Triangle(Vector3.zero, a, b, Vector3.down, 0);
            }
            return mesh.Finish("Six sided pine crown");
        }

        private static Mesh BuildRock()
        {
            var mesh = new MeshBuilder(1);
            for (int i = 0; i < 6; i++)
            {
                int next = (i + 1) % 6;
                Vector3 a = Corner(i, 0.52f, 0.18f);
                Vector3 b = Corner(next, 0.52f, 0.18f);
                Vector3 highA = Corner(i, i % 2 == 0 ? 0.38f : 0.31f, 0.58f);
                Vector3 highB = Corner(next, next % 2 == 0 ? 0.38f : 0.31f, 0.58f);
                Vector3 normal = (a + b).normalized;
                mesh.Quad(Corner(i, 0.35f, 0), Corner(next, 0.35f, 0), b, a, normal, 0);
                mesh.Quad(a, b, highB, highA, normal + Vector3.up * 0.25f, 0);
                mesh.Triangle(highA, highB, new Vector3(0.06f, 0.78f, -0.03f), Vector3.up, 0);
            }
            return mesh.Finish("Faceted stone");
        }

        public void Tree(Transform parent, Vector3 position, float height, int variant)
        {
            var root = new GameObject("Harvestable tree").transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            root.localRotation = Quaternion.Euler(0, variant * 47, 0);
            root.localScale = Vector3.one * height;
            Box("Trunk", root, new Vector3(0, 0.24f, 0), new Vector3(0.07f, 0.48f, 0.07f), Bark);
            var low = MeshObject("Lower pine crown", root, Cone, Leaves[variant % Leaves.Length]);
            low.transform.localPosition = new Vector3(0, 0.18f, 0);
            low.transform.localScale = new Vector3(0.56f, 0.58f, 0.56f);
            var high = MeshObject("Upper pine crown", root, Cone, Leaves[(variant + 1) % Leaves.Length]);
            high.transform.localPosition = new Vector3(0, 0.48f, 0);
            high.transform.localScale = new Vector3(0.39f, 0.50f, 0.39f);
        }

        public void Boulder(Transform parent, Vector3 position, Vector3 scale, int variant, Material material = null)
        {
            var rock = MeshObject("Rock outcrop", parent, Rock, material ?? Cliff);
            rock.transform.localPosition = position;
            rock.transform.localScale = scale;
            rock.transform.localRotation = Quaternion.Euler(0, variant * 61, 0);
        }

        public void Building(Transform parent, BuildingKind kind)
        {
            switch (kind)
            {
                case BuildingKind.Depot: Depot(parent); break;
                case BuildingKind.Forestry: Forestry(parent); break;
                case BuildingKind.Mine: Mine(parent); break;
                case BuildingKind.Smelter: Smelter(parent); break;
                case BuildingKind.House: House(parent); break;
                case BuildingKind.Forager: Forager(parent); break;
                case BuildingKind.Farm: Farm(parent); break;
                case BuildingKind.Forester: Forester(parent); break;
                case BuildingKind.DeepMine: DeepMine(parent); break;
            }
        }

        private void Depot(Transform parent)
        {
            Box("Foundation", parent, new Vector3(0, 0.03f, 0), new Vector3(0.90f, 0.06f, 0.72f), Concrete);
            Box("Depot hall", parent, new Vector3(-0.05f, 0.23f, 0.04f), new Vector3(0.67f, 0.38f, 0.48f), Cream);
            Box("Roof", parent, new Vector3(-0.05f, 0.44f, 0.04f), new Vector3(0.76f, 0.055f, 0.56f), PaintedBlue);
            Box("Loading door", parent, new Vector3(-0.03f, 0.17f, -0.206f), new Vector3(0.29f, 0.27f, 0.014f), DarkSteel);
            for (int i = 0; i < 4; i++)
                Box("Door slat", parent, new Vector3(-0.03f, 0.08f + i * 0.053f, -0.218f),
                    new Vector3(0.285f, 0.012f, 0.008f), Steel);
            Box("Office window", parent, new Vector3(-0.389f, 0.27f, 0.01f), new Vector3(0.012f, 0.12f, 0.20f), Glass);
            Box("Side container", parent, new Vector3(0.42f, 0.135f, 0.05f), new Vector3(0.19f, 0.22f, 0.43f), PaintedBlue);
            Box("Antenna", parent, new Vector3(-0.22f, 0.64f, 0.13f), new Vector3(0.018f, 0.39f, 0.018f), Steel);
            Box("Antenna crossbar", parent, new Vector3(-0.22f, 0.79f, 0.13f), new Vector3(0.18f, 0.014f, 0.014f), Steel);
            Box("Depot banner", parent, new Vector3(0.19f, 0.57f, 0.12f), new Vector3(0.16f, 0.16f, 0.018f), Selection);
            Box("Banner pole", parent, new Vector3(0.12f, 0.57f, 0.12f), new Vector3(0.015f, 0.30f, 0.015f), Steel);
            for (int i = 0; i < 3; i++)
                Box("Loading bollard", parent, new Vector3(-0.30f + i * 0.28f, 0.08f, -0.40f), new Vector3(0.04f, 0.16f, 0.04f), Ochre);
        }

        private void Forestry(Transform parent)
        {
            Box("Work platform", parent, new Vector3(0, 0.025f, 0), new Vector3(0.70f, 0.05f, 0.52f), Concrete);
            foreach (float x in new[] { -0.26f, 0.26f })
            foreach (float z in new[] { -0.16f, 0.16f })
                Box("Shed post", parent, new Vector3(x, 0.19f, z), new Vector3(0.045f, 0.33f, 0.045f), Timber);
            Box("Shed roof", parent, new Vector3(0, 0.38f, 0), new Vector3(0.69f, 0.05f, 0.50f), PaintedBlue,
                Quaternion.Euler(0, 0, -7));
            Box("Saw bench", parent, new Vector3(-0.08f, 0.13f, -0.04f), new Vector3(0.38f, 0.18f, 0.16f), Ochre);
            for (int i = 0; i < 4; i++)
                Primitive("Stacked log", PrimitiveType.Cylinder, parent,
                    new Vector3(-0.26f + i * 0.14f, 0.075f, -0.43f),
                    new Vector3(0.105f, 0.20f, 0.105f), Timber, Quaternion.Euler(90, 0, 0));
            Primitive("Upper log", PrimitiveType.Cylinder, parent,
                new Vector3(-0.05f, 0.17f, -0.43f), new Vector3(0.105f, 0.20f, 0.105f), Timber,
                Quaternion.Euler(90, 0, 0));
        }

        private void Mine(Transform parent)
        {
            Box("Crane footing", parent, new Vector3(-0.62f, 0.04f, 0.12f), new Vector3(0.27f, 0.08f, 0.34f), Concrete);
            Beam("Crane tower left", parent, new Vector3(-0.72f, 0.08f, 0.02f),
                new Vector3(-0.61f, 0.62f, 0.10f), 0.045f, 0.045f, Ochre);
            Beam("Crane tower right", parent, new Vector3(-0.51f, 0.08f, 0.22f),
                new Vector3(-0.61f, 0.62f, 0.10f), 0.045f, 0.045f, Ochre);
            Beam("Crane boom", parent, new Vector3(-0.72f, 0.60f, 0.10f),
                new Vector3(0.23f, 0.72f, 0.10f), 0.06f, 0.06f, Ochre);
            Beam("Crane support cable", parent, new Vector3(-0.61f, 0.78f, 0.10f),
                new Vector3(0.23f, 0.72f, 0.10f), 0.010f, 0.010f, DarkSteel);
            Box("Tower tip", parent, new Vector3(-0.61f, 0.68f, 0.10f), new Vector3(0.025f, 0.21f, 0.025f), Ochre);
            Box("Winch cable", parent, new Vector3(0.20f, 0.38f, 0.10f), new Vector3(0.012f, 0.63f, 0.012f), DarkSteel);
            Box("Ore bucket", parent, new Vector3(0.20f, 0.055f, 0.10f), new Vector3(0.14f, 0.12f, 0.13f), Steel);
            Box("Site office", parent, new Vector3(0.55f, 0.125f, -0.29f), new Vector3(0.30f, 0.25f, 0.21f), PaintedBlue);
            Box("Office roof", parent, new Vector3(0.55f, 0.265f, -0.29f), new Vector3(0.34f, 0.035f, 0.25f), Cream);
            Box("Office window", parent, new Vector3(0.55f, 0.15f, -0.399f), new Vector3(0.17f, 0.08f, 0.01f), Glass);
            Box("Safety barrier", parent, new Vector3(0, 0.10f, -0.62f), new Vector3(0.50f, 0.07f, 0.025f), Ochre);
            for (int i = 0; i < 3; i++)
                Box("Barrier stripe", parent, new Vector3(-0.16f + i * 0.16f, 0.10f, -0.636f),
                    new Vector3(0.055f, 0.074f, 0.006f), DarkSteel, Quaternion.Euler(0, 0, -22));
        }

        private void Smelter(Transform parent)
        {
            Box("Refinery foundation", parent, new Vector3(0, 0.035f, 0), new Vector3(0.88f, 0.07f, 0.71f), Concrete);
            Box("Furnace hall", parent, new Vector3(0.07f, 0.24f, 0), new Vector3(0.57f, 0.40f, 0.48f), DarkSteel);
            Box("Hall roof", parent, new Vector3(0.07f, 0.455f, 0), new Vector3(0.66f, 0.055f, 0.55f), Steel);
            Box("Furnace mouth", parent, new Vector3(0.06f, 0.18f, -0.247f), new Vector3(0.27f, 0.23f, 0.014f), Ochre);
            Box("Furnace opening", parent, new Vector3(0.06f, 0.17f, -0.258f), new Vector3(0.17f, 0.15f, 0.013f), DarkSteel);
            Primitive("Exhaust stack", PrimitiveType.Cylinder, parent,
                new Vector3(-0.31f, 0.42f, 0.14f), new Vector3(0.14f, 0.39f, 0.14f), Cream);
            Primitive("Stack cap", PrimitiveType.Cylinder, parent,
                new Vector3(-0.31f, 0.84f, 0.14f), new Vector3(0.19f, 0.04f, 0.19f), DarkSteel);
            Primitive("Stack band", PrimitiveType.Cylinder, parent,
                new Vector3(-0.31f, 0.63f, 0.14f), new Vector3(0.15f, 0.045f, 0.15f), Ochre);
            Beam("Extraction pipe", parent, new Vector3(-0.24f, 0.53f, 0.14f),
                new Vector3(0.09f, 0.53f, 0.14f), 0.075f, 0.075f, Steel);
            for (int i = 0; i < 3; i++)
                Box("Metal stack", parent, new Vector3(0.38f, 0.09f + 0.043f * i, -0.36f),
                    new Vector3(0.27f, 0.037f, 0.12f), Steel);
        }

        public GameObject Carrier(Transform parent)
        {
            var root = new GameObject("Resource shipment");
            root.transform.SetParent(parent, false);
            Box("Chassis", root.transform, new Vector3(0, 0.095f, 0), new Vector3(0.17f, 0.08f, 0.29f), DarkSteel);
            Box("Cab", root.transform, new Vector3(0, 0.18f, 0.085f), new Vector3(0.16f, 0.14f, 0.105f), Ochre);
            Box("Windshield", root.transform, new Vector3(0, 0.198f, 0.141f), new Vector3(0.124f, 0.056f, 0.006f), Glass);
            Box("Cargo", root.transform, new Vector3(0, 0.175f, -0.064f), new Vector3(0.16f, 0.09f, 0.15f), Timber);
            foreach (float x in new[] { -0.091f, 0.091f })
            foreach (float z in new[] { -0.088f, 0.088f })
                Primitive("Wheel", PrimitiveType.Cylinder, root.transform, new Vector3(x, 0.070f, z),
                    new Vector3(0.095f, 0.023f, 0.095f), DarkSteel, Quaternion.Euler(0, 0, 90));
            return root;
        }

        public void Dispose()
        {
            foreach (Material material in _materials) Release(material);
            foreach (Mesh mesh in _meshes) Release(mesh);
            _materials.Clear();
            _meshes.Clear();
        }

        public static void Release(Object value)
        {
            if (value == null) return;
            if (Application.isPlaying) Object.Destroy(value);
            else Object.DestroyImmediate(value);
        }

        private sealed class MeshBuilder
        {
            private readonly List<Vector3> _vertices = new List<Vector3>();
            private readonly List<Vector2> _uv = new List<Vector2>();
            private readonly List<int>[] _triangles;

            public MeshBuilder(int submeshes)
            {
                _triangles = new List<int>[submeshes];
                for (int i = 0; i < submeshes; i++) _triangles[i] = new List<int>();
            }

            public void Triangle(Vector3 a, Vector3 b, Vector3 c, Vector3 facing, int material)
            {
                if (Vector3.Dot(Vector3.Cross(b - a, c - a), facing) < 0)
                {
                    Vector3 swap = b;
                    b = c;
                    c = swap;
                }
                int start = _vertices.Count;
                _vertices.Add(a); _vertices.Add(b); _vertices.Add(c);
                _uv.Add(new Vector2(a.x, a.z)); _uv.Add(new Vector2(b.x, b.z)); _uv.Add(new Vector2(c.x, c.z));
                _triangles[material].Add(start);
                _triangles[material].Add(start + 1);
                _triangles[material].Add(start + 2);
            }

            public void Quad(Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 facing, int material)
            {
                Triangle(a, b, c, facing, material);
                Triangle(a, c, d, facing, material);
            }

            public Mesh Finish(string name)
            {
                var mesh = new Mesh { name = name };
                mesh.SetVertices(_vertices);
                mesh.SetUVs(0, _uv);
                mesh.subMeshCount = _triangles.Length;
                for (int i = 0; i < _triangles.Length; i++) mesh.SetTriangles(_triangles[i], i);
                mesh.RecalculateNormals();
                mesh.RecalculateBounds();
                return mesh;
            }
        }
    }
}
