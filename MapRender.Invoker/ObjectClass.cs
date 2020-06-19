using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MapRender.Invoker
{
    public enum ObjectClass
    {
        Mob = 1,
        Player,
        Npc,
        InMapPortal,
        CrossMapPortal,
        Foothold,
        LadderRope,
        Unknown
    }
}
