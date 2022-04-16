using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ProtoBuf;
using VRage.Utils;
using VRageMath;

namespace Jakaria.Definitions
{
    [ProtoContract]
    public class NebulaLightningDefinition
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
        public Vector4 Color = new Vector4(3, 3, 3, 3);

        [ProtoMember(25), XmlElement("Material")]
        public string MaterialId;

        [ProtoIgnore, XmlIgnore]
        public MyStringId Material;

        public void Init()
        {
            Material = MyStringId.GetOrCompute(MaterialId);
        }
    }
}
