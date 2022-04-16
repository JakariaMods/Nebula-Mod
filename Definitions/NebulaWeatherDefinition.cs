using ProtoBuf;
using Sandbox.Game.Entities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace Jakaria.Definitions
{
    [ProtoContract]
    public class NebulaWeatherDefinition
    {
        [ProtoMember(1)]
        public string Name;

        [ProtoMember(2)]
        public string SubtypeId;

        //public readonly MyFogProperties FogProperties;
        //public MyParticleEffect ParticleEffect;

        [ProtoMember(5)]
        public int MinLightningFrequency;

        [ProtoMember(6)]
        public int MaxLightningFrequency;

        [ProtoMember(7)]
        public NebulaLightningDefinition Lightning;

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

        [ProtoMember(21)]
        public bool RenderComets;

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

            if(Lightning != null)
                Lightning.Init();
        }
    }
}
