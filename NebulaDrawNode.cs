using Jakaria.Utils;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Utils;
using VRageMath;
using VRageRender;
using static VRageRender.MyBillboard;

namespace Jakaria
{
    public class NebulaDrawNode
    {
        Vector3D CenterPosition;
        float NodeDiameter;
        int NodeDepth = 0;
        float NodeNoiseValue;
        Vector4 NodeColor;
        Nebula Nebula;
        MyBillboard Billboard;

        NebulaDrawNode[] RootNodes;

        public void BuildNode()
        {
            if (NodeDiameter > 20000 && (NodeDiameter > 300000 || IsPositionInsideDrawNode(NebulaMod.Session.CameraPosition)))
            {
                RootNodes = new NebulaDrawNode[8];

                int tempDepth = NodeDepth + 1;
                float tempHalfRadius = NodeDiameter / 2f;
                float tempQuarterRadius = NodeDiameter / 4f;

                for (int i = 0; i < 8; i++)
                {
                    RootNodes[i] = new NebulaDrawNode(tempDepth, CenterPosition + (NebulaData.CornerDirections[i] * tempQuarterRadius), ref tempHalfRadius, Nebula);
                }
            }
            else
            {

            }
        }

        public void Draw()
        {
            if (NebulaMod.Session.CameraRotation.Dot(CenterPosition - NebulaMod.Session.CameraPosition + (NebulaMod.Session.CameraRotation * NodeDiameter)) <= 0)
                return;

            if (RootNodes == null)
            {
                Vector3D tempVector = CenterPosition * Nebula.NoiseScale;
                NodeNoiseValue = Math.Min((float)Nebula.Noise.GetNoise(tempVector.X, tempVector.Y, tempVector.Z) * Nebula.NoiseMultiplier, 1f);
                if (NodeNoiseValue > 0.35)
                {
                    float tempNoise2 = (float)Nebula.Noise.GetNoise(CenterPosition.X + (MyAPIGateway.Session.ElapsedPlayTime.TotalSeconds * 0.008), CenterPosition.Y, CenterPosition.Z);

                    if (Billboard == null)
                    {
                        NodeColor = Vector4.Lerp(Nebula.PrimaryColor, Nebula.SecondaryColor, (float)Nebula.Noise.GetNoise(CenterPosition.X * Nebula.ColorNoiseScale, CenterPosition.Y * Nebula.ColorNoiseScale, CenterPosition.Z * Nebula.ColorNoiseScale));

                        Billboard = new MyBillboard()
                        {
                            Material = NebulaData.NebulaTextures[(int)MyMath.Clamp(tempNoise2 * NebulaData.NebulaTextures.Length, 0, NebulaData.NebulaTextures.Length)],
                            Color = NodeColor,

                            CustomViewProjection = -1,
                            BlendType = BlendTypeEnum.LDR,
                            SoftParticleDistanceScale = NodeDiameter,
                            ColorIntensity = MyMath.Clamp(NodeNoiseValue * Nebula.Density, 0, 1f),
                            UVSize = new Vector2(1f, 1f),
                        };
                    }

                    MyQuadD Quad;
                    MyUtils.GetBillboardQuadAdvancedRotated(out Quad, CenterPosition, NodeDiameter * (3 + (tempNoise2 * 2)), tempNoise2 * 360, NebulaMod.Session.CameraPosition);

                    Billboard.Position0 = Quad.Point0;
                    Billboard.Position1 = Quad.Point1;
                    Billboard.Position2 = Quad.Point2;
                    Billboard.Position3 = Quad.Point3;
                    
                    NebulaMod.Static.Billboards.Add(Billboard);
                }
            }
            else
            {
                foreach (var node in RootNodes)
                {
                    node.Draw();
                }
            }

        }

        public bool IsPositionInsideDrawNode(Vector3D Position)
        {
            return (Position - CenterPosition).AbsMax() < NodeDiameter;
        }

        public NebulaDrawNode(int Depth, Vector3D CenterPosition, ref float Diameter, Nebula Nebula)
        {
            this.NodeDepth = Depth;
            this.CenterPosition = CenterPosition;
            this.NodeDiameter = Diameter;
            this.Nebula = Nebula;
            BuildNode();
        }

        public override string ToString()
        {
            return "Position: " + this.CenterPosition.ToString() + " Radius: " + this.NodeDiameter + " Depth: " + this.NodeDepth;
        }
    }
}
