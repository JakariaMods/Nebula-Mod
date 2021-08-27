﻿using Sandbox.Game;
using Sandbox.Game.Entities;
using Sandbox.ModAPI;
using System;
using System.Collections.Generic;
using VRage.Game;
using VRage.Game.Components;
using VRage.ModAPI;
using VRage.Utils;
using VRageMath;
using Jakaria.Utils;
using BlendTypeEnum = VRageRender.MyBillboard.BlendTypeEnum;
using System.Xml.Serialization;
using VRage.Game.ModAPI;
using Sandbox.Game.World;
using Jakaria.API;
using System.Linq;
using ProtoBuf;

namespace Jakaria
{
    [MySessionComponentDescriptor(MyUpdateOrder.AfterSimulation)]
    class NebulaMod : MySessionComponentBase
    {
        List<MyPlanet> Planets = new List<MyPlanet>();
        List<MyVoxelBase> Asteroids = new List<MyVoxelBase>();

        [XmlArray("Nebulae"), XmlArrayItem("Nebula")]
        public List<Nebula> Nebulae = new List<Nebula>();

        public static NebulaMod Static;

        public Dictionary<string, WeatherBuilder> WeatherBuilders = new Dictionary<string, WeatherBuilder>();
        public List<string> WeatherRandomizer = new List<string>();

        public List<Lightning> Lightnings = new List<Lightning>();
        public List<IMyPlayer> Players = new List<IMyPlayer>();

        public float AmbientVolume = 0;
        public MyEntity3DSoundEmitter AmbientSound = new MyEntity3DSoundEmitter(null);
        public MyEntity3DSoundEmitter DamageSound = new MyEntity3DSoundEmitter(null);

        public List<SimpleParticle> Ions = new List<SimpleParticle>();
        public List<SimpleParticle> Comets = new List<SimpleParticle>();
        public List<SimpleParticle> Dust = new List<SimpleParticle>();

        public int RadiationPixelsAmount = 0;
        public int DustAmount = 0;

        public int? RadiationOverride = null;
        public int? RenderDustOverride = null;
        public bool? RenderIonsOverride = null;
        public bool? RenderCometsOverride = null;
        public Vector3D IonSpeed = Vector3D.Forward;
        public Vector3D CometSpeed = Vector3D.Forward;
        public Vector3D DustSpeed = Vector3D.Right;

        public static class Session
        {
            public static Vector3D CameraPosition = Vector3D.Zero;
            public static Vector3D CameraRotation = Vector3D.Zero;
            public static Vector3 SunDirection = Vector3.Zero;
            public static bool InsideNebulaBounding = false;
            public static Vector3D PlayerPosition = Vector3D.Zero;
            public static Nebula ClosestNebula = null;
            public static MyPlanet ClosestPlanet = null;
            public static SpaceWeather ClosestSpaceWeather = null;
            public static float ClosestWeatherIntensity = 0;
            public static float NebulaDepthRatioRaw = 0f;
            public static float NebulaDepthRatio = 0f;
            public static bool PreviousInsideNebulaState = false;
            public static bool PreviousInsideWeatherState = false;
            public static bool DebugDraw = false;
        }

        public int PlayerDamageCounter = 0;
        public Vector3D LastTreeBuildPosition = Vector3D.MaxValue;

        public NebulaMod()
        {
            if (Static == null)
                Static = this;
        }

        public override void Init(MyObjectBuilder_SessionComponent sessionComponent)
        {
            WeatherBuilder WeatherBuilder;
            LightningBuilder LightningBuilder;

            WeatherBuilder = WeatherBuilders["LightningStorm"] = new WeatherBuilder();
            WeatherBuilder.Name = "LightningStorm";
            WeatherBuilder.MinLightningFrequency = 5;
            WeatherBuilder.MaxLightningFrequency = 30;
            WeatherBuilder.HudWarning = "Lightning Storm Inbound";
            WeatherBuilder.Weight = 2;
            LightningBuilder = WeatherBuilder.Lightning = new LightningBuilder();
            LightningBuilder.MaxLife = 25;
            LightningBuilder.BoltParts = 50;
            LightningBuilder.BoltVariation = 100;
            LightningBuilder.BoltRadius = 5;
            LightningBuilder.Color = Vector4.One * 3;
            WeatherBuilder.Init();

            WeatherBuilder = WeatherBuilders["RadiationStorm"] = new WeatherBuilder();
            WeatherBuilder.Name = "RadiationStorm";
            WeatherBuilder.RadiationCharacterDamage = 3;
            WeatherBuilder.AmbientSound = "JGeigerAmbient";
            WeatherBuilder.HudWarning = "Radiation Storm Inbound, Seek Shelter Immediately";
            WeatherBuilder.AmbientRadiationAmount = 5;
            WeatherBuilder.DamageRadiationAmount = 100;
            WeatherBuilder.Weight = 2;
            WeatherBuilder.Init();

            WeatherBuilder = WeatherBuilders["IonStorm"] = new WeatherBuilder();
            WeatherBuilder.Name = "IonStorm";
            WeatherBuilder.DisableDampenersCharacter = true;
            WeatherBuilder.DisableDampenersGrid = true;
            WeatherBuilder.RenderIons = true;
            WeatherBuilder.AmbientSound = "JIonInterference";
            WeatherBuilder.BlocksToDisable = new string[]
            {
            "MyObjectBuilder_JumpDrive"
            };
            WeatherBuilder.HudWarning = "Ion Storm Inbound, Electronics May Fail";
            WeatherBuilder.Weight = 1;
            WeatherBuilder.Init();

            WeatherBuilder = WeatherBuilders["DustStorm"] = new WeatherBuilder();
            WeatherBuilder.Name = "DustStorm";
            WeatherBuilder.DustAmount = 256;
            WeatherBuilder.GridDragForce = 75;
            WeatherBuilder.CharacterDragForce = 5;
            WeatherBuilder.AmbientSound = "JWindAmbient";
            WeatherBuilder.HudWarning = "Dust Storm Inbound, Expect Added Resistance";
            WeatherBuilder.Weight = 2;
            WeatherBuilder.Init();

            WeatherBuilder = WeatherBuilders["CometStorm"] = new WeatherBuilder();
            WeatherBuilder.Name = "CometStorm";
            WeatherBuilder.RenderComets = true;
            WeatherBuilder.AmbientSound = "JWindAmbient";
            WeatherBuilder.HudWarning = "Comet Storm Inbound, No Danger Detected";
            WeatherBuilder.Weight = 3;
            WeatherBuilder.Init();

            JakUtils.ShowMessage(NebulaTexts.NebulaModVersion.Replace("{0}", NebulaData.Version));

            MyAPIGateway.Utilities.MessageEntered += Utilities_MessageEntered;
            MyVisualScriptLogicProvider.PlayerConnected += PlayerConnected;
            MyAPIGateway.Multiplayer.RegisterMessageHandler(NebulaData.ClientHandlerID, ClientHandler);

            HashSet<IMyEntity> entities = new HashSet<IMyEntity>();

            MyAPIGateway.Entities.GetEntities(null, delegate (IMyEntity e)
            {
                OnEntityAdd(e);
                return false;
            });

            MyAPIGateway.Entities.OnEntityAdd += OnEntityAdd;

            entities.Clear();
        }

        public override void BeforeStart()
        {
            NebulaBackend.BeforeStart();
        }

        private void ClientHandler(byte[] packet)
        {
            if (packet == null)
                return;

            NebulaPacket DeserializedPacket = MyAPIGateway.Utilities.SerializeFromBinary<NebulaPacket>(packet);

            if (DeserializedPacket != null)
            {
                if (DeserializedPacket.Nebulae != null)
                {
                    Nebulae = DeserializedPacket.Nebulae;

                    if (MyAPIGateway.Session.IsServer)
                        SyncToClients(NebulaPacketType.Nebulae);

                    if (!MyAPIGateway.Utilities.IsDedicated)
                        foreach (var nebula in Nebulae)
                        {
                            nebula.BuildTree();
                        }
                }

                if (DeserializedPacket.Lightnings != null)
                {
                    Lightnings = DeserializedPacket.Lightnings;

                    if (MyAPIGateway.Session.IsServer)
                        SyncToClients(NebulaPacketType.Lightning);
                }
            }

            Players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players);
        }

        private void PlayerConnected(long playerId)
        {
            if (MyAPIGateway.Session.IsServer)
                SyncClient((ulong)playerId, NebulaPacketType.Both);

            Players.Clear();
            MyAPIGateway.Multiplayer.Players.GetPlayers(Players);
        }

        public void SyncToServer(NebulaPacketType packetType)
        {
            switch (packetType)
            {
                case NebulaPacketType.Nebulae:
                    MyAPIGateway.Multiplayer.SendMessageToServer(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae)));
                    break;
                case NebulaPacketType.Lightning:
                    MyAPIGateway.Multiplayer.SendMessageToServer(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Lightnings)));
                    break;
                case NebulaPacketType.Both:
                    MyAPIGateway.Multiplayer.SendMessageToServer(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae, Lightnings)));
                    break;
            }
        }

        public void SyncToClients(NebulaPacketType packetType)
        {
            if (MyAPIGateway.Session.IsServer)
                switch (packetType)
                {
                    case NebulaPacketType.Nebulae:
                        MyAPIGateway.Multiplayer.SendMessageToOthers(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae)));
                        break;
                    case NebulaPacketType.Lightning:
                        MyAPIGateway.Multiplayer.SendMessageToOthers(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Lightnings)));
                        break;
                    case NebulaPacketType.Both:
                        MyAPIGateway.Multiplayer.SendMessageToOthers(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae, Lightnings)));
                        break;
                }
        }

        private void SyncClient(ulong recipient, NebulaPacketType packetType)
        {
            if (MyAPIGateway.Session.IsServer)
                switch (packetType)
                {
                    case NebulaPacketType.Nebulae:
                        MyAPIGateway.Multiplayer.SendMessageTo(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae)), recipient);
                        break;
                    case NebulaPacketType.Lightning:
                        MyAPIGateway.Multiplayer.SendMessageTo(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Lightnings)), recipient);
                        break;
                    case NebulaPacketType.Both:
                        MyAPIGateway.Multiplayer.SendMessageTo(NebulaData.ClientHandlerID, MyAPIGateway.Utilities.SerializeToBinary(new NebulaPacket(Nebulae, Lightnings)), recipient);
                        break;
                }
        }

        protected override void UnloadData()
        {
            MyAPIGateway.Utilities.MessageEntered -= Utilities_MessageEntered;
            MyVisualScriptLogicProvider.PlayerConnected -= PlayerConnected;
            MyAPIGateway.Multiplayer.UnregisterMessageHandler(NebulaData.ClientHandlerID, ClientHandler);
            NebulaBackend.UnloadData();
        }

        public void Utilities_MessageEntered(string messageText, ref bool sendToOthers)
        {
            if (!messageText.StartsWith("/") || messageText.Length == 0)
                return;

            string[] args = messageText.TrimStart('/').Split(' ');

            if (args.Length == 0)
                return;

            switch (args[0])
            {
                case "nclear":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    foreach (var nebula in Nebulae)
                    {
                        if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                        {
                            bool foundWeather = false;

                            for (int i = nebula.SpaceWeathers.Count - 1; i >= 0; i--)
                            {
                                SpaceWeather Weather = nebula.SpaceWeathers[i];
                                double LineDistance = MyUtils.GetPointLineDistance(ref Weather.StartPosition, ref Weather.EndPosition, ref Session.CameraPosition);
                                if (LineDistance < Weather.Radius || Vector3D.Distance(Session.CameraPosition, Weather.Position) < Weather.Radius)
                                {
                                    nebula.SpaceWeathers.RemoveAtFast(i);
                                    foundWeather = true;
                                }
                            }

                            if (foundWeather)
                            {
                                JakUtils.ShowMessage(NebulaTexts.NebulaClear);
                                SyncToServer(NebulaPacketType.Nebulae);
                            }
                            else
                                JakUtils.ShowMessage(NebulaTexts.NoWeather);

                            break;
                        }
                    }

                    break;
                case "nsmite":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }
                    
                    IHitInfo Hit;
                    if (MyAPIGateway.Physics.CastRay(Session.CameraPosition + Session.CameraRotation, Session.CameraPosition + (Session.CameraRotation * 1000), out Hit))
                    {
                        Lightnings.Add(new Lightning(Hit.Position, Hit.Normal, "LightningStorm"));
                    }
                    break;
                case "nweather":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    if (args.Length == 2)
                        if (WeatherBuilders.ContainsKey(args[1]))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    if (nebula.CreateWeather(Session.CameraPosition, args[1], false))
                                        JakUtils.ShowMessage(NebulaTexts.NebulaSpawnWeather.Replace("{0}", args[1]));
                                    else
                                        JakUtils.ShowMessage(NebulaTexts.NebulaSpawnWeatherFail);

                                    break;
                                }
                            }
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseWeather.Replace("{0}", args[1]));
                    else if (Session.ClosestSpaceWeather != null)
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetWeather.Replace("{0}", Session.ClosestSpaceWeather.Weather));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NoWeather);

                    break;

                case "nrweather":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    foreach (var nebula in Nebulae)
                    {
                        if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                        {
                            if (nebula.CreateRandomWeather(Session.CameraPosition))
                                JakUtils.ShowMessage(NebulaTexts.NebulaSpawnRandomWeather);
                            else
                                JakUtils.ShowMessage(NebulaTexts.NebulaSpawnWeatherFail);
                            break;
                        }
                    }

                    break;

                case "nweatherlist":
                    sendToOthers = false;
                    if (Session.ClosestNebula != null)
                    {
                        string list = "";
                        string[] keys = WeatherBuilders.Keys.ToArray();
                        for (int i = 0; i < keys.Length; i++)
                        {
                            list = list + keys[i] + ((i < keys.Length - 1) ? ", " : ".");
                        }

                        JakUtils.ShowMessage(NebulaTexts.NebulaListWeather.Replace("{0}", list));
                    }
                    break;
                case "nversion":
                    sendToOthers = false;

                    JakUtils.ShowMessage(NebulaTexts.NebulaModVersion.Replace("{0}", NebulaData.Version));
                    break;

                case "ndebug":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    Session.DebugDraw = !Session.DebugDraw;

                    JakUtils.ShowMessage(NebulaTexts.NebulaToggleDebug);
                    break;

                case "ncreate":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula != null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoRoom);
                        break;
                    }

                    int tempRadius = 1000;

                    if (args.Length >= 2)
                        if (!int.TryParse(args[1], out tempRadius))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                            break;
                        }

                    Nebulae.Add(new Nebula(Session.CameraPosition, MyUtils.GetClampInt(tempRadius, 100, 1500), MyUtils.GetRandomInt(int.MaxValue)));
                    SyncToServer(NebulaPacketType.Nebulae);
                    JakUtils.ShowMessage(NebulaTexts.NebulaCreate);

                    break;

                case "nremove":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    for (int i = 0; i < Nebulae.Count; i++)
                    {
                        if (Nebulae[i].IsInsideNebulaBounding(Session.CameraPosition))
                        {
                            Nebulae.RemoveAtFast(i);
                            SyncToServer(NebulaPacketType.Nebulae);
                            JakUtils.ShowMessage(NebulaTexts.NebulaRemove);
                            break;
                        }
                    }

                    break;

                case "nfrequency":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    int tempMinFrequency = 1000;
                    int tempMaxFrequency = 1000;

                    if (args.Length == 3)
                    {
                        if (int.TryParse(args[1], out tempMinFrequency))
                        {
                            if (int.TryParse(args[1], out tempMaxFrequency))
                            {
                                foreach (var nebula in Nebulae)
                                {
                                    if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                    {
                                        nebula.MinWeatherFrequency = tempMinFrequency * 60;
                                        nebula.MaxWeatherFrequency = tempMaxFrequency * 60;
                                    }
                                }

                                SyncToServer(NebulaPacketType.Nebulae);

                                JakUtils.ShowMessage(NebulaTexts.NebulaSetFrequency.Replace("{0}", tempMinFrequency.ToString()).Replace("{1}", tempMaxFrequency.ToString()));
                            }
                            else
                                JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                    }
                    else if (args.Length == 1)
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetFrequency.Replace("{0}", (Session.ClosestNebula.MinWeatherFrequency / 60).ToString()).Replace("{1}", (Session.ClosestNebula.MaxWeatherFrequency / 60).ToString()));
                    else
                        JakUtils.ShowMessage(NebulaTexts.ExpectedParameters2);

                    break;

                case "nlength":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    int tempMinLength = 100;
                    int tempMaxLength = 1000;

                    if (args.Length == 3)
                    {
                        if (int.TryParse(args[1], out tempMinLength))
                        {
                            if (int.TryParse(args[2], out tempMaxLength))
                            {
                                foreach (var nebula in Nebulae)
                                {
                                    if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                    {
                                        nebula.MinWeatherLength = tempMinLength * 60;
                                        nebula.MaxWeatherLength = tempMaxLength * 60;
                                    }
                                }

                                SyncToServer(NebulaPacketType.Nebulae);

                                JakUtils.ShowMessage(NebulaTexts.NebulaSetLength.Replace("{0}", tempMinLength.ToString()).Replace("{1}", tempMaxLength.ToString()));
                            }
                            else
                                JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                    }
                    else if (args.Length == 1)
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetLength.Replace("{0}", (Session.ClosestNebula.MinWeatherLength / 60).ToString()).Replace("{1}", (Session.ClosestNebula.MaxWeatherLength / 60).ToString()));
                    else
                        JakUtils.ShowMessage(NebulaTexts.ExpectedParameters2);

                    break;

                case "ndensity":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    float tempDensity = 0.75f;

                    if (args.Length == 2)
                        if (float.TryParse(args[1], out tempDensity))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    nebula.NoiseRatioCutoff = 1f - MyMath.Clamp(tempDensity, 0f, 1f);
                                    nebula.BuildTree();
                                }
                            }

                            SyncToServer(NebulaPacketType.Nebulae);

                            JakUtils.ShowMessage(NebulaTexts.NebulaSetDensity.Replace("{0}", MyMath.Clamp(tempDensity, 0f, 1f).ToString()));
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[1]));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetDensity.Replace("{0}", (1f - Session.ClosestNebula.NoiseRatioCutoff).ToString()));
                    break;

                case "nseed":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }


                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    int tempSeed;

                    if (args.Length == 2)
                        if (int.TryParse(args[1], out tempSeed))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    nebula.Seed = tempSeed;
                                    nebula.BuildTree();
                                }
                            }

                            JakUtils.ShowMessage(NebulaTexts.NebulaSetSeed.Replace("{0}", tempSeed.ToString()));
                            SyncToServer(NebulaPacketType.Nebulae);
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetSeed.Replace("{0}", Session.ClosestNebula.Seed.ToString()));

                    break;
                case "nscale":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    float tempScale;

                    if (args.Length == 2)
                        if (float.TryParse(args[1], out tempScale))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    nebula.NoiseScale = MyMath.Clamp(tempScale, 0f, 1f);
                                    nebula.BuildTree();
                                }
                            }

                            JakUtils.ShowMessage(NebulaTexts.NebulaSetScale.Replace("{0}", MyMath.Clamp(tempScale, 0f, 1f).ToString()));
                            SyncToServer(NebulaPacketType.Nebulae);
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[1]));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetScale.Replace("{0}", Session.ClosestNebula.NoiseScale.ToString()));

                    break;
                case "ncolorscale":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    float tempColorScale;

                    if (args.Length == 2)
                        if (float.TryParse(args[1], out tempColorScale))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    nebula.ColorNoiseScale = MyMath.Clamp(tempColorScale, 0f, 1f);
                                    nebula.BuildTree();
                                }
                            }

                            JakUtils.ShowMessage(NebulaTexts.NebulaSetColorScale.Replace("{0}", MyMath.Clamp(tempColorScale, 0f, 1f).ToString()));
                            SyncToServer(NebulaPacketType.Nebulae);
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[1]));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetColorScale.Replace("{0}", Session.ClosestNebula.ColorNoiseScale.ToString()));
                    break;
                case "nprimary":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    Vector4 tempPrimaryColor = new Vector4(0, 0, 0, 0.75f);

                    if (args.Length >= 4)
                    {
                        //Red
                        if (!float.TryParse(args[1], out tempPrimaryColor.X))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[1]));
                            break;
                        }

                        //Green
                        if (!float.TryParse(args[2], out tempPrimaryColor.Y))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[2]));
                            break;
                        }

                        //Blue
                        if (!float.TryParse(args[3], out tempPrimaryColor.Z))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[3]));
                            break;
                        }

                        if (args.Length >= 5)
                        {
                            //Alpha
                            if (!float.TryParse(args[4], out tempPrimaryColor.W))
                            {
                                JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[4]));
                                break;
                            }
                        }

                        if (tempPrimaryColor.X > 1 || tempPrimaryColor.Y > 1 || tempPrimaryColor.Z > 1)
                            tempPrimaryColor = new Vector4(tempPrimaryColor.X / 255f, tempPrimaryColor.Y / 255f, tempPrimaryColor.Z / 255f, tempPrimaryColor.W);

                        foreach (var nebula in Nebulae)
                        {
                            if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                            {
                                nebula.PrimaryColor = tempPrimaryColor;
                                nebula.BuildTree();
                            }
                        }

                        JakUtils.ShowMessage(NebulaTexts.NebulaSetPrimaryColor);

                        SyncToServer(NebulaPacketType.Nebulae);
                    }
                    else if (args.Length == 1)
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetPrimaryColor.Replace("{0}", Session.ClosestNebula.PrimaryColor.ToString()));
                    else
                        JakUtils.ShowMessage(NebulaTexts.ExpectedParameters4);

                    break;
                case "nsecondary":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    Vector4 tempSecondaryColor = new Vector4(0, 0, 0, 0.75f);

                    if (args.Length >= 4)
                    {
                        //Red
                        if (!float.TryParse(args[1], out tempSecondaryColor.X))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[1]));
                            break;
                        }

                        //Green
                        if (!float.TryParse(args[2], out tempSecondaryColor.Y))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[2]));
                            break;
                        }

                        //Blue
                        if (!float.TryParse(args[3], out tempSecondaryColor.Z))
                        {
                            JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[3]));
                            break;
                        }

                        if (args.Length >= 5)
                        {
                            //Alpha
                            if (!float.TryParse(args[4], out tempSecondaryColor.W))
                            {
                                JakUtils.ShowMessage(NebulaTexts.NoParseFloat.Replace("{0}", args[4]));
                                break;
                            }
                        }

                        foreach (var nebula in Nebulae)
                        {
                            if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                            {
                                nebula.SecondaryColor = tempSecondaryColor;
                                nebula.BuildTree();
                            }
                        }

                        JakUtils.ShowMessage(NebulaTexts.NebulaSetSecondaryColor);

                        SyncToServer(NebulaPacketType.Nebulae);
                    }
                    else if (args.Length == 1)
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetSecondaryColor.Replace("{0}", Session.ClosestNebula.SecondaryColor.ToString()));
                    else
                        JakUtils.ShowMessage(NebulaTexts.ExpectedParameters4);

                    break;
                case "nradius":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    int tempRadius2;

                    if (args.Length == 2)
                        if (int.TryParse(args[1], out tempRadius2))
                        {
                            foreach (var nebula in Nebulae)
                            {
                                if (nebula.IsInsideNebulaBounding(Session.CameraPosition))
                                {
                                    nebula.Radius = MyUtils.GetClampInt(tempRadius2, 100, 1500);
                                    nebula.BuildTree();
                                }
                            }

                            JakUtils.ShowMessage(NebulaTexts.NebulaSetRadius.Replace("{0}", tempRadius2.ToString()));
                            SyncToServer(NebulaPacketType.Nebulae);
                        }
                        else
                            JakUtils.ShowMessage(NebulaTexts.NoParseInt.Replace("{0}", args[1]));
                    else
                        JakUtils.ShowMessage(NebulaTexts.NebulaGetRadius.Replace("{0}", Session.ClosestNebula.Radius.ToString()));

                    break;
                case "nreset":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    for (int i = 0; i < Nebulae.Count; i++)
                    {
                        if (Nebulae[i].IsInsideNebulaBounding(Session.CameraPosition))
                        {
                            Nebulae[i] = new Nebula(Nebulae[i].Position, Nebulae[i].Radius, Nebulae[i].Seed);
                            Nebulae[i].BuildTree();
                        }
                    }

                    JakUtils.ShowMessage(NebulaTexts.NebulaReset);
                    SyncToServer(NebulaPacketType.Nebulae);

                    break;
                case "nregen":
                    sendToOthers = false;

                    if (!MyAPIGateway.Session.HasCreativeRights)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoPermissions);
                        break;
                    }

                    if (Session.ClosestNebula == null)
                    {
                        JakUtils.ShowMessage(NebulaTexts.NoNebula);
                        break;
                    }

                    for (int i = 0; i < Nebulae.Count; i++)
                    {
                        if (Nebulae[i].IsInsideNebulaBounding(Session.CameraPosition))
                        {
                            Nebulae[i] = new Nebula(Nebulae[i].Position, Nebulae[i].Radius, MyUtils.GetRandomInt(int.MaxValue));
                            Nebulae[i].BuildTree();
                        }
                    }

                    JakUtils.ShowMessage(NebulaTexts.NebulaRegen);
                    SyncToServer(NebulaPacketType.Nebulae);

                    break;
            }
        }

        public void CreateLightning(Vector3D position, string builderId)
        {
            Lightnings.Add(new Lightning(position, MyUtils.GetRandomVector3Normalized(), builderId));
            SyncToClients(NebulaPacketType.Lightning);
        }

        private void OnEntityAdd(IMyEntity obj)
        {
            if (obj is MyPlanet)
                Planets.Add(obj as MyPlanet);
        }

        public override void UpdateAfterSimulation()
        {
            Session.SunDirection = MyVisualScriptLogicProvider.GetSunDirection();

            if (MyAPIGateway.Session.IsServer)
            {
                PlayerDamageCounter++;

                if (PlayerDamageCounter >= 60)
                {
                    PlayerDamageCounter = 0;
                }
            }

            if (!MyAPIGateway.Utilities.IsDedicated)
            {

                if (MyAPIGateway.Session?.Player != null)
                {
                    Session.PlayerPosition = MyAPIGateway.Session.Player?.Character?.GetHeadMatrix(false).Translation ?? Session.CameraPosition;
                }

                if (Session.ClosestNebula != null)
                {
                    if (Session.ClosestSpaceWeather?.Builder != null)
                    {
                        IonSpeed = Session.SunDirection * 100 * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                        CometSpeed = Session.ClosestSpaceWeather.Direction * 600 * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;
                        DustSpeed = Session.ClosestSpaceWeather.Direction * 10 * MyEngineConstants.PHYSICS_STEP_SIZE_IN_SECONDS;

                        if (Session.ClosestSpaceWeather.Builder.RenderIons || RenderIonsOverride == true)
                        {
                            if (Ions.Count < 64 && Session.ClosestPlanet == null)
                                Ions.Add(new SimpleParticle(Session.CameraPosition + (MyUtils.GetRandomVector3() * MyUtils.GetRandomFloat(1, 200)), 25));
                        }

                        if (Session.ClosestSpaceWeather.Builder.RenderComets || RenderCometsOverride == true)
                        {
                            if (Comets.Count < 256)
                                Comets.Add(new SimpleParticle(Session.CameraPosition + (MyUtils.GetRandomVector3() * MyUtils.GetRandomFloat(2000, 3000)) + (CometSpeed * 128), 256));
                        }

                        if (Session.ClosestSpaceWeather.Builder.DustAmount > 0 || RenderDustOverride != 0)
                        {
                            if (Dust.Count < ((RenderDustOverride != null) ? RenderDustOverride : DustAmount) && Session.ClosestPlanet == null)
                                Dust.Add(new SimpleParticle(Session.CameraPosition + (MyUtils.GetRandomVector3() * MyUtils.GetRandomFloat(5, 50)), 100));
                        }

                        if (Session.ClosestSpaceWeather.Builder?.AmbientSoundPair != null)
                        {
                            if (AmbientSound == null)
                                AmbientSound = new MyEntity3DSoundEmitter(null);
                            if (DamageSound == null)
                                DamageSound = new MyEntity3DSoundEmitter(null);

                            if (!AmbientSound.IsPlaying || AmbientSound.SoundPair != Session.ClosestSpaceWeather.Builder.AmbientSoundPair)
                            {
                                if (AmbientSound.SoundPair != Session.ClosestSpaceWeather.Builder.AmbientSoundPair)
                                    AmbientVolume = 0;

                                AmbientSound.VolumeMultiplier = AmbientVolume;
                                AmbientSound.PlaySound(Session.ClosestSpaceWeather.Builder.AmbientSoundPair, stopPrevious: true, alwaysHearOnRealistic: true, force2D: true);
                            }

                            AmbientVolume = (AmbientVolume + ((1f - AmbientVolume) * 0.01f)) * Math.Min(1.5f - (Vector3.Distance(Session.CameraPosition, Session.ClosestSpaceWeather.Position) / Session.ClosestSpaceWeather.Radius), 1);
                            AmbientSound.VolumeMultiplier = AmbientVolume;
                        }
                        else if (AmbientSound.IsPlaying)
                            AmbientSound.StopSound(true);
                    }
                    else
                    {
                        AmbientVolume = 0;
                        if (AmbientSound.IsPlaying)
                            AmbientSound.StopSound(true);
                        if (DamageSound.IsPlaying)
                            DamageSound.StopSound(true);
                    }
                }

                if (Ions != null)
                    for (int i = Ions.Count - 1; i >= 0; i--)
                    {
                        SimpleParticle Ion = Ions[i];

                        if (Ion == null || Ion.Life <= 0)
                        {
                            Ions.RemoveAtFast(i);
                            continue;
                        }

                        Ion.Life--;
                        Ion.Position -= IonSpeed;
                    }
                if (Comets != null)
                    for (int i = Comets.Count - 1; i >= 0; i--)
                    {
                        SimpleParticle Comet = Comets[i];

                        if (Comet == null || Comet.Life <= 0)
                        {
                            Comets.RemoveAtFast(i);
                            continue;
                        }

                        Comet.Life--;
                        Comet.Position -= CometSpeed;
                    }
                if (Dust != null)
                    for (int i = Dust.Count - 1; i >= 0; i--)
                    {
                        SimpleParticle dust = Dust[i];

                        if (dust == null || dust.Life <= 0)
                        {
                            Dust.RemoveAtFast(i);
                            continue;
                        }

                        dust.Life--;
                        dust.Position -= DustSpeed;
                    }
            }

            if (Nebulae != null)
                foreach (var Nebula in Nebulae)
                {
                    Nebula.Simulate();
                }

            if (Lightnings != null)
                for (int i = Lightnings.Count - 1; i >= 0; i--)
                {
                    Lightning Lightning = Lightnings[i];

                    if (Lightning == null || Lightning.Life > 25)
                    {
                        Lightning?.PlaySound();
                        Lightnings.RemoveAtFast(i);
                        continue;
                    }

                    if (Lightning.Builder == null)
                        Lightning.Init();

                    Lightning.Simulate();
                }
        }

        public Nebula GetClosestNebula(Vector3D Position)
        {
            foreach (var nebula in Nebulae)
            {
                if (nebula.IsInsideNebulaBounding(Position))
                {
                    return nebula;
                }
            }
            return null;
        }

        public override void Draw()
        {
            Session.CameraPosition = MyAPIGateway.Session.Camera.WorldMatrix.Translation;
            Session.CameraRotation = MyAPIGateway.Session.Camera.WorldMatrix.Forward;

            Session.ClosestPlanet = MyGamePruningStructure.GetClosestPlanet(Session.CameraPosition);
            Session.ClosestNebula = GetClosestNebula(Session.CameraPosition);
            Session.ClosestSpaceWeather = Session.ClosestNebula?.GetClosestWeather(Session.CameraPosition);
            Session.InsideNebulaBounding = false;
            Session.NebulaDepthRatioRaw = 0;

            Session.ClosestWeatherIntensity = MyAPIGateway.Session.WeatherEffects.GetWeatherIntensity(Session.CameraPosition);

            if (Nebulae != null)
            {
                if ((LastTreeBuildPosition - Session.CameraPosition).AbsMax() > 50)
                {
                    BoundingSphereD sphere = new BoundingSphereD(MyAPIGateway.Session.Camera.Position, 30000);
                    Asteroids.Clear();
                    MyGamePruningStructure.GetAllVoxelMapsInSphere(ref sphere, Asteroids);

                    LastTreeBuildPosition = Session.CameraPosition;
                    foreach (var nebula in Nebulae)
                    {
                        nebula.BuildTree();
                    }
                }

                foreach (var nebula in Nebulae)
                {
                    nebula.Draw();
                }
            }

            if (Lightnings != null)
                foreach (var Lightning in Lightnings)
                {
                    if (Lightning.Builder != null)
                        Lightning.Draw();
                }

            if (Session.ClosestSpaceWeather != null)
            {
                Vector4 IonColor = Vector4.One * 0.04f;
                foreach (var Ion in Ions)
                {
                    MyTransparentGeometry.AddLineBillboard(NebulaData.FlareMaterial, IonColor * ((25f - Ion.Life) / 25f), Ion.Position, Session.SunDirection, 100, 10);
                }

                foreach (var Comet in Comets)
                {
                    MyTransparentGeometry.AddLineBillboard(NebulaData.CometMaterial, NebulaData.CometColor * (1f - (Math.Abs(Comet.Life - 128f) / 128f)), Comet.Position, Session.ClosestSpaceWeather.Direction, 100, 6.25f);
                }

                Vector4 DustColor = Vector4.One * 0.75f;
                foreach (var dust in Dust)
                {
                    MyTransparentGeometry.AddPointBillboard(NebulaData.FlareMaterial, DustColor, dust.Position, 0.05f, 0);
                }

                for (int i = 0; i < (RadiationOverride == null ? RadiationPixelsAmount : RadiationOverride); i++)
                {
                    MyTransparentGeometry.AddPointBillboard(NebulaData.FlareMaterial, Vector4.One, Session.CameraPosition + (MyUtils.GetRandomVector3() * MyUtils.GetRandomFloat(1, 5)), 0.01f, 0, blendType: BlendTypeEnum.AdditiveTop);
                }
            }

            double tempDot = Math.Abs(Session.CameraRotation.Dot(Session.SunDirection));
            Vector4 tempShadowColor = new Vector4(NebulaData.ShadowColor.X, NebulaData.ShadowColor.X, NebulaData.ShadowColor.X, (float)(NebulaData.ShadowColor.W * (1f - (Math.Max(tempDot - 0.95f, 0) / 0.05f))));

            if (Session.ClosestNebula != null)
            {
                Session.InsideNebulaBounding = Session.ClosestNebula.IsInsideNebulaBounding(Session.CameraPosition);

                Session.NebulaDepthRatioRaw = Session.ClosestNebula.GetDepthRatio(Session.CameraPosition);
                if (Session.ClosestNebula.NoiseRatioCutoff != 0)
                {
                    Session.NebulaDepthRatio = MyMath.Clamp((Session.NebulaDepthRatioRaw - Session.ClosestNebula.NoiseRatioCutoff) / Session.ClosestNebula.NoiseRatioCutoff, 0, 1f);
                }

                if (Session.InsideNebulaBounding)
                {
                    if (Session.NebulaDepthRatioRaw > Session.ClosestNebula.NoiseRatioCutoff)
                    {

                        float tempScaledIntensity = 0.1f * Session.NebulaDepthRatio;

                        if (Session.ClosestWeatherIntensity < 0.05f)
                        {
                            MyAPIGateway.Session.WeatherEffects.SunIntensityOverride = Math.Max(100f - (Session.NebulaDepthRatio * 200f), 50);
                            MyAPIGateway.Session.WeatherEffects.FogDensityOverride = tempScaledIntensity;
                            MyAPIGateway.Session.WeatherEffects.FogMultiplierOverride = tempScaledIntensity;
                            MyAPIGateway.Session.WeatherEffects.FogAtmoOverride = tempScaledIntensity;
                            MyAPIGateway.Session.WeatherEffects.FogSkyboxOverride = tempScaledIntensity;
                            MyAPIGateway.Session.WeatherEffects.FogColorOverride = Session.ClosestNebula.GetColor(Session.CameraPosition);
                        }
                    }

                    //Asteroid Shadows
                    foreach (var asteroid in Asteroids)
                    {
                        if (asteroid is MyPlanet || asteroid.StorageName?[0] == '|' || (asteroid.StorageName?[0] == 'P' && asteroid.StorageName?[1] == '(') || Session.CameraRotation.Dot(asteroid.PositionComp.GetPosition() - Session.CameraPosition) <= 0 || !Session.ClosestNebula.IsInsideNebula(asteroid.PositionComp.GetPosition()))
                            continue;

                        float tempRadius = asteroid.PositionComp.LocalVolume.Radius / 2;

                        float tempDistance = Vector3.Distance(Session.CameraPosition, asteroid.PositionComp.GetPosition());
                        if (tempDistance - tempRadius > tempRadius)
                            MyTransparentGeometry.AddLineBillboard(NebulaData.ShadowTextures[0], tempShadowColor, asteroid.PositionComp.GetPosition() + (Session.SunDirection * tempRadius), -Session.SunDirection, tempRadius * 8, tempRadius, BlendTypeEnum.LDR);
                        else
                            MyTransparentGeometry.AddLineBillboard(NebulaData.ShadowTextures[0], new Vector4(NebulaData.ShadowColor.X, NebulaData.ShadowColor.Y, NebulaData.ShadowColor.Z, NebulaData.ShadowColor.W * (Math.Min(tempDistance - tempRadius, tempRadius) / tempRadius)), asteroid.PositionComp.GetPosition() + (Session.SunDirection * tempRadius), -Session.SunDirection, tempRadius * 8, tempRadius, BlendTypeEnum.LDR);
                    }
                }
            }

            //Planet Shadows
            foreach (var planet in Planets)
            {
                foreach (var nebula in Nebulae)
                {
                    if (!nebula.IsInsideNebula(planet.PositionComp.GetPosition()))
                        continue;

                    int ShadowLength;

                    if (nebula.IsInsideNebula(planet.PositionComp.GetPosition() + (Session.SunDirection * (planet.MinimumRadius * 9))))
                        ShadowLength = 0;
                    //else if (nebula.IsInsideNebula(planet.PositionComp.GetPosition() + (Session.SunDirection * (planet.MinimumRadius * 5))))
                    //ShadowLength = 1;
                    else
                        ShadowLength = 2;

                    float tempDistance = Vector3.Distance(Session.CameraPosition, planet.PositionComp.GetPosition());
                    if (tempDistance - planet.MinimumRadius > planet.MinimumRadius)
                        MyTransparentGeometry.AddLineBillboard(NebulaData.ShadowTextures[ShadowLength], tempShadowColor, planet.PositionComp.GetPosition() + (Session.SunDirection * planet.MinimumRadius), -Session.SunDirection, planet.MinimumRadius * 8, planet.MinimumRadius, BlendTypeEnum.LDR);
                    else
                        MyTransparentGeometry.AddLineBillboard(NebulaData.ShadowTextures[ShadowLength], new Vector4(NebulaData.ShadowColor.X, NebulaData.ShadowColor.Y, NebulaData.ShadowColor.Z, NebulaData.ShadowColor.W * (Math.Min(tempDistance - planet.MinimumRadius, planet.MinimumRadius) / planet.MinimumRadius)), planet.PositionComp.GetPosition() + (Session.SunDirection * planet.MinimumRadius), -Session.SunDirection, planet.MinimumRadius * 8, planet.MinimumRadius, BlendTypeEnum.LDR);
                }
            }
            if (Session.PreviousInsideNebulaState != Session.InsideNebulaBounding)
            {
                Session.PreviousInsideNebulaState = Session.InsideNebulaBounding;

                if (!Session.InsideNebulaBounding)
                {
                    MyAPIGateway.Session.WeatherEffects.SunIntensityOverride = null;
                    MyAPIGateway.Session.WeatherEffects.FogMultiplierOverride = null;
                    MyAPIGateway.Session.WeatherEffects.FogColorOverride = null;
                    MyAPIGateway.Session.WeatherEffects.FogAtmoOverride = null;
                    MyAPIGateway.Session.WeatherEffects.FogSkyboxOverride = null;
                    MyAPIGateway.Session.WeatherEffects.FogDensityOverride = null;

                    Session.NebulaDepthRatioRaw = 0f;
                }
            }
        }

        public override void LoadData()
        {
            string packet;
            MyAPIGateway.Utilities.GetVariable("JNebula2", out packet);

            if (packet == null)
                return;

            Nebulae = MyAPIGateway.Utilities.SerializeFromXML<List<Nebula>>(packet);
            NebulaBackend.LoadData();
        }

        public override void SaveData()
        {
            MyAPIGateway.Utilities.SetVariable("JNebula2", MyAPIGateway.Utilities.SerializeToXML(Nebulae));
        }

        public string GetRandomWeather()
        {
            return WeatherRandomizer[MyUtils.GetRandomInt(0, WeatherBuilders.Count)];
        }

        public bool GetRandomWeather(out string weather)
        {
            if (WeatherRandomizer.Count > 0)
            {
                weather = WeatherRandomizer[MyUtils.GetRandomInt(0, WeatherBuilders.Count)];
                return true;
            }

            weather = null;
            return false;
        }

        [ProtoContract]
        public class NebulaPacket
        {
            [ProtoMember(1)]
            public List<Lightning> Lightnings;
            [ProtoMember(5)]
            public List<Nebula> Nebulae;

            public NebulaPacket()
            {

            }

            public NebulaPacket(List<Lightning> lightnings)
            {
                Lightnings = lightnings;
            }

            public NebulaPacket(List<Nebula> nebulae)
            {
                Nebulae = nebulae;
            }

            public NebulaPacket(List<Nebula> nebulae, List<Lightning> lightnings)
            {
                Nebulae = nebulae;
                Lightnings = lightnings;
            }
        }

        public enum NebulaPacketType
        {
            Nebulae,
            Lightning,
            Both
        }
    }
}
