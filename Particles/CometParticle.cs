using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace Jakaria
{
    public class CometParticle : SimpleParticle
    {
        public Vector3 Direction;
        public CometParticle(Vector3D position, int life, Vector3 direction) : base(position, life)
        {
            Direction = direction;
        }
    }
}
