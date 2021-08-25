using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;
using Sandbox.Game.Entities;
using Sandbox.Game;
using Sandbox.ModAPI;
using VRage.Utils;
using Sandbox.Definitions;
using VRage.Game;
using VRage.Game.Entity;
using System.Diagnostics;
using VRage.Game.ModAPI;
using VRageRender;
using VRage.Game.ModAPI.Ingame.Utilities;

namespace Jakaria.Utils
{
    public static class JakUtils
    {
        public static string ModName = MyAPIGateway.Utilities.GamePaths.ModScopeName.Split('_')[1];

        /// <summary>
        /// Returns a vector perpendicular to a vector, takes an angle
        /// </summary>
        public static Vector3D GetPerpendicularVector(Vector3D vector, double angle)
        {
            Vector3D perpVector = Vector3D.CalculatePerpendicularVector(Vector3.Normalize(vector));
            Vector3D bitangent; Vector3D.Cross(ref vector, ref perpVector, out bitangent);
            return Vector3D.Normalize(Math.Cos(angle) * perpVector + Math.Sin(angle) * bitangent);
        }

        /// <summary>
        /// Turns certain special characters into an xml compatible string
        /// </summary>
        public static string ValidateXMLData(string input)
        {
            input = input.Replace("<", "&lt;");
            input = input.Replace(">", "&gt;");
            return input;
        }

        /// <summary>
        /// Returns how far a position is into the night on a planet
        /// </summary>
        public static float GetNightValue(MyPlanet planet, Vector3 position)
        {
            if (planet == null)
                return 0;

            return Vector3.Dot(MyVisualScriptLogicProvider.GetSunDirection(), Vector3.Normalize(position - planet.PositionComp.GetPosition()));
        }

        /// <summary>
        /// Sends a chat message using the mod's assembly name as the sender, not synced
        /// </summary>
        public static void ShowMessage(string message)
        {
            MyAPIGateway.Utilities.ShowMessage(ModName, message);
        }

        /// <summary>
        /// Sends a notification, not synced
        /// </summary>
        public static void ShowNotification(string message, int time = 2000, string font = "White")
        {
            MyAPIGateway.Utilities.ShowNotification(message, time, font);
        }

        /// <summary>
        /// Sends a chat message using WaterMod as the sender, not synced
        /// </summary>
        public static void ShowNotification(object message, int time = 2000)
        {
            MyAPIGateway.Utilities.ShowNotification(message.ToString(), time);
        }

        /// <summary>
        /// Sends a chat message using WaterMod as the sender, not synced
        /// </summary>
        public static void ShowMessage(object message)
        {
            MyAPIGateway.Utilities.ShowMessage(ModName, message.ToString());
        }

        public static void WriteLog(string message)
        {
            MyLog.Default.WriteLine("WaterMod: " + message);
        }

        /// <summary>
        /// Removes brackets to help players parse their commands if for some reason they put them
        /// </summary>
        public static string ValidateCommandData(string input)
        {
            input = input.Replace("[", "");
            input = input.Replace("]", "");
            return input;
        }

        /// <summary>
        /// Returns a random meteor material name
        /// </summary>
        public static string GetRandomMeteorMaterial()
        {
            MyVoxelMaterialDefinition material = null;
            int tries = MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Count() * 2; // max amount of tries

            while (material == null || !material.IsRare || !material.SpawnsFromMeteorites)
            {
                if (--tries < 0) // to prevent infinite loops in case all materials are disabled just use the meteorites' initial material
                {
                    return "Stone";
                }
                material = MyDefinitionManager.Static.GetVoxelMaterialDefinitions().ElementAt(MyUtils.GetRandomInt(MyDefinitionManager.Static.GetVoxelMaterialDefinitions().Count() - 1));
            }
            string materialName = material.MinedOre;

            if (materialName == null)
                materialName = "Stone";

            return materialName;
        }

        /// <summary>
        /// Checks if a position is airtight, SLOW!
        /// </summary>
        public static bool IsPositionAirtight(Vector3 position)
        {
            if (!MyAPIGateway.Session.SessionSettings.EnableOxygenPressurization)
                return false;

            BoundingSphereD sphere = new BoundingSphereD(position, 5);
            List<MyEntity> entities = new List<MyEntity>();
            MyGamePruningStructure.GetAllTopMostEntitiesInSphere(ref sphere, entities);

            foreach (var entity in entities)
            {
                MyCubeGrid grid = entity as MyCubeGrid;

                if (grid != null)
                {
                    if (grid.IsRoomAtPositionAirtight(grid.WorldToGridInteger(position)))
                    {
                        return true;
                    }
                }
            }
            return false;
        }

        /// <summary>
        /// Checks if a position on a planet is underground
        /// </summary>
        public static bool IsUnderGround(this MyPlanet planet, Vector3D position, double altitudeOffset = 0)
        {
            double altitude = (position - planet.WorldMatrix.Translation).Length() + altitudeOffset;

            if (altitude < planet.MinimumRadius)
                return true;

            if (altitude > planet.MaximumRadius)
                return false;

            if ((altitude - (planet.GetClosestSurfacePointGlobal(position) - planet.WorldMatrix.Translation).Length()) < 0)
                return true;

            return false;
        }

        /// <summary>
        /// Checks if a player is in a floating state
        /// </summary>
        public static bool IsPlayerFloating(IMyCharacter player)
        {
            return IsPlayerStateFloating(player.CurrentMovementState);
        }

        /// <summary>
        /// Checks if a player is in a floating/jetpack state
        /// </summary>
        public static bool IsPlayerStateFloating(MyCharacterMovementEnum state)
        {
            return state == MyCharacterMovementEnum.Falling || state == MyCharacterMovementEnum.Jump || state == MyCharacterMovementEnum.Flying;
        }

        /// <summary>
        /// Converts an inputted HSV to Color
        /// </summary>
        public static Color HSVtoColor(float h, float s, float v)
        {
            int h_i = (int)(h * 6);
            float f = h * 6 - h_i;
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);

            switch (h_i)
            {
                case 1:
                    return new Color(v, t, p);
                case 2:
                    return new Color(q, v, p);
                case 3:
                    return new Color(p, v, t);
                case 4:
                    return new Color(p, q, v);
                case 5:
                    return new Color(t, p, v);
                default:
                    return new Color(v, p, q);
            }
        }

        /// <summary>
        /// Converts an inputted HSV to Vector3
        /// </summary>
        public static Vector3 HSVtoVector3(float h, float s, float v)
        {
            int h_i = (int)(h * 6);
            float f = h * 6 - h_i;
            float p = v * (1 - s);
            float q = v * (1 - f * s);
            float t = v * (1 - (1 - f) * s);

            switch (h_i)
            {
                case 1:
                    return new Vector3(v, t, p);
                case 2:
                    return new Vector3(q, v, p);
                case 3:
                    return new Vector3(p, v, t);
                case 4:
                    return new Vector3(p, q, v);
                case 5:
                    return new Vector3(t, p, v);
                default:
                    return new Vector3(v, p, q);
            }
        }
    }
}
