using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Xml.Serialization;
using ProtoBuf;

namespace Jakaria.Definitions
{
    [ProtoContract]
    public class NebulaModDefinintions
    {
        [ProtoMember(1), XmlArrayItem("NebulaWeatherDefinition")]
        public NebulaWeatherDefinition[] NebulaWeatherDefinitions;
    }
}
