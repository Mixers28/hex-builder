using UnityEngine;

namespace AlphaHex.Runtime
{
    internal sealed partial class WorldArt
    {
        private readonly Mesh GableRoof;

        private static Mesh BuildGableRoof()
        {
            var mesh = new MeshBuilder(1);
            var frontLeft = new Vector3(-0.5f, 0, -0.5f);
            var frontRight = new Vector3(0.5f, 0, -0.5f);
            var backLeft = new Vector3(-0.5f, 0, 0.5f);
            var backRight = new Vector3(0.5f, 0, 0.5f);
            var frontPeak = new Vector3(0, 1, -0.5f);
            var backPeak = new Vector3(0, 1, 0.5f);
            mesh.Quad(frontLeft, frontPeak, backPeak, backLeft, new Vector3(-1, 0.5f, 0).normalized, 0);
            mesh.Quad(frontPeak, frontRight, backRight, backPeak, new Vector3(1, 0.5f, 0).normalized, 0);
            mesh.Triangle(frontLeft, frontRight, frontPeak, Vector3.back, 0);
            mesh.Triangle(backRight, backLeft, backPeak, Vector3.forward, 0);
            return mesh.Finish("Rustic gable roof");
        }

        private Transform Cottage(Transform parent, string name, Vector3 position, float scale, Material roofMaterial)
        {
            Transform hut = new GameObject(name).transform;
            hut.SetParent(parent, false);
            hut.localPosition = position;
            hut.localScale = Vector3.one * scale;
            Box("Stone footing", hut, new Vector3(0, 0.025f, 0), new Vector3(0.77f, 0.05f, 0.66f), Cliff);
            Box("Plaster walls", hut, new Vector3(0, 0.25f, 0), new Vector3(0.66f, 0.44f, 0.56f), Cream);
            foreach (float x in new[] { -0.31f, 0.31f })
                foreach (float z in new[] { -0.27f, 0.27f })
                    Box("Oak corner post", hut, new Vector3(x, 0.25f, z), new Vector3(0.055f, 0.46f, 0.055f), Bark);
            Box("Front timber", hut, new Vector3(0, 0.42f, -0.29f), new Vector3(0.70f, 0.055f, 0.055f), Bark);
            Box("Door", hut, new Vector3(-0.11f, 0.17f, -0.29f), new Vector3(0.16f, 0.28f, 0.025f), Timber);
            Box("Front window", hut, new Vector3(0.18f, 0.27f, -0.292f), new Vector3(0.14f, 0.14f, 0.02f), Glass);
            Box("Window cross", hut, new Vector3(0.18f, 0.27f, -0.308f), new Vector3(0.018f, 0.15f, 0.02f), Timber);
            GameObject roof = MeshObject("Pitched roof", hut, GableRoof, roofMaterial);
            roof.transform.localPosition = new Vector3(0, 0.47f, 0);
            roof.transform.localScale = new Vector3(0.82f, 0.28f, 0.73f);
            Box("Roof ridge", hut, new Vector3(0, 0.75f, 0), new Vector3(0.06f, 0.045f, 0.77f), Bark);
            return hut;
        }

        private void House(Transform parent)
        {
            Transform hut = Cottage(parent, "Pioneer cottage", new Vector3(0, 0, 0.09f), 0.94f, Ochre);
            Box("Stone chimney", hut, new Vector3(0.20f, 0.64f, 0.15f), new Vector3(0.10f, 0.43f, 0.12f), Cliff);
            Box("Garden bed", parent, new Vector3(-0.23f, 0.035f, -0.55f), new Vector3(0.38f, 0.055f, 0.22f), Bark);
            for (int i = 0; i < 6; i++)
                Box("Vegetables", parent, new Vector3(-0.35f + i % 3 * 0.12f, 0.09f, -0.59f + i / 3 * 0.10f), new Vector3(0.07f, 0.07f, 0.07f), Leaves[2]);
            for (int i = 0; i < 4; i++)
                Box("Firewood", parent, new Vector3(0.48f, 0.07f + i / 2 * 0.07f, -0.13f + i % 2 * 0.08f), new Vector3(0.22f, 0.06f, 0.065f), Timber);
        }

        private void Forager(Transform parent)
        {
            Cottage(parent, "Forager shelter", new Vector3(0.12f, 0, 0.08f), 0.72f, Leaves[0]);
            Box("Drying table", parent, new Vector3(-0.37f, 0.15f, -0.30f), new Vector3(0.37f, 0.055f, 0.24f), Timber);
            for (int i = 0; i < 3; i++)
                Primitive("Foraged basket", PrimitiveType.Cylinder, parent, new Vector3(-0.48f + i * 0.12f, 0.21f, -0.30f),
                    new Vector3(0.09f, 0.04f, 0.09f), Ochre);
        }

        private void Farm(Transform parent)
        {
            Cottage(parent, "Farm barn", new Vector3(0.12f, 0, 0.29f), 0.70f, Ochre);
            for (int row = 0; row < 4; row++)
            {
                float z = -0.62f + row * 0.18f;
                Box("Tilled soil", parent, new Vector3(-0.06f, 0.025f, z), new Vector3(1.06f, 0.045f, 0.13f), Bark);
                for (int plant = 0; plant < 7; plant++)
                    Box("Growing crop", parent, new Vector3(-0.50f + plant * 0.145f, 0.11f, z), new Vector3(0.055f, 0.15f, 0.055f), row % 2 == 0 ? Ochre : Leaves[2]);
            }
            for (int i = 0; i < 5; i++)
                Box("Fence post", parent, new Vector3(-0.66f, 0.12f, -0.61f + i * 0.29f), new Vector3(0.035f, 0.24f, 0.035f), Timber);
            Box("Fence rail", parent, new Vector3(-0.66f, 0.15f, -0.03f), new Vector3(0.035f, 0.035f, 1.22f), Timber);
        }

        private void Forester(Transform parent)
        {
            Cottage(parent, "Forester nursery", new Vector3(0.16f, 0, 0.21f), 0.73f, PaintedBlue);
            for (int row = 0; row < 2; row++)
            {
                Box("Seedling bed", parent, new Vector3(-0.17f, 0.025f, -0.33f - row * 0.23f), new Vector3(0.75f, 0.05f, 0.18f), Bark);
                for (int i = 0; i < 4; i++) Tree(parent, new Vector3(-0.44f + i * 0.18f, 0.05f, -0.33f - row * 0.23f), 0.23f, i);
            }
        }

        private void DeepMine(Transform parent)
        {
            Mine(parent);
            foreach (float x in new[] { -0.24f, 0.24f })
                foreach (float z in new[] { -0.13f, 0.22f })
                    Box("Deep headframe post", parent, new Vector3(x, 0.55f, z), new Vector3(0.07f, 1.1f, 0.07f), Timber);
            Beam("Headframe brace", parent, new Vector3(-0.24f, 0.14f, -0.13f), new Vector3(0.24f, 0.96f, -0.13f), 0.045f, 0.045f, Bark);
            Box("Hoist beam", parent, new Vector3(0, 1.08f, 0.04f), new Vector3(0.64f, 0.11f, 0.45f), DarkSteel);
            Primitive("Hoist wheel", PrimitiveType.Cylinder, parent, new Vector3(0, 1.22f, 0.04f), new Vector3(0.30f, 0.045f, 0.30f), Ochre, Quaternion.Euler(90, 0, 0));
        }

        public GameObject Citizen(Transform parent, int index)
        {
            var citizen = new GameObject("Citizen " + (index + 1));
            citizen.transform.SetParent(parent, false);
            Box("Boots", citizen.transform, new Vector3(0, 0.035f, 0), new Vector3(0.065f, 0.07f, 0.055f), Bark);
            Box("Coat", citizen.transform, new Vector3(0, 0.125f, 0), new Vector3(0.095f, 0.14f, 0.065f), index % 3 == 0 ? Ochre : index % 3 == 1 ? PaintedBlue : Leaves[1]);
            Primitive("Head", PrimitiveType.Sphere, citizen.transform, new Vector3(0, 0.23f, 0), Vector3.one * 0.075f, Timber);
            Box("Hat", citizen.transform, new Vector3(0, 0.275f, 0), new Vector3(0.10f, 0.025f, 0.095f), Cream);
            return citizen;
        }
    }
}
