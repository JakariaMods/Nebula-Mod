using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using VRageMath;

namespace Jakaria
{
    public class SimpleParticle
    {
        public Vector3D Position;
        public int Life;

        public SimpleParticle(Vector3D position, int life)
        {
            Position = position;
            Life = life;
        }
    }
}
