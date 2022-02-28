using MelonLoader;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace DiscordPlus
{
    public class Entry : MelonMod
    {
        public override void OnApplicationStart()
        {
            DiscordPlus.DoPatching();
        }
    }
}
