using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using ProtoBuf;
using VRageMath;
using Jakaria.Utils;
using System.Xml.Serialization;
using VRage.Game;
using VRage.Utils;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using Sandbox.ModAPI;
using static Jakaria.NebulaMod;
using Sandbox.Game;

namespace Jakaria
{
    [ProtoContract]
    public class Nebula
    {
        [ProtoIgnore, XmlIgnore]
        public FastNoiseLite Noise { get; }
        [ProtoIgnore, XmlIgnore]
        NebulaDrawNode RootNode;

        [ProtoMember(1), XmlElement]
        public Vector3D Position;
        [ProtoMember(5), XmlElement]
        public int Radius = 1000;
        [ProtoMember(10), XmlElement]
        public int Seed = 1337;
        [ProtoMember(15), XmlElement]
        public float NoiseScale = 0.0001f;
        [ProtoMember(16), XmlElement]
        public float ColorNoiseScale = 0.00005f;
        [ProtoMember(17), XmlElement]
        public float NoiseRatioCutoff = 0.35f;
        [ProtoMember(20), XmlElement]
        public Vector4 PrimaryColor = new Vector4(0.1f, 0.1f, 0.2f, 0.75f);
        [ProtoMember(25), XmlElement]
        public Vector4 SecondaryColor = new Vector4(0.2f, 0.1f, 0.1f, 0.75f);
        [ProtoMember(30), XmlElement]
        public List<SpaceWeather> SpaceWeathers = new List<SpaceWeather>();
        [ProtoMember(31), XmlElement]
        public int NextWeather = 0;
        [ProtoMember(32), XmlElement]
        public int MinWeatherFrequency = 144000;
        [ProtoMember(33), XmlElement]
        public int MaxWeatherFrequency = 288000;
        [ProtoMember(35), XmlElement]
        public int MinWeatherLength = 18000;
        [ProtoMember(36), XmlElement]
        public int MaxWeatherLength = 36000;

        public Nebula(Vector3 position, int radius, int seed = 1337)
        {
            this.Position = position;
            this.Radius = radius;
            this.Seed = seed;

            Noise = new FastNoiseLite(seed);
            NextWeather = MyUtils.GetRandomInt(MinWeatherFrequency, MaxWeatherFrequency);
            BuildTree();
        }

        public Nebula()
        {
            Noise = new FastNoiseLite(Seed);
            NextWeather = MyUtils.GetRandomInt(MinWeatherFrequency, MaxWeatherFrequency);
        }

        public SpaceWeather GetClosestWeather(Vector3D position)
        {
            foreach (var Weather in SpaceWeathers)
            {
                if (Vector3D.Distance(Weather.Position, position) < Weather.Radius)
                    return Weather;
            }
            return null;
        }

        public bool CreateRandomWeather(Vector3D position)
        {
            return CreateWeather(position, NebulaMod.Static.GetRandomWeather(), true);
        }

        public bool CreateWeatherDetailed(Vector3D position, string weather, Vector3D velocity, int maxLife, float radius)
        {
            foreach (var Weather in SpaceWeathers)
            {
                double LineDistance = MyUtils.GetPointLineDistance(ref Weather.StartPosition, ref Weather.EndPosition, ref position);
                if (LineDistance - radius < Weather.Radius)
                    return false;
            }

            SpaceWeathers.Add(new SpaceWeather(position, velocity, radius, maxLife, weather));

            if (MyAPIGateway.Session.IsServer)
                NebulaMod.Static.SyncToClients(NebulaPacketType.Nebulae);
            else
                NebulaMod.Static.SyncToServer(NebulaPacketType.Nebulae);

            return true;
        }

        public bool CreateWeather(Vector3D position, string weather, bool natural)
        {
            float radius = (Radius * 0.1f) * 100;
            int lifeTime = MyUtils.GetRandomInt(MinWeatherLength, MaxWeatherLength);
            float speed = radius / (lifeTime / 60f);
            Vector3D direction = MyUtils.GetRandomVector3Normalized();
            foreach (var Weather in SpaceWeathers)
            {
                double LineDistance = MyUtils.GetPointLineDistance(ref Weather.StartPosition, ref Weather.EndPosition, ref position);
                if (LineDistance - radius < Weather.Radius || Vector3D.Distance(Weather.StartPosition, position) < radius)
                    return false;
            }

            if (natural)
                SpaceWeathers.Add(new SpaceWeather(position - (direction * radius), direction * speed, radius, lifeTime, weather));
            else
                SpaceWeathers.Add(new SpaceWeather(position, Vector3D.Zero, radius, -1, weather));

            if (MyAPIGateway.Session.IsServer)
                NebulaMod.Static.SyncToClients(NebulaPacketType.Nebulae);
            else if (MyAPIGateway.Session.HasCreativeRights)
                NebulaMod.Static.SyncToServer(NebulaPacketType.Nebulae);

            return true;
        }

        public void Simulate()
        {
            if (MyAPIGateway.Session.IsServer)
            {
                if (MinWeatherFrequency > 0)
                {
                    NextWeather--;

                    if (NextWeather < 0)
                    {
                        NextWeather = MyUtils.GetRandomInt(MinWeatherFrequency, MaxWeatherFrequency);
                        foreach (var Player in NebulaMod.Static.Players)
                        {
                            if (IsInsideNebulaBounding(Player.GetPosition()))
                                CreateRandomWeather(Player.GetPosition());
                        }
                    }
                }
            }

            for (int i = 0; i < SpaceWeathers.Count; i++)
            {
                SpaceWeather Weather = SpaceWeathers[i];
                Weather.Simulate();

                if (Weather.Builder == null)
                    Weather.Init();

                if (Weather.MaxLife != -1 && Weather.Life >= Weather.MaxLife || Weather.Builder == null)
                {
                    SpaceWeathers.RemoveAtFast(i);

                    if (MyAPIGateway.Session.IsServer)
                        NebulaMod.Static.SyncToServer(NebulaPacketType.Nebulae);
                }
            }
        }

        public void BuildTree()
        {
            RootNode = new NebulaDrawNode(0, Position, Radius * 2000, this);
        }

        public bool IsInsideNebula(Vector3D Position)
        {
            Position *= NoiseScale;
            return Noise.GetNoise(Position.X, Position.Y, Position.Z) > NoiseRatioCutoff;
        }

        public bool IsInsideNebulaBounding(Vector3D Position)
        {
            return (this.Position - Position).AbsMax() < Radius * 1000;
        }

        public float GetDepthRatio(Vector3D Position)
        {
            Position *= NoiseScale;
            return Noise.GetNoise(Position.X, Position.Y, Position.Z);
        }

        public Vector4 GetColorWithAlpha(Vector3D Position)
        {
            return Vector4.Lerp(PrimaryColor, SecondaryColor, Noise.GetNoise(Position.X * ColorNoiseScale, Position.Y * ColorNoiseScale, Position.Z * ColorNoiseScale));
        }

        public Vector3 GetColor(Vector3D Position)
        {
            Vector3 tempVector = new Vector3(PrimaryColor.X, PrimaryColor.Y, PrimaryColor.Z);
            Vector3 tempVector2 = new Vector3(SecondaryColor.X, SecondaryColor.Y, SecondaryColor.Z);

            return Vector3.Lerp(tempVector, tempVector2, Noise.GetNoise(Position.X * ColorNoiseScale, Position.Y * ColorNoiseScale, Position.Z * ColorNoiseScale));
        }

        public void Draw()
        {
            if (NebulaMod.Session.DebugDraw)
            {
                MatrixD tempMatrix = MatrixD.CreateWorld(Position, Vector3.Forward, Vector3.Up);
                BoundingBoxD tempBounding = new BoundingBoxD(-new Vector3D(Radius, Radius, Radius) * 1000, new Vector3D(Radius, Radius, Radius) * 1000);
                Color tempColor = new Color(255, 255, 255, 128);
                MySimpleObjectDraw.DrawTransparentBox(ref tempMatrix, ref tempBounding, ref tempColor, MySimpleObjectRasterizer.Wireframe, 1);

                foreach (var Weather in SpaceWeathers)
                {
                    Weather.Draw();
                }
            }

            RootNode?.Draw(this);
        }

        public class NebulaDrawNode
        {
            Vector3D CenterPosition;
            float NodeDiameter;
            int NodeDepth = 0;
            float NodeNoiseValue;
            Vector4 NodeColor;

            NebulaDrawNode[] RootNodes;

            public void BuildNode(Nebula nebula)
            {
                if (NodeDiameter > 20000 && (NodeDiameter > 300000 || IsPositionInsideDrawNode(NebulaMod.Session.CameraPosition)))
                {
                    RootNodes = new NebulaDrawNode[8];

                    int tempDepth = NodeDepth + 1;
                    float tempHalfRadius = NodeDiameter / 2f;
                    float tempQuarterRadius = NodeDiameter / 4f;

                    RootNodes[0] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(-tempQuarterRadius, -tempQuarterRadius, -tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[1] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(tempQuarterRadius, -tempQuarterRadius, -tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[2] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(-tempQuarterRadius, -tempQuarterRadius, tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[3] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(tempQuarterRadius, -tempQuarterRadius, tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[4] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(-tempQuarterRadius, tempQuarterRadius, -tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[5] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(tempQuarterRadius, tempQuarterRadius, -tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[6] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(-tempQuarterRadius, tempQuarterRadius, tempQuarterRadius), tempHalfRadius, nebula);
                    RootNodes[7] = new NebulaDrawNode(tempDepth, CenterPosition + new Vector3D(tempQuarterRadius, tempQuarterRadius, tempQuarterRadius), tempHalfRadius, nebula);
                }
                else
                {
                    Vector3D tempVector = CenterPosition * nebula.NoiseScale;
                    NodeNoiseValue = nebula.Noise.GetNoise(tempVector.X, tempVector.Y, tempVector.Z);

                    NodeColor = Vector4.Lerp(nebula.PrimaryColor, nebula.SecondaryColor, nebula.Noise.GetNoise(CenterPosition.X * nebula.ColorNoiseScale, CenterPosition.Y * nebula.ColorNoiseScale, CenterPosition.Z * nebula.ColorNoiseScale));
                    NodeColor.W *= MyMath.Clamp((nebula.NoiseRatioCutoff - NodeNoiseValue) / nebula.NoiseRatioCutoff, 0, 1f);
                }
            }

            public void Draw(Nebula nebula)
            {
                if (NebulaMod.Session.CameraRotation.Dot(CenterPosition - NebulaMod.Session.CameraPosition + (NebulaMod.Session.CameraRotation * NodeDiameter)) <= 0)
                    return;

                if (RootNodes == null)
                {
                    if (NodeNoiseValue > nebula.NoiseRatioCutoff)
                    {
                        float tempNoise2 = nebula.Noise.GetNoise(CenterPosition.X + (MyAPIGateway.Session.ElapsedPlayTime.TotalSeconds * 0.008), CenterPosition.Y, CenterPosition.Z);
                        float tempRadius = NodeDiameter * (3 + (tempNoise2 * 2));

                        if (NodeDiameter > 2500)
                            MyTransparentGeometry.AddPointBillboard(NebulaData.NebulaTextures[(int)MyMath.Clamp(tempNoise2 * NebulaData.NebulaTextures.Length, 0, NebulaData.NebulaTextures.Length)], NodeColor, CenterPosition, tempRadius, tempNoise2 * 360, blendType: BlendTypeEnum.LDR);
                        else
                            MyTransparentGeometry.AddPointBillboard(NebulaData.NebulaTextures[(int)MyMath.Clamp(tempNoise2 * NebulaData.NebulaTextures.Length, 0, NebulaData.NebulaTextures.Length)], NodeColor, CenterPosition, tempRadius, tempNoise2 * 360, blendType: BlendTypeEnum.LDR);
                    }

                }
                else
                {
                    foreach (var node in RootNodes)
                    {
                        node.Draw(nebula);
                    }
                }

            }

            public bool IsPositionInsideDrawNode(Vector3D Position)
            {
                return (Position - CenterPosition).AbsMax() < NodeDiameter;
            }

            public NebulaDrawNode(int Depth, Vector3D CenterPosition, float Diameter, Nebula nebula)
            {
                this.NodeDepth = Depth;
                this.CenterPosition = CenterPosition;
                this.NodeDiameter = Diameter;

                BuildNode(nebula);
            }

            public override string ToString()
            {
                return "Position: " + this.CenterPosition.ToString() + " Radius: " + this.NodeDiameter + " Depth: " + this.NodeDepth;
            }
        }
    }
}
