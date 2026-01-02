using ModLoader;
using UnityEngine;
using WorldBuilderCoop.Behavior;
using WorldBuilderCoop.Events;
using WorldBuilderCoop.Network;

namespace WorldBuilderCoop
{
    public class Core : ModBase
    {
        public static NetworkObjectManager networkObjectManager { get; private set; }
        public static WorldBuilderEventManager EventManager { get; private set; }
        public static NetworkSyncHandler NetworkSync { get; private set; }

        public override string ModName => "WorldBuilderCoop";
        public override string ModVersion => "1.0.0";
        public override string ModAuthor => "Zefire";

        protected override void OnInitialize()
        {
            Logger.Info("===================================");
            Logger.Info("  World Builder Coop Mod");
            Logger.Info("  Version: 1.0.0");
            Logger.Info("  Author: Zefire");
            Logger.Info("===================================");
            Logger.Info("Loading system ...");

            networkObjectManager = new NetworkObjectManager();
            EventManager = WorldBuilderEventManager.Instance;
            NetworkSync = new NetworkSyncHandler();

            PatchAll();
        }
    }
}