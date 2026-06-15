using System;
using System.Collections.Generic;
using Oxide.Core;
using Oxide.Game.Rust.Cui;
using UnityEngine;

namespace Oxide.Plugins
{
    [Info("BearrussEconomyCore", "Bearruss Plugins", "1.0.0")]
    [Description("Core economy with HUD, admin commands, silent rewards and blood harvesting.")]
    public class BearrussEconomyCore : RustPlugin
    {
        private const string PermAdmin = "bearrusseconomycore.admin";
        private const string HudName = "BearrussEconomyCore.HUD";
    }
}
