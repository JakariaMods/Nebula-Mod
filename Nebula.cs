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
using VRageRender;

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
        public float Density = 0.35f;
        [ProtoMember(18), XmlElement]
        public float NoiseMultiplier = 1f;
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

        [ProtoMember(40), XmlElement]
        public ShadowDrawEnum DrawShadows = ShadowDrawEnum.Both;

        public Nebula(Vector3D position, int radius, int seed)
        {
            this.Position = position;
            this.Radius = radius;
            this.Seed = seed;

            Noise = new FastNoiseLite(Seed);
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

        public bool CreateWeatherDetailed(Vector3D position, string weather, Vector3 velocity, int maxLife, float radius)
        {
            if (IsNearWeather(position))
                return false;

            SpaceWeathers.Add(new SpaceWeather(position, velocity, radius, maxLife, weather));

            if (MyAPIGateway.Session.IsServer)
                NebulaMod.Static.SyncToClients(NebulaPacketType.Nebulae);
            else
                NebulaMod.Static.SyncToServer(NebulaPacketType.Nebulae);

            return true;
        }

        public bool CreateWeather(Vector3D position, string weather, bool natural)
        {
            if (IsNearWeather(position))
                return false;

            float radius = (Radius * 0.1f) * 100;
            int lifeTime = MyUtils.GetRandomInt(MinWeatherLength, MaxWeatherLength);
            float speed = radius / (lifeTime / 60f);
            Vector3 direction = MyUtils.GetRandomVector3Normalized();

            if (natural)
                SpaceWeathers.Add(new SpaceWeather(position - (direction * radius), direction * speed, radius, lifeTime, weather));
            else
                SpaceWeathers.Add(new SpaceWeather(position, Vector3.Zero, radius, -1, weather));

            if (MyAPIGateway.Session.IsServer)
                NebulaMod.Static.SyncToClients(NebulaPacketType.Nebulae);
            else if (MyAPIGateway.Session.HasCreativeRights)
                NebulaMod.Static.SyncToServer(NebulaPacketType.Nebulae);

            return true;
        }

        public bool IsNearWeather(Vector3D position)
        {
            foreach (var Weather in SpaceWeathers)
            {
                double LineDistance = MyUtils.GetPointLineDistance(ref Weather.StartPosition, ref Weather.EndPosition, ref position);
                if (LineDistance < Weather.Radius || Vector3D.Distance(Weather.StartPosition, position) < Weather.Radius)
                    return true;
            }

            return false;
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
                            if (Player.IsBot)
                                continue;

                            Vector3D Position = Player.GetPosition();
                            if (IsInsideNebulaBounding(Position) && IsInsideNebula(Position))
                                CreateRandomWeather(Position);
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
            float RootRadius = Radius * 2000;

            RootNode = new NebulaDrawNode(0, Position, ref RootRadius, this);
        }

        public bool IsInsideNebula(Vector3D Position)
        {
            Position *= NoiseScale;
            return Noise.GetNoise(Position.X, Position.Y, Position.Z) * NoiseMultiplier > Density;
        }

        public bool IsInsideNebulaBounding(Vector3D Position)
        {
            return (this.Position - Position).AbsMax() < Radius * 1000;
        }

        public double GetDepthRatio(Vector3D Position)
        {
            Position *= NoiseScale;
            return Math.Min(Noise.GetNoise(Position.X, Position.Y, Position.Z) * NoiseMultiplier, 1);
        }

        public Vector4 GetColorWithAlpha(Vector3D Position)
        {
            return Vector4.Lerp(PrimaryColor, SecondaryColor, (float)Noise.GetNoise(Position.X * ColorNoiseScale, Position.Y * ColorNoiseScale, Position.Z * ColorNoiseScale));
        }

        public Vector3 GetColor(Vector3D Position)
        {
            Vector3 tempVector = new Vector3(PrimaryColor.X, PrimaryColor.Y, PrimaryColor.Z);
            Vector3 tempVector2 = new Vector3(SecondaryColor.X, SecondaryColor.Y, SecondaryColor.Z);

            return Vector3.Lerp(tempVector, tempVector2, (float)Noise.GetNoise(Position.X * ColorNoiseScale, Position.Y * ColorNoiseScale, Position.Z * ColorNoiseScale));
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

            RootNode?.Draw();
        }
    }
}
