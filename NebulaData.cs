using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRage.Audio;
using VRage.Utils;
using VRageMath;

namespace Jakaria
{
    public static class NebulaData
    {
        public const string Version = "1.6EA";
        public const ushort ClientHandlerID = 50280;
        public const ushort ModHandlerID = 50281;

        public static readonly Vector4 ShadowColor = new Vector4(1f, 1f, 1f, 0.3f);
        public static readonly Vector4 WhiteColor = Vector4.One;

        public static readonly MyStringId LightningMaterial = MyStringId.GetOrCompute("WeaponLaserIgnoreDepth");
        public static readonly MyStringId DebugMaterial = MyStringId.GetOrCompute("Square");
        public static readonly MyStringId FlareMaterial = MyStringId.GetOrCompute("particle_glare_alpha");

        public static readonly MyStringId[] ShadowTextures =
        {
            MyStringId.GetOrCompute("SphereShadow8"),
            MyStringId.GetOrCompute("SphereShadow4"),
            MyStringId.GetOrCompute("SphereShadow2"),
        };

        public static readonly MyStringId[] NebulaTextures =
        {
            //MyStringId.GetOrCompute("Nebula_A1"),
            //MyStringId.GetOrCompute("Nebula_B1"),
            //MyStringId.GetOrCompute("Nebula_B2"),
            MyStringId.GetOrCompute("Nebula_C1"),
            MyStringId.GetOrCompute("Nebula_C2"),
            MyStringId.GetOrCompute("Nebula_C3"),
            MyStringId.GetOrCompute("Nebula_C4"),
            //MyStringId.GetOrCompute("Nebula_C5"),
        };

        public static readonly MySoundPair LightningSound = new MySoundPair("JLightningSpace");
        public static readonly MySoundPair GeigerSound = new MySoundPair("JGeiger");
        public static readonly MySoundPair GeigerAmbientSound = new MySoundPair("JGeigerAmbient");
        public static readonly MySoundPair IonInterferenceSound = new MySoundPair("JIonInterference");
    }
}
