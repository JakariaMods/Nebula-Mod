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
using Sandbox.ModAPI;
using VRage.Game.ModAPI;
using Sandbox.Game.Entities;
using CollisionLayers = Sandbox.Engine.Physics.MyPhysics.CollisionLayers;
using VRage.Game.Entity;
using Sandbox.Game.Entities.Character;
using Sandbox.Game;

namespace Jakaria
{
    [ProtoContract]
    public class SpaceWeather
    {
        [ProtoMember(1), XmlElement]
        public Vector3D Position;
        [ProtoMember(2), XmlElement]
        public Vector3D StartPosition;
        [ProtoMember(3), XmlElement]
        public Vector3D EndPosition;
        [ProtoMember(5), XmlElement]
        public Vector3D Velocity;
        [ProtoMember(10), XmlElement]
        public float Radius;
        [ProtoMember(15), XmlElement]
        public int Life;
        [ProtoMember(16), XmlElement]
        public int MaxLife;
        [ProtoMember(20), XmlElement]
        public int NextLightning;
        [ProtoMember(25), XmlElement]
        public string Weather;

        [ProtoIgnore, XmlIgnore]
        public WeatherBuilder Builder;
        [ProtoIgnore, XmlIgnore]
        List<MyEntity> ContainedEntities = new List<MyEntity>();
        [ProtoIgnore, XmlIgnore]
        public int NextEntityCollection = 0;

        public SpaceWeather(Vector3D position, Vector3D velocity, float radius, int maxLife, string weather)
        {
            Position = position;
            Velocity = velocity;
            Radius = radius;
            MaxLife = maxLife;
            Weather = weather;
        }

        public SpaceWeather()
        {

        }

        public void Init()
        {
            NebulaMod.Static.WeatherBuilders.TryGetValue(Weather, out Builder);

            if (!MyAPIGateway.Utilities.IsDedicated && Builder?.HudWarning != null)
            {
                double LineDistance = MyUtils.GetPointLineDistance(ref StartPosition, ref EndPosition, ref NebulaMod.Session.CameraPosition);
                if (double.IsNaN(LineDistance) || LineDistance < Radius)
                {
                    JakUtils.ShowNotification(Builder.HudWarning, 10000, "Red");
                }
            }

            StartPosition = Position;
            EndPosition = StartPosition + (MaxLife * Velocity * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS);
        }

        public virtual void Simulate()
        {
            if (Builder == null)
                Init();

            if (Builder == null)
            {
                Life = MaxLife;
                return;
            }

            Position += Velocity * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
            Life++;

            NextEntityCollection++;
            if (NextEntityCollection >= 10)
            {
                ContainedEntities.Clear();
                BoundingSphereD Sphere = new BoundingSphereD(Position, Radius);
                MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref Sphere, ContainedEntities);
                NextEntityCollection = 0;
            }

            foreach (var Entity in ContainedEntities)
            {
                if (Entity.MarkedForClose || Entity.IsPreview || Entity.Physics == null)
                    continue;

                if (Entity is MyCubeGrid)
                {
                    MyCubeGrid Grid = (Entity as MyCubeGrid);
                    //(Entity as MyCubeGrid).EntityThrustComponent.DampenersEnabled = false;

                    if (!Grid.Physics.IsStatic && Vector3.IsZero(Grid.Physics.Gravity))
                    {
                        if (Builder.DisableDampenersGrid && Grid.DampenersEnabled)
                            MyVisualScriptLogicProvider.SetDampenersEnabled(Entity.Name, false);

                        if (MyAPIGateway.Session.IsServer)
                        {
                            if (Builder.GridDragForce > 0)
                                Grid.Physics.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_FORCE, -(Grid.Physics.LinearVelocity) * Builder.GridDragForce * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS, null, null);

                            if (Builder.GridWindForce != 0)
                                Grid.Physics.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_FORCE, NebulaMod.Session.SunDirection * -Builder.GridWindForce * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS, null, null);
                        }
                    }

                    if (Builder.BlocksToDisable?.Length > 0)
                        foreach (var Block in Grid.GetFatBlocks())
                        {
                            if (!Block.IsFunctional || Block.IsPreview)
                                continue;

                            if (Block is IMyFunctionalBlock)
                            {
                                if (Builder.BlocksToDisable.Contains(Block.BlockDefinition.Id.TypeId.ToString()))
                                    (Block as IMyFunctionalBlock).Enabled = false;
                            }

                        }
                }

                if (Entity is IMyCharacter)
                {
                    IMyCharacter Character = Entity as IMyCharacter;

                    if(Vector3.IsZero(Character.Physics.Gravity))
                    {
                        if (Builder.DisableDampenersCharacter && Character.EnabledDamping)
                            Character.SwitchDamping();

                        if (Builder.CharacterDragForce > 0)
                            Character.Physics?.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_FORCE, -(Character.Physics.LinearVelocity) * Builder.CharacterDragForce, null, null);

                        if (Builder.CharacterWindForce != 0)
                            Character.Physics.AddForce(VRage.Game.Components.MyPhysicsForceType.APPLY_WORLD_FORCE, NebulaMod.Session.SunDirection * -Builder.CharacterWindForce * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS, null, null);
                    }
                    
                    if (MyAPIGateway.Session.IsServer)
                    {
                        if (NebulaMod.Static.PlayerDamageCounter == 0 && (Builder.RadiationCharacterDamage > 0 || Builder.CharacterWindForce != 0))
                        {
                            IHitInfo Hit;
                            Vector3D PlayerPosition = Character.GetHeadMatrix(false).Translation;

                            if (!MyAPIGateway.Physics.CastRay(PlayerPosition, PlayerPosition + (NebulaMod.Session.SunDirection * 1000), out Hit, CollisionLayers.CollisionLayerWithoutCharacter))
                            {
                                if (Builder.RadiationCharacterDamage > 0)
                                    Character.DoDamage(Builder.RadiationCharacterDamage, MyDamageType.Radioactivity, true);
                            }
                        }
                    }
                }
            }

            if (MyAPIGateway.Session.IsServer)
            {
                if (Builder.MinLightningFrequency != 0)
                {
                    NextLightning--;

                    if (NextLightning < 0)
                    {
                        NextLightning = MyUtils.GetRandomInt(Builder.MinLightningFrequency, Builder.MaxLightningFrequency);
                        NebulaMod.Static.CreateLightning(Position + (MyUtils.GetRandomVector3Normalized() * MyUtils.GetRandomFloat(0, Radius)), Builder.Name);
                    }
                }
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {
                bool InRadiation = false;
                if (Vector3D.Distance(NebulaMod.Session.PlayerPosition, Position) < Radius)
                {
                    if (Builder.RadiationCharacterDamage > 0)
                    {
                        IHitInfo Hit;
                        if (MyAPIGateway.Physics.CastRay(NebulaMod.Session.PlayerPosition, NebulaMod.Session.PlayerPosition + (NebulaMod.Session.SunDirection * 5000), out Hit, CollisionLayers.CollisionLayerWithoutCharacter))
                        {
                            if (NebulaMod.Static.DamageSound.IsPlaying)
                                NebulaMod.Static.DamageSound.StopSound(true);
                        }
                        else
                        {
                            InRadiation = true;
                            if (!NebulaMod.Static.DamageSound.IsPlaying)
                                NebulaMod.Static.DamageSound.PlaySound(NebulaData.GeigerSound);
                        }
                    }
                    if (Builder.DustAmount > 0)
                        NebulaMod.Static.DustAmount = Builder.DustAmount;

                    //AmbientVolume = (AmbientVolume + ((1f - AmbientVolume) * 0.01f))
                    if (Builder.DamageRadiationAmount > 0 && InRadiation)
                        NebulaMod.Static.RadiationPixelsAmount = (int)(NebulaMod.Static.RadiationPixelsAmount + ((Builder.DamageRadiationAmount - NebulaMod.Static.RadiationPixelsAmount) * 0.1f));
                    else if (Builder.AmbientRadiationAmount > 0)
                    {
                        NebulaMod.Static.RadiationPixelsAmount = (int)(NebulaMod.Static.RadiationPixelsAmount + ((Builder.AmbientRadiationAmount - NebulaMod.Static.RadiationPixelsAmount) * 0.1f));
                    }
                    else
                        NebulaMod.Static.RadiationPixelsAmount = (int)(NebulaMod.Static.RadiationPixelsAmount + ((0 - NebulaMod.Static.RadiationPixelsAmount) * 0.1f));
                }
            }
        }

        public virtual void Draw()
        {
            MatrixD Matrix = MatrixD.CreateTranslation(Position);
            Color White = Color.White;
            MySimpleObjectDraw.DrawTransparentSphere(ref Matrix, Radius, ref White, MySimpleObjectRasterizer.Wireframe, 8, null, null, 10);
            MyTransparentGeometry.AddLineBillboard(NebulaData.DebugMaterial, Vector4.One, StartPosition, Vector3.Normalize(Velocity), (float)(EndPosition - StartPosition).Length(), 10);

            if (Builder?.RadiationCharacterDamage > 0)
            {
                IHitInfo Hit;

                if (MyAPIGateway.Physics.CastRay(NebulaMod.Session.PlayerPosition, NebulaMod.Session.PlayerPosition + (NebulaMod.Session.SunDirection * 5000), out Hit, CollisionLayers.CollisionLayerWithoutCharacter))
                    MyTransparentGeometry.AddLineBillboard(NebulaData.DebugMaterial, Vector4.One, NebulaMod.Session.PlayerPosition, NebulaMod.Session.SunDirection, (float)(Hit.Position - NebulaMod.Session.PlayerPosition).Length(), 1);
                else
                    MyTransparentGeometry.AddLineBillboard(NebulaData.DebugMaterial, Vector4.One, NebulaMod.Session.PlayerPosition, NebulaMod.Session.SunDirection, 5000, 1);
            }
        }
    }

    [ProtoContract]
    public class WeatherBuilder
    {
        [ProtoMember(1)]
        public string Name;

        //public readonly MyFogProperties FogProperties;
        //public MyParticleEffect ParticleEffect;

        [ProtoMember(5)]
        public int MinLightningFrequency;

        [ProtoMember(6)]
        public int MaxLightningFrequency;

        [ProtoMember(7)]
        public LightningBuilder Lightning;

        [ProtoMember(10)]
        public float RadiationCharacterDamage;

        [ProtoMember(11)]
        public string AmbientSound;

        [ProtoIgnore, XmlIgnore]
        public MySoundPair AmbientSoundPair;

        [ProtoMember(15), Obsolete]
        public bool ForceDisableDampeners; //Unused

        [ProtoMember(16)]
        public bool DisableDampenersGrid;

        [ProtoMember(17)]
        public bool DisableDampenersCharacter;

        [ProtoMember(20)]
        public bool RenderIons;

        //[ProtoMember(21)]
        //public bool RenderComets; //TODO

        [ProtoMember(25)]
        public int AmbientRadiationAmount;
        [ProtoMember(26)]
        public int DamageRadiationAmount;

        [ProtoMember(30)]
        public string[] BlocksToDisable;

        [ProtoMember(35)]
        public string HudWarning;

        [ProtoMember(36)]
        public int Weight;

        [ProtoMember(40)]
        public float CharacterWindForce;

        [ProtoMember(41)]
        public float GridWindForce;

        [ProtoMember(45)]
        public int DustAmount;

        [ProtoMember(46)]
        public float GridDragForce;

        [ProtoMember(47)]
        public float CharacterDragForce;

        public void Init()
        {
            AmbientSoundPair = new MySoundPair(AmbientSound);
            for (int i = 0; i < Weight; i++)
            {
                NebulaMod.Static.WeatherRandomizer.Add(Name);
            }
        }
    }

    [ProtoContract]
    public class LightningBuilder
    {
        [ProtoMember(1)]
        public int MaxLife = 25;

        [ProtoMember(5)]
        public int BoltParts = 50;

        [ProtoMember(10)]
        public int BoltVariation = 100;

        [ProtoMember(15)]
        public int BoltRadius = 5;

        [ProtoMember(20)]
        public Vector4 Color = Vector4.One * 3;
    }
}