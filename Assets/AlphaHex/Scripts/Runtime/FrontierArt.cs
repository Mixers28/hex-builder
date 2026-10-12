using UnityEngine;

namespace AlphaHex.Runtime
{
    internal sealed partial class WorldArt
    {
        private void Watchtower(Transform parent, int level)
        {
            Material support = level == 2 ? Cliff : Bark;
            float height = level == 2 ? 1.8f : 1.35f;
            for (int x = -1; x <= 1; x += 2) for (int z = -1; z <= 1; z += 2)
                Box("Tower support", parent, new Vector3(x * .3f, height / 2, z * .3f), new Vector3(level == 2 ? .22f : .10f, height, level == 2 ? .22f : .10f), support);
            Box("Survey platform", parent, new Vector3(0, height, 0), new Vector3(.9f, .10f, .9f), Timber);
            for (int i = 0; i < 4; i++) Box("Parapet", parent, new Vector3(i % 2 == 0 ? -.4f : .4f, height + .18f, i < 2 ? -.4f : .4f), new Vector3(.08f, .34f, .08f), Bark);
            var roof = MeshObject("Tower roof", parent, Cone, PaintedBlue); roof.transform.localPosition = new Vector3(0, height + .48f, 0); roof.transform.localScale = new Vector3(1.2f, .4f, 1.2f);
            for (int i = 0; i < 8; i++) Box("Ladder rung", parent, new Vector3(0, .10f + i * height / 9, -.39f), new Vector3(.25f, .03f, .05f), Timber);
            Box("Survey banner", parent, new Vector3(.55f, height + .35f, 0), new Vector3(.25f, .3f, .025f), Ochre);
        }
        private void Quarry(Transform parent)
        {
            Box("Quarry timber frame", parent, new Vector3(-.35f, .45f, .15f), new Vector3(.10f, .9f, .10f), Bark);
            Box("Quarry boom", parent, new Vector3(-.05f, .87f, .15f), new Vector3(.8f, .08f, .10f), Timber);
            Box("Hoist rope", parent, new Vector3(.25f, .57f, .15f), new Vector3(.02f, .58f, .02f), DarkSteel);
            for (int i = 0; i < 5; i++) Box("Cut stone", parent, new Vector3(-.45f + (i % 3) * .28f, .1f + i / 3 * .16f, -.42f), new Vector3(.24f, .16f, .2f), Concrete);
        }
        private void ClayPit(Transform parent)
        {
            Primitive("Clay bed", PrimitiveType.Cylinder, parent, new Vector3(0, .025f, 0), new Vector3(1.2f, .03f, 1.1f), Clay);
            for (int i = 0; i < 3; i++) Primitive("Clay bucket", PrimitiveType.Cylinder, parent, new Vector3(-.45f + i * .32f, .14f, -.5f), new Vector3(.23f, .13f, .23f), Timber);
            Box("Shovel handle", parent, new Vector3(.45f, .35f, .1f), new Vector3(.03f, .65f, .03f), Timber, Quaternion.Euler(0, 0, -18));
            Box("Shovel blade", parent, new Vector3(.55f, .07f, .1f), new Vector3(.18f, .17f, .04f), DarkSteel);
        }
        private void Kiln(Transform parent)
        {
            Primitive("Brick firing chamber", PrimitiveType.Cylinder, parent, new Vector3(0, .28f, .08f), new Vector3(.85f, .28f, .85f), Brick);
            var dome = MeshObject("Kiln dome", parent, Cone, Clay); dome.transform.localPosition = new Vector3(0, .55f, .08f); dome.transform.localScale = new Vector3(.95f, .35f, .95f);
            Box("Kiln chimney", parent, new Vector3(.24f, .78f, .28f), new Vector3(.19f, .7f, .2f), Brick);
            Box("Firing door", parent, new Vector3(0, .18f, -.36f), new Vector3(.3f, .25f, .03f), DarkSteel);
            Box("Fire glow", parent, new Vector3(0, .11f, -.38f), new Vector3(.20f, .07f, .015f), Embers);
            for (int i = 0; i < 8; i++) Box("Cooling bricks", parent, new Vector3(-.55f + (i % 2) * .16f, .04f + i / 4 * .08f, -.12f + (i / 2 % 2) * .18f), new Vector3(.14f, .07f, .13f), Brick);
        }
        private void Bloomery(Transform parent)
        {
            Primitive("Low bloomery", PrimitiveType.Cylinder, parent, new Vector3(0, .34f, 0), new Vector3(.58f, .34f, .58f), Cliff);
            Primitive("Charcoal opening", PrimitiveType.Cylinder, parent, new Vector3(0, .685f, 0), new Vector3(.35f, .015f, .35f), DarkSteel);
            Primitive("Ember bed", PrimitiveType.Cylinder, parent, new Vector3(0, .70f, 0), new Vector3(.24f, .01f, .24f), Embers);
            Box("Bellows", parent, new Vector3(.42f, .20f, 0), new Vector3(.45f, .18f, .28f), Timber);
            Box("Bellows handle", parent, new Vector3(.52f, .38f, 0), new Vector3(.44f, .04f, .08f), Bark, Quaternion.Euler(0, 0, -20));
            Boulder(parent, new Vector3(-.43f, 0, -.24f), new Vector3(.28f, .25f, .3f), 1, Ochre);
            for (int i = 0; i < 3; i++) Box("Fuel stack", parent, new Vector3(-.40f, .05f + i * .07f, .3f), new Vector3(.43f, .06f, .17f), Bark);
        }
    }
}
