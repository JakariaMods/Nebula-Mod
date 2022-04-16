using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;
using Jakaria.Utils;
using Sandbox.ModAPI;
using static Jakaria.NebulaMod;
using Jakaria.Definitions;

namespace Jakaria.API
{
    public class NebulaBackend
    {
        public const int MinVersion = 1;
        public const ushort ModHandlerID = 13377;
        public const ushort ModHandlerIDWeather = 13378;

        private static readonly Dictionary<string, Delegate> ModAPIMethods = new Dictionary<string, Delegate>()
        {
            ["InsideNebulaBounding"] = new Func<Vector3D, bool>(InsideNebulaBounding),
            ["InsideNebula"] = new Func<Vector3D, bool>(InsideNebula),
            ["GetNebulaDensity"] = new Func<Vector3D, float>(GetNebulaDensity),
            ["VerifyVersion"] = new Func<int, string, bool>(VerifyVersion),
            ["GetMaterial"] = new Func<Vector3D, float>(GetMaterial),
            ["CreateLightning"] = new Action<Vector3D>(CreateLightning),
            ["CreateWeather"] = new Func<Vector3D, string, bool, bool>(CreateWeather),
            ["CreateWeatherDetailed"] = new Func<Vector3D, string, Vector3D, int, float, bool>(CreateWeatherDetailed),
            ["CreateRandomWeather"] = new Func<Vector3D, bool>(CreateRandomWeather),
            ["RemoveWeather"] = new Func<Vector3D, bool>(RemoveWeather),
            ["GetWeather"] = new Func<Vector3D, string>(GetWeather),
            ["ForceRenderRadiation"] = new Action<int?>(ForceRenderRadiation),
            ["ForceRenderIons"] = new Action<bool?>(ForceRenderIons),
            ["RunCommand"] = new Action<string>(RunCommand),

            //5
            ["ForceRenderComets"] = new Action<bool?>(ForceRenderComets),
            ["GetRandomWeather"] = new Func<string>(GetRandomWeather),
            ["IsNearWeather"] = new Func<Vector3D, bool>(IsNearWeather),

            //6
            ["CreateNebula"] = new Action<Vector3D, int>(CreateNebula),
        };

        private static void CreateNebula(Vector3D position, int radius)
        {
            if(MyAPIGateway.Session.IsServer)
                NebulaMod.Static.CreateNebula(position, radius);
        }

        private static bool IsNearWeather(Vector3D arg)
        {
            foreach (var Nebula in NebulaMod.Static.Nebulae)
            {
                if (Nebula.IsNearWeather(arg))
                    return true;
            }

            return false;
        }

        private static string GetRandomWeather()
        {
            return NebulaMod.Static.GetRandomWeather();
        }

        private static void ForceRenderComets(bool? obj)
        {
            NebulaMod.Static.RenderCometsOverride = obj;
        }

        private static bool CreateWeatherDetailed(Vector3D arg1, string arg2, Vector3D arg3, int arg4, float arg5)
        {
            if (MyAPIGateway.Session.IsServer)
                foreach (var Nebula in NebulaMod.Static.Nebulae)
                {
                    if (Nebula.IsInsideNebulaBounding(arg1))
                    {
                        Nebula.CreateWeatherDetailed(arg1, arg2, (Vector3)arg3, arg4, arg5);
                        return true;
                    }
                }

            return false;
        }

        private static void RunCommand(string obj)
        {
            bool sendToOthers = false;
            NebulaMod.Static.Utilities_MessageEntered(obj, ref sendToOthers);
        }

        private static void ForceRenderIons(bool? obj)
        {
            NebulaMod.Static.RenderIonsOverride = obj;
        }

        private static void ForceRenderRadiation(int? obj)
        {
            NebulaMod.Static.RadiationOverride = obj;
        }

        private static string GetWeather(Vector3D arg)
        {
            foreach (var Nebula in NebulaMod.Static.Nebulae)
            {
                return Nebula.GetClosestWeather(arg).Weather;
            }

            return null;
        }

        private static bool RemoveWeather(Vector3D arg)
        {
            bool foundWeather = false;

            if (MyAPIGateway.Session.IsServer)
            {
                foreach (var nebula in NebulaMod.Static.Nebulae)
                {
                    for (int i = nebula.SpaceWeathers.Count - 1; i >= 0; i--)
                    {
                        SpaceWeather Weather = nebula.SpaceWeathers[i];
                        if (Vector3D.Distance(Weather.Position, arg) < Weather.Radius)
                        {
                            nebula.SpaceWeathers.RemoveAtFast(i);
                            foundWeather = true;
                        }
                    }
                }

                if (foundWeather)
                    NebulaMod.Static.SyncToClients(NebulaPacketType.Nebulae);
            }

            return foundWeather;
        }

        private static bool CreateRandomWeather(Vector3D arg)
        {
            if (MyAPIGateway.Session.IsServer)
                foreach (var Nebula in NebulaMod.Static.Nebulae)
                {
                    if (Nebula.IsInsideNebulaBounding(arg))
                    {
                        Nebula.CreateRandomWeather(arg);
                        return true;
                    }
                }

            return false;
        }

        private static bool CreateWeather(Vector3D arg1, string arg2, bool arg3)
        {
            if (MyAPIGateway.Session.IsServer)
                foreach (var Nebula in NebulaMod.Static.Nebulae)
                {
                    if (Nebula.IsInsideNebulaBounding(arg1))
                    {
                        Nebula.CreateWeather(arg1, arg2, arg3);
                        return true;
                    }
                }

            return false;
        }

        private static void CreateLightning(Vector3D obj)
        {
            if (MyAPIGateway.Session.IsServer)
                NebulaMod.Static.CreateLightning(obj, "LightningStorm");
        }

        private static bool InsideNebulaBounding(Vector3D Position)
        {
            foreach (var nebula in NebulaMod.Static.Nebulae)
            {
                if (nebula.IsInsideNebulaBounding(Position))
                    return true;
            }

            return false;
        }

        private static bool InsideNebula(Vector3D Position)
        {
            foreach (var nebula in NebulaMod.Static.Nebulae)
            {
                if (nebula.IsInsideNebula(Position))
                    return true;
            }

            return false;
        }

        private static float GetNebulaDensity(Vector3D Position)
        {
            return (float)NebulaMod.Static.GetClosestNebula(Position).GetDepthRatio(Position);
        }

        private static bool VerifyVersion(int ModAPIVersion, string ModName)
        {
            if (ModAPIVersion < MinVersion)
            {
                JakUtils.ShowMessage("The Mod '" + ModName + "' is using an oudated Nebula Mod API, tell the author to update!");
                return false;
            }

            return true;

        }

        private static float GetMaterial(Vector3D Position)
        {
            foreach (var nebula in NebulaMod.Static.Nebulae)
            {
                if (nebula.IsInsideNebulaBounding(Position))
                    return MyMath.Clamp((float)nebula.Noise.GetNoise(Position.X * nebula.ColorNoiseScale, Position.Y * nebula.ColorNoiseScale, Position.Z * nebula.ColorNoiseScale), 0, 1);
            }

            return 0;
        }

        public static void BeforeStart()
        {
            MyAPIGateway.Utilities.SendModMessage(ModHandlerID, ModAPIMethods);
        }

        public static void LoadData()
        {
            MyAPIGateway.Utilities.RegisterMessageHandler(ModHandlerIDWeather, ModHandler);
        }

        public static void ModHandler(object obj)
        {

        }

        public static void UnloadData()
        {
            MyAPIGateway.Utilities.UnregisterMessageHandler(ModHandlerIDWeather, ModHandler);
        }
    }
}