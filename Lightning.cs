using Jakaria.Utils;
using ProtoBuf;
using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Collections;
using VRage.Game;
using VRage.Game.Components;
using VRage.Game.Entity;
using VRage.Game.ModAPI;
using VRage.Utils;
using VRageMath;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;

namespace Jakaria
{
    [ProtoContract]
    class Lightning
    {
        [ProtoMember(1)]
        public int Life { get; private set; }
        [ProtoMember(2)]
        public int MaxLife { get; private set; }

        [ProtoIgnore]
        public Vector3D[] Parts;

        [ProtoMember(5)]
        public Vector3D Position;

        [ProtoMember(10)]
        public Vector3D Direction;

        [ProtoIgnore]
        public MyEntity3DSoundEmitter SoundEmitter;

        [ProtoIgnore]
        public LightningBuilder Builder;

        [ProtoMember(15)]
        public string BuilderId;

        [ProtoMember(20)]
        public float Length;

        public Lightning()
        {

        }

        public void Init()
        {
            Builder = NebulaMod.Static.WeatherBuilders[BuilderId].Lightning;
            Parts = new Vector3D[50];

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                Parts[0] = Position;

                Length *= 1f / Builder.BoltParts;
                for (int i = 1; i < Builder.BoltParts; i++)
                {
                    Parts[i] = Parts[i - 1] + Direction * (Length * (i / (float)Builder.BoltParts)) + (MyUtils.GetRandomPerpendicularVector(ref Direction) * MyUtils.GetRandomFloat(0, Builder.BoltVariation));
                }
            }

            if (MyAPIGateway.Session.IsServer || Vector3D.DistanceSquared(Position, NebulaMod.Session.CameraPosition) < 16000)
            {
                if (MyGamePruningStructure.GetClosestPlanet(Position) != null)
                    return;

                BoundingSphereD Sphere = new BoundingSphereD(Position, Builder.BoltVariation);

                List<MyEntity> Result = new List<MyEntity>();
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref Sphere, Result, MyEntityQueryType.Dynamic);

                MyCubeBlock TargetBlock = null;

                if (Result.Count > 0)
                {
                    Result.ShuffleList();
                    foreach (var Entity in Result)
                    {
                        if (Entity.MarkedForClose || Entity.IsPreview)
                            continue;

                        if (Entity is MyCubeGrid)
                        {
                            MyCubeGrid Grid = (Entity as MyCubeGrid);

                            ListReader<MyCubeBlock> Blocks = Grid.GetFatBlocks();

                            if (Blocks.Count <= 0)
                                continue;

                            TargetBlock = Blocks[MyUtils.GetRandomInt(0, Blocks.Count - 1)];
                            break;
                        }
                    }

                    FindLightningRod(ref Result, ref TargetBlock);

                    if (TargetBlock != null)
                    {
                        IHitInfo hitInfo;
                        if (MyAPIGateway.Physics.CastRay(Parts[0], TargetBlock.PositionComp.GetPosition(), out hitInfo))
                            Parts[0] = hitInfo.Position;
                        else
                            Parts[0] = TargetBlock.Position;
                    }
                }

                if (MyAPIGateway.Session.IsServer)
                {
                    MyExplosionInfo ExplosionInfo = new MyExplosionInfo(50, 300, new BoundingSphereD(Parts[0], 1), MyExplosionTypeEnum.CUSTOM, true);
                    ExplosionInfo.KeepAffectedBlocks = true;
                    ExplosionInfo.AffectVoxels = false;
                    ExplosionInfo.CreateParticleEffect = true;
                    MyExplosions.AddExplosion(ref ExplosionInfo);
                }
            }
        }

        public void FindLightningRod(ref List<MyEntity> entities, ref MyCubeBlock block)
        {
            foreach (var Entity in entities)
            {
                if (Entity.MarkedForClose || Entity.IsPreview)
                    continue;

                if (Entity is MyCubeGrid)
                {
                    foreach (var Block in ((MyCubeGrid)Entity).GetFatBlocks())
                    {
                        if (Block is IMyDecoy || Block is IMyBeacon || Block is IMyLaserAntenna)
                        {
                            block = Block;
                            return;
                        }
                    }
                }
            }
        }

        public Lightning(Vector3D position, Vector3D direction, string builderId, float length = 1000)
        {
            Position = position;
            Direction = direction;
            BuilderId = builderId;
            Length = length;

            Init();
        }

        public void PlaySound()
        {
            SoundEmitter = new MyEntity3DSoundEmitter(null);
            SoundEmitter.SetPosition(Position);
            SoundEmitter.PlaySound(NebulaData.LightningSound, alwaysHearOnRealistic: true);
        }

        public void Draw()
        {
            float ratio = (1f - JakUtils.EaseOutBounce(Life / (float)Builder.MaxLife));
            Vector4 color = Builder.Color * ratio;
            color.W *= ratio;
            
            for (int i = 1; i < Parts.Length; i++)
            {
                MySimpleObjectDraw.DrawLine(Parts[i - 1], Parts[i], NebulaData.LightningMaterial, ref color, Builder.BoltRadius, BlendTypeEnum.LDR);
                MyTransparentGeometry.AddPointBillboard(NebulaData.FlareMaterial, color * 0.015f, Parts[i - 1], Builder.BoltRadius * 250 * NebulaData.FlareIntensity, 0);

            }
        }

        public void Simulate()
        {
            Life++;
        }
    }
}